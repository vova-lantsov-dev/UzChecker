using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Wrap;
using Telegram.Bot;
using UzChecker.AppHost.Exceptions;
using UzChecker.AppHost.Helpers;
using UzChecker.AppHost.Options;
using UzChecker.Data;
using UzChecker.Data.Entities;

namespace UzChecker.AppHost.Services;

public sealed class WorkerService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ITelegramBotClient _botClient;
    private readonly ITelegramNotifier _telegramNotifier;
    private readonly ILogger<WorkerService> _logger;
    private readonly TelegramOptions _telegramOptions;
    private readonly UzOptions _uzOptions;
    private readonly WorkerOptions _options;

    private static readonly AsyncPolicyWrap RetryPolicy = Policy
        .Handle<UzApiServerException>()
        .WaitAndRetryForeverAsync(_ => TimeSpan.FromMinutes(10))
        .WrapAsync(
            Policy.Handle<Exception>(ex => ex is not UzApiServerException)
                .WaitAndRetryAsync(3, static attempt => TimeSpan.FromSeconds(10 * attempt))
        );

    private static readonly TimeZoneInfo UkrainianTimeZone = TimeZoneInfo.FindSystemTimeZoneById("FLE Standard Time");

    public WorkerService(
        IServiceScopeFactory serviceScopeFactory,
        ITelegramBotClient botClient,
        ITelegramNotifier telegramNotifier,
        IOptions<WorkerOptions> options,
        IOptions<UzOptions> uzOptions,
        IOptions<TelegramOptions> telegramOptions,
        ILogger<WorkerService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _botClient = botClient;
        _telegramNotifier = telegramNotifier;
        _logger = logger;
        _telegramOptions = telegramOptions.Value;
        _uzOptions = uzOptions.Value;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var statusMessage = await _telegramNotifier.SendInitialMessageAsync(_uzOptions.Subscriptions, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RetryPolicy.ExecuteAsync(async ct =>
                {
                    foreach (var subscription in _uzOptions.Subscriptions)
                    {
                        await ExecuteSubscriptionInternal(subscription, ct);
                    }
                },
                stoppingToken);

            try
            {
                await _telegramNotifier.EditStatusMessageAsync(
                    statusMessage.MessageId,
                    _uzOptions.Subscriptions,
                    stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("An error occurred while updating status message in Telegram. Error: {Error}",
                    ex.ToString());
            }
        }
    }

    private async Task ExecuteSubscriptionInternal(UzSubscription subscription, CancellationToken stoppingToken)
    {
        await using var browserScope = await _serviceScopeFactory.CreateBrowserScopeAsync();
        var dbContext = browserScope.ServiceProvider.GetRequiredService<UzCheckerContext>();
        var api = browserScope.Api;

        var stations = await api.FindStationsAsync(stoppingToken);

        // For each date in the subscription
        foreach (var dateObj in subscription.Dates)
        {
            var date = DateTime.Parse(dateObj.Date);
            var dateUa = TimeZoneInfo.ConvertTime(DateTime.UtcNow, UkrainianTimeZone).Date;

            if (dateUa >= date || date - dateUa >= TimeSpan.FromDays(20))
            {
                // Skipping past dates
                continue;
            }

            // Fetch trips for this date and stations
            var tripsResponse = await api.FetchTripsAsync(
                fromStation: stations.First(s => s.Name == subscription.StationFrom).Id,
                toStation: stations.First(s => s.Name == subscription.StationTo).Id,
                date: dateObj.Date,
                stoppingToken);

            // For each train in the date
            foreach (var trainNumber in dateObj.Trains)
            {
                if (tripsResponse.Direct.All(t => t.Train.Number != trainNumber))
                    continue;

                var trips = tripsResponse.Direct
                    .Where(t => t.Train.Number == trainNumber)
                    .ToList();

                foreach (var trip in trips)
                {
                    var previousSeats = await dbContext.Seats
                        .Include(s => s.Trip)
                        .Include(s => s.Wagon)
                        .Where(s => s.TripId == trip.Id)
                        .ToListAsync(stoppingToken);

                    var context = new UzSubscriptionContext(subscription, previousSeats, dbContext);

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
                    }

                    foreach (var wagonClass in trip.Train.WagonClasses.Where(wc =>
                                 subscription.TrainTypes.Contains(wc.Id) && wc.Price > 0))
                    {
                        // Fetch all wagons of this class
                        var wagons = await api.InspectWagonSeatsByClassAsync(trip.Id, wagonClass.Id, stoppingToken);

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
                        }

                        foreach (var wagon in wagons)
                        {
                            wagon.Seats
                                .Select(seatNum => new Seat
                                {
                                    TripId = trip.Id,
                                    Trip = dbTrip,
                                    WagonId = wagonClass.Id,
                                    Wagon = dbWagon,
                                    WagonNumber = wagon.Number,
                                    SeatNumber = seatNum
                                })
                                .ToList()
                                .ForEach(s => context.AddCurrentSeat(s));

                            await dbContext.SaveChangesAsync(stoppingToken);
                        }
                    }

                    dbContext.Seats.RemoveRange(context.RemovedSeats);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    if (context.IsChanged)
                        await _telegramNotifier.NotifyAsync(context, stoppingToken);
                }
            }
        }
    }
}