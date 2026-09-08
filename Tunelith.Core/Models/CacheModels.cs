namespace Tunelith.Core.Models;

public class CachedTrack
{
    public int Id { get; set; }
    public string SpotifyTrackId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ArtistIds { get; set; } = string.Empty;
    public string ArtistNames { get; set; } = string.Empty;
    public string AlbumId { get; set; } = string.Empty;
    public string AlbumName { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string? AudioFeaturesJson { get; set; }
    public string? ArtistGenresJson { get; set; }
    public string? Category { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class CachedPlaylist
{
    public int Id { get; set; }
    public string SpotifyPlaylistId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int TotalTracks { get; set; }
    public string? OwnerId { get; set; }
    public DateTime CachedAt { get; set; } = DateTime.UtcNow;
}

public class CachedPlaylistTrack
{
    public int Id { get; set; }
    public int PlaylistId { get; set; }
    public string SpotifyTrackId { get; set; } = string.Empty;
    public int Position { get; set; }
}

public class CachedCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CachedScanHistory
{
    public int Id { get; set; }
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public int TotalTracks { get; set; }
    public int LikedSongsCount { get; set; }
    public int PlaylistsCount { get; set; }
    public int DuplicatesFound { get; set; }
    public int CategoriesCreated { get; set; }
    public int HealthScore { get; set; }
}

public class CachedUserPreference
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
