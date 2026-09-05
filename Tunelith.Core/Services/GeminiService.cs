using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public interface IGeminiService
{
    Task<CategorizationResult> CategorizeTracksAsync(
        List<TrackMetadata> tracks,
        List<string> existingCategoryNames,
        CancellationToken cancellationToken = default);

    Task<List<GeminiDedupeResult>> FuzzyDedupeAsync(
        List<DedupeCandidate> candidates,
        CancellationToken cancellationToken = default);
}

public class GeminiService : IGeminiService
{
    private readonly HttpClient _httpClient;
    private readonly RateLimitHandler _rateLimitHandler;
    private const string ApiEndpoint = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent";

    public GeminiService(HttpClient httpClient, RateLimitHandler rateLimitHandler)
    {
        _httpClient = httpClient;
        _rateLimitHandler = rateLimitHandler;
    }

    public async Task<CategorizationResult> CategorizeTracksAsync(
        List<TrackMetadata> tracks,
        List<string> existingCategoryNames,
        CancellationToken cancellationToken = default)
    {
        var existingCategoriesClause = existingCategoryNames.Any()
            ? $"EXISTING CATEGORIES (reuse these names when tracks fit): {string.Join(", ", existingCategoryNames)}"
            : "No existing categories yet — generate appropriate names.";

        var tracksJson = JsonSerializer.Serialize(tracks, new JsonSerializerOptions { WriteIndented = false });

        var prompt = $@"You are a music librarian organizing a Spotify library. Analyze these tracks and group them into meaningful categories.

{existingCategoriesClause}

RULES:
1. When tracks fit an existing category, use that exact category name.
2. Only create new category names when no existing category fits.
3. Categories should be based on the track's actual musical characteristics: valence, energy, tempo, acousticness, instrumentalness, and genres.
4. Aim for 4-8 categories total. Each category should have a clear, descriptive name.
5. Every track must be assigned to exactly one category.

TRACKS:
{tracksJson}

Respond with JSON only:
{{
  ""categories"": [
    {{
      ""name"": ""Category Name"",
      ""description"": ""Brief description of this category"",
      ""track_ids"": [""track_id_1"", ""track_id_2""]
    }}
  ]
}}";

        var responseText = await CallGeminiAsync(prompt, cancellationToken);

        var result = JsonSerializer.Deserialize<GeminiCategorizationResponse>(responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (result?.Categories == null || !result.Categories.Any())
        {
            return new CategorizationResult
            {
                Categories = new List<CategoryDefinition>
                {
                    new() { Name = "Uncategorized", Description = "Tracks that could not be categorized", TrackIds = tracks.Select(t => t.TrackId).ToList() }
                },
                TrackCategoryMap = tracks.ToDictionary(t => t.TrackId, _ => "Uncategorized")
            };
        }

        var categorizationResult = new CategorizationResult
        {
            Categories = result.Categories.Select(c => new CategoryDefinition
            {
                Name = c.Name,
                Description = c.Description,
                TrackIds = c.TrackIds
            }).ToList(),
            TrackCategoryMap = result.Categories
                .SelectMany(c => c.TrackIds.Select(id => new { Id = id, c.Name }))
                .ToDictionary(x => x.Id, x => x.Name)
        };

        return categorizationResult;
    }

    public async Task<List<GeminiDedupeResult>> FuzzyDedupeAsync(
        List<DedupeCandidate> candidates,
        CancellationToken cancellationToken = default)
    {
        if (!candidates.Any()) return new List<GeminiDedupeResult>();

        var candidatesJson = JsonSerializer.Serialize(candidates, new JsonSerializerOptions { WriteIndented = false });

        var prompt = $@"You are a music deduplication expert. Analyze these track pairs and determine if they are duplicates (same song, different versions).

Each pair has a string similarity score. Pairs with very high similarity (>0.9) are almost certainly duplicates. Pairs with moderate similarity (0.5-0.9) need careful analysis.

Consider:
- Same song title with Remastered, Live, Deluxe, Edit suffixes = likely duplicate
- Same artist + very similar title = likely duplicate
- Different songs that share words = not duplicates
- feat. or ft. variants of the same song = duplicate

CANDIDATES:
{candidatesJson}

Respond with JSON only:
{{
  ""duplicates"": [
    {{
      ""track_a_id"": ""id1"",
      ""track_b_id"": ""id2"",
      ""is_duplicate"": true,
      ""confidence"": 0.95,
      ""reason"": ""Same song, remastered version""
    }}
  ]
}}";

        var responseText = await CallGeminiAsync(prompt, cancellationToken);

        var result = JsonSerializer.Deserialize<GeminiDedupeResponse>(responseText,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return result?.Duplicates ?? new List<GeminiDedupeResult>();
    }

    private async Task<string> CallGeminiAsync(string prompt, CancellationToken cancellationToken)
    {
        await _rateLimitHandler.WaitForGeminiSlot(cancellationToken);

        return await _rateLimitHandler.ExecuteWithBackoff(
            async ct =>
            {
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = prompt } }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,
                        topP = 0.8,
                        maxOutputTokens = 8192
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                var url = $"{ApiEndpoint}?key={apiKey}";

                var response = await _httpClient.PostAsync(url, content, ct);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync(ct);
                var doc = JsonSerializer.Deserialize<JsonElement>(responseJson);

                return doc
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? string.Empty;
            },
            _rateLimitHandler.GetGeminiRetryDelay,
            cancellationToken);
    }
}
