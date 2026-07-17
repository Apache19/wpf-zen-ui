// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Media;
using Wpf.Ui.Controls;
using Wpf.Ui.Converters;

namespace Wpf.Ui.UnitTests.Converters;

public class IconElementCloneConverterTests
{
    private static readonly IconElementCloneConverter Converter = new();

    [Fact]
    public void Convert_SymbolIcon_CopiesSymbolAndFilled()
    {
        RunOnStaThread(() =>
        {
            var source = new SymbolIcon(SymbolRegular.Search24, filled: true) { FontSize = 20 };

            var result = Assert.IsType<SymbolIcon>(Convert(source));

            Assert.NotSame(source, result);
            Assert.Equal(SymbolRegular.Search24, result.Symbol);
            Assert.True(result.Filled);
        });
    }

    [Fact]
    public void Convert_FontIcon_CopiesGlyphAndFontFamily()
    {
        RunOnStaThread(() =>
        {
            var fontFamily = new FontFamily("Segoe UI");
            var source = new FontIcon
            {
                Glyph      = "",
                FontFamily = fontFamily,
            };

            var result = Assert.IsType<FontIcon>(Convert(source));

            Assert.NotSame(source, result);
            Assert.Equal("", result.Glyph);
            Assert.Same(fontFamily, result.FontFamily);
        });
    }

    [Fact]
    public void Convert_ImageIcon_CopiesSource()
    {
        RunOnStaThread(() =>
        {
            var imageSource = new DrawingImage();
            var source = new ImageIcon { Source = imageSource };

            var result = Assert.IsType<ImageIcon>(Convert(source));

            Assert.NotSame(source, result);
            Assert.Same(imageSource, result.Source);
        });
    }

    [Fact]
    public void Convert_IconSourceElement_UsesCreateIconElement()
    {
        RunOnStaThread(() =>
        {
            var iconSource = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            var source = new IconSourceElement { IconSource = iconSource };

            var result = Convert(source);

            Assert.NotNull(result);
            Assert.NotSame(source, result);
            Assert.IsNotType<IconSourceElement>(result);
        });
    }

    [Fact]
    public void Convert_Null_ReturnsNull()
    {
        Assert.Null(Convert(null));
    }

    [Fact]
    public void Convert_NonCloneableType_ReturnsSameInstance()
    {
        // A plain IconElement subclass not handled by the converter should pass through.
        var nonCloneable = new object();

        Assert.Same(nonCloneable, Convert(nonCloneable));
    }

    [Fact]
    public void ConvertBack_ThrowsNotSupportedException()
    {
        Assert.Throws<NotSupportedException>(() =>
            Converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture)
        );
    }

    private static object? Convert(object? value) =>
        Converter.Convert(value, typeof(object), null, CultureInfo.InvariantCulture);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
