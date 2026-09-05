namespace Tunelith.Core.Services;

public static class StringSimilarity
{
    public static int LevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source)) return target?.Length ?? 0;
        if (string.IsNullOrEmpty(target)) return source.Length;

        var sourceLen = source.Length;
        var targetLen = target.Length;
        var distances = new int[sourceLen + 1, targetLen + 1];

        for (int i = 0; i <= sourceLen; i++) distances[i, 0] = i;
        for (int j = 0; j <= targetLen; j++) distances[0, j] = j;

        for (int i = 1; i <= sourceLen; i++)
        {
            for (int j = 1; j <= targetLen; j++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;
                distances[i, j] = Math.Min(
                    Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                    distances[i - 1, j - 1] + cost);
            }
        }

        return distances[sourceLen, targetLen];
    }

    public static float LevenshteinSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target)) return 1.0f;
        var maxLen = Math.Max(source?.Length ?? 0, target?.Length ?? 0);
        if (maxLen == 0) return 1.0f;
        return 1.0f - (float)LevenshteinDistance(source ?? "", target ?? "") / maxLen;
    }

    public static float TokenOverlap(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0f;

        var sourceTokens = NormalizeForComparison(source)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var targetTokens = NormalizeForComparison(target)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var sourceSet = new HashSet<string>(sourceTokens);
        var targetSet = new HashSet<string>(targetTokens);
        var intersection = sourceSet.Intersect(targetSet).Count();
        var union = sourceSet.Union(targetSet).Count();

        return union == 0 ? 0f : (float)intersection / union;
    }

    public static float CombinedSimilarity(string source, string target)
    {
        var levSim = LevenshteinSimilarity(source, target);
        var tokenSim = TokenOverlap(source, target);
        return (levSim * 0.4f) + (tokenSim * 0.6f);
    }

    public static string NormalizeForComparison(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var normalized = input.ToLowerInvariant().Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"[^\w\s]", "");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");

        var noiseWords = new HashSet<string> { "feat", "featuring", "ft", "remix", "live", "version", "remaster", "remastered", "deluxe", "edit" };
        var tokens = normalized.Split(' ').Where(t => !noiseWords.Contains(t));
        return string.Join(" ", tokens);
    }
}
