using System;

class ScreensaverSettings
{
    private static ScreensaverSettings? _instance;
    public static ScreensaverSettings Instance => _instance ??= new ScreensaverSettings();

    public double SpeedMultiplier { get; set; } = 3.0;

    public int MaxWindows { get; set; } = 5;

    public bool UseTimer { get; set; } = false;

    public int TimerDurationSeconds { get; set; } = 60;

}