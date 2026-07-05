// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using Wpf.Ui.Behaviors;
using WpfSeparator = System.Windows.Controls.Separator;

namespace Wpf.Ui.UnitTests.Behaviors;

public class OrientationAssistTests
{
    [Fact]
    public void DetectedOrientation_DefaultsToVertical_WhenNoOrientationParentExists()
    {
        RunOnStaThread(
            () =>
            {
                var separator = new WpfSeparator();

                OrientationAssist.SetAutoOrientation(separator, true);

                Assert.Equal(Orientation.Vertical, OrientationAssist.GetDetectedOrientation(separator));
            }
        );
    }

    [Fact]
    public void DetectedOrientation_UsesNearestOrientationParentThroughNestedElements()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new StackPanel { Orientation = Orientation.Horizontal };
                var border = new Border();
                var grid = new Grid();
                var separator = new WpfSeparator();

                parent.Children.Add(border);
                border.Child = grid;
                grid.Children.Add(separator);

                OrientationAssist.SetAutoOrientation(separator, true);

                Assert.Equal(Orientation.Horizontal, OrientationAssist.GetDetectedOrientation(separator));
            }
        );
    }

    [Fact]
    public void DetectedOrientation_UsesNearestOrientationParent()
    {
        RunOnStaThread(
            () =>
            {
                var outerParent = new StackPanel { Orientation = Orientation.Horizontal };
                var innerParent = new StackPanel { Orientation = Orientation.Vertical };
                var separator = new WpfSeparator();

                outerParent.Children.Add(innerParent);
                innerParent.Children.Add(separator);

                OrientationAssist.SetAutoOrientation(separator, true);

                Assert.Equal(Orientation.Vertical, OrientationAssist.GetDetectedOrientation(separator));
            }
        );
    }

    [Fact]
    public void DetectedOrientation_UpdatesWhenParentOrientationChanges()
    {
        RunOnStaThread(
            () =>
            {
                var parent = new StackPanel { Orientation = Orientation.Horizontal };
                var separator = new WpfSeparator();

                parent.Children.Add(separator);
                OrientationAssist.SetAutoOrientation(separator, true);

                parent.Orientation = Orientation.Vertical;

                Assert.Equal(Orientation.Vertical, OrientationAssist.GetDetectedOrientation(separator));
            }
        );
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
