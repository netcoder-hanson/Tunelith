using System.Text.Json.Serialization;

namespace Tunelith.Core.Models;

public class SpotifyTrack
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("artists")]
    public List<SpotifyArtist> Artists { get; set; } = new();

    [JsonPropertyName("album")]
    public SpotifyAlbum? Album { get; set; }

    [JsonPropertyName("duration_ms")]
    public int DurationMs { get; set; }

    [JsonPropertyName("is_local")]
    public bool IsLocal { get; set; }

    public string ArtistNames => string.Join(", ", Artists.Select(a => a.Name));
    public string DisplayName => $"{Name} — {ArtistNames}";
}

public class SpotifyArtist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("genres")]
    public List<string> Genres { get; set; } = new();
}

public class SpotifyAlbum
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<SpotifyImage> Images { get; set; } = new();
}

public class SpotifyImage
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("width")]
    public int Width { get; set; }
}

public class SpotifyPlaylist
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("tracks")]
    public SpotifyPlaylistTracks? Tracks { get; set; }

    [JsonPropertyName("images")]
    public List<SpotifyImage> Images { get; set; } = new();

    [JsonPropertyName("owner")]
    public SpotifyUser? Owner { get; set; }
}

public class SpotifyPlaylistTracks
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class SpotifyUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}

public class SpotifyAudioFeatures
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("acousticness")]
    public float Acousticness { get; set; }

    [JsonPropertyName("danceability")]
    public float Danceability { get; set; }

    [JsonPropertyName("energy")]
    public float Energy { get; set; }

    [JsonPropertyName("instrumentalness")]
    public float Instrumentalness { get; set; }

    [JsonPropertyName("liveness")]
    public float Liveness { get; set; }

    [JsonPropertyName("speechiness")]
    public float Speechiness { get; set; }

    [JsonPropertyName("tempo")]
    public float Tempo { get; set; }

    [JsonPropertyName("valence")]
    public float Valence { get; set; }

    [JsonPropertyName("key")]
    public int Key { get; set; }

    [JsonPropertyName("mode")]
    public int Mode { get; set; }

    [JsonPropertyName("time_signature")]
    public int TimeSignature { get; set; }
}

public class SpotifyPagedResponse<T>
{
    [JsonPropertyName("href")]
    public string Href { get; set; } = string.Empty;

    [JsonPropertyName("items")]
    public List<T> Items { get; set; } = new();

    [JsonPropertyName("limit")]
    public int Limit { get; set; }

    [JsonPropertyName("offset")]
    public int Offset { get; set; }

    [JsonPropertyName("next")]
    public string? Next { get; set; }

    [JsonPropertyName("previous")]
    public string? Previous { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public class SpotifyPlaylistTrackItem
{
    [JsonPropertyName("track")]
    public SpotifyTrack? Track { get; set; }
}
