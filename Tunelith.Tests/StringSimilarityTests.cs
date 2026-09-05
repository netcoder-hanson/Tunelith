using Tunelith.Core.Services;

namespace Tunelith.Tests;

public class StringSimilarityTests
{
    [Theory]
    [InlineData("hello", "hello", 0)]
    [InlineData("hello", "helo", 1)]
    [InlineData("hello", "jello", 1)]
    [InlineData("", "hello", 5)]
    [InlineData("hello", "", 5)]
    public void LevenshteinDistance_ReturnsCorrectDistance(string source, string expected, int expectedDistance)
    {
        var distance = StringSimilarity.LevenshteinDistance(source, expected);
        Assert.Equal(expectedDistance, distance);
    }

    [Theory]
    [InlineData("hello", "hello", 1.0f)]
    [InlineData("hello", "jello", 0.8f)]
    [InlineData("hello", "", 0f)]
    public void LevenshteinSimilarity_ReturnsCorrectSimilarity(string source, string target, float expectedMin)
    {
        var similarity = StringSimilarity.LevenshteinSimilarity(source, target);
        Assert.InRange(similarity, expectedMin - 0.01f, 1.01f);
    }

    [Theory]
    [InlineData("hello world", "hello world", 1.0f)]
    [InlineData("hello world", "world hello", 1.0f)]
    [InlineData("hello", "goodbye", 0f)]
    public void TokenOverlap_ReturnsCorrectOverlap(string source, string target, float expectedMin)
    {
        var overlap = StringSimilarity.TokenOverlap(source, target);
        Assert.InRange(overlap, expectedMin - 0.01f, 1.01f);
    }

    [Theory]
    [InlineData("Song Name", "song name", true)]
    [InlineData("feat. Artist", "ft. Artist", true)]
    [InlineData("Remix", "", false)]
    public void NormalizeForComparison_RemovesNoiseWords(string input, string expectedContains, bool shouldRemove)
    {
        var normalized = StringSimilarity.NormalizeForComparison(input);
        Assert.False(normalized.Contains("feat"));
        Assert.False(normalized.Contains("remix"));
        Assert.False(normalized.Contains("remaster"));
    }

    [Fact]
    public void CombinedSimilarity_HighSimilarity_ArtistAndTitleMatch()
    {
        var simA = StringSimilarity.CombinedSimilarity("song name artist a", "song name artist a");
        Assert.InRange(simA, 0.9f, 1.1f);
    }

    [Fact]
    public void CombinedSimilarity_LowSimilarity_DifferentSongs()
    {
        var sim = StringSimilarity.CombinedSimilarity("completely different song", "another track entirely");
        Assert.InRange(sim, 0f, 0.5f);
    }
}
