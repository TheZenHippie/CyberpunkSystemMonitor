using System;
using System.Windows;
using System.Windows.Media;

namespace CyberpunkSystemMonitor.Controls
{
    /// <summary>
    /// Segmented Cyberpunk Neon Level Bar with multi-color thresholding.
    /// </summary>
    public class CyberBarMeter : FrameworkElement
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentCountProperty =
            DependencyProperty.Register(nameof(SegmentCount), typeof(int), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(20, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SegmentSpacingProperty =
            DependencyProperty.Register(nameof(SegmentSpacing), typeof(double), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty BaseColorProperty =
            DependencyProperty.Register(nameof(BaseColor), typeof(Color), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0xF0, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty ActiveColorProperty =
            DependencyProperty.Register(nameof(ActiveColor), typeof(Color), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0xF0, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender, OnActiveColorChanged));

        public static readonly DependencyProperty WarningColorProperty =
            DependencyProperty.Register(nameof(WarningColor), typeof(Color), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(Color.FromRgb(0xFF, 0xB0, 0x00), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CriticalColorProperty =
            DependencyProperty.Register(nameof(CriticalColor), typeof(Color), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(Color.FromRgb(0xFF, 0x00, 0x55), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty UseDynamicThresholdsProperty =
            DependencyProperty.Register(nameof(UseDynamicThresholds), typeof(bool), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty IsSegmentedProperty =
            DependencyProperty.Register(nameof(IsSegmented), typeof(bool), typeof(CyberBarMeter),
                new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public int SegmentCount
        {
            get => (int)GetValue(SegmentCountProperty);
            set => SetValue(SegmentCountProperty, value);
        }

        public double SegmentSpacing
        {
            get => (double)GetValue(SegmentSpacingProperty);
            set => SetValue(SegmentSpacingProperty, value);
        }

        public Color BaseColor
        {
            get => (Color)GetValue(BaseColorProperty);
            set => SetValue(BaseColorProperty, value);
        }

        public Color ActiveColor
        {
            get => (Color)GetValue(ActiveColorProperty);
            set => SetValue(ActiveColorProperty, value);
        }

        public Color WarningColor
        {
            get => (Color)GetValue(WarningColorProperty);
            set => SetValue(WarningColorProperty, value);
        }

        public Color CriticalColor
        {
            get => (Color)GetValue(CriticalColorProperty);
            set => SetValue(CriticalColorProperty, value);
        }

        public bool UseDynamicThresholds
        {
            get => (bool)GetValue(UseDynamicThresholdsProperty);
            set => SetValue(UseDynamicThresholdsProperty, value);
        }

        public bool IsSegmented
        {
            get => (bool)GetValue(IsSegmentedProperty);
            set => SetValue(IsSegmentedProperty, value);
        }

        private static void OnActiveColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CyberBarMeter meter && e.NewValue is Color c)
            {
                meter.BaseColor = c;
            }
        }

        public CyberBarMeter()
        {
            Height = 12.0;
            MinHeight = 6.0;
            MinWidth = 40.0;
            ClipToBounds = true;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double min = Minimum;
            double max = Maximum > min ? Maximum : min + 1.0;
            double clampedVal = Math.Clamp(Value, min, max);
            double pct = (clampedVal - min) / (max - min);

            int segments = Math.Max(2, SegmentCount);

            // 1. Draw overall container track background
            var trackBrush = new SolidColorBrush(Color.FromArgb(35, 20, 30, 45));
            trackBrush.Freeze();
            var trackBorderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 0.6);
            trackBorderPen.Freeze();

            dc.DrawRoundedRectangle(trackBrush, trackBorderPen, new Rect(0, 0, w, h), 2.0, 2.0);

            Color primaryColor = (ActiveColor != Color.FromRgb(0x00, 0xF0, 0xFF) && BaseColor == Color.FromRgb(0x00, 0xF0, 0xFF)) ? ActiveColor : BaseColor;

            if (IsSegmented)
            {
                double gap = Math.Max(0.5, SegmentSpacing);
                double totalGaps = gap * (segments - 1);
                double segWidth = Math.Max(1.0, (w - 4.0 - totalGaps) / segments);
                int activeSegments = (int)Math.Round(pct * segments);

                for (int i = 0; i < segments; i++)
                {
                    double x = 2.0 + i * (segWidth + gap);
                    double segRatio = (i + 1.0) / segments;
                    bool isLit = i < activeSegments;

                    Rect segRect = new Rect(x, 2.0, segWidth, Math.Max(1.0, h - 4.0));

                    if (isLit)
                    {
                        Color segColor = primaryColor;
                        if (UseDynamicThresholds)
                        {
                            if (segRatio > 0.85) segColor = CriticalColor;
                            else if (segRatio > 0.65) segColor = WarningColor;
                        }

                        // Bloom halo
                        var haloBrush = new SolidColorBrush(Color.FromArgb(70, segColor.R, segColor.G, segColor.B));
                        haloBrush.Freeze();
                        dc.DrawRoundedRectangle(haloBrush, null, new Rect(x - 0.5, 1.0, segWidth + 1.0, h - 2.0), 1.0, 1.0);

                        // Lit solid segment
                        var litBrush = new SolidColorBrush(segColor);
                        litBrush.Freeze();
                        dc.DrawRoundedRectangle(litBrush, null, segRect, 1.0, 1.0);
                    }
                    else
                    {
                        // Unlit segment
                        var unlitBrush = new SolidColorBrush(Color.FromArgb(20, 150, 180, 210));
                        unlitBrush.Freeze();
                        dc.DrawRoundedRectangle(unlitBrush, null, segRect, 1.0, 1.0);
                    }
                }
            }
            else
            {
                // Continuous smooth neon fill
                double fillWidth = Math.Max(0, (w - 4.0) * pct);
                if (fillWidth > 0)
                {
                    Color barColor = primaryColor;
                    if (UseDynamicThresholds)
                    {
                        if (pct > 0.85) barColor = CriticalColor;
                        else if (pct > 0.65) barColor = WarningColor;
                    }

                    var fillBrush = new SolidColorBrush(barColor);
                    fillBrush.Freeze();
                    dc.DrawRoundedRectangle(fillBrush, null, new Rect(2.0, 2.0, fillWidth, h - 4.0), 1.5, 1.5);
                }
            }
        }
    }
}

