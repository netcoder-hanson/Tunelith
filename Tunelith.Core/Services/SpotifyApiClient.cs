using System.Net.Http.Headers;
using System.Text.Json;
using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public interface ISpotifyApiClient
{
    Task SetTokenAsync(string accessToken);
    Task<SpotifyUser> GetCurrentUserIdAsync();
    Task<List<SpotifyTrack>> GetLikedSongsAsync(IProgress<int>? progress = null);
    Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync();
    Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId);
    Task<List<SpotifyAudioFeatures>> GetAudioFeaturesAsync(IEnumerable<string> trackIds);
    Task<List<SpotifyArtist>> GetArtistsAsync(IEnumerable<string> artistIds);
    Task<SpotifyPlaylist> CreatePlaylistAsync(string userId, string name, string? description, bool isPublic);
    Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackIds);
    Task RemoveTracksFromPlaylistAsync(string playlistId, IEnumerable<string> trackIds);
}

public class SpotifyApiClient : ISpotifyApiClient
{
    private readonly HttpClient _httpClient;
    private readonly RateLimitHandler _rateLimitHandler;
    private string _accessToken = string.Empty;
    private const string BaseUrl = "https://api.spotify.com/v1";

    public SpotifyApiClient(HttpClient httpClient, RateLimitHandler rateLimitHandler)
    {
        _httpClient = httpClient;
        _rateLimitHandler = rateLimitHandler;
    }

    public Task SetTokenAsync(string accessToken)
    {
        _accessToken = accessToken;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return Task.CompletedTask;
    }

    public async Task<SpotifyUser> GetCurrentUserIdAsync()
    {
        var response = await GetAsync<SpotifyUser>("/me");
        return response;
    }

    public async Task<List<SpotifyTrack>> GetLikedSongsAsync(IProgress<int>? progress = null)
    {
        var allTracks = new List<SpotifyTrack>();
        string? url = "/me/tracks?limit=50";
        int offset = 0;

        while (url != null)
        {
            await _rateLimitHandler.WaitForSpotifySlot();
            var response = await _rateLimitHandler.ExecuteWithBackoff(
                ct => GetAsync<SpotifyPagedResponse<SpotifyTrack>>(url),
                _rateLimitHandler.GetSpotifyRetryDelay);

            allTracks.AddRange(response.Items);
            offset += response.Items.Count;
            progress?.Report(offset);

            url = response.Next != null ? null : null;
            if (response.Next != null)
            {
                url = $"/me/tracks?limit=50&offset={offset}";
            }
        }

        return allTracks;
    }

    public async Task<List<SpotifyPlaylist>> GetUserPlaylistsAsync()
    {
        var allPlaylists = new List<SpotifyPlaylist>();
        string? url = "/me/playlists?limit=50";
        int offset = 0;

        while (url != null)
        {
            await _rateLimitHandler.WaitForSpotifySlot();
            var response = await _rateLimitHandler.ExecuteWithBackoff(
                ct => GetAsync<SpotifyPagedResponse<SpotifyPlaylist>>(url),
                _rateLimitHandler.GetSpotifyRetryDelay);

            allPlaylists.AddRange(response.Items);
            offset += response.Items.Count;

            url = response.Next != null ? null : null;
            if (response.Next != null)
            {
                url = $"/me/playlists?limit=50&offset={offset}";
            }
        }

        return allPlaylists;
    }

    public async Task<List<SpotifyTrack>> GetPlaylistTracksAsync(string playlistId)
    {
        var allTracks = new List<SpotifyTrack>();
        int offset = 0;

        while (true)
        {
            await _rateLimitHandler.WaitForSpotifySlot();
            var response = await _rateLimitHandler.ExecuteWithBackoff(
                ct => GetAsync<SpotifyPagedResponse<SpotifyPlaylistTrackItem>>(
                    $"/playlists/{playlistId}/tracks?limit=50&offset={offset}"),
                _rateLimitHandler.GetSpotifyRetryDelay);

            allTracks.AddRange(response.Items
                .Where(i => i.Track != null)
                .Select(i => i.Track!));

            offset += response.Items.Count;
            if (response.Next == null) break;
        }

        return allTracks;
    }

    public async Task<List<SpotifyAudioFeatures>> GetAudioFeaturesAsync(IEnumerable<string> trackIds)
    {
        var allFeatures = new List<SpotifyAudioFeatures>();
        var idList = trackIds.ToList();

        for (int i = 0; i < idList.Count; i += 100)
        {
            var batch = idList.Skip(i).Take(100);
            var idsParam = string.Join(",", batch);

            await _rateLimitHandler.WaitForSpotifySlot();
            var response = await _rateLimitHandler.ExecuteWithBackoff(
                ct => GetAsync<SpotifyAudioFeaturesResponse>($"/audio-features?ids={idsParam}"),
                _rateLimitHandler.GetSpotifyRetryDelay);

            allFeatures.AddRange(response.AudioFeatures.Where(f => f != null)!);
        }

        return allFeatures;
    }

    public async Task<List<SpotifyArtist>> GetArtistsAsync(IEnumerable<string> artistIds)
    {
        var allArtists = new List<SpotifyArtist>();
        var idList = artistIds.Distinct().ToList();

        for (int i = 0; i < idList.Count; i += 50)
        {
            var batch = idList.Skip(i).Take(50);
            var idsParam = string.Join(",", batch);

            await _rateLimitHandler.WaitForSpotifySlot();
            var response = await _rateLimitHandler.ExecuteWithBackoff(
                ct => GetAsync<ArtistsResponse>($"/artists?ids={idsParam}"),
                _rateLimitHandler.GetSpotifyRetryDelay);

            allArtists.AddRange(response.Artists);
        }

        return allArtists;
    }

    public async Task<SpotifyPlaylist> CreatePlaylistAsync(string userId, string name, string? description, bool isPublic)
    {
        var body = JsonSerializer.Serialize(new { name, description, @public = isPublic });
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        await _rateLimitHandler.WaitForSpotifySlot();
        return await _rateLimitHandler.ExecuteWithBackoff(
            ct => PostAsync<SpotifyPlaylist>($"/users/{userId}/playlists", content),
            _rateLimitHandler.GetSpotifyRetryDelay);
    }

    public async Task AddTracksToPlaylistAsync(string playlistId, IEnumerable<string> trackIds)
    {
        var uris = trackIds.Select(id => $"spotify:track:{id}").ToList();

        for (int i = 0; i < uris.Count; i += 100)
        {
            var batch = uris.Skip(i).Take(100);
            var body = JsonSerializer.Serialize(new { uris = batch });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            await _rateLimitHandler.WaitForSpotifySlot();
            await _rateLimitHandler.ExecuteWithBackoff(
                ct => PostAsync<object>($"/playlists/{playlistId}/tracks", content),
                _rateLimitHandler.GetSpotifyRetryDelay);
        }
    }

    public async Task RemoveTracksFromPlaylistAsync(string playlistId, IEnumerable<string> trackIds)
    {
        var uris = trackIds.Select(id => new { uri = $"spotify:track:{id}" }).ToList();

        for (int i = 0; i < uris.Count; i += 100)
        {
            var batch = uris.Skip(i).Take(100);
            var body = JsonSerializer.Serialize(new { tracks = batch });
            var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            await _rateLimitHandler.WaitForSpotifySlot();
            var request = new HttpRequestMessage(HttpMethod.Delete, $"/playlists/{playlistId}/tracks")
            {
                Content = content
            };
            await _rateLimitHandler.ExecuteWithBackoff(
                ct => SendAsync<object>(request),
                _rateLimitHandler.GetSpotifyRetryDelay);
        }
    }

    private async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync($"{BaseUrl}{endpoint}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json) ?? throw new JsonException("Failed to deserialize");
    }

    private async Task<T> PostAsync<T>(string endpoint, HttpContent content)
    {
        var response = await _httpClient.PostAsync($"{BaseUrl}{endpoint}", content);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json) ?? throw new JsonException("Failed to deserialize");
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request)
    {
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return default!;
        return JsonSerializer.Deserialize<T>(json) ?? throw new JsonException("Failed to deserialize");
    }

    private class SpotifyAudioFeaturesResponse
    {
        public List<SpotifyAudioFeatures?> AudioFeatures { get; set; } = new();
    }

    private class ArtistsResponse
    {
        public List<SpotifyArtist> Artists { get; set; } = new();
    }
}
