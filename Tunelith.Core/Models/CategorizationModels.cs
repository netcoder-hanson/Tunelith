using System.Text.Json.Serialization;

namespace Tunelith.Core.Models;

public class CategorizedTrack
{
    public SpotifyTrack Track { get; set; } = new();
    public SpotifyAudioFeatures? AudioFeatures { get; set; }
    public List<string> ArtistGenres { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public float CategoryConfidence { get; set; }
}

public class CategoryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> TrackIds { get; set; } = new();
}

public class DuplicateGroup
{
    public string NormalizedKey { get; set; } = string.Empty;
    public List<SpotifyTrack> Tracks { get; set; } = new();
    public DuplicateType Type { get; set; }
    public bool IsConfirmed { get; set; }
}

public enum DuplicateType
{
    Exact,
    NearDuplicate
}

public class ChangeReport
{
    public List<PlaylistChange> PlaylistsToCreate { get; set; } = new();
    public List<PlaylistChange> PlaylistsToUpdate { get; set; } = new();
    public List<DuplicateGroup> DuplicatesToRemove { get; set; } = new();
    public int TotalTracksResorted { get; set; }
}

public class PlaylistChange
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> TrackIds { get; set; } = new();
    public bool IsNew { get; set; }
    public string? ExistingPlaylistId { get; set; }
}

public class CategorizationResult
{
    public List<CategoryDefinition> Categories { get; set; } = new();
    public Dictionary<string, string> TrackCategoryMap { get; set; } = new();
}

public class GeminiCategorizationRequest
{
    public List<TrackMetadata> Tracks { get; set; } = new();
    public List<string> ExistingCategoryNames { get; set; } = new();
}

public class TrackMetadata
{
    public string TrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public List<string> Genres { get; set; } = new();
    public float Valence { get; set; }
    public float Energy { get; set; }
    public float Acousticness { get; set; }
    public float Instrumentalness { get; set; }
    public float Danceability { get; set; }
    public float Tempo { get; set; }
}

public class GeminiCategorizationResponse
{
    [JsonPropertyName("categories")]
    public List<GeminiCategory> Categories { get; set; } = new();
}

public class GeminiCategory
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("track_ids")]
    public List<string> TrackIds { get; set; } = new();
}

public class GeminiDedupeRequest
{
    public List<DedupeCandidate> Candidates { get; set; } = new();
}

public class DedupeCandidate
{
    public string TrackAId { get; set; } = string.Empty;
    public string TrackAName { get; set; } = string.Empty;
    public string TrackAArtist { get; set; } = string.Empty;
    public string TrackBId { get; set; } = string.Empty;
    public string TrackBName { get; set; } = string.Empty;
    public string TrackBArtist { get; set; } = string.Empty;
    public float StringSimilarity { get; set; }
}

public class GeminiDedupeResponse
{
    [JsonPropertyName("duplicates")]
    public List<GeminiDedupeResult> Duplicates { get; set; } = new();
}

public class GeminiDedupeResult
{
    [JsonPropertyName("track_a_id")]
    public string TrackAId { get; set; } = string.Empty;

    [JsonPropertyName("track_b_id")]
    public string TrackBId { get; set; } = string.Empty;

    [JsonPropertyName("is_duplicate")]
    public bool IsDuplicate { get; set; }

    [JsonPropertyName("confidence")]
    public float Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}
