using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UzChecker.AppHost.Helpers;
using UzChecker.AppHost.Models;
using UzChecker.AppHost.Options;
using UzChecker.Data;
using UzChecker.Data.Entities;

namespace UzChecker.AppHost.Services;

public sealed class WorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<WorkerService> _logger;
    private readonly TelegramOptions _telegramOptions;
    private readonly UzOptions _uzOptions;
    private readonly WorkerOptions _options;

    private static readonly IAsyncPolicy RetryPolicy = Policy.Handle<Exception>()
        .WaitAndRetryAsync(3, static attempt => TimeSpan.FromSeconds(10 * attempt));

    private static readonly TimeZoneInfo UkrainianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");
    private static readonly IFormatProvider UkrainianCulture = new CultureInfo("uk-UA");

    public WorkerService(IServiceScopeFactory serviceScopeFactory, ITelegramBotClient botClient,
        IOptions<WorkerOptions> options, IOptions<UzOptions> uzOptions, IOptions<TelegramOptions> telegramOptions,
        ILogger<WorkerService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _botClient = botClient;
        _logger = logger;
        _telegramOptions = telegramOptions.Value;
        _uzOptions = uzOptions.Value;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var statusMessage = await _botClient.SendMessage(_telegramOptions.RecipientId, "Бот запущено",
            cancellationToken: stoppingToken);

        await _botClient.PinChatMessage(_telegramOptions.RecipientId, statusMessage.MessageId, disableNotification: true,
            cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RetryPolicy.ExecuteAsync(async ct => await ExecuteInternal(statusMessage.MessageId, ct),
                stoppingToken);
        }
    }

    private async Task ExecuteInternal(int statusMessageId, CancellationToken stoppingToken)
    {
        await using var browserScope = await _serviceScopeFactory.CreateBrowserScopeAsync();

        // A trip is actually the train from point A to point B
        var filteredTrips = await GetFilteredTripsAsync(browserScope.Api, stoppingToken);

        var dbContext = browserScope.ServiceProvider.GetRequiredService<UzCheckerContext>();

        foreach (var trip in filteredTrips)
        {
            var availableSeats = new List<Seat>();
            var noLongerAvailableSeats = new List<Seat>();

            // Ensure that the trip exists in a database
            var dbTrip = await dbContext.Trips.FirstOrDefaultAsync(t => t.TrainNumber == trip.Train.Number,
                cancellationToken: stoppingToken);
            if (dbTrip == null)
            {
                dbContext.Trips.Add(dbTrip = new Trip
                {
                    Id = trip.Id,
                    TrainNumber = trip.Train.Number
                });

                await dbContext.SaveChangesAsync(stoppingToken);
            }

            // Sometimes train consists of multiple classes such as "Купе" or "Люкс"
            // We check only those we're interested in and which are being sold for this specific train
            foreach (var wagonClass in trip.Train.WagonClasses.Where(wc =>
                         _uzOptions.Types.Contains(wc.Id) && wc is { FreeSeats: > 0, Price: > 0 }))
            {
                // Ensure that the wagon type exists in a database
                var dbWagon = await dbContext.Wagons.FirstOrDefaultAsync(w => w.Id == wagonClass.Id,
                    cancellationToken: stoppingToken);

                if (dbWagon == null)
                {
                    dbContext.Wagons.Add(dbWagon = new Wagon
                    {
                        Id = wagonClass.Id,
                        Name = wagonClass.Name
                    });

                    await dbContext.SaveChangesAsync(stoppingToken);
                }

                // Retrieve all wagons of specific class for this train
                var wagons = await browserScope.Api.InspectTripSeatsAsync(trip.Id, wagonClass.Id, stoppingToken);

                await dbContext.SaveChangesAsync(stoppingToken);

                // This is a state of previous check, so we can compare it with the current state
                var previousSeats = await dbContext.Seats
                    .Include(s => s.Trip)
                    .Include(s => s.Wagon)
                    .Where(s => s.TripId == trip.Id && s.WagonId == wagonClass.Id)
                    .ToListAsync(stoppingToken);

                var currentSeats = new HashSet<(string WagonNumber, int SeatNumber)>(
                    wagons.SelectMany(wagon => wagon.Seats.Select(seat => (wagon.Number, seat)))
                );
                availableSeats.AddRange(currentSeats.Select(s => new Seat
                {
                    TripId = trip.Id,
                    WagonId = wagonClass.Id,
                    WagonNumber = s.WagonNumber,
                    SeatNumber = s.SeatNumber,
                    Wagon = dbWagon,
                    Trip = dbTrip
                }));

                var previousSeatsSet = new HashSet<(string WagonNumber, int SeatNumber)>(
                    previousSeats.Select(s => (s.WagonNumber, s.SeatNumber))
                );

                var actualSeats = new List<Seat>();

                foreach (var (wagonNumber, seatNumber) in currentSeats.Except(previousSeatsSet))
                {
                    actualSeats.Add(new Seat
                    {
                        TripId = trip.Id,
                        WagonId = wagonClass.Id,
                        WagonNumber = wagonNumber,
                        SeatNumber = seatNumber,
                        Wagon = dbWagon,
                        Trip = dbTrip
                    });
                }

                foreach (var (wagonNumber, seatNumber) in previousSeatsSet.Except(currentSeats))
                {
                    var seat = previousSeats.FirstOrDefault(s =>
                        s.WagonNumber == wagonNumber && s.SeatNumber == seatNumber);
                    if (seat != null)
                        noLongerAvailableSeats.Add(seat);
                }

                if (actualSeats.Count == 0 && noLongerAvailableSeats.Count == 0)
                {
                    continue;
                }

                dbContext.Seats.AddRange(actualSeats);
                dbContext.Seats.RemoveRange(noLongerAvailableSeats);

                await dbContext.SaveChangesAsync(stoppingToken);

                actualSeats.ForEach(s =>
                {
                    dbContext.Seats.Entry(s).Reference(it => it.Wagon).Load();
                    dbContext.Seats.Entry(s).Reference(it => it.Trip).Load();
                });
            }

            var localTime = TimeZoneInfo.ConvertTime(DateTime.UtcNow, UkrainianTimeZone)
                .ToString("g", UkrainianCulture);
            var newStatus = $"Бот запущено\n\nОстаннє оновлення: {localTime}";

            try
            {
                await _botClient.EditMessageText(_telegramOptions.RecipientId, statusMessageId, newStatus,
                    cancellationToken: stoppingToken);
            }
            catch
            {
                // ignore
            }

            if (availableSeats.Count > 0 || noLongerAvailableSeats.Count > 0)
                await NotifyAboutChangesAsync(availableSeats, noLongerAvailableSeats, stoppingToken);

            availableSeats.Clear();
            noLongerAvailableSeats.Clear();
        }

        await Task.Delay(TimeSpan.FromSeconds(_options.IntervalInSeconds), stoppingToken);
    }

    private async Task NotifyAboutChangesAsync(List<Seat> availableSeats, List<Seat> occupiedSeats,
        CancellationToken stoppingToken)
    {
        var firstSeat = availableSeats.FirstOrDefault() ?? occupiedSeats.First();

        string wagonName = firstSeat.Wagon.Name;
        string trainNumber = firstSeat.Trip.TrainNumber;

        var msg = new StringBuilder();
        msg.Append($"<b>{trainNumber}</b> ({wagonName})\n");

        bool isSilent = false;

        if (availableSeats.Count > 0)
        {
            msg.Append("\n\nДоступні зараз місця:");

            foreach (var wagon in availableSeats.GroupBy(s => s.WagonNumber))
            {
                // TODO remove hardcoded rooms number
                var rooms = Enumerable.Range(0, 9)
                    .Select(i => availableSeats
                        .Where(s => s.SeatNumber > i * 4 && s.SeatNumber <= (i + 1) * 4 &&
                                    s.WagonNumber == wagon.Key)
                        .Select(s => s.SeatNumber)
                        .OrderBy(n => n)
                        .ToList());

                // TODO add price and air conditioning info
                //  | {(wagon.Price / 100M).ToString(CultureInfo.InvariantCulture)} грн{(wagon.AirConditioner ? " | ❄️" : ")}

                msg.Append(
                    $"\n\n<b>Вагон {wagon.Key}</b>\n");
                msg.AppendJoin(", ", rooms.Select(r => $"[{string.Join(",", r)}]"));
            }
        }
        else
        {
            isSilent = true;
            msg.Append("\n\nМісця більше не доступні 😢");
        }

        await _botClient.SendMessage(_telegramOptions.RecipientId, msg.ToString(),
            ParseMode.Html, disableNotification: isSilent, cancellationToken: stoppingToken);
    }

    private async Task<List<DirectTrip>> GetFilteredTripsAsync(IApiClient api, CancellationToken stoppingToken)
    {
        var (from, to) = await api.FindStationsByNameAsync(_uzOptions.StationFrom, _uzOptions.StationTo, stoppingToken);

        var trips = await api.FetchTripsAsync(from, to, stoppingToken);
        var filteredTrips = trips.Direct
            .Where(t => _uzOptions.Trains.Contains(t.Train.Number, StringComparer.OrdinalIgnoreCase)
                        && t.Train.WagonClasses.Any(wc => wc.Price > 0 && _uzOptions.Types.Contains(wc.Id)))
            .ToList();

        return filteredTrips;
    }
}