using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace FloatingPomodoro.Services
{
    /// <summary>
    /// Sends global media keys so whichever app owns the Windows media session
    /// (YouTube Music in Chrome/Edge, or the YT Music desktop app) responds.
    /// </summary>
    public static class MediaController
    {
        private const string YouTubeMusicUrl = "https://music.youtube.com";

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        public static void PlayPause() => SendMediaKey(VK_MEDIA_PLAY_PAUSE);

        public static void NextTrack() => SendMediaKey(VK_MEDIA_NEXT_TRACK);

        public static void PreviousTrack() => SendMediaKey(VK_MEDIA_PREV_TRACK);

        public static void OpenYouTubeMusic()
        {
            try
            {
                // UseShellExecute is required for .NET Core to launch a URL.
                Process.Start(new ProcessStartInfo(YouTubeMusicUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open YouTube Music.\n{ex.Message}",
                    "Open Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void SendMediaKey(byte virtualKey)
        {
            keybd_event(virtualKey, 0, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(virtualKey, 0, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
