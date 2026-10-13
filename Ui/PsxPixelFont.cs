using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace _3DLight.Ui;

/// <summary>A compact 5x7 bitmap alphabet rendered with solid, integer-sized pixels.</summary>
internal static class PsxPixelFont
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;
    private const int Advance = 6;
    private const int LineAdvance = 9;
    private static readonly Dictionary<char, ulong> Glyphs = CreateGlyphs();

    public static Vector2 MeasureString(string text, int scale = 2)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        if (text.Length == 0)
            return Vector2.Zero;

        int width = 0, maximumWidth = 0, lines = 1;
        foreach (char character in text)
        {
            if (character == '\r')
                continue;
            if (character == '\n')
            {
                maximumWidth = Math.Max(maximumWidth, width);
                width = 0;
                lines++;
            }
            else
                width += Advance;
        }
        maximumWidth = Math.Max(maximumWidth, width);
        return new Vector2(Math.Max(0, maximumWidth - 1) * scale,
            ((lines - 1) * LineAdvance + GlyphHeight) * scale);
    }

    public static void Draw(SpriteBatch spriteBatch, Texture2D pixel, string text,
        Vector2 position, Color color, int scale = 2)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(scale);
        string uppercase = text.ToUpperInvariant();
        int originX = (int)MathF.Round(position.X);
        int x = originX, y = (int)MathF.Round(position.Y);
        foreach (char character in uppercase)
        {
            if (character == '\r')
                continue;
            if (character == '\n')
            {
                x = originX;
                y += LineAdvance * scale;
                continue;
            }

            ulong glyph = Glyphs.TryGetValue(character, out ulong found) ? found : Glyphs['?'];
            for (int row = 0; row < GlyphHeight; row++)
            {
                for (int column = 0; column < GlyphWidth; column++)
                {
                    int bit = GlyphWidth * GlyphHeight - 1 - row * GlyphWidth - column;
                    if ((glyph & (1UL << bit)) != 0)
                        spriteBatch.Draw(pixel, new Rectangle(
                            x + column * scale, y + row * scale, scale, scale), color);
                }
            }
            x += Advance * scale;
        }
    }

    private static Dictionary<char, ulong> CreateGlyphs()
    {
        var glyphs = new Dictionary<char, ulong>();
        Add('A', ".###./#...#/#...#/#####/#...#/#...#/#...#");
        Add('B', "####./#...#/#...#/####./#...#/#...#/####.");
        Add('C', ".###./#...#/#..../#..../#..../#...#/.###.");
        Add('D', "####./#...#/#...#/#...#/#...#/#...#/####.");
        Add('E', "#####/#..../#..../####./#..../#..../#####");
        Add('F', "#####/#..../#..../####./#..../#..../#....");
        Add('G', ".###./#...#/#..../#.###/#...#/#...#/.###.");
        Add('H', "#...#/#...#/#...#/#####/#...#/#...#/#...#");
        Add('I', "#####/..#../..#../..#../..#../..#../#####");
        Add('J', "..###/...#./...#./...#./...#./#..#./.##..");
        Add('K', "#...#/#..#./#.#../##.../#.#../#..#./#...#");
        Add('L', "#..../#..../#..../#..../#..../#..../#####");
        Add('M', "#...#/##.##/#.#.#/#.#.#/#...#/#...#/#...#");
        Add('N', "#...#/##..#/#.#.#/#..##/#...#/#...#/#...#");
        Add('O', ".###./#...#/#...#/#...#/#...#/#...#/.###.");
        Add('P', "####./#...#/#...#/####./#..../#..../#....");
        Add('Q', ".###./#...#/#...#/#...#/#.#.#/#..#./.##.#");
        Add('R', "####./#...#/#...#/####./#.#../#..#./#...#");
        Add('S', ".####/#..../#..../.###./....#/....#/####.");
        Add('T', "#####/..#../..#../..#../..#../..#../..#..");
        Add('U', "#...#/#...#/#...#/#...#/#...#/#...#/.###.");
        Add('V', "#...#/#...#/#...#/#...#/#...#/.#.#./..#..");
        Add('W', "#...#/#...#/#...#/#.#.#/#.#.#/##.##/#...#");
        Add('X', "#...#/#...#/.#.#./..#../.#.#./#...#/#...#");
        Add('Y', "#...#/#...#/.#.#./..#../..#../..#../..#..");
        Add('Z', "#####/....#/...#./..#../.#.../#..../#####");

        foreach ((char russian, char latin) in new[]
        {
            ('А', 'A'), ('В', 'B'), ('Е', 'E'), ('К', 'K'), ('М', 'M'), ('Н', 'H'),
            ('О', 'O'), ('Р', 'P'), ('С', 'C'), ('Т', 'T'), ('Х', 'X')
        })
            glyphs[russian] = glyphs[latin];
        Add('Б', "#####/#..../#..../####./#...#/#...#/####.");
        Add('Г', "#####/#..../#..../#..../#..../#..../#....");
        Add('Д', ".###./.#.#./.#.#./.#.#./#...#/#####/#...#");
        Add('Ё', ".#.#./...../#####/#..../####./#..../#####");
        Add('Ж', "#.#.#/#.#.#/.###./..#../.###./#.#.#/#.#.#");
        Add('З', ".###./#...#/....#/..##./....#/#...#/.###.");
        Add('И', "#...#/#..##/#..##/#.#.#/##..#/##..#/#...#");
        Add('Й', ".#.#./..#../#...#/#..##/#.#.#/##..#/#...#");
        Add('Л', "..###/.#..#/.#..#/.#..#/.#..#/#...#/#...#");
        Add('П', "#####/#...#/#...#/#...#/#...#/#...#/#...#");
        Add('У', "#...#/#...#/.#.#./..###/....#/#...#/.###.");
        Add('Ф', "..#../.###./#.#.#/#.#.#/#.#.#/.###./..#..");
        Add('Ц', "#..#./#..#./#..#./#..#./#..#./####./....#");
        Add('Ч', "#...#/#...#/#...#/.####/....#/....#/....#");
        Add('Ш', "#.#.#/#.#.#/#.#.#/#.#.#/#.#.#/#.#.#/#####");
        Add('Щ', "#.#.#/#.#.#/#.#.#/#.#.#/#.#.#/#####/....#");
        Add('Ъ', "##.../.#.../.###./.#..#/.#..#/.#..#/.###.");
        Add('Ы', "#...#/#...#/###.#/#..##/#..##/#..##/###.#");
        Add('Ь', "#..../#..../####./#...#/#...#/#...#/####.");
        Add('Э', ".###./#...#/....#/..###/....#/#...#/.###.");
        Add('Ю', "#..#./#.#.#/#.#.#/###.#/#.#.#/#.#.#/#..#.");
        Add('Я', ".####/#...#/#...#/.####/..#.#/.#..#/#...#");

        Add('0', ".###./#...#/#..##/#.#.#/##..#/#...#/.###.");
        Add('1', "..#../.##../..#../..#../..#../..#../.###.");
        Add('2', ".###./#...#/....#/...#./..#../.#.../#####");
        Add('3', ".###./#...#/....#/..##./....#/#...#/.###.");
        Add('4', "...#./..##./.#.#./#..#./#####/...#./...#.");
        Add('5', "#####/#..../#..../####./....#/#...#/.###.");
        Add('6', ".###./#..../#..../####./#...#/#...#/.###.");
        Add('7', "#####/....#/...#./..#../.#.../.#.../.#...");
        Add('8', ".###./#...#/#...#/.###./#...#/#...#/.###.");
        Add('9', ".###./#...#/#...#/.####/....#/....#/.###.");
        Add('[', ".###./.#.../.#.../.#.../.#.../.#.../.###.");
        Add(']', ".###./...#./...#./...#./...#./...#./.###.");
        Add('-', "...../...../...../#####/...../...../.....");
        Add(' ', "...../...../...../...../...../...../.....");
        Add('?', ".###./#...#/....#/...#./..#../...../..#..");
        Add('.', "...../...../...../...../...../..#../..#..");
        Add(':', "...../..#../..#../...../..#../..#../.....");
        return glyphs;

        void Add(char character, string rows)
        {
            ulong mask = 0;
            foreach (char cell in rows)
            {
                if (cell != '/')
                    mask = (mask << 1) | (cell == '#' ? 1UL : 0UL);
            }
            glyphs[character] = mask;
        }
    }
}
