using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace CyberpunkSystemMonitor.Controls
{
    /// <summary>
    /// Round glowing LED indicator for disk activity (Read/Write) and status beacons.
    /// </summary>
    public class RoundLedIndicator : FrameworkElement
    {
        public static readonly DependencyProperty IsOnProperty =
            DependencyProperty.Register(nameof(IsOn), typeof(bool), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsOnChanged));

        public static readonly DependencyProperty IsActiveProperty =
            DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnIsActiveChanged));

        public static readonly DependencyProperty OnBrushProperty =
            DependencyProperty.Register(nameof(OnBrush), typeof(Brush), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x66)), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty OffBrushProperty =
            DependencyProperty.Register(nameof(OffBrush), typeof(Brush), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(40, 20, 30, 40)), FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty ActiveColorProperty =
            DependencyProperty.Register(nameof(ActiveColor), typeof(Color), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0xFF, 0x66), FrameworkPropertyMetadataOptions.AffectsRender, OnColorChanged));

        public static readonly DependencyProperty InactiveColorProperty =
            DependencyProperty.Register(nameof(InactiveColor), typeof(Color), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(Color.FromArgb(50, 40, 50, 65), FrameworkPropertyMetadataOptions.AffectsRender, OnColorChanged));

        public static readonly DependencyProperty LedSizeProperty =
            DependencyProperty.Register(nameof(LedSize), typeof(double), typeof(RoundLedIndicator),
                new FrameworkPropertyMetadata(10.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnDimensionChanged));

        public bool IsOn
        {
            get => (bool)GetValue(IsOnProperty);
            set => SetValue(IsOnProperty, value);
        }

        public bool IsActive
        {
            get => (bool)GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        public Brush OnBrush
        {
            get => (Brush)GetValue(OnBrushProperty);
            set => SetValue(OnBrushProperty, value);
        }

        public Brush OffBrush
        {
            get => (Brush)GetValue(OffBrushProperty);
            set => SetValue(OffBrushProperty, value);
        }

        public Color ActiveColor
        {
            get => (Color)GetValue(ActiveColorProperty);
            set => SetValue(ActiveColorProperty, value);
        }

        public Color InactiveColor
        {
            get => (Color)GetValue(InactiveColorProperty);
            set => SetValue(InactiveColorProperty, value);
        }

        public double LedSize
        {
            get => (double)GetValue(LedSizeProperty);
            set => SetValue(LedSizeProperty, value);
        }

        private readonly DispatcherTimer _flashTimer;
        private bool _brushesDirty = true;
        private SolidColorBrush? _litBrush;
        private SolidColorBrush? _haloBrush;
        private SolidColorBrush? _specularBrush;
        private SolidColorBrush? _unlitBrush;
        private Pen? _unlitPen;

        public RoundLedIndicator()
        {
            UpdateDimensions();

            _flashTimer = new DispatcherTimer();
            _flashTimer.Tick += (s, e) =>
            {
                _flashTimer.Stop();
                IsOn = false;
                IsActive = false;
            };
        }

        private static void OnIsOnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoundLedIndicator indicator && (bool)e.NewValue != indicator.IsActive)
            {
                indicator.IsActive = (bool)e.NewValue;
            }
        }

        private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoundLedIndicator indicator && (bool)e.NewValue != indicator.IsOn)
            {
                indicator.IsOn = (bool)e.NewValue;
            }
        }

        private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoundLedIndicator indicator)
            {
                indicator._brushesDirty = true;
            }
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoundLedIndicator indicator)
            {
                indicator._brushesDirty = true;
            }
        }

        private static void OnDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RoundLedIndicator indicator)
            {
                indicator.UpdateDimensions();
            }
        }

        private void UpdateDimensions()
        {
            Width = LedSize + 6.0;
            Height = LedSize + 6.0;
        }

        public void Flash(int durationMs = 120)
        {
            _flashTimer.Stop();
            IsOn = true;
            IsActive = true;
            _flashTimer.Interval = TimeSpan.FromMilliseconds(durationMs);
            _flashTimer.Start();
        }

        private void EnsureBrushes()
        {
            if (!_brushesDirty && _litBrush != null) return;

            Color onColor = ActiveColor;
            if (OnBrush is SolidColorBrush scbOn)
            {
                onColor = scbOn.Color;
            }

            _litBrush = new SolidColorBrush(onColor);
            _litBrush.Freeze();

            _haloBrush = new SolidColorBrush(Color.FromArgb(90, onColor.R, onColor.G, onColor.B));
            _haloBrush.Freeze();

            _specularBrush = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            _specularBrush.Freeze();

            if (OffBrush is SolidColorBrush scbOff)
            {
                _unlitBrush = scbOff;
            }
            else
            {
                _unlitBrush = new SolidColorBrush(InactiveColor);
                _unlitBrush.Freeze();
            }

            _unlitPen = new Pen(new SolidColorBrush(Color.FromArgb(50, 60, 75, 95)), 0.8);
            _unlitPen.Freeze();

            _brushesDirty = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            EnsureBrushes();

            double radius = LedSize / 2.0;
            Point center = new Point(ActualWidth / 2.0, ActualHeight / 2.0);

            if (IsOn || IsActive)
            {
                // Bloom Halo
                dc.DrawEllipse(_haloBrush, null, center, radius + 3.0, radius + 3.0);

                // Main Core
                dc.DrawEllipse(_litBrush, null, center, radius, radius);

                // Specular Light
                Point spec = new Point(center.X - radius * 0.35, center.Y - radius * 0.35);
                dc.DrawEllipse(_specularBrush, null, spec, radius * 0.35, radius * 0.35);
            }
            else
            {
                // Dark Unlit Diode
                dc.DrawEllipse(_unlitBrush, _unlitPen, center, radius, radius);
            }
        }
    }
}

