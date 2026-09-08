using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class ChangeReportViewModel : ViewModelBase
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly TunelithDbContext _dbContext;
    private readonly ScanSession _session;

    private bool _isApplying;
    public bool IsApplying
    {
        get => _isApplying;
        set => SetProperty(ref _isApplying, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private ChangeReport _report = new();
    public ChangeReport Report
    {
        get => _report;
        set => SetProperty(ref _report, value);
    }

    private List<CategoryDefinition> _categories = new();
    public List<CategoryDefinition> Categories
    {
        get => _categories;
        set => SetProperty(ref _categories, value);
    }

    private List<DuplicateGroup> _duplicates = new();
    public List<DuplicateGroup> Duplicates
    {
        get => _duplicates;
        set => SetProperty(ref _duplicates, value);
    }

    public AsyncRelayCommand ApplyChangesCommand { get; }
    public AsyncRelayCommand ReviewSelectiveCommand { get; }

    public ChangeReportViewModel(
        ISpotifyApiClient spotifyClient,
        TunelithDbContext dbContext,
        ScanSession session)
    {
        _spotifyClient = spotifyClient;
        _dbContext = dbContext;
        _session = session;
        ApplyChangesCommand = new AsyncRelayCommand(ApplyChangesAsync);
        ReviewSelectiveCommand = new AsyncRelayCommand(ReviewSelectiveAsync);
    }

    public void InitializeFromSession()
    {
        var categorizationResult = _session.CategorizationResult;
        var duplicates = _session.Duplicates;

        if (categorizationResult is null) return;

        Categories = categorizationResult.Categories;
        Duplicates = duplicates;

        Report = new ChangeReport
        {
            PlaylistsToCreate = Categories.Select(c => new PlaylistChange
            {
                Name = c.Name,
                Description = c.Description,
                TrackIds = c.TrackIds,
                IsNew = true
            }).ToList(),
            DuplicatesToRemove = duplicates.Where(d => d.IsConfirmed).ToList(),
            TotalTracksResorted = categorizationResult.TrackCategoryMap.Count
        };
    }

    private async Task ApplyChangesAsync()
    {
        IsApplying = true;
        StatusMessage = "Applying changes to Spotify...";

        try
        {
            var accessToken = await SecureStorage.GetAsync("spotify_access_token");
            if (string.IsNullOrEmpty(accessToken)) return;
            await _spotifyClient.SetTokenAsync(accessToken);

            var userId = (await _spotifyClient.GetCurrentUserIdAsync()).Id;

            StatusMessage = "Creating playlists...";
            int playlistsCreated = 0;
            foreach (var playlistChange in Report.PlaylistsToCreate)
            {
                var playlist = await _spotifyClient.CreatePlaylistAsync(
                    userId, playlistChange.Name, playlistChange.Description, false);

                if (playlistChange.TrackIds.Any())
                {
                    await _spotifyClient.AddTracksToPlaylistAsync(
                        playlist.Id, playlistChange.TrackIds);
                }
                playlistsCreated++;
            }

            StatusMessage = "Removing duplicates...";
            int duplicatesRemoved = 0;
            foreach (var duplicate in Report.DuplicatesToRemove)
            {
                if (duplicate.Tracks.Count > 1)
                {
                    // Remove duplicate tracks from the user's liked songs
                    var trackIdsToRemove = duplicate.Tracks.Skip(1).Select(t => t.Id).ToList();
                    if (trackIdsToRemove.Any())
                    {
                        await _spotifyClient.RemoveSavedTracksAsync(trackIdsToRemove);
                    }
                    duplicatesRemoved++;
                }
            }

            int tracksResorted = Report.TotalTracksResorted;

            // Store stats in session for the success screen
            _session.DuplicatesRemoved = duplicatesRemoved;
            _session.NewPlaylists = playlistsCreated;
            _session.TracksResorted = tracksResorted;

            StatusMessage = "Changes applied successfully!";

            await Shell.Current.GoToAsync("LibraryMasteredPage");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying changes: {ex.Message}";
        }
        finally
        {
            IsApplying = false;
        }
    }

    private async Task ReviewSelectiveAsync()
    {
        await Shell.Current.GoToAsync("DuplicateCleanupPage");
    }
}
