namespace FloatingPomodoro.Models
{
    public class PomodoroConfig
    {
        public int WorkDurationMinutes { get; set; } = 25;
        public int RestDurationMinutes { get; set; } = 5;
        public double WindowOpacity { get; set; } = 0.8;
        public bool SoundEnabled { get; set; } = true;
    }
}
