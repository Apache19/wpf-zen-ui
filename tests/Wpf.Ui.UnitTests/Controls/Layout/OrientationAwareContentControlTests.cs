// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.Layout;

public class OrientationAwareContentControlTests
{
    [Fact]
    public void ActualOrientation_IsHorizontal_WhenWidthIsGreaterThanHeight()
    {
        RunOnStaThread(() =>
        {
            var control = new OrientationAwareContentControl();

            Arrange(control, width: 100d, height: 20d);

            Assert.Equal(Orientation.Horizontal, control.ActualOrientation);
        });
    }

    [Fact]
    public void ActualOrientation_IsVertical_WhenHeightIsGreaterThanWidth()
    {
        RunOnStaThread(() =>
        {
            var control = new OrientationAwareContentControl();

            Arrange(control, width: 20d, height: 100d);

            Assert.Equal(Orientation.Vertical, control.ActualOrientation);
        });
    }

    [Fact]
    public void ActualOrientation_PreservesCurrentValue_WhenDimensionsAreEqual()
    {
        RunOnStaThread(() =>
        {
            var control = new OrientationAwareContentControl();

            Arrange(control, width: 20d, height: 100d);
            Arrange(control, width: 50d, height: 50d);

            Assert.Equal(Orientation.Vertical, control.ActualOrientation);
        });
    }

    [Fact]
    public void ActualOrientation_CannotBeSetDirectly()
    {
        RunOnStaThread(() =>
        {
            var control = new OrientationAwareContentControl();

            Assert.Throws<InvalidOperationException>(() =>
                control.SetValue(
                    OrientationAwareContentControl.ActualOrientationProperty,
                    Orientation.Vertical
                )
            );
        });
    }

    private static void Arrange(OrientationAwareContentControl control, double width, double height)
    {
        var size = new Size(width, height);

        control.Width = width;
        control.Height = height;
        control.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        control.Arrange(new Rect(size));
        control.UpdateLayout();
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
