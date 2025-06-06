using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using UzChecker.AppHost.Data;
using UzChecker.AppHost.Data.Entities;
using UzChecker.AppHost.Options;

namespace UzChecker.AppHost.Services;

public sealed class WorkerService : BackgroundService
{
    private readonly IApiClient _apiClient;
    private readonly IBrowser _browser;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly TelegramOptions _telegramOptions;
    private readonly UzOptions _uzOptions;
    private readonly WorkerOptions _options;

    public WorkerService(IApiClient apiClient, IBrowser browser, IServiceScopeFactory serviceScopeFactory,
        ITelegramBotClient botClient, IOptions<WorkerOptions> options, IOptions<UzOptions> uzOptions,
        IOptions<TelegramOptions> telegramOptions)
    {
        _apiClient = apiClient;
        _browser = browser;
        _serviceScopeFactory = serviceScopeFactory;
        _botClient = botClient;
        _telegramOptions = telegramOptions.Value;
        _uzOptions = uzOptions.Value;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/137.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                DeviceScaleFactor = 1,
                IsMobile = false,
                HasTouch = false,
                Locale = "uk-UA"
            });

            var page = await context.NewPageAsync();
            await page.GotoAsync("https://booking.uz.gov.ua/");

            var (from, to) = await _apiClient.FindStationsByNameAsync(page.APIRequest, _uzOptions.StationFrom,
                _uzOptions.StationTo, stoppingToken);

            var trips = await _apiClient.FetchTripsAsync(page.APIRequest, from, to, stoppingToken);
            var filteredTrips = trips.Direct
                .Where(t => _uzOptions.Trains.Contains(t.Train.Number, StringComparer.OrdinalIgnoreCase)
                            && t.Train.WagonClasses.Any(wc => wc.Price > 0 && _uzOptions.Types.Contains(wc.Id)))
                .ToList();

            foreach (var trip in filteredTrips)
            {
                foreach (var wagonClass in trip.Train.WagonClasses.Where(wc =>
                             _uzOptions.Types.Contains(wc.Id) && wc is { FreeSeats: > 0, Price: > 0 }))
                {
                    var wagons = await _apiClient.InspectTripSeatsAsync(page.APIRequest, trip.Id, wagonClass.Id,
                        stoppingToken);

                    using var scope = _serviceScopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<UzCheckerContext>();

                    if (!await dbContext.Trips.AnyAsync(t => t.TrainNumber == trip.Train.Number,
                            cancellationToken: stoppingToken))
                    {
                        dbContext.Trips.Add(new Trip
                        {
                            Id = trip.Id,
                            TrainNumber = trip.Train.Number
                        });
                    }

                    if (!await dbContext.Wagons.AnyAsync(w => w.Id == wagonClass.Id,
                            cancellationToken: stoppingToken))
                    {
                        dbContext.Wagons.Add(new Wagon
                        {
                            Id = wagonClass.Id,
                            Name = wagonClass.Name
                        });
                    }

                    await dbContext.SaveChangesAsync(stoppingToken);

                    var previousSeats = await dbContext.Seats
                        .Include(s => s.Trip)
                        .Include(s => s.Wagon)
                        .Where(s => s.TripId == trip.Id && s.WagonId == wagonClass.Id)
                        .ToListAsync(stoppingToken);

                    var addedSeats = new List<Seat>();
                    var removedSeats = new List<Seat>();

                    var currentSeats = new HashSet<(string WagonNumber, int SeatNumber)>(
                        wagons.SelectMany(wagon => wagon.Seats.Select(seat => (wagon.Number, seat)))
                    );

                    var previousSeatsSet = new HashSet<(string WagonNumber, int SeatNumber)>(
                        previousSeats.Select(s => (s.WagonNumber, s.SeatNumber))
                    );

                    foreach (var (wagonNumber, seatNumber) in currentSeats.Except(previousSeatsSet))
                    {
                        addedSeats.Add(new Seat
                        {
                            TripId = trip.Id,
                            WagonId = wagonClass.Id,
                            WagonNumber = wagonNumber,
                            SeatNumber = seatNumber
                        });
                    }

                    foreach (var (wagonNumber, seatNumber) in previousSeatsSet.Except(currentSeats))
                    {
                        var seat = previousSeats.FirstOrDefault(s =>
                            s.WagonNumber == wagonNumber && s.SeatNumber == seatNumber);
                        if (seat != null)
                            removedSeats.Add(seat);
                    }

                    if (addedSeats.Count == 0 && removedSeats.Count == 0)
                    {
                        continue;
                    }

                    dbContext.Seats.AddRange(addedSeats);
                    dbContext.Seats.RemoveRange(removedSeats);

                    await dbContext.SaveChangesAsync(stoppingToken);

                    var msg = new StringBuilder();
                    msg.Append($"<b>{trip.Train.Number}</b> ({wagonClass.Name})\n");

                    if (addedSeats.Count > 0)
                    {
                        msg.Append("\n\nДоступні зараз місця:");
                        foreach (var wagon in wagons)
                        {
                            var rooms = Enumerable.Range(0, 9)
                                .Select(i => addedSeats
                                    .Where(s => s.SeatNumber > i * 4 && s.SeatNumber <= (i + 1) * 4 &&
                                                s.WagonNumber == wagon.Number)
                                    .Select(s => s.SeatNumber)
                                    .OrderBy(n => n)
                                    .ToList());

                            msg.Append($"\n\n<b>Вагон {wagon.Number}</b> | {(wagon.Price / 100M).ToString(CultureInfo.InvariantCulture)} грн{(wagon.AirConditioner ? " | ❄️" : "")}\n");
                            msg.AppendJoin(", ", rooms.Select(r => $"[{string.Join(",", r)}]"));
                        }
                    }

                    if (removedSeats.Count > 0)
                    {
                        msg.Append($"\n\nСкільки місць забрали з попередньої перевірки: {removedSeats.Count}");
                    }

                    await _botClient.SendMessage(_telegramOptions.RecipientId, msg.ToString(),
                        ParseMode.Html, cancellationToken: stoppingToken);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.IntervalInSeconds), stoppingToken);
        }
    }
}