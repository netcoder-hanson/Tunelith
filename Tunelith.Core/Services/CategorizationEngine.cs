using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public class CategorizationEngine
{
    private readonly IGeminiService _geminiService;

    public CategorizationEngine(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<CategorizationResult> CategorizeAsync(
        List<CategorizedTrack> tracks,
        List<string> existingCategoryNames,
        CancellationToken cancellationToken = default)
    {
        var ruleBasedResult = ApplyRuleBasedCategories(tracks);

        var trackMetadata = tracks.Select(t => new TrackMetadata
        {
            TrackId = t.Track.Id,
            Name = t.Track.Name,
            Artist = t.Track.ArtistNames,
            Genres = t.ArtistGenres,
            Valence = t.AudioFeatures?.Valence ?? 0.5f,
            Energy = t.AudioFeatures?.Energy ?? 0.5f,
            Acousticness = t.AudioFeatures?.Acousticness ?? 0.5f,
            Instrumentalness = t.AudioFeatures?.Instrumentalness ?? 0.5f,
            Danceability = t.AudioFeatures?.Danceability ?? 0.5f,
            Tempo = t.AudioFeatures?.Tempo ?? 120f
        }).ToList();

        var allCategoryNames = existingCategoryNames
            .Union(ruleBasedResult.Categories.Select(c => c.Name))
            .Distinct()
            .ToList();

        try
        {
            var aiResult = await _geminiService.CategorizeTracksAsync(
                trackMetadata, allCategoryNames, cancellationToken);

            return MergeResults(ruleBasedResult, aiResult);
        }
        catch (Exception)
        {
            return ruleBasedResult;
        }
    }

    private CategorizationResult ApplyRuleBasedCategories(List<CategorizedTrack> tracks)
    {
        var categories = new Dictionary<string, CategoryDefinition>
        {
            ["High Energy"] = new() { Name = "High Energy", Description = "Upbeat, energetic tracks" },
            ["Chill"] = new() { Name = "Chill", Description = "Relaxed, low-energy tracks" },
            ["Focus"] = new() { Name = "Focus", Description = "Instrumental, concentration-friendly" },
            ["Acoustic"] = new() { Name = "Acoustic", Description = "Acoustic and organic sounds" },
            ["Party"] = new() { Name = "Party", Description = "Danceable, high tempo tracks" },
            ["Melancholy"] = new() { Name = "Melancholy", Description = "Emotional, lower valence tracks" }
        };

        var trackCategoryMap = new Dictionary<string, string>();

        foreach (var track in tracks)
        {
            var features = track.AudioFeatures;
            if (features == null)
            {
                trackCategoryMap[track.Track.Id] = "Chill";
                continue;
            }

            string category = DetermineCategory(features, track.ArtistGenres);
            trackCategoryMap[track.Track.Id] = category;

            if (categories.TryGetValue(category, out var cat))
            {
                cat.TrackIds.Add(track.Track.Id);
            }
        }

        return new CategorizationResult
        {
            Categories = categories.Values.ToList(),
            TrackCategoryMap = trackCategoryMap
        };
    }

    private string DetermineCategory(SpotifyAudioFeatures features, List<string> genres)
    {
        bool isInstrumental = features.Instrumentalness > 0.5f;
        bool isAcoustic = features.Acousticness > 0.5f;
        bool isHighEnergy = features.Energy > 0.7f;
        bool isDanceable = features.Danceability > 0.7f;
        bool isLowValence = features.Valence < 0.3f;
        bool isHighTempo = features.Tempo > 120f;

        if (isInstrumental) return "Focus";
        if (isAcoustic && !isHighEnergy) return "Acoustic";
        if (isDanceable && isHighTempo) return "Party";
        if (isHighEnergy && features.Valence > 0.5f) return "High Energy";
        if (isLowValence) return "Melancholy";

        return "Chill";
    }

    private CategorizationResult MergeResults(CategorizationResult ruleBased, CategorizationResult ai)
    {
        var mergedCategories = new List<CategoryDefinition>();
        var mergedMap = new Dictionary<string, string>();

        var aiCategoryMap = ai.Categories.ToDictionary(c => c.Name, c => c);

        foreach (var ruleCat in ruleBased.Categories)
        {
            if (aiCategoryMap.TryGetValue(ruleCat.Name, out var aiCat))
            {
                mergedCategories.Add(new CategoryDefinition
                {
                    Name = ruleCat.Name,
                    Description = aiCat.Description ?? ruleCat.Description,
                    TrackIds = aiCat.TrackIds.Any() ? aiCat.TrackIds : ruleCat.TrackIds
                });
            }
            else
            {
                mergedCategories.Add(ruleCat);
            }
        }

        foreach (var aiCat in ai.Categories)
        {
            if (!mergedCategories.Any(c => c.Name == aiCat.Name))
            {
                mergedCategories.Add(aiCat);
            }
        }

        foreach (var kvp in ai.TrackCategoryMap)
        {
            mergedMap[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in ruleBased.TrackCategoryMap)
        {
            if (!mergedMap.ContainsKey(kvp.Key))
            {
                mergedMap[kvp.Key] = kvp.Value;
            }
        }

        foreach (var cat in mergedCategories)
        {
            cat.TrackIds = cat.TrackIds.Where(id => mergedMap.ContainsKey(id)).ToList();
        }

        return new CategorizationResult
        {
            Categories = mergedCategories,
            TrackCategoryMap = mergedMap
        };
    }
}
