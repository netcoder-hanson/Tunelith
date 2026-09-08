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

    private int _estimatedDuplicates;
    public int EstimatedDuplicates
    {
        get => _estimatedDuplicates;
        set => SetProperty(ref _estimatedDuplicates, value);
    }

    private bool _hasAnalysis;
    public bool HasAnalysis
    {
        get => _hasAnalysis;
        set => SetProperty(ref _hasAnalysis, value);
    }

    private int _healthScore;
    public int HealthScore
    {
        get => _healthScore;
        set => SetProperty(ref _healthScore, value);
    }

    private string _healthGrade = string.Empty;
    public string HealthGrade
    {
        get => _healthGrade;
        set => SetProperty(ref _healthGrade, value);
    }

    private bool _showScanReminder;
    public bool ShowScanReminder
    {
        get => _showScanReminder;
        set => SetProperty(ref _showScanReminder, value);
    }

    private string _scanReminderMessage = string.Empty;
    public string ScanReminderMessage
    {
        get => _scanReminderMessage;
        set => SetProperty(ref _scanReminderMessage, value);
    }

    private List<CachedPlaylist> _playlists = new();
    public List<CachedPlaylist> Playlists
    {
        get => _playlists;
        set => SetProperty(ref _playlists, value);
    }

    public AsyncRelayCommand ScanLibraryCommand { get; }
    public AsyncRelayCommand StartCategorizationCommand { get; }
    public AsyncRelayCommand LogoutCommand { get; }
    public AsyncRelayCommand ViewHistoryCommand { get; }

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
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        ViewHistoryCommand = new AsyncRelayCommand(ViewHistoryAsync);
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

        HasAnalysis = cachedCount > 0;

        // Load health score from last scan
        var lastScan = await _dbContext.GetLastScanHistoryAsync();
        if (lastScan != null)
        {
            HealthScore = lastScan.HealthScore;
            HealthGrade = HealthScore switch
            {
                >= 90 => "A+",
                >= 80 => "A",
                >= 70 => "B+",
                >= 60 => "B",
                >= 50 => "C+",
                >= 40 => "C",
                >= 30 => "D",
                _ => "F"
            };
            EstimatedDuplicates = lastScan.DuplicatesFound;
        }

        // Check scan reminder
        await CheckScanReminderAsync();
    }

    private async Task CheckScanReminderAsync()
    {
        var lastScan = await _dbContext.GetLastScanHistoryAsync();
        if (lastScan == null)
        {
            ShowScanReminder = false;
            return;
        }

        var intervalStr = await _dbContext.GetUserPreferenceAsync("scan_reminder_interval");
        if (string.IsNullOrEmpty(intervalStr) || !int.TryParse(intervalStr, out int intervalDays))
        {
            intervalDays = 14; // default: 14 days
        }

        var elapsed = DateTime.UtcNow - lastScan.ScannedAt;
        if (elapsed.TotalDays >= intervalDays)
        {
            var daysText = intervalDays switch
            {
                7 => "weekly",
                14 => "biweekly",
                30 => "monthly",
                90 => "quarterly",
                180 => "semi-annually",
                _ => $"every {intervalDays} days"
            };
            ScanReminderMessage = $"Last scan was {elapsed.Days} days ago. Consider scanning {daysText}.";
            ShowScanReminder = true;
        }
        else
        {
            ShowScanReminder = false;
        }
    }

    private async Task ViewHistoryAsync()
    {
        await Shell.Current.GoToAsync("ScanHistoryPage");
    }

    public async Task LogoutAsync()
    {
        SecureStorage.Remove("spotify_access_token");
        SecureStorage.Remove("spotify_refresh_token");
        await Shell.Current.GoToAsync("//LoginPage");
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

            await Shell.Current.GoToAsync("AnalyzingStudioPage");
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
