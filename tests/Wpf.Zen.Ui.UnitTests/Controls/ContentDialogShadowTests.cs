// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    /// <summary>
    /// Verifies that layout clipping of fractional dialog content preserves the outer shadow.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultShadow_ExtendsOutsideDialogBounds(bool clipToBounds)
    {
        Run(scope =>
        {
            scope.Background.Background = Brushes.White;
            scope.Host.Background = Brushes.Transparent;
            var dialog = scope.CreateDialog();
            dialog.Background = Brushes.White;
            dialog.ClipToBounds = clipToBounds;
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            scope.Host.UpdateLayout();

            var origin = dialog.TranslatePoint(default, scope.Host);
            var bitmap = new RenderTargetBitmap(
                (int)scope.Host.ActualWidth, (int)scope.Host.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scope.Host);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(
                (int)(origin.X + dialog.ActualWidth / 2),
                (int)(origin.Y + dialog.ActualHeight + 4), 1, 1), pixel, 4, 0);
            Assert.True(pixel[0] < 250, $"Expected shadow below dialog; blue={pixel[0]}.");

            dialog.Close();
            Pump(task);
        });
    }
}
