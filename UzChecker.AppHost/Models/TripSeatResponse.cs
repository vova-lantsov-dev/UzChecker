namespace UzChecker.AppHost.Models;

public record TripSeatResponse(
    string Id,
    string Number,
    string MockupName,
    int[] Seats,
    int FreeSeatsTop,
    int FreeSeatsLower,
    int Price,
    bool AirConditioner,
    Services[] Services,
    Privileges[] Privileges
);

public record Services(
    string Id,
    string Title,
    Details Details,
    int Price,
    string SelectType,
    object SelectUnitsMax,
    bool SelectedByDefault
);

public record Details(
    string Photo,
    Content[] Content
);

public record Content(
    string Title,
    string Description
);

public record Privileges(
    int Id,
    string Name,
    string Description,
    int InputType,
    bool Active,
    object CompanionId,
    string Rules,
    object Hint
);