using Tunelith.Core.Models;

namespace Tunelith.Maui.ViewModels;

public class LibraryMasteredViewModel : ViewModelBase
{
    private readonly ScanSession _session;

    private int _duplicatesRemoved;
    public int DuplicatesRemoved
    {
        get => _duplicatesRemoved;
        set => SetProperty(ref _duplicatesRemoved, value);
    }

    private int _newPlaylists;
    public int NewPlaylists
    {
        get => _newPlaylists;
        set => SetProperty(ref _newPlaylists, value);
    }

    private int _tracksResorted;
    public int TracksResorted
    {
        get => _tracksResorted;
        set => SetProperty(ref _tracksResorted, value);
    }

    public AsyncRelayCommand ReturnToDashboardCommand { get; }
    public AsyncRelayCommand ShareStatsCommand { get; }

    public LibraryMasteredViewModel(ScanSession session)
    {
        _session = session;
        ReturnToDashboardCommand = new AsyncRelayCommand(ReturnToDashboardAsync);
        ShareStatsCommand = new AsyncRelayCommand(ShareStatsAsync);
    }

    public void InitializeFromSession()
    {
        DuplicatesRemoved = _session.DuplicatesRemoved;
        NewPlaylists = _session.NewPlaylists;
        TracksResorted = _session.TracksResorted;
    }

    private async Task ReturnToDashboardAsync()
    {
        _session.Clear();
        await Shell.Current.GoToAsync("//LibraryPage");
    }

    private async Task ShareStatsAsync()
    {
        await Share.RequestAsync(new ShareTextRequest
        {
            Title = "My Tunelith Stats",
            Text = $"I just mastered my Spotify library with Tunelith! Removed {DuplicatesRemoved} duplicates, created {NewPlaylists} new playlists, and re-sorted {TracksResorted} tracks."
        });
    }
}
