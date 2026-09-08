using Tunelith.Core.Models;

namespace Tunelith.Maui.ViewModels;

public class DuplicateCleanupViewModel : ViewModelBase
{
    private readonly ScanSession _session;
    private readonly SyncService _syncService;

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

    public DuplicateCleanupViewModel(ScanSession session, SyncService syncService)
    {
        _session = session;
        _syncService = syncService;
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
            // Reconcile per-track toggles back into DuplicateGroup.IsConfirmed
            // A group is confirmed only if ALL its non-first tracks are still toggled on
            foreach (var group in _session.Duplicates)
            {
                var groupItems = DuplicateItems
                    .Where(i => i.GroupId == group.NormalizedKey)
                    .ToList();

                // First track in group is always kept (not a duplicate).
                // Group is confirmed if any of the later tracks are still marked as duplicate.
                var laterTracksConfirmed = groupItems
                    .Skip(1)
                    .Any(i => i.IsDuplicate);

                group.IsConfirmed = laterTracksConfirmed;
            }

            // Build the list of confirmed duplicates for removal
            var confirmedDuplicates = _session.Duplicates
                .Where(d => d.IsConfirmed)
                .ToList();

            // Build playlist creation list from categories (same as ChangeReportViewModel)
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

            // Apply via shared service
            await _syncService.ApplyAsync(
                playlistsToCreate,
                confirmedDuplicates,
                totalTracksResorted);

            StatusMessage = "Changes applied successfully!";
            await Shell.Current.GoToAsync("LibraryMasteredPage");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
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
