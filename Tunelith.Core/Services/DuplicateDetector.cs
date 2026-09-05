using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public class DuplicateDetector
{
    private readonly IGeminiService _geminiService;
    private const float PreFilterThreshold = 0.5f;

    public DuplicateDetector(IGeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(
        List<CategorizedTrack> allTracks,
        CancellationToken cancellationToken = default)
    {
        var exactDuplicates = FindExactDuplicates(allTracks);
        var nearDuplicateCandidates = FindNearDuplicateCandidates(allTracks);

        var ambiguousCandidates = nearDuplicateCandidates
            .Where(c => c.StringSimilarity < 0.9f)
            .ToList();

        var highSimilarityPairs = nearDuplicateCandidates
            .Where(c => c.StringSimilarity >= 0.9f)
            .ToList();

        var aiConfirmed = new List<DuplicateGroup>();
        if (ambiguousCandidates.Any())
        {
            try
            {
                var aiResults = await _geminiService.FuzzyDedupeAsync(ambiguousCandidates, cancellationToken);
                aiConfirmed = ConvertAiDedupeResults(aiResults, allTracks);
            }
            catch (Exception)
            {
                aiConfirmed = ambiguousCandidates
                    .Where(c => c.StringSimilarity >= 0.7f)
                    .Select(c => CreateDuplicateGroup(c, allTracks, DuplicateType.NearDuplicate))
                    .ToList();
            }
        }

        var highSimGroups = highSimilarityPairs
            .Select(c => CreateDuplicateGroup(c, allTracks, DuplicateType.NearDuplicate))
            .ToList();

        var allGroups = new List<DuplicateGroup>();
        allGroups.AddRange(exactDuplicates);
        allGroups.AddRange(highSimGroups);
        allGroups.AddRange(aiConfirmed);

        return MergeDuplicateGroups(allGroups);
    }

    private List<DuplicateGroup> FindExactDuplicates(List<CategorizedTrack> tracks)
    {
        var groups = tracks
            .GroupBy(t => t.Track.Id)
            .Where(g => g.Count() > 1)
            .Select(g => new DuplicateGroup
            {
                NormalizedKey = g.Key,
                Tracks = g.Select(t => t.Track).ToList(),
                Type = DuplicateType.Exact
            })
            .ToList();

        return groups;
    }

    private List<DedupeCandidate> FindNearDuplicateCandidates(List<CategorizedTrack> tracks)
    {
        var candidates = new List<DedupeCandidate>();

        var buckets = new Dictionary<string, List<CategorizedTrack>>();
        foreach (var track in tracks)
        {
            var normalizedArtist = StringSimilarity.NormalizeForComparison(track.Track.ArtistNames);
            var bucketKey = normalizedArtist.Length >= 2 ? normalizedArtist[..2] : normalizedArtist;

            if (!buckets.ContainsKey(bucketKey))
                buckets[bucketKey] = new List<CategorizedTrack>();
            buckets[bucketKey].Add(track);
        }

        foreach (var bucket in buckets.Values)
        {
            for (int i = 0; i < bucket.Count; i++)
            {
                for (int j = i + 1; j < bucket.Count; j++)
                {
                    var trackA = bucket[i].Track;
                    var trackB = bucket[j].Track;

                    if (trackA.Id == trackB.Id) continue;

                    var normalizedA = StringSimilarity.NormalizeForComparison(trackA.DisplayName);
                    var normalizedB = StringSimilarity.NormalizeForComparison(trackB.DisplayName);

                    var similarity = StringSimilarity.CombinedSimilarity(normalizedA, normalizedB);

                    if (similarity >= PreFilterThreshold)
                    {
                        candidates.Add(new DedupeCandidate
                        {
                            TrackAId = trackA.Id,
                            TrackAName = trackA.Name,
                            TrackAArtist = trackA.ArtistNames,
                            TrackBId = trackB.Id,
                            TrackBName = trackB.Name,
                            TrackBArtist = trackB.ArtistNames,
                            StringSimilarity = similarity
                        });
                    }
                }
            }
        }

        return candidates.OrderByDescending(c => c.StringSimilarity).ToList();
    }

    private DuplicateGroup CreateDuplicateGroup(DedupeCandidate candidate, List<CategorizedTrack> allTracks, DuplicateType type)
    {
        var trackA = allTracks.FirstOrDefault(t => t.Track.Id == candidate.TrackAId)?.Track;
        var trackB = allTracks.FirstOrDefault(t => t.Track.Id == candidate.TrackBId)?.Track;

        var tracks = new List<SpotifyTrack>();
        if (trackA != null) tracks.Add(trackA);
        if (trackB != null) tracks.Add(trackB);

        return new DuplicateGroup
        {
            NormalizedKey = StringSimilarity.NormalizeForComparison($"{candidate.TrackAName} {candidate.TrackAArtist}"),
            Tracks = tracks,
            Type = type
        };
    }

    private List<DuplicateGroup> ConvertAiDedupeResults(List<GeminiDedupeResult> results, List<CategorizedTrack> allTracks)
    {
        return results
            .Where(r => r.IsDuplicate)
            .Select(r =>
            {
                var trackA = allTracks.FirstOrDefault(t => t.Track.Id == r.TrackAId)?.Track;
                var trackB = allTracks.FirstOrDefault(t => t.Track.Id == r.TrackBId)?.Track;

                var tracks = new List<SpotifyTrack>();
                if (trackA != null) tracks.Add(trackA);
                if (trackB != null) tracks.Add(trackB);

                return new DuplicateGroup
                {
                    NormalizedKey = StringSimilarity.NormalizeForComparison($"{trackA?.Name} {trackA?.ArtistNames}"),
                    Tracks = tracks,
                    Type = DuplicateType.NearDuplicate,
                    IsConfirmed = r.Confidence > 0.8f
                };
            })
            .ToList();
    }

    private List<DuplicateGroup> MergeDuplicateGroups(List<DuplicateGroup> groups)
    {
        var merged = new List<DuplicateGroup>();
        var usedTrackIds = new HashSet<string>();

        foreach (var group in groups.OrderByDescending(g => g.Type == DuplicateType.Exact ? 1 : 0)
                                    .ThenByDescending(g => g.Tracks.Count))
        {
            var unusedTracks = group.Tracks.Where(t => !usedTrackIds.Contains(t.Id)).ToList();
            if (unusedTracks.Count < 2) continue;

            foreach (var track in unusedTracks)
            {
                usedTrackIds.Add(track.Id);
            }

            merged.Add(new DuplicateGroup
            {
                NormalizedKey = group.NormalizedKey,
                Tracks = unusedTracks,
                Type = group.Type,
                IsConfirmed = group.IsConfirmed
            });
        }

        return merged;
    }
}
