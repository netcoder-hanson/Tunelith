using SQLite;
using Tunelith.Core.Models;

namespace Tunelith.Data;

public class TunelithDbContext
{
    private readonly SQLiteAsyncConnection _database;

    public TunelithDbContext(string dbPath)
    {
        _database = new SQLiteAsyncConnection(dbPath);
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync()
    {
        await _database.CreateTableAsync<CachedTrack>();
        await _database.CreateTableAsync<CachedPlaylist>();
        await _database.CreateTableAsync<CachedPlaylistTrack>();
        await _database.CreateTableAsync<CachedCategory>();
    }

    public async Task<List<CachedTrack>> GetCachedTracksAsync()
    {
        return await _database.Table<CachedTrack>().ToListAsync();
    }

    public async Task<CachedTrack?> GetCachedTrackBySpotifyIdAsync(string spotifyId)
    {
        return await _database.Table<CachedTrack>()
            .Where(t => t.SpotifyTrackId == spotifyId)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertCachedTrackAsync(CachedTrack track)
    {
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
        foreach (var track in tracks)
        {
            await UpsertCachedTrackAsync(track);
        }
    }

    public async Task<List<CachedPlaylist>> GetCachedPlaylistsAsync()
    {
        return await _database.Table<CachedPlaylist>().ToListAsync();
    }

    public async Task<CachedPlaylist?> GetCachedPlaylistBySpotifyIdAsync(string spotifyId)
    {
        return await _database.Table<CachedPlaylist>()
            .Where(p => p.SpotifyPlaylistId == spotifyId)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertCachedPlaylistAsync(CachedPlaylist playlist)
    {
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
        return await _database.Table<CachedPlaylistTrack>()
            .Where(t => t.PlaylistId == playlistId)
            .OrderBy(t => t.Position)
            .ToListAsync();
    }

    public async Task UpsertCachedPlaylistTracksAsync(int playlistId, IEnumerable<CachedPlaylistTrack> tracks)
    {
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
        return await _database.Table<CachedCategory>().ToListAsync();
    }

    public async Task UpsertCachedCategoryAsync(CachedCategory category)
    {
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
        await _database.DeleteAllAsync<CachedTrack>();
        await _database.DeleteAllAsync<CachedPlaylist>();
        await _database.DeleteAllAsync<CachedPlaylistTrack>();
        await _database.DeleteAllAsync<CachedCategory>();
    }

    public async Task<int> GetCachedTrackCountAsync()
    {
        return await _database.Table<CachedTrack>().CountAsync();
    }
}
