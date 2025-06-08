using UzChecker.AppHost.Models;

namespace UzChecker.AppHost.Services;

public interface IApiClient
{
    ValueTask<(int fromId, int toId)> FindStationsByNameAsync(string from, string to,
        CancellationToken cancellationToken);

    ValueTask<TripsResponse> FetchTripsAsync(int fromStation, int toStation, CancellationToken cancellationToken);

    ValueTask<List<TripSeatResponse>> InspectTripSeatsAsync(int tripId, string wagonClass,
        CancellationToken cancellationToken);
}