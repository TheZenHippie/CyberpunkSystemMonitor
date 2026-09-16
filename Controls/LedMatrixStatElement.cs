using System;
using System.Windows;
using System.Windows.Media;

namespace CyberpunkSystemMonitor.Controls
{
    /// <summary>
    /// High-performance vector LED dot-matrix display element for system metrics readouts.
    /// </summary>
    public class LedMatrixStatElement : FrameworkElement
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(nameof(Text), typeof(string), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata("00%", FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

        public static readonly DependencyProperty LitColorProperty =
            DependencyProperty.Register(nameof(LitColor), typeof(Color), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(Color.FromRgb(0x00, 0xF0, 0xFF), FrameworkPropertyMetadataOptions.AffectsRender, OnColorChanged));

        public static readonly DependencyProperty LedBrushProperty =
            DependencyProperty.Register(nameof(LedBrush), typeof(Brush), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnBrushChanged));

        public static readonly DependencyProperty LedOpacityProperty =
            DependencyProperty.Register(nameof(LedOpacity), typeof(double), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender, OnColorChanged));

        public static readonly DependencyProperty MatrixColsProperty =
            DependencyProperty.Register(nameof(MatrixCols), typeof(int), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(24, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnDimensionChanged));

        public static readonly DependencyProperty MatrixRowsProperty =
            DependencyProperty.Register(nameof(MatrixRows), typeof(int), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(7, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnDimensionChanged));

        public static readonly DependencyProperty PitchProperty =
            DependencyProperty.Register(nameof(Pitch), typeof(double), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(7.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DotSizeProperty =
            DependencyProperty.Register(nameof(DotSize), typeof(double), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(2.6, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty DotSpacingProperty =
            DependencyProperty.Register(nameof(DotSpacing), typeof(double), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty GlowBlurRadiusProperty =
            DependencyProperty.Register(nameof(GlowBlurRadius), typeof(double), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Use3x5FontProperty =
            DependencyProperty.Register(nameof(Use3x5Font), typeof(bool), typeof(LedMatrixStatElement),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender, OnTextChanged));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public Color LitColor
        {
            get => (Color)GetValue(LitColorProperty);
            set => SetValue(LitColorProperty, value);
        }

        public Brush? LedBrush
        {
            get => (Brush?)GetValue(LedBrushProperty);
            set => SetValue(LedBrushProperty, value);
        }

        public double LedOpacity
        {
            get => (double)GetValue(LedOpacityProperty);
            set => SetValue(LedOpacityProperty, value);
        }

        public int MatrixCols
        {
            get => (int)GetValue(MatrixColsProperty);
            set => SetValue(MatrixColsProperty, value);
        }

        public int MatrixRows
        {
            get => (int)GetValue(MatrixRowsProperty);
            set => SetValue(MatrixRowsProperty, value);
        }

        public double Pitch
        {
            get => (double)GetValue(PitchProperty);
            set => SetValue(PitchProperty, value);
        }

        public double DotSize
        {
            get => (double)GetValue(DotSizeProperty);
            set => SetValue(DotSizeProperty, value);
        }

        public double DotSpacing
        {
            get => (double)GetValue(DotSpacingProperty);
            set => SetValue(DotSpacingProperty, value);
        }

        public double GlowBlurRadius
        {
            get => (double)GetValue(GlowBlurRadiusProperty);
            set => SetValue(GlowBlurRadiusProperty, value);
        }

        public bool Use3x5Font
        {
            get => (bool)GetValue(Use3x5FontProperty);
            set => SetValue(Use3x5FontProperty, value);
        }

        private bool[,] _matrix = new bool[32, 7];
        private bool _brushesDirty = true;
        private SolidColorBrush? _litBrush;
        private SolidColorBrush? _haloBrush;
        private SolidColorBrush? _specularBrush;
        private SolidColorBrush? _unlitBrush;
        private Pen? _unlitPen;

        public LedMatrixStatElement()
        {
            ClipToBounds = true;
            UpdateMatrixDimensions();
            RebuildMatrix();
        }

        private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LedMatrixStatElement el)
            {
                el.RebuildMatrix();
            }
        }

        private static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LedMatrixStatElement el)
            {
                el._brushesDirty = true;
            }
        }

        private static void OnBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LedMatrixStatElement el)
            {
                if (e.NewValue is SolidColorBrush scb)
                {
                    el.LitColor = scb.Color;
                }
                el._brushesDirty = true;
            }
        }

        private static void OnDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is LedMatrixStatElement el)
            {
                el.UpdateMatrixDimensions();
                el.RebuildMatrix();
            }
        }

        private void UpdateMatrixDimensions()
        {
            int cols = Math.Max(5, MatrixCols);
            int rows = Math.Max(5, MatrixRows);
            if (_matrix.GetLength(0) != cols || _matrix.GetLength(1) != rows)
            {
                _matrix = new bool[cols, rows];
            }
            Width = cols * Pitch;
            Height = rows * Pitch;
        }

        private void RebuildMatrix()
        {
            int cols = _matrix.GetLength(0);
            int rows = _matrix.GetLength(1);
            Array.Clear(_matrix, 0, _matrix.Length);

            string str = Text ?? string.Empty;
            int currentX = 0;

            foreach (char c in str)
            {
                if (Use3x5Font)
                {
                    bool[,] glyph = LedFont.GetGlyph3x5(c);
                    int charWidth = 3;
                    if (currentX + charWidth > cols) break;

                    for (int col = 0; col < charWidth; col++)
                    {
                        for (int row = 0; row < 5; row++)
                        {
                            if (glyph[row, col] && (row + 1) < rows)
                            {
                                _matrix[currentX + col, row + 1] = true;
                            }
                        }
                    }
                    currentX += charWidth + 1;
                }
                else
                {
                    bool[,] glyph = LedFont.GetGlyph5x7(c);
                    int charWidth = 5;
                    if (currentX + charWidth > cols) break;

                    for (int col = 0; col < charWidth; col++)
                    {
                        for (int row = 0; row < 7; row++)
                        {
                            if (glyph[row, col] && row < rows)
                            {
                                _matrix[currentX + col, row] = true;
                            }
                        }
                    }
                    currentX += charWidth + 1;
                }
            }

            InvalidateVisual();
        }

        private void EnsureBrushes()
        {
            if (!_brushesDirty && _litBrush != null) return;

            Color baseColor = LitColor;
            if (LedBrush is SolidColorBrush scb)
            {
                baseColor = scb.Color;
            }

            double op = Math.Clamp(LedOpacity, 0.0, 1.0);

            _litBrush = new SolidColorBrush(Color.FromArgb((byte)(255 * op), baseColor.R, baseColor.G, baseColor.B));
            _litBrush.Freeze();

            _haloBrush = new SolidColorBrush(Color.FromArgb((byte)(55 * op), baseColor.R, baseColor.G, baseColor.B));
            _haloBrush.Freeze();

            _specularBrush = new SolidColorBrush(Color.FromArgb((byte)(220 * op), 255, 255, 255));
            _specularBrush.Freeze();

            _unlitBrush = new SolidColorBrush(Color.FromArgb(20, 30, 45, 60));
            _unlitBrush.Freeze();

            _unlitPen = new Pen(new SolidColorBrush(Color.FromArgb(30, 20, 30, 45)), 0.5);
            _unlitPen.Freeze();

            _brushesDirty = false;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            EnsureBrushes();

            int cols = _matrix.GetLength(0);
            int rows = _matrix.GetLength(1);
            double radius = DotSize > 0 ? DotSize : (Pitch * 0.38);

            for (int x = 0; x < cols; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    double cx = (x + 0.5) * Pitch;
                    double cy = (y + 0.5) * Pitch;
                    Point center = new Point(cx, cy);

                    if (_matrix[x, y])
                    {
                        // Outer Halo Glow
                        if (GlowBlurRadius > 0)
                        {
                            dc.DrawEllipse(_haloBrush, null, center, radius + 2.0, radius + 2.0);
                        }

                        // Core Diode
                        dc.DrawEllipse(_litBrush, null, center, radius, radius);

                        // Specular Highlight
                        Point spec = new Point(cx - radius * 0.3, cy - radius * 0.3);
                        dc.DrawEllipse(_specularBrush, null, spec, radius * 0.35, radius * 0.35);
                    }
                    else
                    {
                        // Unlit Dot
                        dc.DrawEllipse(_unlitBrush, _unlitPen, center, radius * 0.7, radius * 0.7);
                    }
                }
            }
        }
    }
}
