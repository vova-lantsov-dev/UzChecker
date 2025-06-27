using UzChecker.Data.Entities;
using UzChecker.AppHost.Options;
using UzChecker.Data;

namespace UzChecker.AppHost.Helpers;

public sealed class UzSubscriptionContext
{
    private readonly UzCheckerContext _dbContext;
    private readonly List<Seat> _addedSeats = [];
    private readonly List<Seat> _removedSeats;

    public IReadOnlyCollection<Seat> PreviousSeats { get; }
    public IReadOnlyCollection<Seat> RemovedSeats => _removedSeats.AsReadOnly();
    public bool IsChanged => _addedSeats.Count > 0 || _removedSeats.Count > 0;

    public UzSubscription Subscription { get; }

    public UzSubscriptionContext(UzSubscription subscription, IReadOnlyCollection<Seat> previousSeats,
        UzCheckerContext dbContext)
    {
        _dbContext = dbContext;

        Subscription = subscription ?? throw new ArgumentNullException(nameof(subscription));
        PreviousSeats = previousSeats ?? throw new ArgumentNullException(nameof(previousSeats));

        _removedSeats = previousSeats.ToList();
    }

    public void AddCurrentSeat(Seat seat)
    {
        var previousSeat = PreviousSeats.FirstOrDefault(s =>
            s.SeatNumber == seat.SeatNumber && s.WagonNumber == seat.WagonNumber && s.TripId == seat.TripId);

        if (previousSeat == null)
        {
            _addedSeats.Add(seat);
            _dbContext.Seats.Add(seat);
        }
        else
        {
            _removedSeats.Remove(previousSeat);
        }
    }
}