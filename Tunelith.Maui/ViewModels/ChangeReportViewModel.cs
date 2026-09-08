using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class ChangeReportViewModel : ViewModelBase
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

    public ChangeReportViewModel(ScanSession session, SyncService syncService)
    {
        _session = session;
        _syncService = syncService;
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
            await _syncService.ApplyAsync(
                Report.PlaylistsToCreate,
                Report.DuplicatesToRemove,
                Report.TotalTracksResorted);

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
