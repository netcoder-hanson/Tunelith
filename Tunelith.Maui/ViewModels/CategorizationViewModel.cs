using Tunelith.Core.Models;
using Tunelith.Core.Services;

namespace Tunelith.Maui.ViewModels;

public class CategorizationViewModel : ViewModelBase
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly TunelithDbContext _dbContext;
    private readonly CategorizationEngine _categorizationEngine;
    private readonly DuplicateDetector _duplicateDetector;

    private bool _isProcessing;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
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

    private CategorizationResult? _categorizationResult;

    public AsyncRelayCommand RunCategorizationCommand { get; }

    public CategorizationViewModel(
        ISpotifyApiClient spotifyClient,
        TunelithDbContext dbContext,
        CategorizationEngine categorizationEngine,
        DuplicateDetector duplicateDetector)
    {
        _spotifyClient = spotifyClient;
        _dbContext = dbContext;
        _categorizationEngine = categorizationEngine;
        _duplicateDetector = duplicateDetector;

        RunCategorizationCommand = new AsyncRelayCommand(RunCategorizationAsync);
    }

    private async Task RunCategorizationAsync()
    {
        IsProcessing = true;
        ProgressPercent = 0;

        try
        {
            var accessToken = await SecureStorage.GetAsync("spotify_access_token");
            if (string.IsNullOrEmpty(accessToken)) return;

            await _spotifyClient.SetTokenAsync(accessToken);

            StatusMessage = "Loading cached tracks...";
            var cachedTracks = await _dbContext.GetCachedTracksAsync();
            ProgressPercent = 10;

            StatusMessage = "Fetching audio features...";
            var trackIds = cachedTracks.Select(t => t.SpotifyTrackId).ToList();
            var audioFeatures = await _spotifyClient.GetAudioFeaturesAsync(trackIds);
            var featuresDict = audioFeatures.ToDictionary(f => f.Id);
            ProgressPercent = 40;

            StatusMessage = "Fetching artist genres...";
            var artistIds = cachedTracks
                .SelectMany(t => t.ArtistIds.Split(','))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            var artists = await _spotifyClient.GetArtistsAsync(artistIds);
            var genresDict = artists.ToDictionary(a => a.Id, a => a.Genres);
            ProgressPercent = 60;

            var categorizedTracks = cachedTracks.Select(ct =>
            {
                var features = featuresDict.GetValueOrDefault(ct.SpotifyTrackId);
                var genres = ct.ArtistIds.Split(',')
                    .Where(id => genresDict.ContainsKey(id))
                    .SelectMany(id => genresDict[id])
                    .Distinct()
                    .ToList();

                return new CategorizedTrack
                {
                    Track = new SpotifyTrack
                    {
                        Id = ct.SpotifyTrackId,
                        Name = ct.Name,
                        Artists = ct.ArtistIds.Split(',').Select(id => new SpotifyArtist
                        {
                            Id = id,
                            Name = ct.ArtistNames,
                            Genres = genresDict.GetValueOrDefault(id, new List<string>())
                        }).ToList()
                    },
                    AudioFeatures = features,
                    ArtistGenres = genres
                };
            }).ToList();

            StatusMessage = "Running categorization engine...";
            var existingCategories = await _dbContext.GetCachedCategoriesAsync();
            var existingNames = existingCategories.Select(c => c.Name).ToList();

            _categorizationResult = await _categorizationEngine.CategorizeAsync(
                categorizedTracks, existingNames);
            Categories = _categorizationResult.Categories;
            ProgressPercent = 80;

            StatusMessage = "Detecting duplicates...";
            Duplicates = await _duplicateDetector.FindDuplicatesAsync(categorizedTracks);
            ProgressPercent = 100;

            foreach (var cat in Categories)
            {
                await _dbContext.UpsertCachedCategoryAsync(new CachedCategory
                {
                    Name = cat.Name,
                    Description = cat.Description
                });
            }

            StatusMessage = $"Found {Categories.Count} categories and {Duplicates.Count} duplicate groups.";

            await Shell.Current.GoToAsync("ChangeReportPage");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
