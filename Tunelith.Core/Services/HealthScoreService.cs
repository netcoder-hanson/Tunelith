using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public class HealthScoreService
{
    /// <summary>
    /// Calculates a 0-100 library health score based on:
    /// - Duplicate ratio (40 points): fewer duplicates = higher score
    /// - Categorization coverage (35 points): more tracks categorized = higher score
    /// - Playlist balance (25 points): more even distribution across categories = higher score
    /// </summary>
    public LibraryHealthScore Calculate(
        int totalTracks,
        int duplicatesFound,
        CategorizationResult? categorizationResult)
    {
        var score = new LibraryHealthScore
        {
            TotalTracks = totalTracks,
            DuplicatesFound = duplicatesFound,
            CategoriesCreated = categorizationResult?.Categories.Count ?? 0
        };

        if (totalTracks == 0)
        {
            score.OverallScore = 0;
            score.Grade = "N/A";
            return score;
        }

        // Duplicate ratio: 0 duplicates = 40 pts, 50%+ duplicates = 0 pts
        var duplicateRatio = (double)duplicatesFound / totalTracks;
        var duplicateScore = Math.Max(0, 40 * (1 - duplicateRatio * 2));
        score.DuplicateScore = (int)Math.Round(duplicateScore);

        // Categorization coverage: % of tracks that are categorized
        var categorizationRatio = categorizationResult != null && categorizationResult.TrackCategoryMap.Count > 0
            ? (double)categorizationResult.TrackCategoryMap.Count / totalTracks
            : 0;
        score.CategorizationScore = (int)Math.Round(35 * categorizationRatio);

        // Playlist balance: how evenly tracks are distributed across categories
        if (categorizationResult?.Categories.Any() == true)
        {
            var trackCounts = categorizationResult.Categories.Select(c => c.TrackIds.Count).ToList();
            var avg = (double)trackCounts.Sum() / trackCounts.Count;
            var variance = trackCounts.Average(c => Math.Pow(c - avg, 2));
            var stdDev = Math.Sqrt(variance);
            // Lower std dev relative to mean = more balanced = higher score
            var balanceRatio = avg > 0 ? Math.Max(0, 1 - (stdDev / avg)) : 0;
            score.BalanceScore = (int)Math.Round(25 * balanceRatio);
        }

        score.OverallScore = score.DuplicateScore + score.CategorizationScore + score.BalanceScore;
        score.Grade = score.OverallScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B+",
            >= 60 => "B",
            >= 50 => "C+",
            >= 40 => "C",
            >= 30 => "D",
            _ => "F"
        };

        return score;
    }
}

public class LibraryHealthScore
{
    public int OverallScore { get; set; }
    public string Grade { get; set; } = string.Empty;
    public int TotalTracks { get; set; }
    public int DuplicatesFound { get; set; }
    public int CategoriesCreated { get; set; }
    public int DuplicateScore { get; set; }
    public int CategorizationScore { get; set; }
    public int BalanceScore { get; set; }
}
