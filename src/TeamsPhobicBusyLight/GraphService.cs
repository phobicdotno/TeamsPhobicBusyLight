using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace TeamsPhobicBusyLight;

public class GraphService
{
    private static readonly string[] Scopes = ["Presence.Read"];

    private readonly IPublicClientApplication _msal;
    private readonly HttpClient _http = new();
    private IAccount? _account;
    private HashSet<string> _activeActivities;

    public GraphService(string clientId, HashSet<string> activeActivities)
    {
        _activeActivities = activeActivities;
        _msal = PublicClientApplicationBuilder
            .Create(clientId)
            .WithRedirectUri("http://localhost")
            .WithAuthority(AzureCloudInstance.AzurePublic, "common")
            .Build();
    }

    public void UpdateActiveActivities(HashSet<string> activities) => _activeActivities = activities;

    public string? LastAvailability { get; private set; }
    public string? LastActivity { get; private set; }

    public async Task<bool> SignInAsync()
    {
        try
        {
            var result = await _msal.AcquireTokenInteractive(Scopes).ExecuteAsync();
            _account = result.Account;
            return true;
        }
        catch { return false; }
    }

    private async Task<string?> GetTokenAsync()
    {
        if (_account is null) return null;
        try
        {
            var result = await _msal.AcquireTokenSilent(Scopes, _account).ExecuteAsync();
            return result.AccessToken;
        }
        catch
        {
            try
            {
                var result = await _msal.AcquireTokenInteractive(Scopes).ExecuteAsync();
                _account = result.Account;
                return result.AccessToken;
            }
            catch { return null; }
        }
    }

    public async Task<bool?> IsInMeetingAsync()
    {
        var token = await GetTokenAsync();
        if (token is null) return null;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me/presence");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var response = await _http.SendAsync(request);

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadFromJsonAsync<PresenceResponse>();
            LastAvailability = json?.Availability;
            LastActivity = json?.Activity;
            return json?.Activity is not null && _activeActivities.Contains(json.Activity);
        }
        catch { return null; }
    }

    private record PresenceResponse(string? Availability, string? Activity);
}
