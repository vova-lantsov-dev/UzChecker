using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using UzChecker.AppHost.Models;
using UzChecker.AppHost.Options;

namespace UzChecker.AppHost.Services;

internal sealed class UzApiClient : IApiClient
{
    private readonly UzOptions _uzOptions;
    private readonly ILogger<UzApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public UzApiClient(IOptions<UzOptions> uzOptions, ILogger<UzApiClient> logger)
    {
        _uzOptions = uzOptions.Value;
        _logger = logger;
    }

    public async ValueTask<(int fromId, int toId)> FindStationsByNameAsync(IAPIRequestContext api, string from,
        string to,
        CancellationToken cancellationToken)
    {
        var response = await GetAsync<List<StationResponse>>(api, "stations", cancellationToken: cancellationToken);

        int fromId = response.First(s => s.Name == from).Id;
        int toId = response.First(s => s.Name == to).Id;

        return (fromId, toId);
    }

    public async ValueTask<TripsResponse> FetchTripsAsync(IAPIRequestContext api, int fromStation, int toStation,
        CancellationToken cancellationToken)
    {
        return await GetAsync<TripsResponse>(api, "v3/trips", new Dictionary<string, string>
        {
            ["station_from_id"] = fromStation.ToString(),
            ["station_to_id"] = toStation.ToString(),
            ["with_transfers"] = "0",
            ["date"] = _uzOptions.Date
        }, cancellationToken);
    }

    public async ValueTask<List<TripSeatResponse>> InspectTripSeatsAsync(IAPIRequestContext api, int tripId,
        string wagonClass, CancellationToken cancellationToken)
    {
        return await GetAsync<List<TripSeatResponse>>(
            api,
            $"v2/trips/{tripId}/wagons-by-class/{Uri.EscapeDataString(wagonClass)}",
            cancellationToken: cancellationToken);
    }

    private async Task<T> GetAsync<T>(IAPIRequestContext api, string url,
        Dictionary<string, string>? queryParams = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalUrl = "https://app.uz.gov.ua/api/" + url + (queryParams is { Count: > 0 }
            ? $"?{string.Join('&', queryParams.Select(it => $"{it.Key}={Uri.EscapeDataString(it.Value)}"))}"
            : string.Empty);

        var response = await api.GetAsync(finalUrl, new()
        {
            Headers =
            [
                new KeyValuePair<string, string>("x-client-locale", "uk"),
                new KeyValuePair<string, string>("sec-ch-ua-platform", "Windows"),
                new KeyValuePair<string, string>("x-user-agent", "UZ/2 Web/1 User/guest"),
                new KeyValuePair<string, string>("referer", "https://booking.uz.gov.ua/"),
                new KeyValuePair<string, string>("accept-language", "uk-UA"),
                new KeyValuePair<string, string>("sec-ch-ua", "\"Not.A/Brand\";v=\"99\", \"Chromium\";v=\"136\""),
                new KeyValuePair<string, string>("sec-ch-ua-mobile", "?0"),
                new KeyValuePair<string, string>("accept", "application/json")
            ]
        });
        var content = await response.TextAsync();
        
        cancellationToken.ThrowIfCancellationRequested();

        if (response.Status is >= 200 and < 300)
        {
            var data = JsonSerializer.Deserialize<T>(content, JsonOptions)!;
            return data;
        }

        _logger.LogError(new Exception(content), "Failed to fetch data from {Url}. Response code: {StatusCode}",
            finalUrl, response.Status);

        throw new Exception("Failed to fetch data from the API.");
    }
}