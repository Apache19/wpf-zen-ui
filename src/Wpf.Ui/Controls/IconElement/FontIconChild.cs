// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Documents;
using FontFamily = System.Windows.Media.FontFamily;
using FontStyle = System.Windows.FontStyle;

namespace Wpf.Ui.Controls;

/// <summary>
/// Lightweight element that renders font icon glyphs using <see cref="GlyphRun"/>.
/// Unlike <see cref="System.Windows.Controls.TextBlock"/>, this correctly renders
/// Unicode code points above <c>0xFFFF</c> that require surrogate pairs in UTF-16.
/// </summary>
internal sealed class FontIconChild : FrameworkElement
{
    private string _glyph = string.Empty;
    private FontFamily _fontFamily = SystemFonts.MessageFontFamily;
    private double _fontSize = SystemFonts.MessageFontSize;
    private FontStyle _fontStyle = FontStyles.Normal;
    private FontWeight _fontWeight = FontWeights.Normal;

    private GlyphRun? _glyphRun;
    private Size _desiredSize;

    public string Glyph
    {
        get => _glyph;
        set
        {
            _glyph = value ?? string.Empty;
            InvalidateGlyph();
        }
    }

    public FontFamily FontFamily
    {
        get => _fontFamily;
        set
        {
            _fontFamily = value;
            InvalidateGlyph();
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            _fontSize = value;
            InvalidateGlyph();
        }
    }

    public FontStyle FontStyle
    {
        get => _fontStyle;
        set
        {
            _fontStyle = value;
            InvalidateGlyph();
        }
    }

    public FontWeight FontWeight
    {
        get => _fontWeight;
        set
        {
            _fontWeight = value;
            InvalidateGlyph();
        }
    }

    public void InvalidateGlyph()
    {
        _glyphRun = null;
        _desiredSize = default;
        InvalidateVisual();
        InvalidateMeasure();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_glyphRun is null)
        {
            _glyphRun = BuildGlyphRun();
        }

        if (_glyphRun is null)
        {
            return;
        }

        var foreground = GetValue(TextElement.ForegroundProperty) as Brush ?? Brushes.Black;
        drawingContext.DrawGlyphRun(foreground, _glyphRun);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_desiredSize == default)
        {
            _desiredSize = CalculateDesiredSize();
        }

        return _desiredSize;
    }

    private GlyphRun? BuildGlyphRun()
    {
        if (string.IsNullOrEmpty(_glyph) || _fontFamily is null || _fontSize <= 0)
        {
            return null;
        }

        var typeface = new Typeface(_fontFamily, _fontStyle, _fontWeight, FontStretches.Normal);

        if (!typeface.TryGetGlyphTypeface(out GlyphTypeface glyphTypeface))
        {
            return null;
        }

        int length = _glyph.Length;
        var glyphIndices = new List<ushort>(length);
        var advanceWidths = new List<double>(length);

        for (int i = 0; i < length; i++)
        {
            int codePoint = GetCodePoint(_glyph, ref i);

            if (glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out ushort glyphIndex))
            {
                glyphIndices.Add(glyphIndex);
                advanceWidths.Add(glyphTypeface.AdvanceWidths[glyphIndex] * _fontSize);
            }
            else
            {
                // Use glyph index 0 (.notdef) as fallback for missing characters
                glyphIndices.Add(0);
                advanceWidths.Add(glyphTypeface.AdvanceWidths.Count > 0 ? glyphTypeface.AdvanceWidths[0] * _fontSize : _fontSize);
            }
        }

        if (glyphIndices.Count == 0)
        {
            return null;
        }

        float pixelsPerDip = GetPixelsPerDip();
        double originY = glyphTypeface.Baseline * _fontSize;

        return new GlyphRun(
            glyphTypeface,
            0,
            false,
            _fontSize,
            pixelsPerDip,
            glyphIndices,
            new Point(0, originY),
            advanceWidths,
            null,
            null,
            null,
            null,
            null,
            null
        );
    }

    private Size CalculateDesiredSize()
    {
        if (string.IsNullOrEmpty(_glyph) || _fontFamily is null || _fontSize <= 0)
        {
            return default;
        }

        var typeface = new Typeface(_fontFamily, _fontStyle, _fontWeight, FontStretches.Normal);

        if (!typeface.TryGetGlyphTypeface(out GlyphTypeface glyphTypeface))
        {
            return new Size(_fontSize, _fontSize);
        }

        double totalWidth = 0;
        int length = _glyph.Length;

        for (int i = 0; i < length; i++)
        {
            int codePoint = GetCodePoint(_glyph, ref i);

            if (glyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out ushort glyphIndex))
            {
                totalWidth += glyphTypeface.AdvanceWidths[glyphIndex] * _fontSize;
            }
            else if (glyphTypeface.AdvanceWidths.Count > 0)
            {
                totalWidth += glyphTypeface.AdvanceWidths[0] * _fontSize;
            }
            else
            {
                totalWidth += _fontSize;
            }
        }

        double height = glyphTypeface.Height * _fontSize;

        return new Size(Math.Ceiling(totalWidth), Math.Ceiling(height));
    }

    private float GetPixelsPerDip()
    {
        var source = PresentationSource.FromVisual(this);
        return (float)(source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0);
    }

    /// <summary>
    /// Extracts the Unicode code point at position <paramref name="i"/>,
    /// correctly combining surrogate pairs for code points above <c>0xFFFF</c>.
    /// Increments <paramref name="i"/> to skip the low surrogate when a pair is consumed.
    /// </summary>
    private static int GetCodePoint(string text, ref int i)
    {
        if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
        {
            int codePoint = char.ConvertToUtf32(text[i], text[i + 1]);
            i++;
            return codePoint;
        }

        return text[i];
    }
}
