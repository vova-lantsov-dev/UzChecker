namespace UzChecker.AppHost.Models;

public record TripsResponse(
    string StationFrom,
    string StationTo,
    DirectTrip[] Direct,
    object WithTransfer,
    object Monitoring
);

public record DirectTrip(
    int Id,
    int DepartAt,
    int ArriveAt,
    string StationFrom,
    string StationTo,
    int StationsTimeOffset,
    Train Train,
    object Discount,
    CustomTag CustomTag,
    Monitoring Monitoring,
    bool IsDeparted
);

public record Train(
    int Id,
    string StationFrom,
    string StationTo,
    string Number,
    int Type,
    WagonClasses[] WagonClasses,
    InfoPopup InfoPopup
);

public record WagonClasses(
    string Id,
    string Name,
    int FreeSeats,
    int Price,
    string[] Amenities
);

public record InfoPopup(
    string Title,
    string Description,
    Button Button
);

public record Button(
    string Text,
    string Url
);

public record CustomTag(
    string Value,
    string Color
);

public record Monitoring(
    bool Allowed,
    bool AutoPurchase
);

