using System;
using System.Threading.Tasks;
using System.Windows.Threading;
using FloatingPomodoro.Models;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace FloatingPomodoro.Services
{
    using SessionManager = GlobalSystemMediaTransportControlsSessionManager;
    using Session = GlobalSystemMediaTransportControlsSession;

    /// <summary>
    /// Reads the current Windows media session (title, artist, artwork, position,
    /// play state) and drives transport commands against it.
    ///
    /// All WinRT callbacks arrive on threadpool threads, so every event this class
    /// raises is marshalled onto the UI dispatcher first.
    /// </summary>
    public class MediaSessionService
    {
        private const double ProgressPollSeconds = 1.0;

        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _pollTimer;

        private SessionManager? _manager;
        private Session? _session;

        // Identity of the last reported track (title + artist + artwork size), so the UI
        // is only updated on a real change. Only recomputed when the session raises
        // MediaPropertiesChanged, never on the progress poll.
        private string _lastTrackKey = string.Empty;

        public event Action<TrackInfo>? TrackChanged;
        public event Action<bool>? PlaybackStateChanged;
        public event Action<TimeSpan, TimeSpan>? ProgressChanged;
        public event Action? SessionLost;

        public bool HasSession => _session != null;

        public MediaSessionService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher;
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(ProgressPollSeconds) };
            _pollTimer.Tick += (_, _) => PublishProgress();
        }

        public async Task InitializeAsync()
        {
            _manager = await SessionManager.RequestAsync();
            _manager.CurrentSessionChanged += (_, _) => _dispatcher.InvokeAsync(AttachCurrentSession);
            AttachCurrentSession();
            _pollTimer.Start();
        }

        private void AttachCurrentSession()
        {
            DetachSession();

            _session = _manager?.GetCurrentSession();
            if (_session == null)
            {
                _lastTrackKey = string.Empty;
                SessionLost?.Invoke();
                return;
            }

            _session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;

            PublishPlaybackState();
            PublishProgress();
            _ = PublishTrackAsync();
        }

        private void DetachSession()
        {
            if (_session == null) return;

            _session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
            _session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
            _session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            _session = null;
        }

        private void OnMediaPropertiesChanged(Session sender, MediaPropertiesChangedEventArgs args) =>
            _dispatcher.InvokeAsync(() => _ = PublishTrackAsync());

        private void OnPlaybackInfoChanged(Session sender, PlaybackInfoChangedEventArgs args) =>
            _dispatcher.InvokeAsync(PublishPlaybackState);

        private void OnTimelinePropertiesChanged(Session sender, TimelinePropertiesChangedEventArgs args) =>
            _dispatcher.InvokeAsync(PublishProgress);

        private async Task PublishTrackAsync()
        {
            var session = _session;
            if (session == null) return;

            try
            {
                var props = await session.TryGetMediaPropertiesAsync();
                if (props == null) return;

                var title = props.Title ?? string.Empty;
                var artist = string.IsNullOrWhiteSpace(props.Artist) ? props.AlbumArtist ?? string.Empty : props.Artist;

                // Players report a placeholder image first (YouTube Music sends the browser
                // icon) and the real artwork a moment later, so artwork size is part of the
                // identity — keying on title+artist alone discards that second update.
                var thumbnail = await ReadThumbnailAsync(props.Thumbnail);
                var trackKey = $"{title}|{artist}|{thumbnail?.Length ?? 0}";
                if (trackKey == _lastTrackKey) return;
                _lastTrackKey = trackKey;

                await _dispatcher.InvokeAsync(() => TrackChanged?.Invoke(new TrackInfo(title, artist, thumbnail)));
            }
            catch
            {
                // Session can vanish mid-call; treated as "nothing playing" on the next poll.
            }
        }

        private static async Task<byte[]?> ReadThumbnailAsync(IRandomAccessStreamReference? reference)
        {
            if (reference == null) return null;

            try
            {
                using var stream = await reference.OpenReadAsync();
                if (stream.Size == 0) return null;

                var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);

                var bytes = new byte[stream.Size];
                reader.ReadBytes(bytes);
                return bytes;
            }
            catch
            {
                return null;
            }
        }

        private void PublishPlaybackState()
        {
            try
            {
                var status = _session?.GetPlaybackInfo()?.PlaybackStatus;
                PlaybackStateChanged?.Invoke(
                    status == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
            }
            catch
            {
                PlaybackStateChanged?.Invoke(false);
            }
        }

        private void PublishProgress()
        {
            var session = _session;
            if (session == null) return;

            try
            {
                var timeline = session.GetTimelineProperties();
                var duration = timeline.EndTime - timeline.StartTime;
                if (duration <= TimeSpan.Zero)
                {
                    ProgressChanged?.Invoke(TimeSpan.Zero, TimeSpan.Zero);
                    return;
                }

                var position = timeline.Position - timeline.StartTime;

                // Timeline is a snapshot that does not tick — extrapolate while playing.
                var isPlaying = session.GetPlaybackInfo()?.PlaybackStatus ==
                                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                if (isPlaying)
                {
                    position += DateTimeOffset.Now - timeline.LastUpdatedTime;
                }

                if (position < TimeSpan.Zero) position = TimeSpan.Zero;
                if (position > duration) position = duration;

                ProgressChanged?.Invoke(position, duration);
            }
            catch
            {
                // Ignore; next tick retries.
            }
        }

        public async Task TogglePlayPauseAsync()
        {
            var session = _session;
            if (session == null) { MediaController.PlayPause(); return; }

            try { await session.TryTogglePlayPauseAsync(); }
            catch { MediaController.PlayPause(); }
        }

        public async Task NextAsync()
        {
            var session = _session;
            if (session == null) { MediaController.NextTrack(); return; }

            try { await session.TrySkipNextAsync(); }
            catch { MediaController.NextTrack(); }
        }

        public async Task PreviousAsync()
        {
            var session = _session;
            if (session == null) { MediaController.PreviousTrack(); return; }

            try { await session.TrySkipPreviousAsync(); }
            catch { MediaController.PreviousTrack(); }
        }

        public async Task SeekAsync(double fraction)
        {
            var session = _session;
            if (session == null) return;

            fraction = Math.Clamp(fraction, 0.0, 1.0);

            try
            {
                var timeline = session.GetTimelineProperties();
                var duration = timeline.EndTime - timeline.StartTime;
                if (duration <= TimeSpan.Zero) return;

                var target = timeline.StartTime + TimeSpan.FromTicks((long)(duration.Ticks * fraction));
                await session.TryChangePlaybackPositionAsync(target.Ticks);
            }
            catch
            {
                // Player does not support seeking; ignore.
            }
        }
    }
}
