using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Media;
using FloatingPomodoro.Models;
using FloatingPomodoro.Services;

namespace FloatingPomodoro
{
    public partial class MainWindow : Window
    {
        // Segoe MDL2 Assets glyphs
        private const string PlayGlyph = "";
        private const string PauseGlyph = "";

        private const string NothingPlaying = "Nothing playing";

        private TimerService _timerService;
        private PomodoroConfig _config;
        private MediaSessionService _mediaSession;
        private SoundPlayer? _soundPlayer;
        private bool _isSettingsOpen = false;

        public MainWindow()
        {
            InitializeComponent();
            _config = new PomodoroConfig();
            _timerService = new TimerService(_config);
            _timerService.OnTick += UpdateTimerDisplay;
            _timerService.OnModeChange += UpdateModeDisplay;
            _timerService.OnTimerComplete += PlayAlert;

            _mediaSession = new MediaSessionService(Dispatcher);
            _mediaSession.TrackChanged += OnTrackChanged;
            _mediaSession.PlaybackStateChanged += OnPlaybackStateChanged;
            _mediaSession.ProgressChanged += OnProgressChanged;
            _mediaSession.SessionLost += OnSessionLost;

            // Initial UI Update
            UpdateTimerDisplay(_timerService.GetTimeRemaining());
            UpdateModeDisplay(_timerService.CurrentMode);

            Loaded += MainWindow_Loaded;

            // Setup Sound
            try {
                 string soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "chime.wav");
                 if (System.IO.File.Exists(soundPath))
                 {
                     _soundPlayer = new SoundPlayer(soundPath);
                 }
                 else
                 {
                    // Fallback to beep if chime not found
                    soundPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "beep.wav");
                    if (System.IO.File.Exists(soundPath)) _soundPlayer = new SoundPlayer(soundPath);
                 }
            } catch { }
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await _mediaSession.InitializeAsync();
            }
            catch
            {
                // Media session unavailable; the pomodoro timer still works and the
                // transport buttons fall back to global media keys.
                OnSessionLost();
            }
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void StartPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_timerService.IsRunning)
            {
                _timerService.Pause();
                StartPauseButton.Content = "Start";
            }
            else
            {
                _timerService.Start();
                StartPauseButton.Content = "Pause";
            }
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            _timerService.Reset();
            StartPauseButton.Content = "Start";
        }

        private void UpdateTimerDisplay(string time)
        {
            TimerText.Text = time;
        }

        private static void ApplyTheme(Brush color, params Control[] controls)
        {
            foreach (var control in controls)
            {
                control.Foreground = color;
                control.BorderBrush = color;
            }
        }

        private static void ApplyTextTheme(Brush color, params TextBlock[] textBlocks)
        {
            foreach (var textBlock in textBlocks)
            {
                textBlock.Foreground = color;
            }
        }

        private void UpdateModeDisplay(TimerMode mode)
        {
            var color = (mode == TimerMode.Work) ? Brushes.Red : Brushes.Green;

            ModeLabel.Text = (mode == TimerMode.Work) ? "WORK" : "REST";

            PanelDivider.BorderBrush = color;
            AlbumArtBorder.BorderBrush = color;

            ApplyTextTheme(color, ModeLabel, TimerText, TrackTitle, TrackArtist,
                PositionText, AlbumArtPlaceholder, WorkLabel, RestLabel);

            ApplyTheme(color, SettingsButton, StartPauseButton, ResetButton, DoneButton,
                WorkDurationInput, RestDurationInput, MusicProgress,
                PrevTrackButton, PlayPauseButton, NextTrackButton, OpenYtMusicButton);
        }

        private void OnTrackChanged(TrackInfo track)
        {
            TrackTitle.Text = string.IsNullOrWhiteSpace(track.Title) ? NothingPlaying : track.Title;
            TrackArtist.Text = track.Artist;
            SetAlbumArt(track.ThumbnailBytes);
        }

        private void SetAlbumArt(byte[]? imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                AlbumArtBorder.Background = Brushes.Transparent;
                AlbumArtPlaceholder.Visibility = Visibility.Visible;
                return;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = new MemoryStream(imageBytes);
                bitmap.EndInit();
                bitmap.Freeze();

                AlbumArtBorder.Background = new ImageBrush(bitmap) { Stretch = Stretch.UniformToFill };
                AlbumArtPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                AlbumArtBorder.Background = Brushes.Transparent;
                AlbumArtPlaceholder.Visibility = Visibility.Visible;
            }
        }

        private void OnPlaybackStateChanged(bool isPlaying)
        {
            PlayPauseButton.Content = isPlaying ? PauseGlyph : PlayGlyph;
        }

        private void OnProgressChanged(TimeSpan position, TimeSpan duration)
        {
            MusicProgress.Value = duration > TimeSpan.Zero
                ? position.TotalSeconds / duration.TotalSeconds
                : 0;
            PositionText.Text = $"{Format(position)} / {Format(duration)}";
        }

        private static string Format(TimeSpan time) => $"{(int)time.TotalMinutes}:{time.Seconds:00}";

        private void OnSessionLost()
        {
            TrackTitle.Text = NothingPlaying;
            TrackArtist.Text = string.Empty;
            MusicProgress.Value = 0;
            PositionText.Text = "0:00 / 0:00";
            PlayPauseButton.Content = PlayGlyph;
            SetAlbumArt(null);
        }

        private async void PrevTrackButton_Click(object sender, RoutedEventArgs e)
        {
            await _mediaSession.PreviousAsync();
        }

        private async void PlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            await _mediaSession.TogglePlayPauseAsync();
        }

        private async void NextTrackButton_Click(object sender, RoutedEventArgs e)
        {
            await _mediaSession.NextAsync();
        }

        private void OpenYtMusicButton_Click(object sender, RoutedEventArgs e)
        {
            MediaController.OpenYouTubeMusic();
        }

        private async void ProgressHitArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Must handle, or the click bubbles to the window and starts a DragMove instead.
            e.Handled = true;

            if (ProgressHitArea.ActualWidth <= 0) return;

            var fraction = e.GetPosition(ProgressHitArea).X / ProgressHitArea.ActualWidth;
            await _mediaSession.SeekAsync(fraction);
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _isSettingsOpen = !_isSettingsOpen;
            if (_isSettingsOpen)
            {
                MainView.Visibility = Visibility.Collapsed;
                SettingsView.Visibility = Visibility.Visible;

                // Load current values
                WorkDurationInput.Text = _config.WorkDurationMinutes.ToString();
                RestDurationInput.Text = _config.RestDurationMinutes.ToString();
            }
            else
            {
                // Cancel/Close without saving if clicked again (or we could save, but Done button is better for explicit save)
                MainView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(WorkDurationInput.Text, out int workMinutes) &&
                int.TryParse(RestDurationInput.Text, out int restMinutes))
            {
                _config.WorkDurationMinutes = workMinutes;
                _config.RestDurationMinutes = restMinutes;

                _timerService.Reset();
                StartPauseButton.Content = "Start";

                MainView.Visibility = Visibility.Visible;
                SettingsView.Visibility = Visibility.Collapsed;
                _isSettingsOpen = false;
            }
            else
            {
                MessageBox.Show("Please enter valid numbers.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PlayAlert()
        {
            if (_config.SoundEnabled)
            {
                if (_soundPlayer != null)
                {
                    try { _soundPlayer.Play(); } catch { SystemSounds.Beep.Play(); }
                }
                else
                {
                    SystemSounds.Beep.Play();
                }
            }
        }
    }
}
