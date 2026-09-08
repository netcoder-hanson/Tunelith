using Tunelith.Core.Models;

namespace Tunelith.Maui.ViewModels;

public class DuplicateCleanupViewModel : ViewModelBase
{
    private readonly ScanSession _session;

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

    public DuplicateCleanupViewModel(ScanSession session)
    {
        _session = session;
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
        _session.DuplicateItems = items;
        DuplicateCount = items.Count(i => i.IsDuplicate);
        StatusMessage = $"{DuplicateCount} TRACKS IDENTIFIED";
    }

    private async Task ConfirmSelectionAsync()
    {
        // Sync toggle state back to session
        foreach (var item in DuplicateItems)
        {
            var existing = _session.DuplicateItems.FirstOrDefault(d =>
                d.Track.Id == item.Track.Id);
            if (existing is not null)
                existing.IsDuplicate = item.IsDuplicate;
        }

        await Shell.Current.GoToAsync("LibraryMasteredPage");
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
