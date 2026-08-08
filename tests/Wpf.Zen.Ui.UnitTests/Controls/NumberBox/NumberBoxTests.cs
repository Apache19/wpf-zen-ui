// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
                    SmallChange = 100,
                    LargeChange = 150,
                };

                Assert.Equal(10, numberBox.Maximum);
                numberBox.Minimum = 0;
                Assert.Equal(5, numberBox.Maximum);
                Assert.Equal(5, numberBox.SmallChange);
                Assert.Equal(5, numberBox.LargeChange);

                numberBox.Maximum = 200;
                Assert.Equal(100, numberBox.SmallChange);
                Assert.Equal(150, numberBox.LargeChange);

                numberBox.Maximum = 10;
                Assert.Equal(10, numberBox.SmallChange);
                Assert.Equal(10, numberBox.LargeChange);
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
            }
        );
    }

    [Fact]
    public void NumberFormatter_IsRequiredAndRefreshesDisplayWhenChanged()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = 1.5 };

                Assert.Throws<ArgumentException>(
                    () => numberBox.SetValue(NumberBox.NumberFormatterProperty, null)
                );

                numberBox.NumberFormatter = new PrefixNumberFormatter();

                Assert.Equal("value:1.5", numberBox.Text);
            }
        );
    }

    [Fact]
    public void Commit_RoundsValueToMaxDecimalPlaces()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { MaxDecimalPlaces = 2, Text = "1.239" };

                CommitText(numberBox);

                Assert.Equal(1.24, numberBox.Value);
                Assert.Equal("1.24", numberBox.Text);

                numberBox.Value = 9.876;
                Assert.Equal(9.88, numberBox.Value);

                numberBox.MaxDecimalPlaces = 1;
                Assert.Equal(9.9, numberBox.Value);
                Assert.Equal("9.9", numberBox.Text);
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
    public void AutomationPeer_ExposesWritableRange()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    Minimum = -10,
                    Maximum = 10,
                    SmallChange = 0.5,
                    LargeChange = 2,
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
                    () => numberBox.ValidationMode = (NumberBoxValidationMode)99
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

    private static AutomationPeer CreateAutomationPeer(NumberBox numberBox)
    {
        return (AutomationPeer)typeof(NumberBox)
            .GetMethod("OnCreateAutomationPeer", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(numberBox, null)!;
    }

    private static void RunOnStaThread(Action action) => WpfTestThread.Run(action);

    private sealed class PrefixNumberFormatter : INumberFormatter, INumberParser
    {
        public string FormatDouble(double? value) =>
            value is null
                ? string.Empty
                : $"value:{value.Value.ToString("G", CultureInfo.InvariantCulture)}";

        public string FormatInt(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        public string FormatUInt(uint? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        public double? ParseDouble(string? value) =>
            double.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out double result)
                ? result
                : null;

        public int? ParseInt(string? value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
                ? result
                : null;

        public uint? ParseUInt(string? value) =>
            uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result)
                ? result
                : null;
    }
}
