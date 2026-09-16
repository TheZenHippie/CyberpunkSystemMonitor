using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using CyberpunkSystemMonitor.Controls;
using CyberpunkSystemMonitor.Models;
using CyberpunkSystemMonitor.Services;
using CyberpunkSystemMonitor.Settings;

namespace CyberpunkSystemMonitor
{
    public partial class MainWindow : Window
    {
        private static readonly Random _random = new Random();
        private static readonly SolidColorBrush[] GlitchBrushes = CreateGlitchBrushes();
        private static readonly List<WriteableBitmap> SharedSnowBitmaps = new List<WriteableBitmap>();
        private static readonly object SnowLock = new object();

        private static readonly DoubleAnimation RainbowAnimation = new DoubleAnimation
        {
            From = 0,
            To = 360,
            Duration = TimeSpan.FromSeconds(4),
            RepeatBehavior = RepeatBehavior.Forever
        };

        private readonly WidgetSettings _settings;
        private readonly SystemMetricsService _metricsService;
        private bool _isInitialized = false;
        private bool _isClosed = false;
        private bool _isSampling = false;

        // Timers
        private readonly DispatcherTimer _telemetryTimer;
        private readonly DispatcherTimer _rgbTimer;
        private readonly DispatcherTimer _glitchTimer;
        private readonly DispatcherTimer _snowTimer;
        private readonly DispatcherTimer _menuHoverTimer;

        // RGB State
        private bool _isRgbCycle = false;
        private double _rgbCycleSpeed = 1.0;
        private double _hue = 180.0; // Cyan
        private Color _currentAccentColor = Color.FromRgb(0x00, 0xF0, 0xFF);

        // Visual FX State
        private bool _rainbowBorderEnabled = false;
        private double _borderWidth = 3.0;

        private bool _crtScanlinesEnabled = false;
        private double _scanlineThickness = 3.0;

        private bool _crtGlitchEnabled = false;
        private double _glitchChance = 15.0;

        private bool _crtSnowEnabled = false;
        private double _snowAmount = 25.0;
        private int _snowFrameIndex = 0;

        public MainWindow()
        {
            InitializeComponent();

            _settings = WidgetSettings.Load();
            _metricsService = new SystemMetricsService();

            // Telemetry update timer
            _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_settings.RefreshIntervalMs) };
            _telemetryTimer.Tick += TelemetryTimer_Tick;

            // RGB color cycling timer (30 fps)
            _rgbTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _rgbTimer.Tick += RgbTimer_Tick;

            // Glitch FX timer (50ms)
            _glitchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _glitchTimer.Tick += GlitchTimer_Tick;

            // Snow static noise timer (~15 fps)
            _snowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(65) };
            _snowTimer.Tick += SnowTimer_Tick;

            // Context menu hover bridge timer
            _menuHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _menuHoverTimer.Tick += (s, e) => _menuHoverTimer.Stop();

            ApplySettings();
            _isInitialized = true;

            _telemetryTimer.Start();
            TriggerTelemetrySample();
        }

        private static SolidColorBrush[] CreateGlitchBrushes()
        {
            var colors = new[]
            {
                Color.FromArgb(180, 255, 0, 85),    // Neon Pink/Red
                Color.FromArgb(180, 0, 240, 255),   // Electric Cyan
                Color.FromArgb(180, 0, 255, 102),   // Cyber Green
                Color.FromArgb(180, 255, 230, 0),   // Bright Amber
                Color.FromArgb(140, 255, 255, 255), // Glitch White
                Color.FromArgb(160, 160, 64, 255)   // Night City Violet
            };

            var brushes = new SolidColorBrush[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                brushes[i] = new SolidColorBrush(colors[i]);
                brushes[i].Freeze();
            }
            return brushes;
        }

        private static List<WriteableBitmap> GetOrCreateSnowBitmaps()
        {
            if (SharedSnowBitmaps.Count == 8) return SharedSnowBitmaps;

            lock (SnowLock)
            {
                if (SharedSnowBitmaps.Count == 8) return SharedSnowBitmaps;

                int w = 256, h = 256;
                for (int f = 0; f < 8; f++)
                {
                    var wb = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
                    int stride = w * 4;
                    byte[] pixels = new byte[stride * h];
                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        byte val = (byte)_random.Next(256);
                        bool colorSpeck = _random.Next(25) == 0;
                        pixels[i] = colorSpeck ? (byte)_random.Next(256) : val;     // B
                        pixels[i + 1] = colorSpeck ? (byte)_random.Next(256) : val; // G
                        pixels[i + 2] = colorSpeck ? (byte)_random.Next(256) : val; // R
                        pixels[i + 3] = 255;
                    }
                    wb.WritePixels(new Int32Rect(0, 0, w, h), pixels, stride, 0);
                    wb.Freeze();
                    SharedSnowBitmaps.Add(wb);
                }
                return SharedSnowBitmaps;
            }
        }

        #region Telemetry Loop & UI Dispatching

        private void TelemetryTimer_Tick(object? sender, EventArgs e)
        {
            if (_isClosed || WindowState == WindowState.Minimized || Visibility != Visibility.Visible)
                return;

            TriggerTelemetrySample();
        }

        private void TriggerTelemetrySample()
        {
            if (_isSampling || _isClosed) return;
            _isSampling = true;

            string sortBy = _settings.TopProcessSortBy;
            int count = _settings.TopProcessCount;

            Task.Run(() =>
            {
                if (_isClosed) return null;
                try
                {
                    return _metricsService.CaptureSnapshot(sortBy, count);
                }
                catch
                {
                    return null;
                }
            }).ContinueWith(task =>
            {
                _isSampling = false;
                if (_isClosed || task.Result == null) return;

                Dispatcher.InvokeAsync(() =>
                {
                    if (!_isClosed)
                    {
                        RenderSnapshot(task.Result);
                    }
                }, DispatcherPriority.Background);
            });
        }

        private void RenderSnapshot(SystemSnapshot s)
        {
            if (_isClosed) return;

            // CPU
            CpuLedStat.Text = $"CPU: {s.Cpu.TotalUsagePercent,4:F1}%";
            CpuTotalBar.Value = s.Cpu.TotalUsagePercent;
            CpuLoadSummaryText.Text = $"{s.Cpu.TotalUsagePercent:F1}%";
            CpuModelText.Text = s.Cpu.CpuName;
            CpuCoresText.Text = $"Cores: {s.Cpu.PhysicalCoresCount}P / {s.Cpu.LogicalCoresCount}T";
            CpuProcessesText.Text = $"Procs: {s.Cpu.ProcessCount:N0}";
            CpuThreadsText.Text = $"Threads: {s.Cpu.ThreadCount:N0}";
            CpuCoresItemsControl.ItemsSource = s.Cpu.Cores;
            HeaderCpuBadge.Text = $" • CPU: {s.Cpu.TotalUsagePercent:F1}%";

            // RAM
            RamLedStat.Text = $"RAM: {s.Memory.UsagePercent,4:F1}%";
            RamBar.Value = s.Memory.UsagePercent;
            RamPercentText.Text = $"{s.Memory.UsagePercent:F1}%";
            RamDetailsText.Text = $"Used: {s.Memory.UsedPhysicalGb:F1} GB / Total: {s.Memory.TotalPhysicalGb:F1} GB";
            RamAvailableText.Text = $"Available: {s.Memory.AvailablePhysicalGb:F1} GB Free";
            RamCommitText.Text = $"Commit: {s.Memory.CommitUsedGb:F1} GB / {s.Memory.CommitTotalGb:F1} GB ({s.Memory.CommitUsagePercent:F0}%)";

            // GPU
            GpuNameText.Text = s.Gpu.GpuName;
            if (s.Gpu.IsAvailable)
            {
                GpuLedStat.Text = $"GPU: {s.Gpu.UsagePercent,4:F1}%";
                GpuLoadBar.Value = s.Gpu.UsagePercent;
                GpuLoadText.Text = $"{s.Gpu.UsagePercent:F1}%";
                VramBar.Value = s.Gpu.VramUsagePercent;
                VramDetailsText.Text = $"{s.Gpu.UsedVramGb:F1} GB / {s.Gpu.TotalVramGb:F1} GB ({s.Gpu.VramUsagePercent:F0}%)";
            }
            else
            {
                GpuLedStat.Text = "GPU: --.-%";
                GpuLoadBar.Value = 0;
                GpuLoadText.Text = "--";
                VramBar.Value = 0;
                VramDetailsText.Text = "Integrated / Unmetered";
            }

            // Drives
            DrivesItemsControl.ItemsSource = s.Drives;

            // Network
            NetworkAdapterText.Text = $"Adapter: {s.Network.AdapterName} [{s.Network.AdapterType}]";
            NetworkIpText.Text = $"IP: {s.Network.IpAddress}";
            NetRxLedStat.Text = $"↓ {s.Network.DownloadSpeedFormatted}";
            NetTxLedStat.Text = $"↑ {s.Network.UploadSpeedFormatted}";
            NetPacketsRecvText.Text = $"Packets Recv: {s.Network.TotalPacketsReceived:N0} ({s.Network.TotalMbReceived:F1} MB)";
            NetPacketsSentText.Text = $"Packets Sent: {s.Network.TotalPacketsSent:N0} ({s.Network.TotalMbSent:F1} MB)";

            // Top Processes
            ProcessesItemsControl.ItemsSource = s.TopProcesses;

            // Footer
            UptimeText.Text = $"UPTIME: {s.Uptime.Days}d {s.Uptime.Hours:00}:{s.Uptime.Minutes:00}:{s.Uptime.Seconds:00}";
            RefreshStatusText.Text = $"POLLING: {_settings.RefreshIntervalMs}ms";
            PulseLed.IsOn = !PulseLed.IsOn;
        }

        #endregion

        #region Settings Application & Persistence

        private void ApplySettings()
        {
            // Window Bounds
            if (_settings.WindowWidth.HasValue && _settings.WindowWidth.Value >= MinWidth)
                Width = _settings.WindowWidth.Value;
            if (_settings.WindowHeight.HasValue && _settings.WindowHeight.Value >= MinHeight)
                Height = _settings.WindowHeight.Value;

            if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
            {
                if (_settings.WindowLeft.Value >= 0 && _settings.WindowTop.Value >= 0 &&
                    _settings.WindowLeft.Value < SystemParameters.VirtualScreenWidth - 100 &&
                    _settings.WindowTop.Value < SystemParameters.VirtualScreenHeight - 100)
                {
                    WindowStartupLocation = WindowStartupLocation.Manual;
                    Left = _settings.WindowLeft.Value;
                    Top = _settings.WindowTop.Value;
                }
                else
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen;
                }
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            // Topmost & Shadow
            Topmost = _settings.AlwaysOnTop;
            AlwaysOnTopMenuItem.IsChecked = _settings.AlwaysOnTop;
            PinBtn.Foreground = _settings.AlwaysOnTop ? new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF)) : new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));

            MainDropShadow.Opacity = _settings.WindowShadow ? 0.35 : 0.0;

            // Opacity & Tint
            _settings.WindowOpacity = Math.Clamp(_settings.WindowOpacity, 0.30, 1.0);
            OpacitySlider.Value = _settings.WindowOpacity;
            OpacityValueText.Text = $"{Math.Round(_settings.WindowOpacity * 100)}%";
            MainBackgroundBorder.Opacity = _settings.WindowOpacity;

            ApplyWindowTint(_settings.WindowBackgroundHex);

            // Polling interval
            UpdateRefreshIntervalMenuCheckmarks(_settings.RefreshIntervalMs);
            _telemetryTimer.Interval = TimeSpan.FromMilliseconds(_settings.RefreshIntervalMs);

            // Process list limit & sort
            UpdateProcessLimitMenuCheckmarks(_settings.TopProcessCount);
            UpdateProcessSortMenuCheckmarks(_settings.TopProcessSortBy);
            UpdateSortButtonsUi();

            // Accent Color & RGB
            _isRgbCycle = _settings.RgbSpectrumCycle;
            _rgbCycleSpeed = Math.Clamp(_settings.RgbCycleSpeed, 0.2, 3.0);
            RgbCycleMenuItem.IsChecked = _isRgbCycle;
            RgbCycleSpeedSlider.Value = _rgbCycleSpeed;
            RgbCycleSpeedValueText.Text = $"{Math.Round(_rgbCycleSpeed, 1)}x";

            _currentAccentColor = ParseHexColor(_settings.FontColorHex, Color.FromRgb(0x00, 0xF0, 0xFF));
            ApplyAccentColor(_currentAccentColor);
            UpdateColorMenuCheckmarks(_settings.FontColorHex);

            if (_isRgbCycle)
            {
                _rgbTimer.Start();
            }
            else
            {
                _rgbTimer.Stop();
            }

            // Visual FX Suite
            _rainbowBorderEnabled = _settings.RainbowBorderEnabled;
            _borderWidth = Math.Clamp(_settings.BorderWidth, 1.0, 10.0);
            RainbowBorderMenuItem.IsChecked = _rainbowBorderEnabled;
            RainbowBorderThicknessSlider.Value = _borderWidth;
            RainbowBorderThicknessValueText.Text = $"{Math.Round(_borderWidth)}px";

            _crtScanlinesEnabled = _settings.CrtScanlinesEnabled;
            _scanlineThickness = Math.Clamp(_settings.ScanlineThickness, 2.0, 10.0);
            CrtScanlinesMenuItem.IsChecked = _crtScanlinesEnabled;
            ScanlineThicknessSlider.Value = _scanlineThickness;
            ScanlineThicknessValueText.Text = $"{Math.Round(_scanlineThickness)}px";

            _crtGlitchEnabled = _settings.CrtGlitchEnabled;
            _glitchChance = Math.Clamp(_settings.GlitchChance, 1.0, 100.0);
            CrtGlitchMenuItem.IsChecked = _crtGlitchEnabled;
            GlitchChanceSlider.Value = _glitchChance;
            GlitchChanceValueText.Text = $"{Math.Round(_glitchChance)}%";

            _crtSnowEnabled = _settings.CrtSnowEnabled;
            _snowAmount = Math.Clamp(_settings.SnowAmount, 5.0, 100.0);
            CrtSnowMenuItem.IsChecked = _crtSnowEnabled;
            SnowAmountSlider.Value = _snowAmount;
            SnowAmountValueText.Text = $"{Math.Round(_snowAmount)}%";

            LedGlowMenuItem.IsChecked = _settings.LedGlowEnabled;
            ApplyLedGlow(_settings.LedGlowEnabled);

            ApplyEffectsState();

            // Font
            ApplyCustomFont(_settings.FontFamily, _settings.FontSize);
        }

        private void SaveCurrentSettings()
        {
            if (!_isInitialized || _settings == null) return;

            _settings.AlwaysOnTop = Topmost;
            _settings.RainbowBorderEnabled = _rainbowBorderEnabled;
            _settings.BorderWidth = _borderWidth;

            _settings.CrtScanlinesEnabled = _crtScanlinesEnabled;
            _settings.ScanlineThickness = _scanlineThickness;

            _settings.CrtGlitchEnabled = _crtGlitchEnabled;
            _settings.GlitchChance = _glitchChance;

            _settings.CrtSnowEnabled = _crtSnowEnabled;
            _settings.SnowAmount = _snowAmount;

            _settings.RgbSpectrumCycle = _isRgbCycle;
            _settings.RgbCycleSpeed = _rgbCycleSpeed;

            if (WindowState == WindowState.Normal)
            {
                _settings.WindowWidth = ActualWidth;
                _settings.WindowHeight = ActualHeight;
                _settings.WindowLeft = Left;
                _settings.WindowTop = Top;
            }

            _settings.Save();
        }

        #endregion

        #region Visual FX Suite (Rainbow, Scanlines, Glitch, Snow)

        private void ApplyEffectsState()
        {
            // Rainbow border
            if (_rainbowBorderEnabled)
            {
                CyberpunkRainbowBorder.BorderThickness = new Thickness(_borderWidth);
                CyberpunkRainbowBorder.Visibility = Visibility.Visible;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, RainbowAnimation);
            }
            else
            {
                CyberpunkRainbowBorder.Visibility = Visibility.Collapsed;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }

            // Scanlines
            CrtScanlinesOverlay.Visibility = _crtScanlinesEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (_crtScanlinesEnabled)
            {
                UpdateScanlinesBrush(_scanlineThickness);
            }

            // Glitch
            CrtGlitchCanvas.Visibility = _crtGlitchEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (_crtGlitchEnabled)
            {
                _glitchTimer.Start();
            }
            else
            {
                _glitchTimer.Stop();
                CrtGlitchCanvas.Children.Clear();
                ContentJitterTransform.X = 0;
                ContentJitterTransform.Y = 0;
            }

            // Snow Static
            CrtSnowOverlay.Visibility = _crtSnowEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (_crtSnowEnabled)
            {
                CrtSnowOverlay.Opacity = (_snowAmount / 100.0) * 0.40;
                _snowTimer.Start();
            }
            else
            {
                _snowTimer.Stop();
                CrtSnowOverlay.Opacity = 0.0;
            }
        }

        private void RainbowBorder_Click(object sender, RoutedEventArgs e)
        {
            _rainbowBorderEnabled = RainbowBorderMenuItem.IsChecked;
            _settings.RainbowBorderEnabled = _rainbowBorderEnabled;

            if (_rainbowBorderEnabled)
            {
                CyberpunkRainbowBorder.BorderThickness = new Thickness(_borderWidth);
                CyberpunkRainbowBorder.Visibility = Visibility.Visible;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, RainbowAnimation);
            }
            else
            {
                CyberpunkRainbowBorder.Visibility = Visibility.Collapsed;
                RainbowRotateTransform.BeginAnimation(RotateTransform.AngleProperty, null);
            }

            SaveCurrentSettings();
        }

        private void RainbowBorderThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _borderWidth = e.NewValue;
            _settings.BorderWidth = _borderWidth;

            if (RainbowBorderThicknessValueText != null)
            {
                RainbowBorderThicknessValueText.Text = $"{Math.Round(_borderWidth)}px";
            }
            if (_rainbowBorderEnabled && CyberpunkRainbowBorder != null)
            {
                CyberpunkRainbowBorder.BorderThickness = new Thickness(_borderWidth);
            }

            SaveCurrentSettings();
        }

        private void CrtScanlines_Click(object sender, RoutedEventArgs e)
        {
            _crtScanlinesEnabled = CrtScanlinesMenuItem.IsChecked;
            _settings.CrtScanlinesEnabled = _crtScanlinesEnabled;

            CrtScanlinesOverlay.Visibility = _crtScanlinesEnabled ? Visibility.Visible : Visibility.Collapsed;
            if (_crtScanlinesEnabled)
            {
                UpdateScanlinesBrush(_scanlineThickness);
            }

            SaveCurrentSettings();
        }

        private void ScanlineThicknessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _scanlineThickness = e.NewValue;
            _settings.ScanlineThickness = _scanlineThickness;

            if (ScanlineThicknessValueText != null)
            {
                ScanlineThicknessValueText.Text = $"{Math.Round(_scanlineThickness)}px";
            }
            if (_crtScanlinesEnabled)
            {
                UpdateScanlinesBrush(_scanlineThickness);
            }

            SaveCurrentSettings();
        }

        private void UpdateScanlinesBrush(double thickness)
        {
            double totalHeight = Math.Max(2.0, thickness * 2.0);
            CrtDrawingBrush.Viewport = new Rect(0, 0, 1, totalHeight);

            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(new GeometryDrawing(
                Brushes.Transparent,
                null,
                new RectangleGeometry(new Rect(0, 0, 1, totalHeight))));

            var scanlineColor = Color.FromArgb(240, 0, 0, 0);
            var scanlineBrush = new SolidColorBrush(scanlineColor);
            scanlineBrush.Freeze();

            drawingGroup.Children.Add(new GeometryDrawing(
                scanlineBrush,
                null,
                new RectangleGeometry(new Rect(0, 0, 1, thickness))));

            CrtDrawingBrush.Drawing = drawingGroup;
        }

        private void CrtGlitch_Click(object sender, RoutedEventArgs e)
        {
            _crtGlitchEnabled = CrtGlitchMenuItem.IsChecked;
            _settings.CrtGlitchEnabled = _crtGlitchEnabled;

            CrtGlitchCanvas.Visibility = _crtGlitchEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (_crtGlitchEnabled)
            {
                _glitchTimer.Start();
            }
            else
            {
                _glitchTimer.Stop();
                CrtGlitchCanvas.Children.Clear();
                ContentJitterTransform.X = 0;
                ContentJitterTransform.Y = 0;
            }

            SaveCurrentSettings();
        }

        private void GlitchChanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _glitchChance = e.NewValue;
            _settings.GlitchChance = _glitchChance;

            if (GlitchChanceValueText != null)
            {
                GlitchChanceValueText.Text = $"{Math.Round(_glitchChance)}%";
            }

            SaveCurrentSettings();
        }

        private void GlitchTimer_Tick(object? sender, EventArgs e)
        {
            if (!_crtGlitchEnabled || _isClosed) return;

            CrtGlitchCanvas.Children.Clear();

            if (_random.Next(100) < _glitchChance)
            {
                int sliceCount = _random.Next(2, 6);
                double w = CrtGlitchCanvas.ActualWidth;
                double h = CrtGlitchCanvas.ActualHeight;

                if (w > 20 && h > 20)
                {
                    for (int i = 0; i < sliceCount; i++)
                    {
                        var rect = new Rectangle
                        {
                            Width = _random.Next(40, (int)w),
                            Height = _random.Next(2, 8),
                            Fill = GlitchBrushes[_random.Next(GlitchBrushes.Length)],
                            Opacity = _random.NextDouble() * 0.7 + 0.3
                        };

                        Canvas.SetLeft(rect, _random.Next(-10, (int)w - 20));
                        Canvas.SetTop(rect, _random.Next(0, (int)h - 8));
                        CrtGlitchCanvas.Children.Add(rect);
                    }

                    // Subtle micro jitter
                    ContentJitterTransform.X = (_random.NextDouble() * 5.0) - 2.5;
                    ContentJitterTransform.Y = (_random.NextDouble() * 3.0) - 1.5;
                }
            }
            else
            {
                ContentJitterTransform.X = 0;
                ContentJitterTransform.Y = 0;
            }
        }

        private void CrtSnow_Click(object sender, RoutedEventArgs e)
        {
            _crtSnowEnabled = CrtSnowMenuItem.IsChecked;
            _settings.CrtSnowEnabled = _crtSnowEnabled;

            CrtSnowOverlay.Visibility = _crtSnowEnabled ? Visibility.Visible : Visibility.Collapsed;

            if (_crtSnowEnabled)
            {
                CrtSnowOverlay.Opacity = (_snowAmount / 100.0) * 0.40;
                _snowTimer.Start();
            }
            else
            {
                _snowTimer.Stop();
                CrtSnowOverlay.Opacity = 0.0;
            }

            SaveCurrentSettings();
        }

        private void SnowAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _snowAmount = e.NewValue;
            _settings.SnowAmount = _snowAmount;

            if (SnowAmountValueText != null)
            {
                SnowAmountValueText.Text = $"{Math.Round(_snowAmount)}%";
            }
            if (_crtSnowEnabled && CrtSnowOverlay != null)
            {
                CrtSnowOverlay.Opacity = (_snowAmount / 100.0) * 0.40;
            }

            SaveCurrentSettings();
        }

        private void SnowTimer_Tick(object? sender, EventArgs e)
        {
            if (!_crtSnowEnabled || _isClosed) return;

            var frames = GetOrCreateSnowBitmaps();
            _snowFrameIndex = (_snowFrameIndex + 1) % frames.Count;
            CrtSnowOverlay.Source = frames[_snowFrameIndex];
        }

        private void LedGlow_Click(object sender, RoutedEventArgs e)
        {
            _settings.LedGlowEnabled = LedGlowMenuItem.IsChecked;
            ApplyLedGlow(_settings.LedGlowEnabled);
            SaveCurrentSettings();
        }

        private void ApplyLedGlow(bool enabled)
        {
            double glow = enabled ? 6.0 : 0.0;
            CpuLedStat.GlowBlurRadius = glow;
            RamLedStat.GlowBlurRadius = glow;
            GpuLedStat.GlowBlurRadius = glow;
            NetRxLedStat.GlowBlurRadius = glow;
            NetTxLedStat.GlowBlurRadius = glow;
        }

        #endregion

        #region Accent Color & RGB Spectrum

        private void RgbTimer_Tick(object? sender, EventArgs e)
        {
            if (_isClosed || !_isRgbCycle) return;

            _hue = (_hue + (45.0 * _rgbCycleSpeed * 0.033)) % 360.0;
            _currentAccentColor = ColorFromHsv(_hue, 1.0, 1.0);
            ApplyAccentColor(_currentAccentColor);
        }

        private void RgbCycle_Click(object sender, RoutedEventArgs e)
        {
            _isRgbCycle = RgbCycleMenuItem.IsChecked;
            _settings.RgbSpectrumCycle = _isRgbCycle;

            if (_isRgbCycle)
            {
                _rgbTimer.Start();
                ClearColorMenuCheckmarks();
            }
            else
            {
                _rgbTimer.Stop();
                _currentAccentColor = ParseHexColor(_settings.FontColorHex, Color.FromRgb(0x00, 0xF0, 0xFF));
                ApplyAccentColor(_currentAccentColor);
                UpdateColorMenuCheckmarks(_settings.FontColorHex);
            }

            SaveCurrentSettings();
        }

        private void RgbCycleSpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _rgbCycleSpeed = e.NewValue;
            _settings.RgbCycleSpeed = _rgbCycleSpeed;

            if (RgbCycleSpeedValueText != null)
            {
                RgbCycleSpeedValueText.Text = $"{Math.Round(_rgbCycleSpeed, 1)}x";
            }

            SaveCurrentSettings();
        }

        private void LedColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string hex)
            {
                _isRgbCycle = false;
                RgbCycleMenuItem.IsChecked = false;
                _rgbTimer.Stop();

                _currentAccentColor = ParseHexColor(hex, Color.FromRgb(0x00, 0xF0, 0xFF));
                _settings.FontColorHex = hex;

                ApplyAccentColor(_currentAccentColor);
                UpdateColorMenuCheckmarks(hex);
                SaveCurrentSettings();
            }
        }

        private void CustomColor_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Window
            {
                Title = "Select HUD Accent Color",
                Width = 320,
                Height = 160,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Background = new SolidColorBrush(Color.FromRgb(20, 24, 35)),
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.ToolWindow
            };

            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock
            {
                Text = "Enter Hex Color Code (e.g. #00F0FF, #FF007F):",
                Foreground = Brushes.WhiteSmoke,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var tb = new TextBox
            {
                Text = $"#{_currentAccentColor.R:X2}{_currentAccentColor.G:X2}{_currentAccentColor.B:X2}",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 12)
            };
            sp.Children.Add(tb);

            var btnOk = new Button
            {
                Content = "Apply Accent",
                Width = 100,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Right,
                IsDefault = true
            };
            btnOk.Click += (s, args) =>
            {
                try
                {
                    string hex = tb.Text.Trim();
                    if (!hex.StartsWith("#")) hex = "#" + hex;
                    var color = (Color)ColorConverter.ConvertFromString(hex);

                    _isRgbCycle = false;
                    RgbCycleMenuItem.IsChecked = false;
                    _rgbTimer.Stop();

                    _currentAccentColor = color;
                    _settings.FontColorHex = hex;

                    ApplyAccentColor(_currentAccentColor);
                    ClearColorMenuCheckmarks();
                    SaveCurrentSettings();

                    dialog.DialogResult = true;
                    dialog.Close();
                }
                catch
                {
                    MessageBox.Show("Please enter a valid hex color (#RRGGBB).", "Invalid Color", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            };
            sp.Children.Add(btnOk);

            dialog.Content = sp;
            dialog.ShowDialog();
        }

        private void ApplyAccentColor(Color c)
        {
            var brush = new SolidColorBrush(c);
            brush.Freeze();

            CpuLedStat.LedBrush = brush;
            MainBackgroundBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(50, c.R, c.G, c.B));
            MainDropShadow.Color = c;
            RainbowGlowEffect.Color = c;
        }

        private void UpdateColorMenuCheckmarks(string currentHex)
        {
            foreach (var item in LedColorMenuItem.Items)
            {
                if (item is MenuItem mi && mi.Tag is string hex)
                {
                    mi.IsChecked = string.Equals(hex, currentHex, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private void ClearColorMenuCheckmarks()
        {
            foreach (var item in LedColorMenuItem.Items)
            {
                if (item is MenuItem mi)
                {
                    mi.IsChecked = false;
                }
            }
        }

        public static Color ColorFromHsv(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = Math.Clamp(value, 0.0, 1.0) * 255.0;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1.0 - saturation));
            byte q = Convert.ToByte(value * (1.0 - f * saturation));
            byte t = Convert.ToByte(value * (1.0 - (1.0 - f) * saturation));

            return hi switch
            {
                0 => Color.FromRgb(v, t, p),
                1 => Color.FromRgb(q, v, p),
                2 => Color.FromRgb(p, v, t),
                3 => Color.FromRgb(p, q, v),
                4 => Color.FromRgb(t, p, v),
                _ => Color.FromRgb(v, p, q),
            };
        }

        private static Color ParseHexColor(string hex, Color fallback)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return fallback;
                var obj = ColorConverter.ConvertFromString(hex);
                if (obj is Color c) return c;
            }
            catch { }
            return fallback;
        }

        #endregion

        #region Polling, Processes & Sorting

        private void RefreshInterval_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string tag && int.TryParse(tag, out int ms))
            {
                _settings.RefreshIntervalMs = ms;
                _telemetryTimer.Interval = TimeSpan.FromMilliseconds(ms);
                UpdateRefreshIntervalMenuCheckmarks(ms);
                SaveCurrentSettings();
            }
        }

        private void UpdateRefreshIntervalMenuCheckmarks(int intervalMs)
        {
            foreach (var item in RefreshIntervalMenuItem.Items)
            {
                if (item is MenuItem mi && mi.Tag is string tag && int.TryParse(tag, out int ms))
                {
                    mi.IsChecked = (ms == intervalMs);
                }
            }
        }

        private void ProcessLimit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string tag && int.TryParse(tag, out int count))
            {
                _settings.TopProcessCount = count;
                UpdateProcessLimitMenuCheckmarks(count);
                SaveCurrentSettings();
                TriggerTelemetrySample();
            }
        }

        private void UpdateProcessLimitMenuCheckmarks(int count)
        {
            foreach (var item in ProcessLimitMenuItem.Items)
            {
                if (item is MenuItem mi && mi.Tag is string tag && int.TryParse(tag, out int c))
                {
                    mi.IsChecked = (c == count);
                }
            }
        }

        private void ProcessSort_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string mode)
            {
                _settings.TopProcessSortBy = mode;
                UpdateProcessSortMenuCheckmarks(mode);
                UpdateSortButtonsUi();
                SaveCurrentSettings();
                TriggerTelemetrySample();
            }
        }

        private void UpdateProcessSortMenuCheckmarks(string mode)
        {
            foreach (var item in ProcessSortMenuItem.Items)
            {
                if (item is MenuItem mi && mi.Tag is string tag)
                {
                    mi.IsChecked = string.Equals(tag, mode, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private void SortMem_Click(object sender, RoutedEventArgs e)
        {
            _settings.TopProcessSortBy = "Memory";
            UpdateProcessSortMenuCheckmarks("Memory");
            UpdateSortButtonsUi();
            SaveCurrentSettings();
            TriggerTelemetrySample();
        }

        private void SortCpu_Click(object sender, RoutedEventArgs e)
        {
            _settings.TopProcessSortBy = "Cpu";
            UpdateProcessSortMenuCheckmarks("Cpu");
            UpdateSortButtonsUi();
            SaveCurrentSettings();
            TriggerTelemetrySample();
        }

        private void UpdateSortButtonsUi()
        {
            bool isMem = string.Equals(_settings.TopProcessSortBy, "Memory", StringComparison.OrdinalIgnoreCase);
            SortMemBtn.Foreground = isMem ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66)) : new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            SortCpuBtn.Foreground = !isMem ? new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF)) : new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            SortMemBtn.Content = isMem ? "MEM ▼" : "MEM";
            SortCpuBtn.Content = !isMem ? "CPU ▼" : "CPU";
        }

        #endregion

        #region Opacity, Tint & Font

        private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || _settings == null) return;

            _settings.WindowOpacity = e.NewValue;
            if (OpacityValueText != null)
            {
                OpacityValueText.Text = $"{Math.Round(_settings.WindowOpacity * 100)}%";
            }
            if (MainBackgroundBorder != null)
            {
                MainBackgroundBorder.Opacity = _settings.WindowOpacity;
            }

            SaveCurrentSettings();
        }

        private void WindowTint_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem item && item.Tag is string hex)
            {
                _settings.WindowBackgroundHex = hex;
                ApplyWindowTint(hex);
                SaveCurrentSettings();
            }
        }

        private void ApplyWindowTint(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                MainBackgroundBorder.Background = new SolidColorBrush(color);
            }
            catch
            {
                MainBackgroundBorder.Background = new SolidColorBrush(Color.FromArgb(232, 10, 13, 21));
            }
        }

        private void FontSettings_Click(object sender, RoutedEventArgs e)
        {
            var fontPicker = new FontPickerWindow(_settings.FontFamily, _settings.FontSize)
            {
                Owner = this
            };

            if (fontPicker.ShowDialog() == true && fontPicker.SelectedFont != null)
            {
                _settings.FontFamily = fontPicker.SelectedFont.Source;
                _settings.FontSize = fontPicker.SelectedFontSize;
                ApplyCustomFont(_settings.FontFamily, _settings.FontSize);
                SaveCurrentSettings();
            }
        }

        private void ApplyCustomFont(string family, double size)
        {
            try
            {
                var font = new FontFamily(family);
                TextElement.SetFontFamily(this, font);
                TextElement.SetFontSize(this, Math.Max(8.0, size));
            }
            catch { }
        }

        #endregion

        #region Window Drag, Sizing, and Commands

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Update clips if any
        }

        private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
        {
            Topmost = !Topmost;
            _settings.AlwaysOnTop = Topmost;
            AlwaysOnTopMenuItem.IsChecked = Topmost;
            PinBtn.Foreground = Topmost ? new SolidColorBrush(Color.FromRgb(0x00, 0xF0, 0xFF)) : new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            SaveCurrentSettings();
        }

        private void ManualRefresh_Click(object sender, RoutedEventArgs e)
        {
            TriggerTelemetrySample();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ResetPosition_Click(object sender, RoutedEventArgs e)
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Left = (SystemParameters.WorkArea.Width - Width) / 2;
            Top = (SystemParameters.WorkArea.Height - Height) / 2;
            SaveCurrentSettings();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Slider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is Slider slider)
            {
                double change = slider.SmallChange > 0 ? slider.SmallChange : 0.05;
                if (e.Delta > 0)
                    slider.Value = Math.Min(slider.Maximum, slider.Value + change);
                else if (e.Delta < 0)
                    slider.Value = Math.Max(slider.Minimum, slider.Value - change);

                e.Handled = true;
            }
        }

        private void Window_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            App.EnsureStandardMenuDropAlignment();
            _menuHoverTimer.Stop();
        }

        private void MainContextMenu_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            _menuHoverTimer.Stop();
        }

        private void MainContextMenu_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _menuHoverTimer.Stop();
        }

        private void MainContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            _menuHoverTimer.Stop();
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            _telemetryTimer.Stop();
            _rgbTimer.Stop();
            _glitchTimer.Stop();
            _snowTimer.Stop();
            _menuHoverTimer.Stop();

            SaveCurrentSettings();
            _metricsService.Dispose();

            base.OnClosed(e);

            App.CleanProcessExit(0);
        }

        #endregion
    }
}
