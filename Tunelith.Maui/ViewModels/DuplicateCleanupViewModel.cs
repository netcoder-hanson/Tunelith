using Microsoft.Extensions.Logging;
using Tunelith.Core.Models;

namespace Tunelith.Maui.ViewModels;

public class DuplicateCleanupViewModel : ViewModelBase
{
    private readonly ScanSession _session;
    private readonly SyncService _syncService;
    private readonly ILogger<DuplicateCleanupViewModel> _logger;

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

    private int _duplicateCount;
    public int DuplicateCount
    {
        get => _duplicateCount;
        set => SetProperty(ref _duplicateCount, value);
    }

    private List<DuplicateTrackItem> _duplicateItems = new();
    public List<DuplicateTrackItem> DuplicateItems
    {
        get => _duplicateItems;
        set => SetProperty(ref _duplicateItems, value);
    }

    public AsyncRelayCommand ConfirmSelectionCommand { get; }

    public DuplicateCleanupViewModel(ScanSession session, SyncService syncService, ILogger<DuplicateCleanupViewModel> logger)
    {
        _session = session;
        _syncService = syncService;
        _logger = logger;
        ConfirmSelectionCommand = new AsyncRelayCommand(ConfirmSelectionAsync);
    }

    public void InitializeFromSession()
    {
        var duplicates = _session.Duplicates;
        var items = new List<DuplicateTrackItem>();

        foreach (var group in duplicates)
        {
            for (int i = 0; i < group.Tracks.Count; i++)
            {
                items.Add(new DuplicateTrackItem
                {
                    Track = group.Tracks[i],
                    IsDuplicate = i > 0,
                    GroupId = group.NormalizedKey
                });
            }
        }

        DuplicateItems = items;
        DuplicateCount = items.Count(i => i.IsDuplicate);
        StatusMessage = $"{DuplicateCount} TRACKS IDENTIFIED";
    }

    private async Task ConfirmSelectionAsync()
    {
        IsApplying = true;
        StatusMessage = "Applying your selections...";

        try
        {
            // Build the exact set of track IDs the user toggled ON (marked as duplicates to remove)
            var trackIdsToRemove = new HashSet<string>(
                DuplicateItems
                    .Where(i => i.IsDuplicate)
                    .Select(i => i.Track.Id));

            // Reconcile per-track toggles back into DuplicateGroup.IsConfirmed
            foreach (var group in _session.Duplicates)
            {
                var groupItems = DuplicateItems
                    .Where(i => i.GroupId == group.NormalizedKey)
                    .ToList();

                var laterTracksConfirmed = groupItems
                    .Skip(1)
                    .Any(i => i.IsDuplicate);

                group.IsConfirmed = laterTracksConfirmed;
            }

            var confirmedDuplicates = _session.Duplicates
                .Where(d => d.IsConfirmed)
                .ToList();

            var categorizationResult = _session.CategorizationResult;
            var playlistsToCreate = categorizationResult?.Categories
                .Select(c => new PlaylistChange
                {
                    Name = c.Name,
                    Description = c.Description,
                    TrackIds = c.TrackIds,
                    IsNew = true
                })
                .ToList() ?? new List<PlaylistChange>();

            int totalTracksResorted = categorizationResult?.TrackCategoryMap.Count ?? 0;

            // Pass specific track IDs so SyncService honors per-track toggle choices
            await _syncService.ApplyAsync(
                playlistsToCreate,
                confirmedDuplicates,
                totalTracksResorted,
                trackIdsToRemove);

            StatusMessage = "Changes applied successfully!";
            await Shell.Current.GoToAsync("LibraryMasteredPage");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply duplicate cleanup");
            StatusMessage = "Failed to apply changes. Please try again.";
        }
        finally
        {
            IsApplying = false;
        }
    }
}

public class DuplicateTrackItem
{
    public SpotifyTrack Track { get; set; } = new();
    public bool IsDuplicate { get; set; }
    public string GroupId { get; set; } = string.Empty;
    public string DisplayName => Track.Name;
    public string ArtistAlbum => $"{Track.ArtistNames} • {Track.Album?.Name ?? ""}";
    public string ImageUrl => Track.Album?.Images?.FirstOrDefault()?.Url ?? "";
}
