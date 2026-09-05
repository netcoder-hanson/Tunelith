using Tunelith.Core.Models;
using Tunelith.Core.Services;

namespace Tunelith.Tests;

public class DuplicateDetectorTests
{
    private readonly IGeminiService _mockGeminiService;

    public DuplicateDetectorTests()
    {
        _mockGeminiService = new MockGeminiService();
    }

    [Fact]
    public async Task FindDuplicates_FindsExactDuplicates()
    {
        var detector = new DuplicateDetector(_mockGeminiService);

        var tracks = new List<CategorizedTrack>
        {
            CreateTrack("track1", "Song Name", "Artist A"),
            CreateTrack("track1", "Song Name", "Artist A"),
            CreateTrack("track2", "Different Song", "Artist B")
        };

        var duplicates = await detector.FindDuplicatesAsync(tracks);

        Assert.Single(duplicates);
        Assert.Equal(DuplicateType.Exact, duplicates[0].Type);
        Assert.Equal(2, duplicates[0].Tracks.Count);
    }

    [Fact]
    public async Task FindDuplicates_FindsNearDuplicates()
    {
        var detector = new DuplicateDetector(_mockGeminiService);

        var tracks = new List<CategorizedTrack>
        {
            CreateTrack("track1", "Song Name (Remastered)", "Artist A"),
            CreateTrack("track2", "Song Name (Remaster)", "Artist A"),
            CreateTrack("track3", "Completely Different", "Artist B")
        };

        var duplicates = await detector.FindDuplicatesAsync(tracks);

        Assert.Contains(duplicates, d => d.Type == DuplicateType.NearDuplicate);
    }

    [Fact]
    public async Task FindDuplicates_IgnoresDifferentSongs()
    {
        var detector = new DuplicateDetector(_mockGeminiService);

        var tracks = new List<CategorizedTrack>
        {
            CreateTrack("track1", "Rock Anthem", "Band X"),
            CreateTrack("track2", "Jazz Standard", "Artist Y"),
            CreateTrack("track3", "Pop Hit", "Singer Z")
        };

        var duplicates = await detector.FindDuplicatesAsync(tracks);

        Assert.Empty(duplicates);
    }

    private static CategorizedTrack CreateTrack(string id, string name, string artist)
    {
        return new CategorizedTrack
        {
            Track = new SpotifyTrack
            {
                Id = id,
                Name = name,
                Artists = new List<SpotifyArtist>
                {
                    new() { Id = $"artist_{id}", Name = artist }
                }
            },
            ArtistGenres = new List<string> { "pop" }
        };
    }

    private class MockGeminiService : IGeminiService
    {
        public Task<CategorizationResult> CategorizeTracksAsync(
            List<TrackMetadata> tracks,
            List<string> existingCategoryNames,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CategorizationResult
            {
                Categories = new List<CategoryDefinition>
                {
                    new() { Name = "Uncategorized", TrackIds = tracks.Select(t => t.TrackId).ToList() }
                },
                TrackCategoryMap = tracks.ToDictionary(t => t.TrackId, _ => "Uncategorized")
            });
        }

        public Task<List<GeminiDedupeResult>> FuzzyDedupeAsync(
            List<DedupeCandidate> candidates,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new List<GeminiDedupeResult>());
        }
    }
}
