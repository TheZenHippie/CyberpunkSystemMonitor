using System;
using System.Collections.Generic;

namespace CyberpunkSystemMonitor.Controls
{
    /// <summary>
    /// Provides bitmap dot patterns for 5x7 and 3x5 LED matrix fonts.
    /// In 5x7: 7 rows of 5 columns.
    /// In 3x5: 5 rows of 3 columns.
    /// </summary>
    public static class LedFont
    {
        private static readonly Dictionary<char, bool[,]> Glyphs5x7 = new Dictionary<char, bool[,]>();
        private static readonly Dictionary<char, bool[,]> Glyphs3x5 = new Dictionary<char, bool[,]>();

        static LedFont()
        {
            Initialize5x7();
            Initialize3x5();
        }

        public static bool[,] GetGlyph5x7(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (Glyphs5x7.TryGetValue(upper, out var glyph))
            {
                return glyph;
            }
            return Glyphs5x7[' '];
        }

        public static bool[,] GetGlyph3x5(char c)
        {
            char upper = char.ToUpperInvariant(c);
            if (Glyphs3x5.TryGetValue(upper, out var glyph))
            {
                return glyph;
            }
            return Glyphs3x5[' '];
        }

        private static void Register5x7(char c, string[] pattern)
        {
            var grid = new bool[7, 5];
            for (int r = 0; r < 7 && r < pattern.Length; r++)
            {
                string line = pattern[r];
                for (int col = 0; col < 5 && col < line.Length; col++)
                {
                    grid[r, col] = (line[col] == '#' || line[col] == '1');
                }
            }
            Glyphs5x7[char.ToUpperInvariant(c)] = grid;
        }

        private static void Register3x5(char c, string[] pattern)
        {
            var grid = new bool[5, 3];
            for (int r = 0; r < 5 && r < pattern.Length; r++)
            {
                string line = pattern[r];
                for (int col = 0; col < 3 && col < line.Length; col++)
                {
                    grid[r, col] = (line[col] == '#' || line[col] == '1');
                }
            }
            Glyphs3x5[char.ToUpperInvariant(c)] = grid;
        }

        private static void Initialize5x7()
        {
            Register5x7(' ', new[] {
                "     ",
                "     ",
                "     ",
                "     ",
                "     ",
                "     ",
                "     "
            });

            Register5x7('0', new[] {
                " ### ",
                "#   #",
                "#  ##",
                "# # #",
                "##  #",
                "#   #",
                " ### "
            });

            Register5x7('1', new[] {
                "  #  ",
                " ##  ",
                "  #  ",
                "  #  ",
                "  #  ",
                "  #  ",
                " ### "
            });

            Register5x7('2', new[] {
                " ### ",
                "#   #",
                "    #",
                "  ## ",
                " #   ",
                "#    ",
                "#####"
            });

            Register5x7('3', new[] {
                " ### ",
                "#   #",
                "    #",
                "  ## ",
                "    #",
                "#   #",
                " ### "
            });

            Register5x7('4', new[] {
                "   # ",
                "  ## ",
                " # # ",
                "#  # ",
                "#####",
                "   # ",
                "   # "
            });

            Register5x7('5', new[] {
                "#####",
                "#    ",
                "#### ",
                "    #",
                "    #",
                "#   #",
                " ### "
            });

            Register5x7('6', new[] {
                " ### ",
                "#   #",
                "#    ",
                "#### ",
                "#   #",
                "#   #",
                " ### "
            });

            Register5x7('7', new[] {
                "#####",
                "    #",
                "   # ",
                "  #  ",
                " #   ",
                " #   ",
                " #   "
            });

            Register5x7('8', new[] {
                " ### ",
                "#   #",
                "#   #",
                " ### ",
                "#   #",
                "#   #",
                " ### "
            });

            Register5x7('9', new[] {
                " ### ",
                "#   #",
                "#   #",
                " ####",
                "    #",
                "#   #",
                " ### "
            });

            Register5x7('%', new[] {
                "#   #",
                "   # ",
                "  #  ",
                " #   ",
                "#   #",
                " #   ",
                "  #  "
            });

            Register5x7(':', new[] {
                "     ",
                "  #  ",
                "     ",
                "     ",
                "  #  ",
                "     ",
                "     "
            });

            Register5x7('-', new[] {
                "     ",
                "     ",
                "     ",
                "#####",
                "     ",
                "     ",
                "     "
            });

            Register5x7('/', new[] {
                "    #",
                "   # ",
                "   # ",
                "  #  ",
                " #   ",
                " #   ",
                "#    "
            });

            Register5x7('.', new[] {
                "     ",
                "     ",
                "     ",
                "     ",
                "     ",
                "  #  ",
                "  #  "
            });

            Register5x7('A', new[] { " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" });
            Register5x7('B', new[] { "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### " });
            Register5x7('C', new[] { " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### " });
            Register5x7('D', new[] { "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### " });
            Register5x7('E', new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####" });
            Register5x7('F', new[] { "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    " });
            Register5x7('G', new[] { " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ### " });
            Register5x7('H', new[] { "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #" });
            Register5x7('I', new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####" });
            Register5x7('J', new[] { "  ###", "    #", "    #", "    #", "    #", "#   #", " ### " });
            Register5x7('K', new[] { "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #" });
            Register5x7('L', new[] { "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####" });
            Register5x7('M', new[] { "#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #" });
            Register5x7('N', new[] { "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #" });
            Register5x7('O', new[] { " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " });
            Register5x7('P', new[] { "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    " });
            Register5x7('Q', new[] { " ### ", "#   #", "#   #", "#   #", "# # #", "#  ##", " ####" });
            Register5x7('R', new[] { "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #" });
            Register5x7('S', new[] { " ### ", "#   #", "#    ", " ### ", "    #", "#   #", " ### " });
            Register5x7('T', new[] { "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  " });
            Register5x7('U', new[] { "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### " });
            Register5x7('V', new[] { "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  " });
            Register5x7('W', new[] { "#   #", "#   #", "#   #", "#   #", "# # #", "## ##", "#   #" });
            Register5x7('X', new[] { "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #" });
            Register5x7('Y', new[] { "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  " });
            Register5x7('Z', new[] { "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####" });
        }

        private static void Initialize3x5()
        {
            Register3x5(' ', new[] { "   ", "   ", "   ", "   ", "   " });
            Register3x5('0', new[] { "###", "# #", "# #", "# #", "###" });
            Register3x5('1', new[] { " # ", "## ", " # ", " # ", "###" });
            Register3x5('2', new[] { "###", "  #", "###", "#  ", "###" });
            Register3x5('3', new[] { "###", "  #", "###", "  #", "###" });
            Register3x5('4', new[] { "# #", "# #", "###", "  #", "  #" });
            Register3x5('5', new[] { "###", "#  ", "###", "  #", "###" });
            Register3x5('6', new[] { "###", "#  ", "###", "# #", "###" });
            Register3x5('7', new[] { "###", "  #", "  #", "  #", "  #" });
            Register3x5('8', new[] { "###", "# #", "###", "# #", "###" });
            Register3x5('9', new[] { "###", "# #", "###", "  #", "###" });
            Register3x5('%', new[] { "# #", "  #", " # ", "#  ", "# #" });
            Register3x5(':', new[] { "   ", " # ", "   ", " # ", "   " });
            Register3x5('-', new[] { "   ", "   ", "###", "   ", "   " });
            Register3x5('/', new[] { "  #", "  #", " # ", "#  ", "#  " });
            Register3x5('.', new[] { "   ", "   ", "   ", " # ", " # " });

            Register3x5('A', new[] { " # ", "# #", "###", "# #", "# #" });
            Register3x5('B', new[] { "## ", "# #", "## ", "# #", "## " });
            Register3x5('C', new[] { "###", "#  ", "#  ", "#  ", "###" });
            Register3x5('D', new[] { "## ", "# #", "# #", "# #", "## " });
            Register3x5('E', new[] { "###", "#  ", "## ", "#  ", "###" });
            Register3x5('F', new[] { "###", "#  ", "## ", "#  ", "#  " });
            Register3x5('G', new[] { "###", "#  ", "# #", "# #", "###" });
            Register3x5('H', new[] { "# #", "# #", "###", "# #", "# #" });
            Register3x5('I', new[] { "###", " # ", " # ", " # ", "###" });
            Register3x5('J', new[] { "  #", "  #", "  #", "# #", " # " });
            Register3x5('K', new[] { "# #", "## ", "#  ", "## ", "# #" });
            Register3x5('L', new[] { "#  ", "#  ", "#  ", "#  ", "###" });
            Register3x5('M', new[] { "# #", "###", "# #", "# #", "# #" });
            Register3x5('N', new[] { "## ", "# #", "# #", "# #", "# #" });
            Register3x5('O', new[] { "###", "# #", "# #", "# #", "###" });
            Register3x5('P', new[] { "###", "# #", "###", "#  ", "#  " });
            Register3x5('Q', new[] { "###", "# #", "# #", "###", "  #" });
            Register3x5('R', new[] { "###", "# #", "## ", "# #", "# #" });
            Register3x5('S', new[] { "###", "#  ", "###", "  #", "###" });
            Register3x5('T', new[] { "###", " # ", " # ", " # ", " # " });
            Register3x5('U', new[] { "# #", "# #", "# #", "# #", "###" });
            Register3x5('V', new[] { "# #", "# #", "# #", "# #", " # " });
            Register3x5('W', new[] { "# #", "# #", "# #", "###", "# #" });
            Register3x5('X', new[] { "# #", "# #", " # ", "# #", "# #" });
            Register3x5('Y', new[] { "# #", "# #", " # ", " # ", " # " });
            Register3x5('Z', new[] { "###", "  #", " # ", "#  ", "###" });
        }
    }
}

