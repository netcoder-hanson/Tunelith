using SQLite;
using Tunelith.Core.Models;

namespace Tunelith.Data;

public class TunelithDbContext
{
    private readonly SQLiteAsyncConnection _database;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public TunelithDbContext(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_initialized) return;

            await _database.CreateTableAsync<CachedTrack>();
            await _database.CreateTableAsync<CachedPlaylist>();
            await _database.CreateTableAsync<CachedPlaylistTrack>();
            await _database.CreateTableAsync<CachedCategory>();
            await _database.CreateTableAsync<CachedScanHistory>();
            await _database.CreateTableAsync<CachedUserPreference>();

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<List<CachedTrack>> GetCachedTracksAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedTrack>().ToListAsync();
    }

    public async Task<CachedTrack?> GetCachedTrackBySpotifyIdAsync(string spotifyId)
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedTrack>()
            .Where(t => t.SpotifyTrackId == spotifyId)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertCachedTrackAsync(CachedTrack track)
    {
        await EnsureInitializedAsync();
        var existing = await GetCachedTrackBySpotifyIdAsync(track.SpotifyTrackId);
        if (existing != null)
        {
            track.Id = existing.Id;
            await _database.UpdateAsync(track);
        }
        else
        {
            await _database.InsertAsync(track);
        }
    }

    public async Task UpsertCachedTracksAsync(IEnumerable<CachedTrack> tracks)
    {
        await EnsureInitializedAsync();
        foreach (var track in tracks)
        {
            await UpsertCachedTrackAsync(track);
        }
    }

    public async Task<List<CachedPlaylist>> GetCachedPlaylistsAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedPlaylist>().ToListAsync();
    }

    public async Task<CachedPlaylist?> GetCachedPlaylistBySpotifyIdAsync(string spotifyId)
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedPlaylist>()
            .Where(p => p.SpotifyPlaylistId == spotifyId)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertCachedPlaylistAsync(CachedPlaylist playlist)
    {
        await EnsureInitializedAsync();
        var existing = await GetCachedPlaylistBySpotifyIdAsync(playlist.SpotifyPlaylistId);
        if (existing != null)
        {
            playlist.Id = existing.Id;
            await _database.UpdateAsync(playlist);
        }
        else
        {
            await _database.InsertAsync(playlist);
        }
    }

    public async Task<List<CachedPlaylistTrack>> GetCachedPlaylistTracksAsync(int playlistId)
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedPlaylistTrack>()
            .Where(t => t.PlaylistId == playlistId)
            .OrderBy(t => t.Position)
            .ToListAsync();
    }

    public async Task UpsertCachedPlaylistTracksAsync(int playlistId, IEnumerable<CachedPlaylistTrack> tracks)
    {
        await EnsureInitializedAsync();
        var existing = await _database.Table<CachedPlaylistTrack>()
            .Where(t => t.PlaylistId == playlistId)
            .ToListAsync();

        foreach (var track in existing)
        {
            await _database.DeleteAsync(track);
        }

        foreach (var track in tracks)
        {
            track.PlaylistId = playlistId;
            await _database.InsertAsync(track);
        }
    }

    public async Task<List<CachedCategory>> GetCachedCategoriesAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedCategory>().ToListAsync();
    }

    public async Task UpsertCachedCategoryAsync(CachedCategory category)
    {
        await EnsureInitializedAsync();
        var existing = await _database.Table<CachedCategory>()
            .Where(c => c.Name == category.Name)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            category.Id = existing.Id;
            await _database.UpdateAsync(category);
        }
        else
        {
            await _database.InsertAsync(category);
        }
    }

    public async Task ClearAllAsync()
    {
        await EnsureInitializedAsync();
        await _database.DeleteAllAsync<CachedTrack>();
        await _database.DeleteAllAsync<CachedPlaylist>();
        await _database.DeleteAllAsync<CachedPlaylistTrack>();
        await _database.DeleteAllAsync<CachedCategory>();
    }

    public async Task<int> GetCachedTrackCountAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedTrack>().CountAsync();
    }

    public async Task StoreScanHistoryAsync(CachedScanHistory history)
    {
        await EnsureInitializedAsync();
        await _database.InsertAsync(history);
    }

    public async Task<List<CachedScanHistory>> GetScanHistoryAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedScanHistory>()
            .OrderByDescending(h => h.ScannedAt)
            .ToListAsync();
    }

    public async Task<CachedScanHistory?> GetLastScanHistoryAsync()
    {
        await EnsureInitializedAsync();
        return await _database.Table<CachedScanHistory>()
            .OrderByDescending(h => h.ScannedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<string?> GetUserPreferenceAsync(string key)
    {
        await EnsureInitializedAsync();
        var pref = await _database.Table<CachedUserPreference>()
            .Where(p => p.Key == key)
            .FirstOrDefaultAsync();
        return pref?.Value;
    }

    public async Task SetUserPreferenceAsync(string key, string value)
    {
        await EnsureInitializedAsync();
        var existing = await _database.Table<CachedUserPreference>()
            .Where(p => p.Key == key)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.Value = value;
            await _database.UpdateAsync(existing);
        }
        else
        {
            await _database.InsertAsync(new CachedUserPreference { Key = key, Value = value });
        }
    }
}
