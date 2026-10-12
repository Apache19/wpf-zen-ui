// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Dialog = Wpf.Zen.Ui.Controls.ContentDialog;
using NativeButton = System.Windows.Controls.Button;
#pragma warning disable xUnit1051

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    [Fact]
    public void DefaultTemplate_KeepsDismissButtonAboveHeaderAndOwnsShellAppearance() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.CornerRadius = new CornerRadius(4D, 8D, 12D, 16D);
        dialog.Footer = new ContentDialogActionBar { Items = { new NativeButton { Content = "Close" }, }, };

        Task<object?> task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.UpdateLayout();

        var header = FindDialogTemplateElement<ContentPresenter>(dialog, element => element.ContentSource == "Header");
        var dismiss = FindDialogTemplateElement<Wpf.Zen.Ui.Controls.Button>(
            dialog, element => AutomationProperties.GetAutomationId(element) == "DismissButton");
        var footer = Assert.IsType<ContentDialogFooterContainer>(dialog.Template.FindName("footerBorder", dialog));
        var shell = Assert.IsType<ClippingBorder>(VisualTreeHelper.GetChild(dialog, 0));

        Point headerOrigin = header.TranslatePoint(default, dialog);
        Point dismissOrigin = dismiss.TranslatePoint(default, dialog);
        Assert.True(
            dismissOrigin.Y < headerOrigin.Y,
            $"The dismiss surface ({dismissOrigin.Y}) must remain above the header baseline ({headerOrigin.Y})."
        );
        Assert.Equal(new Thickness(24D, 20D, 48D, 0D), dialog.HeaderPadding);
        Assert.Equal(new Thickness(24D, 20D, 24D, 20D), footer.Padding);
        Assert.Equal(new Thickness(0D, 1D, 0D, 0D), footer.BorderThickness);
        Assert.Equal(dialog.CornerRadius, shell.CornerRadius);
        Assert.Equal(default(CornerRadius), footer.CornerRadius);

        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void CornerRadius_IsAValidatedLocalXamlProperty() => Run(_ =>
    {
        var dialog = Assert.IsType<Dialog>(XamlReader.Parse("""
                                                            <ui:ContentDialog
                                                                xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                                                                CornerRadius="4,8,12,16" />
                                                            """));

        Assert.Equal(new CornerRadius(4D, 8D, 12D, 16D), dialog.CornerRadius);
        Assert.Equal(
            BaseValueSource.Local,
            DependencyPropertyHelper.GetValueSource(dialog, Dialog.CornerRadiusProperty).BaseValueSource
        );
        Assert.Throws<ArgumentException>(() => dialog.CornerRadius = new CornerRadius(-1D));
    });

    [Theory]
    [InlineData(HorizontalAlignment.Center, VerticalAlignment.Center, false, false)]
    [InlineData(HorizontalAlignment.Stretch, VerticalAlignment.Center, true, false)]
    [InlineData(HorizontalAlignment.Center, VerticalAlignment.Stretch, false, true)]
    [InlineData(HorizontalAlignment.Stretch, VerticalAlignment.Stretch, true, true)]
    public void Alignment_StretchesOnlyRequestedAxesAndPreservesHostPadding(
        HorizontalAlignment expectedHorizontalAlignment,
        VerticalAlignment expectedVerticalAlignment,
        bool stretchesWidth,
        bool stretchesHeight) => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.HorizontalAlignment = expectedHorizontalAlignment;
        dialog.VerticalAlignment = expectedVerticalAlignment;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.UpdateLayout();

        var presenter = Assert.IsType<ContentDialogTransitionPresenter>(
            scope.Host.Template.FindName("PART_DialogPresenter", scope.Host)
        );
        var dialogRoot = Assert.IsType<ContentPresenter>(
            presenter.Template.FindName("PART_DialogRoot", presenter)
        );

        Assert.Equal(expectedHorizontalAlignment, dialog.HorizontalAlignment);
        Assert.Equal(expectedVerticalAlignment, dialog.VerticalAlignment);
        Assert.Equal(stretchesWidth, double.IsPositiveInfinity(dialog.MaxWidth));
        Assert.Equal(stretchesHeight, double.IsPositiveInfinity(dialog.MaxHeight));

        if (stretchesWidth)
        {
            Assert.Equal(
                presenter.ActualWidth - 48D,
                dialog.ActualWidth
            );
        }
        else
        {
            Assert.True(dialog.ActualWidth < presenter.ActualWidth - 48D);
        }

        if (stretchesHeight)
        {
            Assert.Equal(
                presenter.ActualHeight - 48D,
                dialog.ActualHeight
            );
        }
        else
        {
            Assert.True(dialog.ActualHeight < presenter.ActualHeight - 48D);
        }

        dialog.Close();
        Pump(task);
    });

    /// <summary>
    /// Explicit dimensions release automatic theme limits independently and Auto restores them.
    /// </summary>
    [Fact]
    public void ExplicitDimensions_ReleaseOnlyTheirOwnDefaultLimit()
    {
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            Assert.Equal(640, dialog.MaxWidth);
            Assert.Equal(850, dialog.MaxHeight);
            dialog.Width = 720;
            dialog.UpdateLayout();
            Assert.Equal(720, dialog.ActualWidth);
            Assert.True(double.IsPositiveInfinity(dialog.MaxWidth));
            Assert.Equal(850, dialog.MaxHeight);
            dialog.Width = double.NaN;
            dialog.HorizontalAlignment = HorizontalAlignment.Right;
            dialog.UpdateLayout();
            Assert.Equal(640, dialog.MaxWidth);
            dialog.Close();
            Pump(task);
        });
    }

    [Fact]
    public void StretchSizing_RespectsExplicitMaximums() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.HorizontalAlignment = HorizontalAlignment.Stretch;
        dialog.VerticalAlignment = VerticalAlignment.Stretch;
        dialog.MaxWidth = 320D;
        dialog.MaxHeight = 240D;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.UpdateLayout();

        Assert.Equal(320D, dialog.MaxWidth);
        Assert.Equal(240D, dialog.MaxHeight);
        Assert.Equal(320D, dialog.ActualWidth);
        Assert.Equal(240D, dialog.ActualHeight);

        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void DerivedDialog_UsesContentDialogThemeStyleWithoutAnImplicitForwardingStyle() => Run(scope =>
    {
        var dialog = new DerivedDialog { Header = "Dialog", Content = "Content" };
        Task<object?> task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.Equal(
            BaseValueSource.DefaultStyle,
            DependencyPropertyHelper.GetValueSource(dialog, Control.TemplateProperty).BaseValueSource
        );
        _ = dialog.ApplyTemplate();
        Assert.NotNull(dialog.Template.FindName("PART_ContentPresenter", dialog));

        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void Footer_HasLogicalOwnershipAndInheritsDataContextBeforePresentation() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var footer = new NativeButton();
        var model = new { Label = "Save" };
        dialog.DataContext = model;
        footer.SetBinding(ContentControl.ContentProperty, new Binding("Label"));
        dialog.Footer = footer;
        Assert.Same(dialog, LogicalTreeHelper.GetParent(footer));
        Assert.Same(model, footer.DataContext);
        Drain();
        Assert.Equal("Save", footer.Content);
        Assert.Contains(footer, LogicalTreeHelper.GetChildren(dialog).Cast<object>());
        dialog.Footer = new Border();
        Assert.Null(LogicalTreeHelper.GetParent(footer));
        Assert.DoesNotContain(footer, LogicalTreeHelper.GetChildren(dialog).Cast<object>());
    });

    [Fact]
    public void LogicalChildren_SnapshotsDoNotRequireDisposal() => Run(_ =>
    {
        var dialog = new LogicalChildrenDialogProbe
        {
            Header = new object(), Content = new object(), Footer = new object(),
        };
        var host = new LogicalChildrenHostProbe { Content = new object() };

        Assert.False(dialog.GetLogicalChildren() is IDisposable);
        Assert.False(host.GetLogicalChildren() is IDisposable);
    });

    [Theory]
    [InlineData("Header")]
    [InlineData("Footer")]
    public void ContentSlots_FormatAndSelectTemplates(string slot) => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        if (slot == "Header")
        {
            dialog.Header = 12;
            dialog.HeaderStringFormat = "Items: {0:D3}";
        }
        else
        {
            dialog.Footer = 12;
            dialog.FooterStringFormat = "Items: {0:D3}";
        }

        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var presenter = FindDialogTemplateElement<ContentPresenter>(dialog, element => element.ContentSource == slot);
        string Text() => Assert.IsType<System.Windows.Controls.TextBlock>(VisualTreeHelper.GetChild(presenter, 0)).Text;
        Assert.Equal("Items: 012", Text());
        var template = (DataTemplate)XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='Selected'/></DataTemplate>");
        var selector = new TitleSelector(template);
        if (slot == "Header")
            dialog.HeaderTemplateSelector = selector;
        else
            dialog.FooterTemplateSelector = selector;
        Drain();
        Assert.Equal("Selected", Text());
        Assert.Equal(12, selector.Item);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void Close_ReturnsBusinessObjectAfterCleanupAndReusesDialog() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        var result = new object();
        var closed = 0;
        dialog.Closed += (_, e) =>
        {
            Assert.Null(scope.Host.CurrentDialog);
            Assert.Null(dialog.Parent);
            Assert.True(dialog.IsOpen);
            Assert.Same(result, e.Result);
            closed++;
        };
        for (var i = 0; i < 2; i++)
        {
            var task = scope.Host.ShowAsync(dialog);
            dialog.Close(result);
            dialog.Close("Ignored");
            Pump(task);
            Assert.Same(result, await task);
            Assert.False(dialog.IsOpen);
            Assert.Same(scope.Background, scope.Host.Content);
        }

        Assert.Equal(2, closed);
    });

    [Fact]
    public void Closing_CanVetoAndRejectsReentrantClose() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        var calls = 0;
        dialog.Closing += (_, e) =>
        {
            calls++;
            dialog.Close("Reentrant");
            e.Cancel = calls == 1;
        };
        var task = scope.Host.ShowAsync(dialog);
        dialog.Close("Veto");
        Assert.False(dialog.IsClosing);
        dialog.Close("Accepted");
        Pump(task);
        Assert.Equal("Accepted", await task);
        Assert.Equal(2, calls);
    });

    [Fact]
    public void NonDismissible_BlocksUserCancellationButAllowsBusinessClose() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.IsDismissible = false;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(Dialog.DismissCommand.CanExecute(null, dialog));
        Assert.True(Dialog.CloseCommand.CanExecute("Success", dialog));
        Dialog.CloseCommand.Execute("Success", dialog);
        Pump(task);
        Assert.Equal("Success", await task);
    });

    [Fact]
    public void NativeCancelButton_DismissesEmbeddedDialogWithoutClosingWindow() => Run(async scope =>
    {
        var cancel = new NativeButton { Content = "Cancel", IsCancel = true };
        var dialog = scope.CreateDialog();
        dialog.Footer = cancel;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Invoke(cancel);
        Pump(task);
        Assert.True(scope.Window.IsVisible);
        Assert.Null(await task);
    });

    [Fact]
    public void NativeCancelButton_RespectsNonDismissible() => Run(scope =>
    {
        var cancel = new NativeButton { Content = "Cancel", IsCancel = true };
        var dialog = scope.CreateDialog();
        dialog.Footer = cancel;
        dialog.IsDismissible = false;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Invoke(cancel);
        Assert.False(task.IsCompleted);
        Assert.True(scope.Window.IsVisible);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void TokenCancellation_CancelsTaskAfterCleanupDespiteVeto() => Run(async scope =>
    {
        using var cancellation = new CancellationTokenSource();
        var dialog = scope.CreateDialog();
        dialog.IsDismissible = false;
        dialog.Closing += (_, _) => throw new InvalidOperationException("Cancellation must bypass Closing");
        var closed = false;
        dialog.Closed += (_, _) => closed = true;
        var task = scope.Host.ShowAsync(dialog, cancellation.Token);
        Drain();
        await Task.Run(() => cancellation.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(task.IsCanceled);
        Assert.True(closed);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.False(dialog.IsOpen);
    });

    [Fact]
    public void CloseAndCancellation_FirstAcceptedOutcomeWins() => Run(async scope =>
    {
        using var cancellation = new CancellationTokenSource();
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog, cancellation.Token);
        dialog.Close("Saved");
        cancellation.Cancel();
        Pump(task);
        Assert.Equal("Saved", await task);
    });

    [Fact]
    public void PreCanceledToken_DoesNotAttachDialog() => Run(scope =>
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog, cancellation.Token);
        Assert.ThrowsAny<OperationCanceledException>(() => Pump(task));
        Assert.Null(scope.Host.CurrentDialog);
        Assert.False(dialog.IsOpen);
    });

    [Fact]
    public void BusyHostAndConcurrentInstance_AreRejected() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Assert.Throws<InvalidOperationException>(() => Pump(scope.Host.ShowAsync(scope.CreateDialog())));
        Assert.Throws<InvalidOperationException>(() => Pump(scope.Host.ShowAsync(dialog)));
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void HostUnload_BypassesVetoAndReleasesSession() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.Closing += (_, e) => e.Cancel = true;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        scope.Window.Content = null;
        Pump(task);
        Assert.Null(await task);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.False(dialog.IsOpen);
    });

    [Fact]
    public void MissingRequiredHostPart_RejectsPresentation() => Run(scope =>
    {
        scope.Host.Template = new ControlTemplate(typeof(ContentDialogHost));
        Assert.Throws<InvalidOperationException>(() => Pump(scope.Host.ShowAsync(scope.CreateDialog())));
        Assert.Null(scope.Host.CurrentDialog);
    });

    [Fact]
    public void Host_DisablesOnlyItsContentAndPreservesBindings() => Run(scope =>
    {
        var model = new EnableModel();
        scope.Background.SetBinding(UIElement.IsEnabledProperty,
            new Binding(nameof(EnableModel.Enabled)) { Source = model });
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(scope.Background.IsEnabled);
        Assert.True(scope.Host.IsEnabled);
        model.Enabled = false;
        dialog.Close();
        Pump(task);
        Drain();
        Assert.False(scope.Background.IsEnabled);
        Assert.True(BindingOperations.IsDataBound(scope.Background, UIElement.IsEnabledProperty));
        model.Enabled = true;
        Drain();
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void Focus_UsesExplicitTargetThenBody() => Run(scope =>
    {
        var body = new System.Windows.Controls.TextBox();
        var footer = new Wpf.Zen.Ui.Controls.Button { Content = "Delete", Appearance = ControlAppearance.Danger };
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        dialog.Footer = footer;
        dialog.InitialFocus = footer;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(footer, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
        dialog.InitialFocus = null;
        task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(body, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void DefaultAndAccessKeys_AreScopedToDialog() => Run(async scope =>
    {
        var background = new NativeButton { Content = "_Go", IsDefault = true };
        scope.Host.Content = background;
        var foreground = new NativeButton
        {
            Content = "_Go", IsDefault = true, Command = Dialog.CloseCommand, CommandParameter = "Selected"
        };
        var dialog = scope.CreateDialog();
        dialog.Footer = foreground;
        var backgroundClicks = 0;
        background.Click += (_, _) => backgroundClicks++;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var query = new AccessKeyPressedEventArgs();
        foreground.RaiseEvent(query);
        Assert.Same(dialog, query.Scope);
        AccessKeyManager.ProcessKey(dialog, "G", false);
        Pump(task);
        Assert.Equal("Selected", await task);
        Assert.Equal(0, backgroundClicks);
    });

    [Fact]
    public void Escape_AllowsBodyToConsumeFirst() => Run(scope =>
    {
        var body = new System.Windows.Controls.TextBox();
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        KeyEventHandler consume = (_, e) => e.Handled = true;
        body.KeyDown += consume;

        void Escape() =>
            body.RaiseEvent(
                new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(body), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent
                });

        Escape();
        Assert.False(task.IsCompleted);
        body.KeyDown -= consume;
        Escape();
        Pump(task);
    });

    [Fact]
    public void CommandTarget_IsHonoredInsideDialogAndBlockedOutside() => Run(scope =>
    {
        var command = new RoutedCommand();
        var target = new System.Windows.Controls.TextBox();
        var footer = new NativeButton { Content = "Run", Command = command, CommandTarget = target };
        var dialog = scope.CreateDialog();
        dialog.Content = target;
        dialog.Footer = footer;
        var calls = 0;
        target.CommandBindings.Add(new CommandBinding(command, (_, _) => calls++, (_, e) => e.CanExecute = true));
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Invoke(footer);
        Assert.Equal(1, calls);
        Assert.False(task.IsCompleted);
        scope.Background.CommandBindings.Add(new CommandBinding(command, (_, _) => calls++,
            (_, e) => e.CanExecute = true));
        footer.CommandTarget = scope.Background;
        Assert.False(command.CanExecute(null, scope.Background));
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void DefaultTemplate_RendersFunctionalDismissButton() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var button = FindDialogTemplateElement<Wpf.Zen.Ui.Controls.Button>(
            dialog, element => AutomationProperties.GetAutomationId(element) == "DismissButton");

        Assert.NotNull(button.Style);
        Assert.NotNull(button.Content);
        Invoke(button);
        Pump(task);
        Assert.Null(await task);
    });

    [Fact]
    public void Automation_UsesHeaderAndDismissalPolicy() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.Header = "Accessible dialog";
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var peer = UIElementAutomationPeer.CreatePeerForElement(dialog);
        Assert.Equal("Accessible dialog", peer.GetName());
        var provider = Assert.IsAssignableFrom<IWindowProvider>(peer.GetPattern(PatternInterface.Window));
        Assert.True(provider.IsModal);
        provider.Close();
        Pump(task);
    });

    [Fact]
    public void DefaultTemplate_UsesDedicatedTransitionPresenterWithoutDialogVisualStates() => Run(scope =>
    {
        _ = scope.Host.ApplyTemplate();
        var stateGroupsRoot = Assert.IsAssignableFrom<FrameworkElement>(VisualTreeHelper.GetChild(scope.Host, 0));
        Assert.DoesNotContain(
            VisualStateManager.GetVisualStateGroups(stateGroupsRoot).Cast<object>(),
            item => item is VisualStateGroup { Name: "DialogStates" }
        );
        Assert.IsType<ContentDialogTransitionPresenter>(
            scope.Host.Template.FindName("PART_DialogPresenter", scope.Host)
        );

        var dialog = scope.CreateDialog();
        var openedCount = 0;
        dialog.Opened += (_, _) => openedCount++;
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.Equal(1, openedCount);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void TransitionSettings_AreForwardedToDefaultPresenter() => Run(scope =>
    {
        var openingEasing = new QuadraticEase();
        var closingEasing = new SineEase();
        scope.Host.OpenDuration = TimeSpan.FromMilliseconds(311);
        scope.Host.CloseDuration = TimeSpan.FromMilliseconds(419);
        scope.Host.OpeningScale = 0.82d;
        scope.Host.ClosingScale = 0.91d;
        scope.Host.OpeningEasingFunction = openingEasing;
        scope.Host.ClosingEasingFunction = closingEasing;
        _ = scope.Host.ApplyTemplate();

        var presenter = Assert.IsType<ContentDialogTransitionPresenter>(
            scope.Host.Template.FindName("PART_DialogPresenter", scope.Host)
        );
        Assert.Equal(scope.Host.OpenDuration, presenter.OpenDuration);
        Assert.Equal(scope.Host.CloseDuration, presenter.CloseDuration);
        Assert.Equal(scope.Host.OpeningScale, presenter.OpeningScale);
        Assert.Equal(scope.Host.ClosingScale, presenter.ClosingScale);
        Assert.Same(openingEasing, presenter.OpeningEasingFunction);
        Assert.Same(closingEasing, presenter.ClosingEasingFunction);
    });

    [Fact]
    public void TransitionSettings_RejectNegativeDurationsAndInvalidScales() => Run(scope =>
    {
        Assert.Throws<ArgumentException>(() => scope.Host.OpenDuration = TimeSpan.FromTicks(-1));
        Assert.Throws<ArgumentException>(() => scope.Host.CloseDuration = TimeSpan.FromTicks(-1));
        Assert.Throws<ArgumentException>(() => scope.Host.OpeningScale = 0.0d);
        Assert.Throws<ArgumentException>(() => scope.Host.ClosingScale = double.NaN);
        Assert.Throws<ArgumentException>(() => scope.Host.ClosingScale = double.PositiveInfinity);
    });

    [Fact]
    public void InputTraffic_DoesNotStarvePresentationOrContentRestoration() => Run(scope =>
    {
        const int starvationLimit = 500;
        scope.Host.Template = CreateHostTemplateWithDurations("0", "0");
        scope.Host.IsAnimationEnabled = true;
        var dialog = scope.CreateDialog();
        var opened = false;
        var openInputOperations = 0;
        dialog.Opened += (_, _) => opened = true;

        void KeepInputBusyUntilOpened()
        {
            openInputOperations++;
            if (!opened && openInputOperations < starvationLimit)
            {
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new Action(KeepInputBusyUntilOpened)
                );
            }
        }

        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(KeepInputBusyUntilOpened)
        );
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.True(opened);
        Assert.True(openInputOperations < starvationLimit, "Input work starved dialog presentation.");
        Assert.False(scope.Background.IsEnabled);

        var closed = false;
        var closeInputOperations = 0;
        dialog.Closed += (_, _) => closed = true;

        void KeepInputBusyUntilClosed()
        {
            closeInputOperations++;
            if (!closed && closeInputOperations < starvationLimit)
            {
                _ = Dispatcher.CurrentDispatcher.BeginInvoke(
                    DispatcherPriority.Input,
                    new Action(KeepInputBusyUntilClosed)
                );
            }
        }

        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(KeepInputBusyUntilClosed)
        );
        dialog.Close();
        Pump(task);

        Assert.True(closed);
        Assert.True(closeInputOperations < starvationLimit, "Input work starved dialog cleanup.");
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void RepeatedAnimatedExit_ReleasesContentAndPresenterClocks() => Run(scope =>
    {
        scope.Host.Template = CreateHostTemplateWithDurations("0", "0:0:0.001");
        scope.Host.IsAnimationEnabled = true;
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var dialog = scope.CreateDialog();
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            Assert.False(task.IsCompleted);
            Assert.Same(dialog, scope.Host.CurrentDialog);
            Assert.False(scope.Background.IsEnabled);

            dialog.Close();
            Pump(task);

            Assert.Null(scope.Host.CurrentDialog);
            Assert.True(scope.Background.IsEnabled);
        }
    });

    [Fact]
    public void CancellationDuringLongEntrance_FinishesImmediately() => Run(scope =>
    {
        using var cancellation = new CancellationTokenSource();
        scope.Host.Template = CreateHostTemplateWithDurations("0:1:0", "0");
        scope.Host.IsAnimationEnabled = true;
        var dialog = scope.CreateDialog();
        var opened = false;
        dialog.Opened += (_, _) => opened = true;
        var task = scope.Host.ShowAsync(dialog, cancellation.Token);
        Drain();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Pump(task));
        Assert.False(opened);
        Assert.Null(scope.Host.CurrentDialog);
    });

    [Fact]
    public void CloseDuringLongEntrance_RedirectsFromCurrentPoseWithoutOpenedEvent() => Run(async scope =>
    {
        scope.Host.Template = CreateHostTemplateWithDurations("0:1:0", "0:0:0.01");
        scope.Host.IsAnimationEnabled = true;
        var dialog = scope.CreateDialog();
        var opened = false;
        dialog.Opened += (_, _) => opened = true;
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.False(opened);
        var presenter = Assert.IsType<ContentDialogTransitionPresenter>(
            scope.Host.Template.FindName("PART_DialogPresenter", scope.Host)
        );
        var dialogRoot = Assert.IsAssignableFrom<UIElement>(
            presenter.Template.FindName("PART_DialogRoot", presenter)
        );
        var opacityBeforeClose = dialogRoot.Opacity;
        dialog.Close("Closed during entrance");
        Assert.Equal(opacityBeforeClose, dialogRoot.Opacity, precision: 10);
        Pump(task);

        Assert.False(opened);
        Assert.Equal("Closed during entrance", await task);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void Focus_IsRestoredBeforeTaskCompletion() => Run(scope =>
    {
        var background = new System.Windows.Controls.TextBox();
        scope.Host.Content = background;
        Drain();
        background.Focus();
        var dialog = scope.CreateDialog();
        dialog.Content = new System.Windows.Controls.TextBox();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.NotSame(background, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
        Assert.Same(background, Keyboard.FocusedElement);
    });

    [Fact]
    public void Focus_ReturnsToAsyncCommandButtonAfterCommandCompletes() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.Content = new System.Windows.Controls.TextBox();
        var command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () =>
            await scope.Host.ShowAsync(dialog));
        var show = new Wpf.Zen.Ui.Controls.Button { Content = "Show", Command = command };
        scope.Host.Content = show;
        Drain();
        Assert.True(show.Focus());
        command.Execute(null);
        Drain();
        Assert.False(show.IsEnabled);
        Assert.True(dialog.IsKeyboardFocusWithin);
        dialog.Close();
        Pump(command.ExecutionTask!);
        Drain();
        Assert.True(show.IsEnabled);
        Assert.Same(show, Keyboard.FocusedElement);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Focus_DeferredRestorationPreservesNewFocus(bool openNextDialog) => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var next = scope.CreateDialog();
        var chosen = new System.Windows.Controls.TextBox();
        Task<object?>? nextTask = null;
        var command = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(async () =>
        {
            await scope.Host.ShowAsync(dialog);
            if (openNextDialog)
            {
                next.Content = chosen;
                nextTask = scope.Host.ShowAsync(next);
            }
            else
            {
                Assert.True(chosen.Focus());
            }
        });
        var show = new NativeButton { Content = "Show", Command = command };
        var body = new StackPanel();
        body.Children.Add(show);
        if (!openNextDialog)
        {
            body.Children.Add(chosen);
        }

        scope.Host.Content = body;
        Drain();
        Assert.True(show.Focus());
        command.Execute(null);
        Drain();
        dialog.Close();
        Pump(command.ExecutionTask!);
        Drain();
        Assert.Same(chosen, Keyboard.FocusedElement);
        if (nextTask is not null)
        {
            next.Close();
            Pump(nextTask);
        }
    });

    [Theory]
    [InlineData(ControlAppearance.Danger)]
    [InlineData(ControlAppearance.Caution)]
    public void Focus_AutomaticSelectionSkipsRiskyButtons(ControlAppearance appearance) => Run(scope =>
    {
        var risky = new Wpf.Zen.Ui.Controls.Button { Content = "Confirm", Appearance = appearance };
        var safe = new NativeButton { Content = "Other action" };
        var body = new StackPanel();
        body.Children.Add(risky);
        body.Children.Add(safe);
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(safe, Keyboard.FocusedElement);
        Assert.True(risky.Focus());
        dialog.Close();
        Pump(task);

        body.Children.Clear();
        dialog.Content = risky;
        task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(dialog, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void Focus_GuardAllowsIndependentRegionAndItsPopup() => Run(scope =>
    {
        var background = new System.Windows.Controls.TextBox();
        var independent = new System.Windows.Controls.TextBox();
        var popupEditor = new System.Windows.Controls.TextBox();
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = independent, Child = popupEditor, StaysOpen = true,
        };
        scope.Host.Content = background;
        ((Panel)scope.Window.Content).Children.Add(independent);
        ((Panel)scope.Window.Content).Children.Add(popup);
        Drain();
        background.Focus();
        var dialog = scope.CreateDialog();
        var editor = new System.Windows.Controls.TextBox();
        dialog.Content = editor;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(editor, Keyboard.FocusedElement);
        Assert.True(independent.Focus());
        Assert.Same(independent, Keyboard.FocusedElement);
        popup.IsOpen = true;
        Drain();
        Assert.True(popupEditor.Focus());
        Assert.Same(popupEditor, Keyboard.FocusedElement);
        popup.IsOpen = false;
        Assert.True(independent.Focus());
        dialog.Close();
        Pump(task);
        Assert.Same(independent, Keyboard.FocusedElement);
    });

    [Fact]
    public void Focus_RestorationIgnoresRememberedElementThatCanNoLongerReceiveFocus() => Run(scope =>
    {
        var background = new System.Windows.Controls.TextBox();
        scope.Host.Content = background;
        Drain();
        background.Focus();
        var dialog = scope.CreateDialog();
        dialog.Content = new System.Windows.Controls.TextBox();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        background.Focusable = false;
        dialog.Close();
        Pump(task);
        Assert.NotSame(background, Keyboard.FocusedElement);
    });

    [Fact]
    public void Popup_InsideDialogMayReceiveFocusAndCannotFocusCoveredContent() => Run(scope =>
    {
        var background = new System.Windows.Controls.TextBox();
        scope.Host.Content = background;
        var anchor = new NativeButton { Content = "Popup" };
        var editor = new System.Windows.Controls.TextBox { Width = 120 };
        var popup = new System.Windows.Controls.Primitives.Popup
        {
            PlacementTarget = anchor, Child = editor, StaysOpen = true
        };
        var body = new StackPanel();
        body.Children.Add(anchor);
        body.Children.Add(popup);
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        popup.IsOpen = true;
        Drain();
        Assert.True(editor.Focus());
        Assert.False(background.Focus());
        Assert.Same(editor, Keyboard.FocusedElement);
        popup.IsOpen = false;
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void Enter_UsesNativeDefaultAndMultilineEditorConsumesIt() => Run(async scope =>
    {
        var editor = new System.Windows.Controls.TextBox { AcceptsReturn = true };
        var button = new NativeButton
        {
            Content = "Save", IsDefault = true, Command = Dialog.CloseCommand, CommandParameter = "Saved"
        };
        var dialog = scope.CreateDialog();
        dialog.Content = editor;
        dialog.Footer = button;
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        void Enter() => InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(editor), Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });

        Enter();
        Assert.False(task.IsCompleted);
        editor.AcceptsReturn = false;
        Enter();
        Pump(task);
        Assert.Equal("Saved", await task);
    });

    [Fact]
    public void TextInput_ReleasesKeyForCharacterTranslationWithoutBubblingToWindow() => Run(scope =>
    {
        var calls = 0;
        scope.Window.KeyDown += (_, _) => calls++;
        var editor = new System.Windows.Controls.TextBox();
        var dialog = scope.CreateDialog();
        dialog.Content = editor;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(editor, Keyboard.FocusedElement);

        var key = new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(editor),
            Environment.TickCount,
            Key.A) { RoutedEvent = Keyboard.KeyDownEvent, };
        // HwndSource needs this result to be false before it permits WM_CHAR translation.
        Assert.False(InputManager.Current.ProcessInput(key));
        Assert.False(key.Handled);
        Assert.Equal(0, calls);
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, "a"));
        Drain();
        Assert.Equal("a", editor.Text);

        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void PlainWindowInputBinding_DoesNotExecuteFromDialog() => Run(scope =>
    {
        var calls = 0;
        scope.Window.InputBindings.Add(new KeyBinding(new TestCommand(() => calls++), Key.F6, ModifierKeys.None));
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(dialog), Environment.TickCount, Key.F6)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });
        Assert.Equal(0, calls);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void OpenedException_CleansUpAndFaultsTask() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        dialog.Opened += (_, _) => throw new InvalidOperationException("Opened failed");
        var task = scope.Host.ShowAsync(dialog);
        Assert.Throws<InvalidOperationException>(() => Pump(task));
        Assert.False(dialog.IsOpen);
        Assert.Null(scope.Host.CurrentDialog);
    });

    [Fact]
    public void Tab_NavigatesAndCyclesInsideDialog() => Run(scope =>
    {
        var first = new System.Windows.Controls.TextBox();
        var second = new System.Windows.Controls.TextBox();
        var body = new StackPanel();
        body.Children.Add(first);
        body.Children.Add(second);
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(first, Keyboard.FocusedElement);
        InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(first), Environment.TickCount, Key.Tab)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        });
        Assert.Same(second, Keyboard.FocusedElement);
        for (var i = 0; i < 8; i++)
        {
            InputManager.Current.ProcessInput(new KeyEventArgs(Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(dialog), Environment.TickCount, Key.Tab)
            {
                RoutedEvent = Keyboard.KeyDownEvent
            });
            Assert.True(dialog.IsKeyboardFocusWithin);
        }

        dialog.Close();
        Pump(task);
    });

    [Fact]
    public async Task DispatcherShutdown_CompletesPublicTaskWithoutAnotherDispatcherTurn()
    {
        Task<object?>? task = null;
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            var scope = new Scope();
            var dialog = scope.CreateDialog();
            task = scope.Host.ShowAsync(dialog);
            Drain();
            Dispatcher.CurrentDispatcher.InvokeShutdown();
            Assert.True(task.IsCompleted);
            Assert.False(dialog.IsOpen);
            Assert.Null(scope.Host.CurrentDialog);
        });
        Assert.Null(await task!);
    }

    [Fact]
    public void ClosedException_StillReleasesHostAndDialog() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        EventHandler<ContentDialogClosedEventArgs>
            fail = (_, _) => throw new InvalidOperationException("Closed failed");
        dialog.Closed += fail;
        var task = scope.Host.ShowAsync(dialog);
        dialog.Close("Saved");
        Assert.Throws<InvalidOperationException>(() => Pump(task));
        Assert.False(dialog.IsOpen);
        Assert.Null(scope.Host.CurrentDialog);
        dialog.Closed -= fail;
        task = scope.Host.ShowAsync(dialog);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void MissingDialogPresenter_ReleasesInvisibleSession() => Run(async scope =>
    {
        scope.Host.Template = (ControlTemplate)XamlReader.Parse("""
                                                                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                                    xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                                                                    TargetType="{x:Type ui:ContentDialogHost}">
                                                                    <ContentPresenter x:Name="PART_ContentRoot" ContentSource="Content" />
                                                                </ControlTemplate>
                                                                """);
        var task = scope.Host.ShowAsync(scope.CreateDialog());
        Pump(task);
        Assert.Null(await task);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void TemplateWithoutTransitionPresenter_PreservesLifecycleAndTransitionsImmediately() => Run(async scope =>
    {
        scope.Host.Template = (ControlTemplate)XamlReader.Parse("""
                                                                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                                    xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                                                                    TargetType="{x:Type ui:ContentDialogHost}">
                                                                    <Grid>
                                                                        <ContentPresenter x:Name="PART_ContentRoot" ContentSource="Content" />
                                                                        <ContentPresenter Content="{TemplateBinding CurrentDialog}" />
                                                                    </Grid>
                                                                </ControlTemplate>
                                                                """);
        scope.Host.IsAnimationEnabled = true;
        var dialog = scope.CreateDialog();
        var opened = false;
        dialog.Opened += (_, _) => opened = true;
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.True(opened);
        Assert.False(task.IsCompleted);
        dialog.Close("No transition presenter");
        Pump(task);

        Assert.Equal("No transition presenter", await task);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.True(scope.Background.IsEnabled);
    });

    [Fact]
    public void HostTemplateReplacement_RetainsOneSessionAndLogicalFooter() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var footer = new NativeButton { Content = "Close", Command = Dialog.CloseCommand };
        dialog.Footer = footer;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var template = (ControlTemplate)XamlReader.Parse(XamlWriter.Save(scope.Host.Template));
        scope.Host.Template = template;
        Drain();
        Assert.False(task.IsCompleted);
        Assert.Same(dialog, scope.Host.CurrentDialog);
        Assert.Same(dialog, footer.Parent);
        Assert.False(scope.Background.IsEnabled);
        Invoke(footer);
        Pump(task);
    });

    [Fact]
    public void LifecycleEvents_SupportGeneralRoutedEventHandlers() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var events = 0;
        RoutedEventHandler handler = (_, _) => events++;
        dialog.AddHandler(Dialog.ClosingEvent, handler);
        dialog.AddHandler(Dialog.ClosedEvent, handler);
        var task = scope.Host.ShowAsync(dialog);
        dialog.Close();
        Pump(task);
        Assert.Equal(2, events);
    });

    [Fact]
    public void CallerWithoutSynchronizationContext_StillCompletesOnHostDispatcher() => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        Task<object?> task;
        try
        {
            task = scope.Host.ShowAsync(dialog);
            Assert.Null(SynchronizationContext.Current);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Drain();
        dialog.Close("Done");
        Pump(task);
        Assert.Equal("Done", await task);
        Assert.Null(scope.Host.CurrentDialog);
    });

    private sealed class EnableModel : INotifyPropertyChanged
    {
        private bool _enabled = true;

        public bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private static void Invoke(NativeButton button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke)).Invoke();
        Drain();
    }

    private static void Run(Action<Scope> action) => WpfTestThread.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        using var scope = new Scope();
        action(scope);
    });

    private static void Run(Func<Scope, Task> action) => WpfTestThread.Run(async () =>
    {
        SynchronizationContext.SetSynchronizationContext(
            new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        using var scope = new Scope();
        await action(scope);
    });

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void Pump(Task task)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        timer.Tick += (_, _) => frame.Continue = false;
        var dispatcher = Dispatcher.CurrentDispatcher;
        task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
        Assert.True(task.IsCompleted, "Dialog presentation did not finish.");
        task.GetAwaiter().GetResult();
    }

    private static ControlTemplate CreateHostTemplateWithDurations(
        string openDuration,
        string closeDuration) =>
        (ControlTemplate)XamlReader.Parse($$"""
                                            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                                xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                                                TargetType="{x:Type ui:ContentDialogHost}">
                                                <Grid>
                                                    <ContentPresenter x:Name="PART_ContentRoot" ContentSource="Content" />
                                                    <ui:ContentDialogTransitionPresenter
                                                        x:Name="PART_DialogPresenter"
                                                        CloseDuration="{{closeDuration}}"
                                                        Dialog="{TemplateBinding CurrentDialog}"
                                                        OpenDuration="{{openDuration}}" />
                                                </Grid>
                                            </ControlTemplate>
                                            """);

    private sealed class Scope : IDisposable
    {
        public ContentDialogHost Host { get; } = new() { IsAnimationEnabled = false };
        public Border Background { get; } = new();

        public Window Window
        {
            get;
        }

        public Scope()
        {
            var root = new Grid();
            root.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml"),
            });
            Host.Content = Background;
            root.Children.Add(Host);
            Window = new Window
            {
                Content = root,
                Width = 600,
                Height = 480,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            Window.Show();
            Drain();
        }

        public Dialog CreateDialog() => new() { Header = "Dialog", Content = "Content" };

        public void Dispose()
        {
            Window.Close();
            Drain();
        }
    }

    private sealed class TitleSelector(DataTemplate template) : DataTemplateSelector
    {
        public object? Item
        {
            get;
            private set;
        }

        public DependencyObject? Container
        {
            get;
            private set;
        }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            Item = item;
            Container = container;
            return template;
        }
    }

    /// <summary>
    /// Represents an application-defined dialog whose inherited default style key must continue to select the library's
    /// <see cref="Dialog" /> theme style. Application subclasses should not need an implicit style that forwards to a
    /// theme resource through <c>StaticResource</c>.
    /// </summary>
    private sealed class DerivedDialog : Dialog
    {
    }

    private sealed class LogicalChildrenDialogProbe : Dialog
    {
        public System.Collections.IEnumerator GetLogicalChildren() => LogicalChildren;
    }

    private sealed class LogicalChildrenHostProbe : ContentDialogHost
    {
        public System.Collections.IEnumerator GetLogicalChildren() => LogicalChildren;
    }

    private sealed class TestCommand(Action execute) : ICommand
    {
        public bool Enabled { get; set; } = true;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => Enabled;
        public void Execute(object? parameter) => execute();
        public void Notify() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}