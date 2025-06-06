using Microsoft.Playwright;
using UzChecker.AppHost.Models;

namespace UzChecker.AppHost.Services;

public interface IApiClient
{
    ValueTask<(int fromId, int toId)> FindStationsByNameAsync(IAPIRequestContext api, string from, string to,
        CancellationToken cancellationToken);

    ValueTask<TripsResponse> FetchTripsAsync(IAPIRequestContext api, int fromStation, int toStation, CancellationToken cancellationToken);

    ValueTask<List<TripSeatResponse>> InspectTripSeatsAsync(IAPIRequestContext api, int tripId, string wagonClass,
        CancellationToken cancellationToken);
}