using Tunelith.Core.Models;
using Tunelith.Core.Services;
using Tunelith.Data;

namespace Tunelith.Maui.ViewModels;

public class AnalyzingStudioViewModel : ViewModelBase
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly TunelithDbContext _dbContext;
    private readonly CategorizationEngine _categorizationEngine;
    private readonly DuplicateDetector _duplicateDetector;
    private readonly HealthScoreService _healthScoreService;
    private readonly SmartDuplicateKeeper _smartDuplicateKeeper;
    private readonly IGeminiService _geminiService;
    private readonly ScanSession _session;

    private bool _isProcessing = true;
    public bool IsProcessing
    {
        get => _isProcessing;
        set => SetProperty(ref _isProcessing, value);
    }

    private int _progressPercent;
    public int ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private string _currentStep = string.Empty;
    public string CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    private int _totalTracks;
    public int TotalTracks
    {
        get => _totalTracks;
        set => SetProperty(ref _totalTracks, value);
    }

    private bool _step1Complete;
    public bool Step1Complete
    {
        get => _step1Complete;
        set => SetProperty(ref _step1Complete, value);
    }

    private bool _step2Complete;
    public bool Step2Complete
    {
        get => _step2Complete;
        set => SetProperty(ref _step2Complete, value);
    }

    private bool _step3Active;
    public bool Step3Active
    {
        get => _step3Active;
        set => SetProperty(ref _step3Active, value);
    }

    private bool _step3Complete;
    public bool Step3Complete
    {
        get => _step3Complete;
        set => SetProperty(ref _step3Complete, value);
    }

    private bool _step4Pending = true;
    public bool Step4Pending
    {
        get => _step4Pending;
        set => SetProperty(ref _step4Pending, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        set => SetProperty(ref _hasError, value);
    }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public AsyncRelayCommand RetryCommand { get; }
    public AsyncRelayCommand GoBackCommand { get; }

    public AnalyzingStudioViewModel(
        ISpotifyApiClient spotifyClient,
        TunelithDbContext dbContext,
        CategorizationEngine categorizationEngine,
        DuplicateDetector duplicateDetector,
        HealthScoreService healthScoreService,
        SmartDuplicateKeeper smartDuplicateKeeper,
        IGeminiService geminiService,
        ScanSession session)
    {
        _spotifyClient = spotifyClient;
        _dbContext = dbContext;
        _categorizationEngine = categorizationEngine;
        _duplicateDetector = duplicateDetector;
        _healthScoreService = healthScoreService;
        _smartDuplicateKeeper = smartDuplicateKeeper;
        _geminiService = geminiService;
        _session = session;

        RetryCommand = new AsyncRelayCommand(RetryAsync);
        GoBackCommand = new AsyncRelayCommand(GoBackAsync);
    }

    public async Task RunAnalysisAsync()
    {
        IsProcessing = true;

        try
        {
            var accessToken = await SecureStorage.GetAsync("spotify_access_token");
            if (string.IsNullOrEmpty(accessToken)) return;

            await _spotifyClient.SetTokenAsync(accessToken);

            StatusMessage = "Fetching Playlists";
            CurrentStep = "Fetching Playlists";
            ProgressPercent = 5;
            await Task.Delay(300);

            var cachedTracks = await _dbContext.GetCachedTracksAsync();
            TotalTracks = cachedTracks.Count;
            ProgressPercent = 15;
            Step1Complete = true;

            StatusMessage = "Mapping Audio Features";
            CurrentStep = "Mapping Audio Features";
            ProgressPercent = 25;

            var trackIds = cachedTracks.Select(t => t.SpotifyTrackId).ToList();
            var audioFeatures = await _spotifyClient.GetAudioFeaturesAsync(trackIds);
            var featuresDict = audioFeatures.ToDictionary(f => f.Id);
            ProgressPercent = 45;

            var artistIds = cachedTracks
                .SelectMany(t => t.ArtistIds.Split(','))
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();
            var artists = await _spotifyClient.GetArtistsAsync(artistIds);
            var genresDict = artists.ToDictionary(a => a.Id, a => a.Genres);
            Step2Complete = true;

            StatusMessage = "Matching Duplicates";
            CurrentStep = "Matching Duplicates";
            ProgressPercent = 55;
            Step3Active = true;

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

            var duplicates = await _duplicateDetector.FindDuplicatesAsync(categorizedTracks);
            ProgressPercent = 70;
            Step3Complete = true;

            // Smart duplicate keeper: reorder groups so best track is first
            var featuresDict2 = featuresDict;
            foreach (var group in duplicates)
            {
                _smartDuplicateKeeper.ReorderGroup(group, featuresDict2);
            }

            StatusMessage = "Generating Playlist Descriptions";
            CurrentStep = "Generating Playlist Descriptions";
            ProgressPercent = 78;

            StatusMessage = "Re-sorting Genres";
            CurrentStep = "Re-sorting Genres";
            ProgressPercent = 85;
            Step4Pending = true;

            var existingCategories = await _dbContext.GetCachedCategoriesAsync();
            var existingNames = existingCategories.Select(c => c.Name).ToList();

            var categorizationResult = await _categorizationEngine.CategorizeAsync(
                categorizedTracks, existingNames);

            // Generate AI playlist descriptions
            try
            {
                var descriptionInputs = categorizationResult.Categories.Select(c => new PlaylistDescriptionInput
                {
                    Name = c.Name,
                    SampleTrackNames = c.TrackIds.Take(5)
                        .Select(id => cachedTracks.FirstOrDefault(t => t.SpotifyTrackId == id)?.Name ?? "")
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList(),
                    Genres = categorizedTracks
                        .Where(t => c.TrackIds.Contains(t.Track.Id))
                        .SelectMany(t => t.ArtistGenres)
                        .Distinct()
                        .Take(5)
                        .ToList()
                }).ToList();

                var descriptions = await _geminiService.GeneratePlaylistDescriptionsAsync(descriptionInputs);

                foreach (var cat in categorizationResult.Categories)
                {
                    var aiDesc = descriptions.FirstOrDefault(d => d.Name == cat.Name);
                    if (aiDesc != null && !string.IsNullOrWhiteSpace(aiDesc.Description))
                    {
                        cat.Description = aiDesc.Description;
                    }
                }
            }
            catch (Exception)
            {
                // Fallback: use default descriptions if Gemini fails
            }

            foreach (var cat in categorizationResult.Categories)
            {
                await _dbContext.UpsertCachedCategoryAsync(new CachedCategory
                {
                    Name = cat.Name,
                    Description = cat.Description
                });
            }

            ProgressPercent = 95;
            StatusMessage = "Calculating Health Score";

            // Calculate health score
            var healthScore = _healthScoreService.Calculate(
                TotalTracks, duplicates.Count, categorizationResult);

            ProgressPercent = 100;
            StatusMessage = "Analysis Complete";

            // Store results in session for downstream screens
            _session.CategorizationResult = categorizationResult;
            _session.Duplicates = duplicates;
            _session.HealthScore = healthScore.OverallScore;

            // Store scan history
            await _dbContext.StoreScanHistoryAsync(new CachedScanHistory
            {
                TotalTracks = TotalTracks,
                LikedSongsCount = cachedTracks.Count,
                PlaylistsCount = (await _dbContext.GetCachedPlaylistsAsync()).Count,
                DuplicatesFound = duplicates.Count,
                CategoriesCreated = categorizationResult.Categories.Count,
                HealthScore = healthScore.OverallScore
            });

            await Task.Delay(500);

            await Shell.Current.GoToAsync("ChangeReportPage");
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Analysis failed: {ex.Message}";
            StatusMessage = "Something went wrong.";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private async Task RetryAsync()
    {
        ResetState();
        await RunAnalysisAsync();
    }

    private async Task GoBackAsync()
    {
        ResetState();
        await Shell.Current.GoToAsync("..");
    }

    private void ResetState()
    {
        HasError = false;
        ErrorMessage = string.Empty;
        IsProcessing = true;
        ProgressPercent = 0;
        Step1Complete = false;
        Step2Complete = false;
        Step3Active = false;
        Step3Complete = false;
        Step4Pending = true;
        CurrentStep = string.Empty;
        StatusMessage = string.Empty;
    }
}
