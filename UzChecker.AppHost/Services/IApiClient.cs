using UzChecker.AppHost.Models;

namespace UzChecker.AppHost.Services;

public interface IApiClient
{
    ValueTask<List<StationResponse>> FindStationsAsync(CancellationToken cancellationToken);

    ValueTask<TripsResponse> FetchTripsAsync(int fromStation, int toStation, string date,
        CancellationToken cancellationToken);

    ValueTask<List<WagonSeatResponse>> InspectWagonSeatsByClassAsync(int tripId, string wagonClass,
        CancellationToken cancellationToken);
}