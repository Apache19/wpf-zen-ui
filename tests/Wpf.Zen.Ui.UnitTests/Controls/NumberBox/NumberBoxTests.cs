// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NumberBoxControl;

public class NumberBoxTests
{
    [Fact]
    public void ApplyTemplate_ParsesTextOnlyInitialization()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Text = "42.5" };

                numberBox.OnApplyTemplate();

                Assert.Equal(42.5, numberBox.Value);
                Assert.Equal("42.5", numberBox.Text);
            }
        );
    }

    [Fact]
    public void RangeChanges_ReevaluateMaximumAndStepBaseValues()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    Minimum = 10,
                    Maximum = 5,
                    SmallStep = 100,
                    LargeStep = 150,
                };

                Assert.Equal(10, numberBox.Maximum);
                numberBox.Minimum = 0;
                Assert.Equal(5, numberBox.Maximum);
                Assert.Equal(5, numberBox.SmallStep);
                Assert.Equal(5, numberBox.LargeStep);

                numberBox.Maximum = 200;
                Assert.Equal(100, numberBox.SmallStep);
                Assert.Equal(150, numberBox.LargeStep);

                numberBox.Maximum = 10;
                Assert.Equal(10, numberBox.SmallStep);
                Assert.Equal(10, numberBox.LargeStep);
            }
        );
    }

    [Fact]
    public void Value_RejectsNaNAndInvalidTextDoesNotThrow()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Minimum = 0, Maximum = 10 };

                Assert.Throws<ArgumentException>(() => numberBox.Value = double.NaN);

                numberBox.Text = "NaN";
                CommitText(numberBox);

                Assert.Null(numberBox.Value);
                Assert.Equal(string.Empty, numberBox.Text);

                numberBox.Value = 5;
                numberBox.Text = "invalid";
                CommitText(numberBox);

                Assert.Equal(5, numberBox.Value);
                Assert.Equal("5", numberBox.Text);
            }
        );
    }

    [Fact]
    public void FormatString_RefreshesDisplayWithoutChangingValue()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = 1.5, FormatString = "F2" };

                Assert.Equal("1.50", numberBox.Text);
                Assert.Equal(1.5, numberBox.Value);
                Assert.Throws<ArgumentException>(() => numberBox.FormatString = "Q");
            }
        );
    }

    [Fact]
    public void Commit_PreservesParsedPrecision()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Text = "1.239" };

                CommitText(numberBox);

                Assert.Equal(1.239, numberBox.Value);
                Assert.Equal("1.239", numberBox.Text);
            }
        );
    }

    [Fact]
    public void InvalidPaste_IsBlockedWithoutDiscardingCurrentEdit()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new TestNumberBox { Value = 5, Text = "12" };
                var dataObject = new System.Windows.DataObject(DataFormats.UnicodeText, "invalid");
                var args = new DataObjectPastingEventArgs(
                    dataObject,
                    isDragDrop: false,
                    DataFormats.UnicodeText
                );

                numberBox.RaiseClipboardPaste(args);

                Assert.True(args.CommandCancelled);
                Assert.Equal("12", numberBox.Text);
                Assert.Equal(5, numberBox.Value);
            }
        );
    }

    [Fact]
    public void ExpressionOperands_UseConfiguredCultureParser()
    {
        RunOnStaThread(
            () =>
            {
                CultureInfo previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    var numberBox = new NumberBox
                    {
                        AcceptsExpression = true,
                        Text = "1,234+1",
                    };

                    CommitText(numberBox);

                    Assert.Equal(1235, numberBox.Value);
                    Assert.Equal("1235", numberBox.Text);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                }
            }
        );
    }

    [Theory]
    [InlineData(128.88695, 127.88695)]
    [InlineData(128.45, 127.45)]
    public void Decrement_UsesDecimalArithmeticForRepresentableValues(double value, double expected)
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = value };

                StepValue(numberBox, -1);

                Assert.Equal(expected, numberBox.Value);
                Assert.Equal(expected.ToString("G", CultureInfo.CurrentCulture), numberBox.Text);
            }
        );
    }

    [Fact]
    public void StepValue_OutsideDecimalRangeFallsBackToDoubleArithmetic()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = 1e100 };

                StepValue(numberBox, 1);

                Assert.Equal(1e100, numberBox.Value);
            }
        );
    }

    [Theory]
    [InlineData("128.88695-1", 127.88695)]
    [InlineData("128.45-1", 127.45)]
    [InlineData("0.1+0.2", 0.3)]
    public void Expression_UsesDecimalArithmeticWhenRepresentable(string expression, double expected)
    {
        RunOnStaThread(
            () =>
            {
                CultureInfo previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    var numberBox = new NumberBox
                    {
                        AcceptsExpression = true,
                        Text = expression,
                    };

                    CommitText(numberBox);

                    Assert.Equal(expected, numberBox.Value);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                }
            }
        );
    }

    [Theory]
    [InlineData("1e29+1e29", 2e29)]
    [InlineData("1e-29+1e-29", 2e-29)]
    public void Expression_OutsideDecimalRangeFallsBackToDoubleArithmetic(string expression, double expected)
    {
        RunOnStaThread(
            () =>
            {
                CultureInfo previousCulture = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    var numberBox = new NumberBox
                    {
                        AcceptsExpression = true,
                        Text = expression,
                    };

                    CommitText(numberBox);

                    Assert.Equal(expected, numberBox.Value);
                }
                finally
                {
                    CultureInfo.CurrentCulture = previousCulture;
                }
            }
        );
    }

    [Fact]
    public void ValueChanged_ReentrantAssignmentRaisesEveryTransition()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox();
                var transitions = new List<(double? OldValue, double? NewValue)>();
                numberBox.ValueChanged += (_, args) =>
                {
                    transitions.Add((args.OldValue, args.NewValue));
                    if (args.NewValue == 1)
                    {
                        numberBox.Value = 2;
                    }
                };

                numberBox.Value = 1;

                Assert.Equal([(null, 1), (1, 2)], transitions);
                Assert.Equal(2, numberBox.Value);
                Assert.Equal("2", numberBox.Text);
            }
        );
    }

    [Fact]
    public void ReadOnly_WithLocallySetPlacement_IgnoresSpinButtonClicks()
    {
        RunOnStaThread(
            () =>
            {
                NumberBox numberBox = CreateTemplatedNumberBox(
                    new NumberBox
                    {
                        IsReadOnly = true,
                        SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
                        Value = 5,
                    }
                );
                var spinButtons = Assert.IsType<ContentControl>(
                    numberBox.Template.FindName("PART_SpinButtons", numberBox)
                );
                RepeatButton incrementButton = Assert.Single(
                    FindVisualDescendants<RepeatButton>(numberBox),
                    button => NumberBox.GetButtonKind(button) == NumberBoxButtonKind.Increment
                );

                Assert.Equal(Visibility.Collapsed, spinButtons.Visibility);
                incrementButton.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, incrementButton)
                );

                Assert.Equal(5, numberBox.Value);
            }
        );
    }

    [Fact]
    public void TemplateReapplication_DoesNotDuplicateSpinClickHandling()
    {
        RunOnStaThread(
            () =>
            {
                NumberBox numberBox = CreateTemplatedNumberBox(new NumberBox { Value = 0 });
                numberBox.OnApplyTemplate();
                RepeatButton incrementButton = Assert.Single(
                    FindVisualDescendants<RepeatButton>(numberBox),
                    button => NumberBox.GetButtonKind(button) == NumberBoxButtonKind.Increment
                );

                incrementButton.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, incrementButton)
                );

                Assert.Equal(1, numberBox.Value);
            }
        );
    }

    [Fact]
    public void CompactPopupClick_IsHandledOnceWhenRouteReturnsToNumberBox()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
                    Value = 0,
                };
                var root = new Grid();
                root.Resources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                            UriKind.Absolute
                        ),
                    }
                );
                root.Children.Add(numberBox);

                var window = new Window
                {
                    Content = root,
                    ShowInTaskbar = false,
                    Width = 320,
                    Height = 200,
                };

                try
                {
                    window.Show();
                    numberBox.ApplyTemplate();
                    root.UpdateLayout();

                    var popup = Assert.IsType<Popup>(
                        numberBox.Template.FindName("PART_CompactPopup", numberBox)
                    );
                    popup.SetCurrentValue(Popup.IsOpenProperty, true);
                    RepeatButton incrementButton = Assert.Single(
                        FindVisualDescendants<RepeatButton>(popup.Child),
                        button => NumberBox.GetButtonKind(button) == NumberBoxButtonKind.Increment
                    );

                    incrementButton.RaiseEvent(
                        new RoutedEventArgs(ButtonBase.ClickEvent, incrementButton)
                    );

                    Assert.Equal(1, numberBox.Value);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void SpinButton_HeldForThreeSecondsUsesLargeStep()
    {
        RunOnStaThread(
            () =>
            {
                NumberBox numberBox = CreateTemplatedNumberBox(
                    new NumberBox { Value = 0, SmallStep = 1, LargeStep = 10 }
                );
                RepeatButton incrementButton = Assert.Single(
                    FindVisualDescendants<RepeatButton>(numberBox),
                    button => NumberBox.GetButtonKind(button) == NumberBoxButtonKind.Increment
                );

                typeof(NumberBox)
                    .GetField("_heldSpinButton", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(numberBox, incrementButton);
                typeof(NumberBox)
                    .GetField("_spinButtonPressTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(numberBox, Stopwatch.GetTimestamp() - (3 * Stopwatch.Frequency));

                incrementButton.RaiseEvent(
                    new RoutedEventArgs(ButtonBase.ClickEvent, incrementButton)
                );

                Assert.Equal(10, numberBox.Value);
            }
        );
    }

    [Fact]
    public void AutomationPeer_ExposesWritableRange()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    Minimum = -10,
                    Maximum = 10,
                    SmallStep = 0.5,
                    LargeStep = 2,
                    Value = 1,
                };
                AutomationPeer peer = CreateAutomationPeer(numberBox);
                var provider = Assert.IsAssignableFrom<IRangeValueProvider>(
                    peer.GetPattern(PatternInterface.RangeValue)
                );

                Assert.Equal(AutomationControlType.Spinner, peer.GetAutomationControlType());
                Assert.Equal(nameof(NumberBox), peer.GetClassName());
                Assert.Equal(-10, provider.Minimum);
                Assert.Equal(10, provider.Maximum);
                Assert.Equal(0.5, provider.SmallChange);
                Assert.Equal(2, provider.LargeChange);
                Assert.Equal(1, provider.Value);
                Assert.False(provider.IsReadOnly);

                provider.SetValue(4);
                Assert.Equal(4, numberBox.Value);

                Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetValue(11));
                Assert.Throws<ArgumentOutOfRangeException>(() => provider.SetValue(double.NaN));

                numberBox.IsReadOnly = true;
                Assert.True(provider.IsReadOnly);
                Assert.Throws<ElementNotEnabledException>(() => provider.SetValue(3));
            }
        );
    }

    [Fact]
    public void EnumDependencyProperties_RejectUndefinedValues()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox();

                Assert.Throws<ArgumentException>(
                    () => numberBox.SpinButtonPlacementMode = (NumberBoxSpinButtonPlacementMode)99
                );
                Assert.Throws<ArgumentException>(
                    () => NumberBox.SetButtonKind(numberBox, (NumberBoxButtonKind)99)
                );
            }
        );
    }

    private static NumberBox CreateTemplatedNumberBox(NumberBox numberBox)
    {
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            }
        );
        root.Children.Add(numberBox);

        Assert.True(numberBox.ApplyTemplate());
        root.Measure(new Size(800, 600));
        root.Arrange(new Rect(0, 0, 800, 600));
        root.UpdateLayout();
        return numberBox;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void CommitText(NumberBox numberBox)
    {
        typeof(NumberBox)
            .GetMethod("ValidateInput", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(numberBox, null);
    }

    private static void StepValue(NumberBox numberBox, double change)
    {
        typeof(NumberBox)
            .GetMethod("StepValue", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(numberBox, [change]);
    }

    private static AutomationPeer CreateAutomationPeer(NumberBox numberBox)
    {
        return (AutomationPeer)typeof(NumberBox)
            .GetMethod("OnCreateAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(numberBox, null)!;
    }

    private static void RunOnStaThread(Action action) => WpfTestThread.Run(action);

    private sealed class TestNumberBox : NumberBox
    {
        public void RaiseClipboardPaste(DataObjectPastingEventArgs args) =>
            OnClipboardPaste(this, args);
    }
}
