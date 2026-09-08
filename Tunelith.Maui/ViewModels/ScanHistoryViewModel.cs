using Tunelith.Core.Models;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class ScanHistoryViewModel : ViewModelBase
{
    private readonly TunelithDbContext _dbContext;

    private List<CachedScanHistory> _scanHistory = new();
    public List<CachedScanHistory> ScanHistory
    {
        get => _scanHistory;
        set => SetProperty(ref _scanHistory, value);
    }

    private CachedScanHistory? _lastScan;
    public CachedScanHistory? LastScan
    {
        get => _lastScan;
        set => SetProperty(ref _lastScan, value);
    }

    private int _totalScans;
    public int TotalScans
    {
        get => _totalScans;
        set => SetProperty(ref _totalScans, value);
    }

    public AsyncRelayCommand GoBackCommand { get; }

    public ScanHistoryViewModel(TunelithDbContext dbContext)
    {
        _dbContext = dbContext;
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
    }

    private async Task GoBackAsync()
    {
        await Shell.Current.GoToAsync("..");
    }

    public async Task LoadHistoryAsync()
    {
        ScanHistory = await _dbContext.GetScanHistoryAsync();
        LastScan = ScanHistory.FirstOrDefault();
        TotalScans = ScanHistory.Count;
    }
}
