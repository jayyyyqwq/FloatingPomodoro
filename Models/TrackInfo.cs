namespace FloatingPomodoro.Models
{
    /// <summary>
    /// Snapshot of the currently reported media track. Artwork is carried as raw
    /// bytes so the service stays free of WPF imaging types — the BitmapImage must
    /// be built on the UI thread.
    /// </summary>
    public record TrackInfo(string Title, string Artist, byte[]? ThumbnailBytes);
}
