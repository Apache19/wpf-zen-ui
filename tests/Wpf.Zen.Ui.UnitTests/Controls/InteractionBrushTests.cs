// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Theming;
using Button = System.Windows.Controls.Button;
using Calendar = System.Windows.Controls.Calendar;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises live template bindings and real Calendar-generated day containers.
/// </summary>
[Collection(nameof(ThemeManagerTestCollection))]
public class InteractionBrushTests
{
    /// <summary>
    /// 弹出层的无限测量空间中，各语言的星期标题仍使用等宽列。
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("zh-CN")]
    [InlineData("de-DE")]
    [InlineData("fr-FR")]
    public void CalendarMonthView_HasUniformColumnsWhenMeasuredToContent(string language)
    {
        WpfTestThread.Run(() =>
        {
            var calendar = CreateCalendar("Light", language);
            calendar.DisplayDateStart = new DateTime(2026, 1, 1);
            calendar.DisplayDateEnd = new DateTime(2026, 12, 31);
            calendar.DisplayDate = new DateTime(2026, 10, 7);
            var blackoutStart = new DateTime(2026, 10, 10);
            var blackoutEnd = new DateTime(2026, 10, 12);
            calendar.BlackoutDates.Add(new CalendarDateRange(blackoutStart, blackoutEnd));
            calendar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            calendar.Arrange(new Rect(calendar.DesiredSize));
            calendar.UpdateLayout();
            var item = Descendants<CalendarItem>(calendar).Single();
            var month = (Grid)item.Template.FindName("PART_MonthView", item);
            var width = month.ColumnDefinitions[0].ActualWidth;
            foreach (var column in month.ColumnDefinitions)
            {
                Assert.Equal(width, column.ActualWidth, 5);
            }

            // 模拟屏幕边缘对弹出层高度的约束，日期按钮应保留完整的布局空间。
            calendar.Measure(new Size(400, 280));
            calendar.Arrange(new Rect(0, 0, 400, 280));
            calendar.UpdateLayout();
            var days = Descendants<CalendarDayButton>(month);
            foreach (var day in days)
            {
                var rowIndex = Grid.GetRow(day);
                var rowHeight = month.RowDefinitions[rowIndex].ActualHeight;
                var requiredHeight = day.ActualHeight + day.Margin.Top + day.Margin.Bottom;
                Assert.True(rowHeight >= requiredHeight);
            }
        });
    }

    /// <summary>
    /// Mouse feedback survives CalendarItem capture, follows dragging and clears on cancellation.
    /// </summary>
    [Theory]
    [InlineData(CalendarSelectionMode.SingleDate)]
    [InlineData(CalendarSelectionMode.SingleRange)]
    [InlineData(CalendarSelectionMode.MultipleRange)]
    public void Calendar_MouseGestureSurvivesCaptureTransfer(CalendarSelectionMode mode)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var calendar = CreateCalendar("Light");
            calendar.SelectionMode = mode;
            calendar.SelectedDate = DateTime.Today;
            var today = FindDay(calendar, DateTime.Today);
            var otherDate = DateTime.Today.Day == 1 ? DateTime.Today.AddDays(1) : DateTime.Today.AddDays(-1);
            var other = FindDay(calendar, otherDate);
            var items = Descendants<CalendarItem>(calendar);
            var item = items.Single();
            var inner = FindCircle(today, "InnerCircle");
            var palette = Interaction.GetPalette(today)!;
            var tracker = new CalendarInteraction.Tracker(calendar);
            try
            {
                SetFrameworkFlag(today, typeof(CalendarDayButton), "IsHighlightedPropertyKey", true);
                tracker.Begin(today);
                SetFrameworkFlag(today, typeof(ButtonBase), "IsPressedPropertyKey", true);
                tracker.CaptureChanged(item);
                SetFrameworkFlag(today, typeof(ButtonBase), "IsPressedPropertyKey", false);
                Pump();
                Assert.False(today.IsPressed);
                Assert.True(CalendarInteraction.GetIsPointerPressed(today));
                Assert.Equal(InteractionState.Pressed, Interaction.GetState(today));
                Assert.Same(palette.Pressed, inner.Fill);

                tracker.Move(other, true);
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));
                Assert.True(CalendarInteraction.GetIsPointerPressed(other));
                tracker.Move(null, true);
                Assert.False(CalendarInteraction.GetIsPointerPressed(other));
                tracker.Move(today, true);
                Assert.True(CalendarInteraction.GetIsPointerPressed(today));
                tracker.Move(today, false);
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));
                tracker.Move(other, true);
                Assert.False(CalendarInteraction.GetIsPointerPressed(other));

                tracker.Begin(today);
                tracker.CaptureChanged(new Border());
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));
                tracker.Begin(today);
                tracker.CaptureChanged(null);
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));
                tracker.Begin(today);
                calendar.IsEnabled = false;
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));
                Assert.Equal(InteractionState.Disabled, Interaction.GetState(today));
                calendar.IsEnabled = true;
                tracker.Begin(today);
                today.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent, today));
                Assert.False(CalendarInteraction.GetIsPointerPressed(today));

                calendar.BlackoutDates.Add(new CalendarDateRange(otherDate));
                tracker.Begin(other);
                Assert.False(CalendarInteraction.GetIsPointerPressed(other));
            }
            finally
            {
                tracker.Detach();
            }
        });
    }

    /// <summary>
    /// The style-installed behavior observes handled input and detaches without leaving press flags.
    /// </summary>
    [Fact]
    public void Calendar_MouseBehaviorObservesHandledEventsAndDetaches()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var calendar = CreateCalendar("Light");
            var today = FindDay(calendar, DateTime.Today);
            Assert.True(CalendarInteraction.GetIsEnabled(calendar));
            RaisePreviewMouse(today, System.Windows.Input.Mouse.PreviewMouseDownEvent);
            Assert.True(CalendarInteraction.GetIsPointerPressed(today));
            RaisePreviewMouse(today, System.Windows.Input.Mouse.PreviewMouseUpEvent);
            Assert.False(CalendarInteraction.GetIsPointerPressed(today));
            RaisePreviewMouse(today, System.Windows.Input.Mouse.PreviewMouseDownEvent);
            CalendarInteraction.SetIsEnabled(calendar, false);
            Assert.False(CalendarInteraction.GetIsPointerPressed(today));
            RaisePreviewMouse(today, System.Windows.Input.Mouse.PreviewMouseDownEvent);
            Assert.False(CalendarInteraction.GetIsPointerPressed(today));
        });
    }

    // Raise only the preview route; hardware button state is deliberately not synthesized here.
    private static void RaisePreviewMouse(UIElement target, RoutedEvent routedEvent)
    {
        var device = System.Windows.Input.Mouse.PrimaryDevice;
        var args = new System.Windows.Input.MouseButtonEventArgs(
            device, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = routedEvent,
            Handled = true,
        };
        target.RaiseEvent(args);
    }

    /// <summary>
    /// Palette entries, whole palettes and missing-state fallbacks remain live on ordinary buttons.
    /// </summary>
    [Fact]
    public void ButtonTemplate_TracksPaletteAndEntryReplacement()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var resources = Load("Controls/Calendar/InteractionBrushes.xaml");
            var button = new PressableButton
            {
                Style = (Style)resources["CalendarInteractionButtonStyle"],
                Template = (ControlTemplate)XamlReader.Parse("""
                    <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        xmlns:m="clr-namespace:Wpf.Zen.Ui.Markup;assembly=Wpf.Zen.Ui"
                        TargetType="Button">
                        <Border Name="Surface" Background="{m:InteractionBrush}" />
                    </ControlTemplate>
                    """),
            };
            var palette = new InteractionBrushSet
            {
                Normal = Brushes.Red,
                PointerOver = Brushes.Green,
                Pressed = Brushes.Blue,
            };
            Interaction.SetPalette(button, palette);
            Layout(button);
            var surface = (Border)button.Template.FindName("Surface", button);
            Assert.Same(Brushes.Red, surface.Background);

            // WPF owns this read-only flag; emulate its input update without moving the real pointer.
            SetFrameworkFlag(button, typeof(UIElement), "IsMouseOverPropertyKey", true);
            Pump();
            Assert.Equal(InteractionState.PointerOver, Interaction.GetState(button));
            Assert.Same(Brushes.Green, surface.Background);

            button.Press(true);
            Pump();
            Assert.Equal(InteractionState.Pressed, Interaction.GetState(button));
            Assert.Same(Brushes.Blue, surface.Background);

            palette.Pressed = Brushes.Gold;
            Pump();
            Assert.Same(Brushes.Gold, surface.Background);
            button.IsEnabled = false;
            Pump();
            Assert.Equal(InteractionState.Disabled, Interaction.GetState(button));
            Assert.Same(Brushes.Red, surface.Background);

            palette.Disabled = Brushes.Gray;
            Pump();
            Assert.Same(Brushes.Gray, surface.Background);
            button.IsEnabled = true;
            button.Press(false);
            SetFrameworkFlag(button, typeof(UIElement), "IsMouseOverPropertyKey", false);
            Interaction.SetPalette(button, new InteractionBrushSet { Normal = Brushes.Purple });
            Pump();
            Assert.Same(Brushes.Purple, surface.Background);
        });
    }

    /// <summary>
    /// Range previews cannot mask pressed or disabled feedback, including today's selected disk.
    /// </summary>
    [Fact]
    public void Calendar_RangeHighlightRespectsInputPriorityAndDisabledToday()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var calendar = CreateCalendar("Light");
            calendar.SelectedDate = DateTime.Today;
            var today = FindDay(calendar, DateTime.Today);
            var inner = FindCircle(today, "InnerCircle");
            SetFrameworkFlag(today, typeof(CalendarDayButton), "IsHighlightedPropertyKey", true);
            Pump();
            Assert.Equal(InteractionState.PointerOver, Interaction.GetState(today));

            SetFrameworkFlag(today, typeof(ButtonBase), "IsPressedPropertyKey", true);
            Pump();
            Assert.Equal(InteractionState.Pressed, Interaction.GetState(today));
            var palette = Interaction.GetPalette(today)!;
            Assert.Same(palette.Pressed, inner.Fill);

            calendar.IsEnabled = false;
            Pump();
            Assert.Equal(InteractionState.Disabled, Interaction.GetState(today));
            Assert.Equal(Visibility.Visible, inner.Visibility);
            Assert.Same(palette.Normal, inner.Fill);
            var disabledText = calendar.FindResource(BrushKeys.TextFillColorDisabledBrush);
            Assert.Same(disabledText, today.Foreground);

            calendar.IsTodayHighlighted = false;
            Pump();
            Assert.Equal(Visibility.Collapsed, inner.Visibility);
            var outer = FindCircle(today, "OuterCircle");
            Assert.Equal(1, outer.StrokeThickness);
        });
    }

    /// <summary>
    /// All geometry/input combinations use two circles and keep bindings after selection changes.
    /// </summary>
    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    [InlineData("HighContrast")]
    public void Calendar_ComposesTodaySelectionAndInteraction(string theme)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var calendar = CreateCalendar(theme);
            var today = FindDay(calendar, DateTime.Today);
            var otherDate = DateTime.Today.Day == 1 ? DateTime.Today.AddDays(1) : DateTime.Today.AddDays(-1);
            var other = FindDay(calendar, otherDate);
            var outer = FindCircle(today, "OuterCircle");
            var inner = FindCircle(today, "InnerCircle");
            Assert.Equal(2, Descendants<Ellipse>(today).Count());
            Assert.Equal(Visibility.Collapsed, inner.Visibility);

            var accent = Interaction.GetPalette(today)!;
            var subtle = Interaction.GetPalette(other)!;
            Assert.NotSame(accent, subtle);
            Assert.NotNull(accent.Normal);
            Assert.NotNull(accent.PointerOver);
            Assert.NotNull(accent.Pressed);

            foreach (var selected in new[] { false, true })
            {
                calendar.SelectedDate = selected ? DateTime.Today : null;
                Pump();
                Assert.Equal(selected ? 1 : 0, outer.StrokeThickness);
                Assert.Equal(selected ? Visibility.Visible : Visibility.Collapsed, inner.Visibility);

                foreach (var state in new[]
                         { InteractionState.Normal, InteractionState.PointerOver, InteractionState.Pressed })
                {
                    Interaction.SetState(today, state);
                    Pump();
                    var expected = Expected(accent, state);
                    Assert.Same(expected, outer.Stroke);
                    Assert.Same(expected, selected ? inner.Fill : outer.Fill);
                    if (selected)
                    {
                        Assert.Equal(Colors.Transparent, ((SolidColorBrush)outer.Fill).Color);
                    }
                }
            }

            calendar.SelectedDate = otherDate;
            var otherOuter = FindCircle(other, "OuterCircle");
            foreach (var state in new[]
                     { InteractionState.Normal, InteractionState.PointerOver, InteractionState.Pressed })
            {
                Interaction.SetState(other, state);
                Pump();
                Assert.Equal(1, otherOuter.StrokeThickness);
                Assert.Same(Expected(subtle, state), otherOuter.Fill);
            }

            // Reusing the same container must restore the binding previously covered by a trigger.
            calendar.SelectedDate = null;
            Interaction.SetState(today, InteractionState.PointerOver);
            Pump();
            Assert.Same(accent.PointerOver, outer.Fill);
            calendar.IsTodayHighlighted = false;
            Pump();
            var current = Interaction.GetPalette(today)!;
            Assert.Same(subtle.PointerOver, current.PointerOver);
            Assert.Same(current.PointerOver, outer.Fill);

            today.ClearValue(Interaction.StateProperty);
            calendar.IsEnabled = false;
            Pump();
            Assert.Equal(InteractionState.Disabled, Interaction.GetState(today));
            Assert.Same(current.Disabled, outer.Fill);
        });
    }

    /// <summary>
    /// Both implicit fill palettes and explicit stroke palettes follow theme resource replacement.
    /// </summary>
    [Fact]
    public void Calendar_UpdatesExistingBrushBindingsWhenThemeChanges()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var calendar = CreateCalendar("Light");
            calendar.SelectedDate = DateTime.Today;
            var today = FindDay(calendar, DateTime.Today);
            var inner = FindCircle(today, "InnerCircle");
            var outer = FindCircle(today, "OuterCircle");
            Interaction.SetState(today, InteractionState.PointerOver);
            Pump();
            var original = inner.Fill;

            var replacement = Brushes.OrangeRed;
            calendar.Resources[BrushKeys.AccentFillSecondaryBrush] = replacement;
            Pump();
            Assert.NotSame(original, inner.Fill);
            Assert.Same(replacement, inner.Fill);
            Assert.Same(replacement, outer.Stroke);

            calendar.Resources.Remove(BrushKeys.AccentFillSecondaryBrush);
            calendar.Resources.MergedDictionaries[0] = Load("Resources/Theme/Dark.xaml");
            Pump();
            var dark = calendar.FindResource(BrushKeys.AccentFillSecondaryBrush);
            Assert.Same(dark, inner.Fill);
            Assert.Same(dark, outer.Stroke);
        });
    }

    // Use real Calendar state population rather than setting framework read-only day properties.
    private static Calendar CreateCalendar(string theme, string language = "en-US")
    {
        var calendar = new Calendar
        {
            DisplayDate = DateTime.Today,
            Language = XmlLanguage.GetLanguage(language),
        };
        calendar.Resources.MergedDictionaries.Add(Load($"Resources/Theme/{theme}.xaml"));
        calendar.Resources.MergedDictionaries.Add(Load("Resources/Variables.xaml"));
        calendar.Resources.MergedDictionaries.Add(Load("Controls/Calendar/Calendar.xaml"));
        Layout(calendar);
        return calendar;
    }

    private static ResourceDictionary Load(string path)
    {
        var uri = new Uri("/Wpf.Zen.Ui;component/" + path, UriKind.Relative);
        return new ResourceDictionary { Source = uri };
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(600, 600));
        element.Arrange(new Rect(element.DesiredSize));
        element.UpdateLayout();
        Pump();
    }

    private static void Pump()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ContextIdle);
    }

    // Framework source declares these internal DP keys; production code only reads public states.
    private static void SetFrameworkFlag(DependencyObject target, Type owner, string name, bool value)
    {
        var field = owner.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        var key = (DependencyPropertyKey)field!.GetValue(null)!;
        target.SetValue(key, value);
    }

    private static CalendarDayButton FindDay(Calendar calendar, DateTime date)
    {
        var days = Descendants<CalendarDayButton>(calendar);
        return days.Single(day => Equals(day.DataContext, date));
    }

    private static Ellipse FindCircle(CalendarDayButton day, string name)
    {
        return (Ellipse)day.Template.FindName(name, day);
    }

    private static Brush? Expected(InteractionBrushSet palette, InteractionState state)
    {
        return state switch
        {
            InteractionState.PointerOver => palette.PointerOver,
            InteractionState.Pressed => palette.Pressed,
            _ => palette.Normal,
        };
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class PressableButton : Button
    {
        internal void Press(bool pressed)
        {
            IsPressed = pressed;
        }
    }
}
