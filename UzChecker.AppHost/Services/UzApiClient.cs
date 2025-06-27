using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using UzChecker.AppHost.Exceptions;
using UzChecker.AppHost.Models;

namespace UzChecker.AppHost.Services;

internal sealed class UzApiClient : IApiClient
{
    private readonly IAPIRequestContext _api;
    private readonly ILogger<UzApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public UzApiClient(IAPIRequestContext api, ILogger<UzApiClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async ValueTask<List<StationResponse>> FindStationsAsync(CancellationToken cancellationToken)
    {
        return await GetAsync<List<StationResponse>>("stations", cancellationToken: cancellationToken);
    }

    public async ValueTask<TripsResponse> FetchTripsAsync(int fromStation, int toStation, string date,
        CancellationToken cancellationToken)
    {
        return await GetAsync<TripsResponse>("v3/trips", new Dictionary<string, string>
        {
            ["station_from_id"] = fromStation.ToString(),
            ["station_to_id"] = toStation.ToString(),
            ["with_transfers"] = "0",
            ["date"] = date
        }, cancellationToken);
    }

    public async ValueTask<List<WagonSeatResponse>> InspectWagonSeatsByClassAsync(int tripId, string wagonClass,
        CancellationToken cancellationToken)
    {
        return await GetAsync<List<WagonSeatResponse>>(
            $"v2/trips/{tripId}/wagons-by-class/{Uri.EscapeDataString(wagonClass)}",
            cancellationToken: cancellationToken);
    }

    private async Task<T> GetAsync<T>(string url, Dictionary<string, string>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var finalUrl = "https://app.uz.gov.ua/api/" + url + (queryParams is { Count: > 0 }
            ? $"?{string.Join('&', queryParams.Select(it => $"{it.Key}={Uri.EscapeDataString(it.Value)}"))}"
            : string.Empty);

        var response = await _api.GetAsync(finalUrl, new APIRequestContextOptions
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

        if (response.Status >= 500)
        {
            throw new UzApiServerException($"UZ services down: {response.Status}",
                response.Status);
        }

        throw new Exception("Failed to fetch data from the API.");
    }
}