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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NumberBoxControl;

public class NumberBoxTests
{
    [Fact]
    public void TextOnlyInitialization_DoesNotDependOnTemplate()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Text = "42.5" };

                Assert.Equal(42.5, numberBox.Value);
                Assert.Equal("42.5", numberBox.Text);

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
    public void RangeChanges_RecoerceValueAndRestoreItsBaseValueWhenPossible()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Minimum = 0, Maximum = 10, Value = 20 };

                Assert.Equal(10, numberBox.Value);
                Assert.Equal("10", numberBox.Text);

                // WPF retains the uncoerced base value. Expanding the range must therefore
                // restore 20 instead of leaving the previous effective boundary value behind.
                numberBox.Maximum = 30;

                Assert.Equal(20, numberBox.Value);
                Assert.Equal("20", numberBox.Text);

                numberBox.Minimum = 25;

                Assert.Equal(25, numberBox.Value);
                Assert.Equal("25", numberBox.Text);

                numberBox.Minimum = 0;

                Assert.Equal(20, numberBox.Value);
                Assert.Equal("20", numberBox.Text);
            }
        );
    }

    [Fact]
    public void NumericDependencyProperties_RejectNonFiniteOrNonPositiveValues()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox();

                Assert.Throws<ArgumentException>(() => numberBox.Value = double.PositiveInfinity);
                Assert.Throws<ArgumentException>(() => numberBox.Value = double.NegativeInfinity);
                Assert.Throws<ArgumentException>(() => numberBox.Minimum = double.NaN);
                Assert.Throws<ArgumentException>(() => numberBox.Maximum = double.PositiveInfinity);
                Assert.Throws<ArgumentException>(() => numberBox.SmallStep = 0);
                Assert.Throws<ArgumentException>(() => numberBox.SmallStep = double.NaN);
                Assert.Throws<ArgumentException>(() => numberBox.LargeStep = -1);
                Assert.Throws<ArgumentException>(() => numberBox.LargeStep = double.PositiveInfinity);
            }
        );
    }

    [Fact]
    public void Metadata_EnforcesSingleLineEditingAndLostFocusValueBinding()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    MinLines = 2,
                    MaxLines = 5,
                };
                var valueMetadata = Assert.IsType<FrameworkPropertyMetadata>(
                    NumberBox.ValueProperty.GetMetadata(typeof(NumberBox))
                );

                Assert.False(numberBox.AcceptsReturn);
                Assert.Equal(TextWrapping.NoWrap, numberBox.TextWrapping);
                Assert.Equal(1, numberBox.MinLines);
                Assert.Equal(1, numberBox.MaxLines);
                Assert.False(InputMethod.GetIsInputMethodEnabled(numberBox));
                Assert.True(valueMetadata.BindsTwoWayByDefault);
                Assert.Equal(UpdateSourceTrigger.LostFocus, valueMetadata.DefaultUpdateSourceTrigger);
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
    public void Commit_SkipsCanonicalTextWithoutRevertingLossyFormatting()
    {
        RunOnStaThread(
            () =>
            {
                // "F0" displays 1.5 as "2". LostFocus and LostKeyboardFocus both commit on the
                // common focus-loss path; the second commit must not parse the display text back
                // and round the stored value.
                var numberBox = new NumberBox { FormatString = "F0", Text = "1.5" };

                // Valid editable text updates Value immediately, but it remains unformatted until
                // the first commit ends the edit. This reproduces the state transition between the
                // two focus-loss callbacks instead of starting from already-canonical text.
                Assert.Equal(1.5, numberBox.Value);
                Assert.Equal("1.5", numberBox.Text);

                CommitText(numberBox);

                Assert.Equal(1.5, numberBox.Value);
                Assert.Equal("2", numberBox.Text);

                CommitText(numberBox);

                Assert.Equal(1.5, numberBox.Value);
                Assert.Equal("2", numberBox.Text);
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
                Assert.Equal(12, numberBox.Value);
            }
        );
    }

    [Fact]
    public void ReapplyingTemplate_DoesNotCommitExpressionBeingEdited()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox
                {
                    AcceptsExpression = true,
                    Text = "1+2",
                };

                numberBox.OnApplyTemplate();

                Assert.Equal(3, numberBox.Value);
                Assert.Equal("1+2", numberBox.Text);
            }
        );
    }

    [Theory]
    [InlineData("3", false)]
    [InlineData("3+4", false)]
    [InlineData("++", true)]
    [InlineData("invalid", true)]
    public void Paste_IsAcceptedOnlyWhenResultingTextCanBeParsed(
        string pastedText,
        bool expectedCancelled
    )
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new TestNumberBox
                {
                    AcceptsExpression = true,
                    Text = "12",
                    SelectionStart = 0,
                    SelectionLength = 2,
                };
                var dataObject = new System.Windows.DataObject(
                    DataFormats.UnicodeText,
                    pastedText
                );
                var args = new DataObjectPastingEventArgs(
                    dataObject,
                    isDragDrop: false,
                    DataFormats.UnicodeText
                );

                numberBox.RaiseClipboardPaste(args);

                Assert.Equal(expectedCancelled, args.CommandCancelled);
                Assert.Equal("12", numberBox.Text);
                Assert.Equal(12, numberBox.Value);
            }
        );
    }

    [Fact]
    public void Text_UpdatesValueAsSoonAsNumberOrExpressionCanBeParsed()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { AcceptsExpression = true, Value = 56 };

                numberBox.Text = "12";
                Assert.Equal(12, numberBox.Value);

                numberBox.Text = "12+";
                Assert.Equal(12, numberBox.Value);

                numberBox.Text = "12+3";
                Assert.Equal(15, numberBox.Value);
                Assert.Equal("12+3", numberBox.Text);
            }
        );
    }

    [Fact]
    public void EmptyAndIncompleteInput_HaveDistinctValueAndCommitSemantics()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = 12 };

                numberBox.Text = string.Empty;

                Assert.Null(numberBox.Value);
                Assert.Equal(string.Empty, numberBox.Text);

                numberBox.Value = 12;
                numberBox.Text = "-";

                Assert.Equal(12, numberBox.Value);
                Assert.Equal("-", numberBox.Text);

                CommitText(numberBox);

                Assert.Equal(12, numberBox.Value);
                Assert.Equal("12", numberBox.Text);
            }
        );
    }

    [Fact]
    public void DisablingExpressions_CommitsTheLastEvaluatedValue()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { AcceptsExpression = true, Text = "1+2" };

                Assert.Equal(3, numberBox.Value);
                Assert.Equal("1+2", numberBox.Text);

                numberBox.AcceptsExpression = false;

                Assert.Equal(3, numberBox.Value);
                Assert.Equal("3", numberBox.Text);
            }
        );
    }

    [Fact]
    public void KeyboardCommands_StepCommitAndDiscardAccordingToTheirContracts()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new TestNumberBox
                {
                    AcceptsExpression = true,
                    SmallStep = 1,
                    LargeStep = 10,
                    Value = 5,
                };
                var window = new Window
                {
                    Content = numberBox,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();
                    PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
                        PresentationSource.FromVisual(numberBox)
                    );

                    numberBox.Text = "-";
                    KeyEventArgs escape = CreateKeyEvent(source, Key.Escape);
                    numberBox.ProcessPreviewKeyDown(escape);

                    Assert.True(escape.Handled);
                    Assert.Equal(5, numberBox.Value);
                    Assert.Equal("5", numberBox.Text);

                    numberBox.Text = "1+2";
                    numberBox.CaretIndex = 0;
                    KeyEventArgs enter = CreateKeyEvent(source, Key.Enter);
                    numberBox.ProcessPreviewKeyDown(enter);

                    Assert.True(enter.Handled);
                    Assert.Equal(3, numberBox.Value);
                    Assert.Equal("3", numberBox.Text);
                    Assert.Equal(numberBox.Text.Length, numberBox.CaretIndex);

                    KeyEventArgs redundantEnter = CreateKeyEvent(source, Key.Enter);
                    numberBox.ProcessPreviewKeyDown(redundantEnter);
                    Assert.False(redundantEnter.Handled);

                    foreach ((Key key, double expected) in new[]
                    {
                        (Key.Up, 4d),
                        (Key.Down, 3d),
                        (Key.PageUp, 13d),
                        (Key.PageDown, 3d),
                    })
                    {
                        KeyEventArgs step = CreateKeyEvent(source, key);
                        numberBox.ProcessPreviewKeyDown(step);

                        Assert.True(step.Handled);
                        Assert.Equal(expected, numberBox.Value);
                    }

                    numberBox.IsReadOnly = true;
                    KeyEventArgs readOnlyStep = CreateKeyEvent(source, Key.Up);
                    numberBox.ProcessPreviewKeyDown(readOnlyStep);

                    Assert.False(readOnlyStep.Handled);
                    Assert.Equal(3, numberBox.Value);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void Text_UsesEffectiveCoercedOrReentrantValueImmediately()
    {
        RunOnStaThread(
            () =>
            {
                var rangedNumberBox = new NumberBox { Minimum = 0, Maximum = 10 };

                rangedNumberBox.Text = "12";

                Assert.Equal(10, rangedNumberBox.Value);
                Assert.Equal("10", rangedNumberBox.Text);

                var reentrantNumberBox = new NumberBox();
                reentrantNumberBox.ValueChanged += (_, args) =>
                {
                    if (args.NewValue == 1)
                    {
                        reentrantNumberBox.Value = 2;
                    }
                };

                reentrantNumberBox.Text = "1";

                Assert.Equal(2, reentrantNumberBox.Value);
                Assert.Equal("2", reentrantNumberBox.Text);
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
    [InlineData(double.MaxValue, double.MaxValue, double.MaxValue)]
    [InlineData(-double.MaxValue, -double.MaxValue, -double.MaxValue)]
    public void StepValue_DoubleOverflowSaturatesAtRangeBoundary(
        double value,
        double change,
        double expected
    )
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new NumberBox { Value = value };

                StepValue(numberBox, change);

                Assert.Equal(expected, numberBox.Value);
                Assert.Equal(expected.ToString("G", CultureInfo.CurrentCulture), numberBox.Text);
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
            .GetMethod("CommitInput", BindingFlags.Instance | BindingFlags.NonPublic)!
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

    private static KeyEventArgs CreateKeyEvent(PresentationSource source, Key key)
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
    }

    private static void RunOnStaThread(Action action) => WpfTestThread.Run(action);

    private sealed class TestNumberBox : NumberBox
    {
        public void RaiseClipboardPaste(DataObjectPastingEventArgs args) =>
            OnClipboardPaste(this, args);

        public void ProcessPreviewKeyDown(KeyEventArgs args) => OnPreviewKeyDown(args);
    }
}
