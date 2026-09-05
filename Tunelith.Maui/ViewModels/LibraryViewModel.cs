using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class LibraryViewModel : ViewModelBase
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly ISpotifyAuthService _authService;
    private readonly TunelithDbContext _dbContext;
    private readonly CategorizationEngine _categorizationEngine;
    private readonly DuplicateDetector _duplicateDetector;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _likedSongsCount;
    public int LikedSongsCount
    {
        get => _likedSongsCount;
        set => SetProperty(ref _likedSongsCount, value);
    }

    private int _playlistsCount;
    public int PlaylistsCount
    {
        get => _playlistsCount;
        set => SetProperty(ref _playlistsCount, value);
    }

    private int _totalTracks;
    public int TotalTracks
    {
        get => _totalTracks;
        set => SetProperty(ref _totalTracks, value);
    }

    private List<CachedPlaylist> _playlists = new();
    public List<CachedPlaylist> Playlists
    {
        get => _playlists;
        set => SetProperty(ref _playlists, value);
    }

    public AsyncRelayCommand ScanLibraryCommand { get; }
    public AsyncRelayCommand StartCategorizationCommand { get; }

    public LibraryViewModel(
        ISpotifyApiClient spotifyClient,
        ISpotifyAuthService authService,
        TunelithDbContext dbContext,
        CategorizationEngine categorizationEngine,
        DuplicateDetector duplicateDetector)
    {
        _spotifyClient = spotifyClient;
        _authService = authService;
        _dbContext = dbContext;
        _categorizationEngine = categorizationEngine;
        _duplicateDetector = duplicateDetector;

        ScanLibraryCommand = new AsyncRelayCommand(ScanLibraryAsync);
        StartCategorizationCommand = new AsyncRelayCommand(StartCategorizationAsync);
    }

    public async Task InitializeAsync()
    {
        var cachedCount = await _dbContext.GetCachedTrackCountAsync();
        if (cachedCount > 0)
        {
            LikedSongsCount = cachedCount;
            Playlists = await _dbContext.GetCachedPlaylistsAsync();
            PlaylistsCount = Playlists.Count;
            TotalTracks = cachedCount + Playlists.Sum(p => p.TotalTracks);
        }
    }

    private async Task ScanLibraryAsync()
    {
        IsLoading = true;
        StatusMessage = "Authenticating...";

        try
        {
            var accessToken = await SecureStorage.GetAsync("spotify_access_token");
            if (string.IsNullOrEmpty(accessToken))
            {
                StatusMessage = "Not authenticated. Please log in.";
                return;
            }

            await _spotifyClient.SetTokenAsync(accessToken);

            var refreshToken = await SecureStorage.GetAsync("spotify_refresh_token");
            if (!string.IsNullOrEmpty(refreshToken))
                _spotifyClient.SetRefreshToken(refreshToken);

            StatusMessage = "Fetching liked songs...";
            var likedSongs = await _spotifyClient.GetLikedSongsAsync();
            LikedSongsCount = likedSongs.Count;

            StatusMessage = "Fetching playlists...";
            var playlists = await _spotifyClient.GetUserPlaylistsAsync();
            PlaylistsCount = playlists.Count;

            StatusMessage = "Caching library data...";
            foreach (var track in likedSongs)
            {
                var cached = new CachedTrack
                {
                    SpotifyTrackId = track.Id,
                    Name = track.Name,
                    ArtistIds = string.Join(",", track.Artists.Select(a => a.Id)),
                    ArtistNames = track.ArtistNames,
                    AlbumId = track.Album?.Id ?? string.Empty,
                    AlbumName = track.Album?.Name ?? string.Empty,
                    DurationMs = track.DurationMs
                };
                await _dbContext.UpsertCachedTrackAsync(cached);
            }

            foreach (var playlist in playlists)
            {
                var cached = new CachedPlaylist
                {
                    SpotifyPlaylistId = playlist.Id,
                    Name = playlist.Name,
                    Description = playlist.Description,
                    TotalTracks = playlist.Tracks?.Total ?? 0,
                    OwnerId = playlist.Owner?.Id
                };
                await _dbContext.UpsertCachedPlaylistAsync(cached);
            }

            Playlists = await _dbContext.GetCachedPlaylistsAsync();
            TotalTracks = LikedSongsCount + Playlists.Sum(p => p.TotalTracks);

            StatusMessage = $"Scanned {LikedSongsCount} liked songs and {PlaylistsCount} playlists.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task StartCategorizationAsync()
    {
        if (LikedSongsCount == 0)
        {
            StatusMessage = "Please scan your library first.";
            return;
        }

        await Shell.Current.GoToAsync("CategorizationPage");
    }
}
