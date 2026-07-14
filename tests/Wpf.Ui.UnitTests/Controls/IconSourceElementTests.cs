// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls;

public class IconSourceElementTests
{
    [Fact]
    public void ConcreteIcons_HostTheirRenderingElementDirectly()
    {
        RunOnStaThread(() =>
        {
            var fontIcon = new FontIcon { Glyph = "A" };
            fontIcon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.IsType<Wpf.Ui.Controls.TextBlock>(VisualTreeHelper.GetChild(fontIcon, 0));

            var imageIcon = new ImageIcon();
            imageIcon.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.IsType<System.Windows.Controls.Image>(VisualTreeHelper.GetChild(imageIcon, 0));
        });
    }

    [Fact]
    public void Element_RemainsRenderableWhileIconSourceChangesBetweenNullAndNonNull()
    {
        RunOnStaThread(() =>
        {
            var element = new IconSourceElement();

            var presenter = GetIconPresenter(element);
            Assert.Null(presenter.Content);

            element.IconSource = new SymbolIconSource { Symbol = SymbolRegular.Home24 };
            var firstIcon = Assert.IsType<SymbolIcon>(presenter.Content);
            Assert.Equal(SymbolRegular.Home24, firstIcon.Symbol);

            element.IconSource = new FontIconSource { Glyph = "A" };
            var replacementIcon = Assert.IsType<FontIcon>(presenter.Content);
            Assert.Equal("A", replacementIcon.Glyph);
            Assert.NotSame(firstIcon, replacementIcon);

            element.IconSource = null;
            Assert.Null(presenter.Content);

            // The stable presenter remains the sole direct visual child throughout source replacement.
            Assert.Same(presenter, VisualTreeHelper.GetChild(element, 0));
        });
    }

    [Fact]
    public void Element_CanBePresentedDirectlyByAContentPresenter()
    {
        RunOnStaThread(() =>
        {
            var iconSourceElement = new IconSourceElement
            {
                IconSource = new SymbolIconSource { Symbol = SymbolRegular.Navigation24 },
            };
            var presenter = new ContentPresenter { Content = iconSourceElement };

            presenter.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            Assert.True(iconSourceElement.IsMeasureValid);
            Assert.IsType<SymbolIcon>(GetIconPresenter(iconSourceElement).Content);
        });
    }

    private static ContentPresenter GetIconPresenter(IconSourceElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        return Assert.IsType<ContentPresenter>(VisualTreeHelper.GetChild(element, 0));
    }

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
