using UzChecker.Data.Entities;

namespace UzChecker.AppHost.Helpers;

public static class WagonHelper
{
    public static string FormatSeats(int trainTypeId, string wagonClass, IEnumerable<Seat> seats)
    {
        var pattern = WagonPatterns.PatternSelector(trainTypeId, wagonClass);

        if (pattern == null)
            return $"Wagon {wagonClass} with train type {trainTypeId} is not supported.";

        return "";
    }
}