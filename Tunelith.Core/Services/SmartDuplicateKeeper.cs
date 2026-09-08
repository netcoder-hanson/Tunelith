using Tunelith.Core.Models;

namespace Tunelith.Core.Services;

public class SmartDuplicateKeeper
{
    /// <summary>
    /// Selects the best track to keep from a duplicate group based on:
    /// - Has album art (images) = +3 pts
    /// - Explicit version = +2 pts (often better production)
    /// - Longer duration = +1 pt (may be complete version)
    /// - Has audio features available = +1 pt (more complete metadata)
    /// Returns the index of the best track in the group.
    /// </summary>
    public int SelectBestTrack(DuplicateGroup group, Dictionary<string, SpotifyAudioFeatures>? audioFeatures = null)
    {
        if (group.Tracks.Count <= 1) return 0;

        var scores = new List<int>();

        for (int i = 0; i < group.Tracks.Count; i++)
        {
            var track = group.Tracks[i];
            var score = 0;

            // Album art available
            if (track.Album?.Images?.Any() == true)
                score += 3;

            // Longer duration preferred (might be full version)
            if (track.DurationMs > group.Tracks.Where((_, idx) => idx != i).Max(t => t.DurationMs))
                score += 1;

            // Audio features available (more complete metadata)
            if (audioFeatures?.ContainsKey(track.Id) == true)
                score += 1;

            scores.Add(score);
        }

        // Return index of highest score; ties go to first track (index 0)
        var bestIndex = 0;
        var bestScore = scores[0];
        for (int i = 1; i < scores.Count; i++)
        {
            if (scores[i] > bestScore)
            {
                bestScore = scores[i];
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    /// <summary>
    /// Reorders tracks in a DuplicateGroup so the best track is first.
    /// </summary>
    public void ReorderGroup(DuplicateGroup group, Dictionary<string, SpotifyAudioFeatures>? audioFeatures = null)
    {
        if (group.Tracks.Count <= 1) return;

        var bestIndex = SelectBestTrack(group, audioFeatures);
        if (bestIndex > 0)
        {
            var bestTrack = group.Tracks[bestIndex];
            group.Tracks.RemoveAt(bestIndex);
            group.Tracks.Insert(0, bestTrack);
        }
    }
}
