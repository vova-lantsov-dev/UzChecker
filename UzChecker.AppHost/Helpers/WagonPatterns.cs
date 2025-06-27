namespace UzChecker.AppHost.Helpers;

internal static class WagonPatterns
{
    public static readonly Func<int, string, WagonPattern?> PatternSelector = (trainTypeId, wagonClass) =>
    {
        return (trainTypeId, wagonClass) switch
        {
            (0, "К" or "КЖ") => GenerateKupePattern(rooms: 10),

            (0, "КД") => GenerateKupePattern(rooms: 9),

            _ => null
        };
    };

    private static WagonPattern GenerateKupePattern(int rooms)
    {
        return new WagonPattern
        {
            Rows =
            [
                new SeatRow
                {
                    Seats = Enumerable.Range(1, rooms * 4)
                        .Where(r => r % 2 == 0)
                        .Select((r, i) => (r, i: i / 2))
                        .GroupBy(it => it.i, it => it.r)
                        .Select(it => new RoomSeats(it.ToArray()))
                        .OfType<SeatBase>()
                        .ToArray()
                },
                new SeatRow
                {
                    Seats = Enumerable.Range(1, rooms * 4)
                        .Where(r => r % 2 == 1)
                        .Select((r, i) => (r, i: i / 2))
                        .GroupBy(it => it.i, it => it.r)
                        .Select(it => new RoomSeats(it.ToArray()))
                        .OfType<SeatBase>()
                        .ToArray()
                }
            ]
        };
    }

    public sealed class WagonPattern
    {
        public IWagonRow[] Rows { get; set; }
    }

    public interface IWagonRow
    {
    }

    public sealed class DelimiterRow : IWagonRow
    {
    }

    public sealed class SeatRow : IWagonRow
    {
        public SeatBase[] Seats { get; set; }
    }

    public abstract class SeatBase
    {
        public SeatBase? Below { get; set; }
        public SeatBase? Above { get; set; }
    }

    public sealed class NoSeat : SeatBase
    {
    }

    public sealed class SingleSeat : SeatBase
    {
        public int Number { get; }

        public SingleSeat(int number)
        {
            Number = number;
        }
    }

    public sealed class RoomSeats : SeatBase
    {
        public int[] Numbers { get; }

        public RoomSeats(params int[] numbers)
        {
            Numbers = numbers;
        }
    }
}