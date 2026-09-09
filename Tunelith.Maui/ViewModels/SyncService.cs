using Tunelith.Core.Models;
using Tunelith.Core.Services;

namespace Tunelith.Maui.ViewModels;

/// <summary>
/// Shared Spotify write logic used by both "Approve All" and "Review Selectively" paths.
/// Creates playlists and removes duplicate tracks from the user's liked songs.
/// </summary>
public class SyncService
{
    private readonly ISpotifyApiClient _spotifyClient;
    private readonly ScanSession _session;

    public SyncService(ISpotifyApiClient spotifyClient, ScanSession session)
    {
        _spotifyClient = spotifyClient;
        _session = session;
    }

    /// <summary>
    /// Applies all pending changes to Spotify: creates new playlists and removes confirmed duplicates.
    /// If specificTrackIdsToRemove is provided (from per-track toggles), only those tracks are removed.
    /// Otherwise, removes all tracks except the first in each group (approve-all behavior).
    /// Returns (playlistsCreated, duplicatesRemoved) for the success screen.
    /// </summary>
    public async Task<(int PlaylistsCreated, int DuplicatesRemoved)> ApplyAsync(
        List<PlaylistChange> playlistsToCreate,
        List<DuplicateGroup> duplicatesToRemove,
        int totalTracksResorted,
        HashSet<string>? specificTrackIdsToRemove = null)
    {
        var accessToken = await SecureStorage.GetAsync("spotify_access_token");
        if (string.IsNullOrEmpty(accessToken))
            throw new InvalidOperationException("Not authenticated.");
        await _spotifyClient.SetTokenAsync(accessToken);

        var userId = (await _spotifyClient.GetCurrentUserIdAsync()).Id;

        int playlistsCreated = 0;
        foreach (var playlistChange in playlistsToCreate)
        {
            var playlist = await _spotifyClient.CreatePlaylistAsync(
                userId, playlistChange.Name, playlistChange.Description, false);

            if (playlistChange.TrackIds.Any())
            {
                await _spotifyClient.AddTracksToPlaylistAsync(
                    playlist.Id, playlistChange.TrackIds);
            }
            playlistsCreated++;
        }

        int duplicatesRemoved = 0;
        foreach (var duplicate in duplicatesToRemove)
        {
            if (duplicate.Tracks.Count > 1)
            {
                List<string> trackIdsToRemove;

                if (specificTrackIdsToRemove != null)
                {
                    // Honor per-track toggle: only remove tracks the user confirmed as duplicates
                    trackIdsToRemove = duplicate.Tracks
                        .Where(t => specificTrackIdsToRemove.Contains(t.Id))
                        .Select(t => t.Id)
                        .ToList();
                }
                else
                {
                    // Approve-all: remove everything except the first track
                    trackIdsToRemove = duplicate.Tracks.Skip(1).Select(t => t.Id).ToList();
                }

                if (trackIdsToRemove.Any())
                {
                    await _spotifyClient.RemoveSavedTracksAsync(trackIdsToRemove);
                }
                duplicatesRemoved++;
            }
        }

        _session.DuplicatesRemoved = duplicatesRemoved;
        _session.NewPlaylists = playlistsCreated;
        _session.TracksResorted = totalTracksResorted;

        return (playlistsCreated, duplicatesRemoved);
    }
}
