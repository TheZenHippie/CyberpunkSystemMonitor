using System;
using System.IO;
using System.Text.Json;

namespace CyberpunkSystemMonitor.Settings
{
    public class WidgetSettings
    {
        public string FontFamily { get; set; } = "Consolas, Cascadia Code, Segoe UI";
        public double FontSize { get; set; } = 11.0;
        public string FontColorHex { get; set; } = "#00F0FF";
        public string WindowBackgroundHex { get; set; } = "#E80A0D15";
        public double WindowOpacity { get; set; } = 0.90;
        public double FontOpacity { get; set; } = 1.0;
        public string? BackgroundImagePath { get; set; }
        public int RefreshIntervalMs { get; set; } = 1000;

        public bool LedGlowEnabled { get; set; } = true;
        public bool RgbSpectrumCycle { get; set; } = false;
        public double RgbCycleSpeed { get; set; } = 1.0;

        public bool RainbowBorderEnabled { get; set; } = false;
        public double BorderWidth { get; set; } = 3.0;

        public bool CrtScanlinesEnabled { get; set; } = false;
        public double ScanlineThickness { get; set; } = 3.0;

        public bool CrtGlitchEnabled { get; set; } = false;
        public double GlitchChance { get; set; } = 15.0;

        public bool CrtSnowEnabled { get; set; } = false;
        public double SnowAmount { get; set; } = 25.0;

        public bool AlwaysOnTop { get; set; } = true;
        public bool WindowShadow { get; set; } = true;

        public double? WindowWidth { get; set; } = 760;
        public double? WindowHeight { get; set; } = 820;
        public double? WindowLeft { get; set; }
        public double? WindowTop { get; set; }

        public string TopProcessSortBy { get; set; } = "Memory"; // "Memory", "Cpu"
        public int TopProcessCount { get; set; } = 8;

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private static string GetSettingsFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folder = Path.Combine(appData, "CyberpunkSystemMonitor");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, "settings.json");
        }

        public static WidgetSettings Load()
        {
            try
            {
                string path = GetSettingsFilePath();
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch { }

            return new WidgetSettings();
        }

        public void Save()
        {
            try
            {
                string path = GetSettingsFilePath();
                string json = JsonSerializer.Serialize(this, JsonOptions);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}

