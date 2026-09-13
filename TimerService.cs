using System;
using System.Windows.Threading;
using FloatingPomodoro.Models;

namespace FloatingPomodoro
{
    public enum TimerMode
    {
        Work,
        Rest
    }

    public class TimerService
    {
        private DispatcherTimer _timer;
        private TimeSpan _timeRemaining;
        private TimerMode _currentMode;
        private PomodoroConfig _config;

        public event Action<string> OnTick;
        public event Action<TimerMode> OnModeChange;
        public event Action OnTimerComplete;

        public TimerMode CurrentMode => _currentMode;
        public bool IsRunning => _timer.IsEnabled;

        public TimerService(PomodoroConfig config)
        {
            _config = config;
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _currentMode = TimerMode.Work;
            ResetTimer();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_timeRemaining.TotalSeconds > 0)
            {
                _timeRemaining = _timeRemaining.Subtract(TimeSpan.FromSeconds(1));
                OnTick?.Invoke(_timeRemaining.ToString(@"mm\:ss"));
            }
            else
            {
                SwitchMode();
            }
        }

        private void SwitchMode()
        {
            _timer.Stop();
            OnTimerComplete?.Invoke();

            if (_currentMode == TimerMode.Work)
            {
                _currentMode = TimerMode.Rest;
                _timeRemaining = TimeSpan.FromMinutes(_config.RestDurationMinutes);
            }
            else
            {
                _currentMode = TimerMode.Work;
                _timeRemaining = TimeSpan.FromMinutes(_config.WorkDurationMinutes);
            }

            OnModeChange?.Invoke(_currentMode);
            OnTick?.Invoke(_timeRemaining.ToString(@"mm\:ss"));
            _timer.Start();
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Pause()
        {
            _timer.Stop();
        }

        public void Reset()
        {
            _timer.Stop();
            _currentMode = TimerMode.Work;
            ResetTimer();
            OnModeChange?.Invoke(_currentMode);
        }

        private void ResetTimer()
        {
            if (_currentMode == TimerMode.Work)
            {
                _timeRemaining = TimeSpan.FromMinutes(_config.WorkDurationMinutes);
            }
            else
            {
                _timeRemaining = TimeSpan.FromMinutes(_config.RestDurationMinutes);
            }
            OnTick?.Invoke(_timeRemaining.ToString(@"mm\:ss"));
        }
        
        public string GetTimeRemaining()
        {
             return _timeRemaining.ToString(@"mm\:ss");
        }
    }
}
