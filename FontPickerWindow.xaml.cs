using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CyberpunkSystemMonitor
{
    public partial class FontPickerWindow : Window
    {
        private readonly List<FontFamily> _allFonts;
        public FontFamily? SelectedFont { get; private set; }
        public double SelectedFontSize { get; private set; }

        public FontPickerWindow(string currentFontName, double currentFontSize)
        {
            InitializeComponent();

            _allFonts = Fonts.SystemFontFamilies
                .OrderBy(f => GetFontDisplayName(f))
                .ToList();

            FontListBox.ItemsSource = _allFonts;

            double[] standardSizes = [8, 9, 10, 11, 12, 13, 14, 16, 18, 20, 24, 28, 32];
            SizeListBox.ItemsSource = standardSizes;

            // Pre-select font
            var match = _allFonts.FirstOrDefault(f =>
                string.Equals(GetFontDisplayName(f), currentFontName, StringComparison.OrdinalIgnoreCase) ||
                f.Source.Equals(currentFontName, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                FontListBox.SelectedItem = match;
                FontListBox.ScrollIntoView(match);
            }
            else if (_allFonts.Count > 0)
            {
                FontListBox.SelectedIndex = 0;
            }

            // Pre-select size
            var sizeMatch = standardSizes.FirstOrDefault(s => Math.Abs(s - currentFontSize) < 0.5);
            if (sizeMatch > 0)
                SizeListBox.SelectedItem = sizeMatch;
            else
                SizeListBox.SelectedItem = 12.0;

            UpdatePreview();
        }

        private static string GetFontDisplayName(FontFamily font)
        {
            if (font.FamilyNames.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage(CultureInfo.CurrentUICulture.IetfLanguageTag), out var name))
                return name;
            if (font.FamilyNames.TryGetValue(System.Windows.Markup.XmlLanguage.GetLanguage("en-us"), out var enName))
                return enName;
            return font.Source;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                FontListBox.ItemsSource = _allFonts;
            }
            else
            {
                FontListBox.ItemsSource = _allFonts
                    .Where(f => GetFontDisplayName(f).Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
        }

        private void FontListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void SizeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (PreviewTextBlock == null) return;

            if (FontListBox?.SelectedItem is FontFamily font)
            {
                PreviewTextBlock.FontFamily = font;
            }

            if (SizeListBox?.SelectedItem is double size)
            {
                PreviewTextBlock.FontSize = Math.Max(8, Math.Min(36, size));
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (FontListBox.SelectedItem is FontFamily font)
                SelectedFont = font;

            if (SizeListBox.SelectedItem is double size)
                SelectedFontSize = size;

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

