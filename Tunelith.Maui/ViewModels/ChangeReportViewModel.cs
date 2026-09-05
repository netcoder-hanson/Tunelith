using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class ChangeReportViewModel : ViewModelBase
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly TunelithDbContext _dbContext;

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

    public ChangeReportViewModel(ISpotifyApiClient spotifyClient, TunelithDbContext dbContext)
    {
        _spotifyClient = spotifyClient;
        _dbContext = dbContext;
        ApplyChangesCommand = new AsyncRelayCommand(ApplyChangesAsync);
    }

    public void LoadReport(CategorizationResult categorizationResult, List<DuplicateGroup> duplicates)
    {
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

    public async Task InitializeFromNavigationAsync()
    {
        var accessToken = await SecureStorage.GetAsync("spotify_access_token");
        if (string.IsNullOrEmpty(accessToken)) return;
        await _spotifyClient.SetTokenAsync(accessToken);
    }

    private async Task ApplyChangesAsync()
    {
        IsApplying = true;
        StatusMessage = "Applying changes to Spotify...";

        try
        {
            var userId = (await _spotifyClient.GetCurrentUserIdAsync()).Id;

            StatusMessage = "Creating playlists...";
            foreach (var playlistChange in Report.PlaylistsToCreate)
            {
                var playlist = await _spotifyClient.CreatePlaylistAsync(
                    userId, playlistChange.Name, playlistChange.Description, false);

                if (playlistChange.TrackIds.Any())
                {
                    await _spotifyClient.AddTracksToPlaylistAsync(
                        playlist.Id, playlistChange.TrackIds);
                }
            }

            StatusMessage = "Removing duplicates...";
            foreach (var duplicate in Report.DuplicatesToRemove)
            {
                if (duplicate.Tracks.Count > 1)
                {
                    var tracksToRemove = duplicate.Tracks.Skip(1).Select(t => t.Id);
                    await _dbContext.GetCachedTracksAsync();
                }
            }

            StatusMessage = "Changes applied successfully!";
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
}
