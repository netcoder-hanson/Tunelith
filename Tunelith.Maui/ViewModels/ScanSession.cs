using Tunelith.Core.Models;

namespace Tunelith.Maui.ViewModels;

/// <summary>
/// Singleton holding the in-progress scan results across screen transitions.
/// All ViewModels read/write from this single instance instead of passing data through navigation.
/// </summary>
public class ScanSession
{
    public CategorizationResult? CategorizationResult { get; set; }
    public List<DuplicateGroup> Duplicates { get; set; } = new();
    public List<DuplicateTrackItem> DuplicateItems { get; set; } = new();

    public int DuplicatesRemoved { get; set; }
    public int NewPlaylists { get; set; }
    public int TracksResorted { get; set; }
    public int HealthScore { get; set; }

    public void Clear()
    {
        CategorizationResult = null;
        Duplicates = new();
        DuplicateItems = new();
        DuplicatesRemoved = 0;
        NewPlaylists = 0;
        TracksResorted = 0;
        HealthScore = 0;
    }
}
