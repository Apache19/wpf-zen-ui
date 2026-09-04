// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
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
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Dialog = Wpf.Zen.Ui.Controls.ContentDialog;
using NativeButton = System.Windows.Controls.Button;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ContentDialogTests
{
    [Fact]
    public void TitlePresentation_FormatsAndSelectsTemplates_AndUpdatesLive() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.Title = 12;
            dialog.TitleStringFormat = "Items: {0:D3}";
            var task = dialog.ShowAsync();
            Drain();
            var presenter = (ContentPresenter)dialog.Template.FindName("TitlePresenter", dialog);
            string Text() => Assert.IsType<System.Windows.Controls.TextBlock>(VisualTreeHelper.GetChild(presenter, 0)).Text;
            Assert.Equal("Items: 012", Text());
            dialog.Title = 34;
            dialog.TitleStringFormat = "Count: {0}";
            Drain();
            Assert.Equal("Count: 34", Text());

            DataTemplate Template(string text) => (DataTemplate)XamlReader.Parse(
                $"""<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="{text}"/></DataTemplate>""");
            var selector = new TitleSelector(Template("Selected"));
            dialog.TitleTemplateSelector = selector;
            Drain();
            Assert.Equal("Selected", Text());
            Assert.Equal(34, selector.Item);
            Assert.Same(presenter, selector.Container);
            dialog.TitleTemplate = Template("Explicit");
            Drain();
            Assert.Equal("Explicit", Text());
            dialog.TitleTemplate = null;
            dialog.TitleTemplateSelector = null;
            Drain();
            Assert.Equal("Count: 34", Text());
            dialog.Hide();
            Pump(task);
        });

    [Theory]
    [InlineData("PART_PrimaryButton")]
    [InlineData("PART_SecondaryButton")]
    [InlineData("PART_TertiaryButton")]
    [InlineData("PART_CloseButton")]
    public void ButtonStyles_AreWiredByControl_InCustomTemplates(string partName) =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            var action = new ContentDialogAction { Content = "Action" };
            void SetAction(ContentDialogAction? value)
            {
                dialog.PrimaryAction = value;
                dialog.SecondaryAction = value;
                dialog.TertiaryAction = value;
            }

            void SetStyle(Style? value)
            {
                action.ButtonStyle = value;
                dialog.CloseButtonStyle = value;
            }

            ControlTemplate Template() => (ControlTemplate)XamlReader.Parse($$"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                    TargetType="{x:Type ui:ContentDialog}">
                    <Button x:Name="{{partName}}">
                        <Button.Style><Style TargetType="Button"><Setter Property="ToolTip" Value="Fallback"/></Style></Button.Style>
                    </Button>
                </ControlTemplate>
                """);
            SetAction(action);
            var style = new Style(typeof(NativeButton));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Override"));
            SetStyle(style);
            dialog.Template = Template();
            var task = dialog.ShowAsync();
            Drain();
            Assert.Same(style, Part(dialog, partName).Style);
            SetStyle(null);
            Drain();
            Assert.Equal("Fallback", Part(dialog, partName).ToolTip);
            action = new ContentDialogAction { ButtonStyle = style };
            SetAction(action);
            dialog.CloseButtonStyle = style;
            Drain();
            Assert.Same(style, Part(dialog, partName).Style);
            dialog.Template = Template();
            Drain();
            Assert.Same(style, Part(dialog, partName).Style);
            SetAction(null);
            dialog.CloseButtonStyle = null;
            Drain();
            Assert.Equal("Fallback", Part(dialog, partName).ToolTip);
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void CloseButtonStyle_ResetRestoresDefaultGlyphAndAccessibleName() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            var task = dialog.ShowAsync();
            Drain();
            var button = Part(dialog, "PART_CloseButton");
            var defaultStyle = button.Style;
            dialog.CloseButtonStyle = new Style(typeof(NativeButton));
            Drain();
            dialog.CloseButtonStyle = null;
            Drain();
            Assert.Same(defaultStyle, button.Style);
            Assert.Equal("×", button.Content);
            Assert.Equal("Close", AutomationProperties.GetName(button));
            dialog.Hide();
            Pump(task);
        });

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void BackdropTransition_SharesCompletionAndCleanup_WithOptionalSurface(bool surface, bool backdrop) =>
        Run(scope =>
        {
            scope.Host.Template = (ControlTemplate)XamlReader.Parse($$"""
                <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                    TargetType="{x:Type ui:ContentDialogHost}">
                    <Grid Background="Transparent">
                        <Border x:Name="{{(backdrop ? "PART_Backdrop" : "UnanimatedBackdrop")}}" Background="Black"/>
                        <Border x:Name="{{(surface ? "PART_TransitionRoot" : "UnanimatedSurface")}}">
                            <ContentPresenter Content="{TemplateBinding Content}"/>
                        </Border>
                    </Grid>
                </ControlTemplate>
                """);
            scope.Host.IsAnimationEnabled = true;
            scope.Host.TransitionDuration = TimeSpan.FromSeconds(5);
            var dialog = scope.CreateDialog();
            int opened = 0;
            dialog.Opened += (_, _) => opened++;
            var task = dialog.ShowAsync();
            Drain();
            var shade = (Border)scope.Host.Template.FindName(backdrop ? "PART_Backdrop" : "UnanimatedBackdrop", scope.Host);
            var root = (Border)scope.Host.Template.FindName(surface ? "PART_TransitionRoot" : "UnanimatedSurface", scope.Host);
            Assert.Equal(backdrop && SystemParameters.ClientAreaAnimation, shade.HasAnimatedProperties);
            Assert.Equal(surface && SystemParameters.ClientAreaAnimation, root.HasAnimatedProperties);
            scope.Host.IsAnimationEnabled = false;
            Drain();
            Assert.Equal(1, opened);
            Assert.Equal(1d, shade.Opacity);
            Assert.False(shade.HasAnimatedProperties);
            scope.Host.IsAnimationEnabled = true;
            dialog.Hide();
            Drain();
            if ((surface || backdrop) && SystemParameters.ClientAreaAnimation)
            {
                Assert.False(task.IsCompleted);
                Assert.False(scope.Background.IsEnabled);
                Assert.Equal(backdrop, shade.HasAnimatedProperties);
                scope.Host.TransitionDuration = TimeSpan.Zero;
            }

            Pump(task);
            Assert.False(shade.HasAnimatedProperties);
            Assert.False(root.HasAnimatedProperties);
            Assert.True(scope.Background.IsEnabled);
        });

    [Theory]
    [InlineData(ContentDialogButton.Primary, ContentDialogResult.Primary)]
    [InlineData(ContentDialogButton.Secondary, ContentDialogResult.Secondary)]
    [InlineData(ContentDialogButton.Tertiary, ContentDialogResult.Tertiary)]
    public void FooterActions_ExecuteBeforeClose_AndReturnDistinctResults(ContentDialogButton button, ContentDialogResult result) =>
        Run(scope =>
        {
            int executions = 0;
            var command = new TestCommand(() => executions++);
            var dialog = scope.CreateDialog();
            dialog.PrimaryAction = new ContentDialogAction { Content = "Save", Command = command };
            dialog.SecondaryAction = new ContentDialogAction { Content = "Discard", Command = command };
            dialog.TertiaryAction = new ContentDialogAction { Content = "Cancel", Command = command };
            int closed = 0;
            dialog.Closed += (_, args) =>
            {
                closed++;
                Assert.Equal(result, args.Result);
                Assert.Equal(1, executions);
            };
            Task<ContentDialogResult> task = dialog.ShowAsync();
            Drain();
            Invoke(Part(dialog, $"PART_{button}Button"));
            Pump(task);
            Assert.Equal(result, task.Result);
            Assert.Equal(1, closed);
            Assert.False(dialog.IsOpen);
            Assert.Null(scope.Host.Content);
            Assert.True(scope.Background.IsEnabled);
        });

    [Fact]
    public void HeaderDismissal_DoesNotRaiseAction_AndReturnsNone() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.TertiaryAction = new ContentDialogAction { Content = "Third choice" };
            int actions = 0;
            dialog.ButtonClicked += (_, _) => actions++;
            var task = dialog.ShowAsync();
            Drain();
            var close = Part(dialog, "PART_CloseButton");
            var third = Part(dialog, "PART_TertiaryButton");
            Assert.True(close.TranslatePoint(default(Point), dialog).Y < third.TranslatePoint(default(Point), dialog).Y);
            Invoke(close);
            Pump(task);
            Assert.Equal(ContentDialogResult.None, task.Result);
            Assert.Equal(0, actions);
            Assert.True(scope.Window.IsVisible);
        });

    [Fact]
    public void DisabledAction_RemainsVisible_AndTracksCommandAndDefinitionChanges() =>
        Run(scope =>
        {
            var command = new TestCommand(() => { }) { Enabled = false };
            var action = new ContentDialogAction { Content = "Save", Command = command };
            var dialog = scope.CreateDialog();
            dialog.PrimaryAction = action;
            var task = dialog.ShowAsync();
            Drain();
            var button = Part(dialog, "PART_PrimaryButton");
            Assert.True(button.IsVisible);
            Assert.False(button.IsEnabled);
            command.Enabled = true;
            command.Notify();
            Drain();
            Assert.True(button.IsEnabled);
            action.IsEnabled = false;
            Drain();
            Assert.True(button.IsVisible);
            Assert.False(button.IsEnabled);
            dialog.PrimaryAction = null;
            Drain();
            Assert.Equal(Visibility.Collapsed, button.Visibility);
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void ActionValidation_CanRejectInvocation_WithoutVetoingHide() =>
        Run(scope =>
        {
            int executions = 0;
            var dialog = scope.CreateDialog();
            dialog.PrimaryAction = new ContentDialogAction { Content = "Save", Command = new TestCommand(() => executions++) };
            dialog.ButtonClicked += (_, args) => args.Cancel = true;
            var task = dialog.ShowAsync();
            Drain();
            Invoke(Part(dialog, "PART_PrimaryButton"));
            Assert.False(task.IsCompleted);
            Assert.Equal(0, executions);
            dialog.Hide(ContentDialogResult.Secondary);
            dialog.Hide(ContentDialogResult.Primary);
            Pump(task);
            Assert.Equal(ContentDialogResult.Secondary, task.Result);
        });

    [Fact]
    public void ActionEvent_BubblesWithTheActualAncestorSender() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.PrimaryAction = new ContentDialogAction { Content = "Save" };
            int notifications = 0;
            scope.Host.AddHandler(
                Dialog.ButtonClickedEvent,
                new EventHandler<ContentDialogButtonClickEventArgs>((sender, args) =>
                {
                    Assert.Same(scope.Host, sender);
                    Assert.Same(dialog, args.Source);
                    notifications++;
                    args.Cancel = true;
                }));
            var task = dialog.ShowAsync();
            Drain();
            Invoke(Part(dialog, "PART_PrimaryButton"));
            Assert.Equal(1, notifications);
            Assert.False(task.IsCompleted);
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void NonClosingAction_AllowsExplicitCompletionAfterWork() =>
        Run(scope =>
        {
            int executions = 0;
            var dialog = scope.CreateDialog();
            dialog.PrimaryAction = new ContentDialogAction
            {
                Content = "Validate",
                ClosesDialog = false,
                Command = new TestCommand(() => executions++),
            };
            var task = dialog.ShowAsync();
            Drain();
            Invoke(Part(dialog, "PART_PrimaryButton"));
            Assert.Equal(1, executions);
            Assert.False(task.IsCompleted);
            dialog.Hide(ContentDialogResult.Primary);
            Pump(task);
        });

    [Fact]
    public void ActionBinding_UsesDialogDataContext_AndTemplatesRemainCustomizable() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.DataContext = new { Caption = "Bound action" };
            var action = new ContentDialogAction();
            BindingOperations.SetBinding(action, ContentDialogAction.ContentProperty, new Binding("Caption"));
            action.ContentTemplate = (DataTemplate)XamlReader.Parse(
                """<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBlock Text="{Binding}" FontStyle="Italic"/></DataTemplate>""");
            var style = new Style(typeof(Wpf.Zen.Ui.Controls.Button));
            style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, "Custom tooltip"));
            action.ButtonStyle = style;
            dialog.PrimaryAction = action;
            var task = dialog.ShowAsync();
            Drain();
            var button = Part(dialog, "PART_PrimaryButton");
            Assert.Equal("Bound action", button.Content);
            Assert.Same(action.ContentTemplate, button.ContentTemplate);
            Assert.Equal("Custom tooltip", button.ToolTip);
            dialog.DataContext = new { Caption = "Updated action" };
            Drain();
            Assert.Equal("Updated action", button.Content);
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void BusyHost_IsRejected_AndExternalRemovalCompletesExactlyOnce() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            int closed = 0;
            dialog.Closed += (_, _) => closed++;
            var first = dialog.ShowAsync();
            var second = scope.CreateDialog().ShowAsync();
            Assert.Throws<InvalidOperationException>(() => second.GetAwaiter().GetResult());
            Assert.Same(dialog, scope.Host.Content);
            // Removal is observable even before Loaded has reached the dialog.
            scope.Host.Content = null;
            Pump(first);
            Assert.Equal(ContentDialogResult.None, first.Result);
            Assert.Equal(1, closed);
        });

    [Fact]
    public void ExternalReplacement_DoesNotClearSuccessor() =>
        Run(scope =>
        {
            var first = scope.CreateDialog();
            var task = first.ShowAsync();
            var replacement = scope.CreateDialog();
            scope.Host.Content = replacement;
            Pump(task);
            Assert.Same(replacement, scope.Host.Content);
            scope.Host.Content = null;
        });

    [Fact]
    public void CancellationFromWorker_CompletesAndAllowsReuse() =>
        Run(scope =>
        {
            using var cancellation = new CancellationTokenSource();
            var dialog = scope.CreateDialog();
            var task = dialog.ShowAsync(cancellation.Token);
            Task.Run(cancellation.Cancel).GetAwaiter().GetResult();
            Pump(task);
            Assert.Equal(ContentDialogResult.None, task.Result);
            var next = dialog.ShowAsync();
            dialog.Hide(ContentDialogResult.Tertiary);
            Pump(next);
            Assert.Equal(ContentDialogResult.Tertiary, next.Result);
        });

    [Fact]
    public void DismissalPolicy_DoesNotPreventProgrammaticClose_AndAutomationIsTruthful() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.IsDismissible = false;
            dialog.Title = "Title";
            AutomationProperties.SetName(dialog, "Accessible name");
            var task = dialog.ShowAsync();
            Drain();
            Assert.False(Dialog.CloseCommand.CanExecute(null, dialog));
            Assert.Equal(Visibility.Collapsed, Part(dialog, "PART_CloseButton").Visibility);
            var peer = UIElementAutomationPeer.CreatePeerForElement(dialog);
            Assert.Equal("Accessible name", peer.GetName());
            Assert.Null(peer.GetPattern(PatternInterface.ScrollItem));
            Assert.IsAssignableFrom<IWindowProvider>(peer.GetPattern(PatternInterface.Window)).Close();
            Drain();
            Assert.False(task.IsCompleted);
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void StandardSizing_ConstrainsLongContent_AndEmptyTemplateIsSafe() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.MaxWidth = 340;
            dialog.MaxHeight = 260;
            dialog.Content = new Border { Width = 2000, Height = 2000 };
            var task = dialog.ShowAsync();
            Drain();
            Assert.InRange(dialog.ActualWidth, 1, 340);
            Assert.InRange(dialog.ActualHeight, 1, 260);
            var scroll = (System.Windows.Controls.ScrollViewer)dialog.Template.FindName("PART_ContentScroll", dialog);
            Assert.True(scroll.ScrollableHeight > 0);
            dialog.Template = new ControlTemplate(typeof(Dialog));
            scope.Window.UpdateLayout();
            dialog.Hide();
            Pump(task);
        });

    [Fact]
    public void ContentTemplate_InitialFocusTargetsRealizedInput() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.Content = new object();
            dialog.ContentTemplate = (DataTemplate)XamlReader.Parse(
                """<DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><TextBox Text="Input"/></DataTemplate>""");
            var task = dialog.ShowAsync();
            Drain();
            var presenter = (ContentPresenter)dialog.Template.FindName("PART_ContentPresenter", dialog);
            var input = (System.Windows.Controls.TextBox)VisualTreeHelper.GetChild(presenter, 0);
            Assert.True(input.IsKeyboardFocused);
            dialog.Hide();
            Pump(task);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExitTransition_HoldsModalState_AndDetachmentOrRetemplateReleasesWaiter(bool replaceTemplate) =>
        Run(scope =>
        {
            scope.Host.IsAnimationEnabled = true;
            scope.Host.TransitionDuration = TimeSpan.FromSeconds(5);
            var dialog = scope.CreateDialog();
            var task = dialog.ShowAsync();
            Drain();
            dialog.Hide(ContentDialogResult.Primary);
            Drain();
            if (SystemParameters.ClientAreaAnimation)
            {
                Assert.False(task.IsCompleted);
                Assert.Same(dialog, scope.Host.Content);
                Assert.False(scope.Background.IsEnabled);
            }

            if (replaceTemplate)
                scope.Host.Template = new ControlTemplate(typeof(ContentDialogHost));
            else
                scope.Window.Close();
            Pump(task);
            Assert.Null(scope.Host.Content);
            Assert.True(scope.Background.IsEnabled);
        });

    [Fact]
    public void AnimationCompletion_PreservesConsumerTransforms_AndOpenedIsOnce() =>
        Run(scope =>
        {
            scope.Host.IsAnimationEnabled = true;
            scope.Host.TransitionDuration = TimeSpan.FromMilliseconds(30);
            var dialog = scope.CreateDialog();
            var transform = new RotateTransform(2);
            dialog.RenderTransform = transform;
            dialog.Opacity = .8;
            int opened = 0;
            dialog.Opened += (_, _) =>
            {
                opened++;
                dialog.Hide(ContentDialogResult.Primary);
            };
            var task = dialog.ShowAsync();
            Pump(task);
            Assert.Equal(1, opened);
            Assert.Same(transform, dialog.RenderTransform);
            Assert.Equal(.8, dialog.Opacity);
        });

    private static NativeButton Part(Dialog dialog, string name) =>
        Assert.IsAssignableFrom<NativeButton>(dialog.Template.FindName(name, dialog));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LosingPresentation_WithoutChangingContent_CompletesTheSession(bool collapseHost) =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            var task = dialog.ShowAsync();
            Drain();
            if (collapseHost)
                scope.Host.Visibility = Visibility.Collapsed;
            else
                scope.Host.Template = new ControlTemplate(typeof(ContentDialogHost));
            Pump(task);
            Assert.Equal(ContentDialogResult.None, task.Result);
            Assert.Null(scope.Host.Content);
            Assert.True(scope.Background.IsEnabled);
        });

    [Fact]
    public void MissingHostPresenter_DoesNotLeaveAnInvisibleSessionPending() =>
        Run(scope =>
        {
            scope.Host.Template = new ControlTemplate(typeof(ContentDialogHost));
            var task = scope.CreateDialog().ShowAsync();
            Pump(task);
            Assert.Equal(ContentDialogResult.None, task.Result);
        });

    [Fact]
    public void Escape_CanBeConsumedByContent_BeforeDialogDismissal() =>
        Run(scope =>
        {
            var input = new System.Windows.Controls.TextBox();
            var dialog = scope.CreateDialog();
            dialog.Content = input;
            var task = dialog.ShowAsync();
            Drain();
            KeyEventHandler consume = (_, args) => args.Handled = true;
            input.KeyDown += consume;
            void Escape() => input.RaiseEvent(new KeyEventArgs(
                Keyboard.PrimaryDevice,
                PresentationSource.FromVisual(input),
                Environment.TickCount,
                Key.Escape)
            { RoutedEvent = Keyboard.KeyDownEvent });
            Escape();
            Assert.False(task.IsCompleted);
            input.KeyDown -= consume;
            Escape();
            Pump(task);
            Assert.Equal(ContentDialogResult.None, task.Result);
        });

    [Fact]
    public void DefaultButton_DoesNotFallBackToAnotherChoice() =>
        Run(scope =>
        {
            var dialog = scope.CreateDialog();
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.PrimaryAction = new ContentDialogAction { Content = "Unavailable", IsEnabled = false };
            dialog.TertiaryAction = new ContentDialogAction { Content = "Cancel" };
            var task = dialog.ShowAsync();
            Drain();
            Assert.False(Part(dialog, "PART_PrimaryButton").IsEnabled);
            Assert.False(Part(dialog, "PART_TertiaryButton").IsDefault);
            Assert.False(Part(dialog, "PART_CloseButton").IsDefault);
            dialog.Hide();
            Pump(task);
        });

    private static void Invoke(NativeButton button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        Assert.IsAssignableFrom<IInvokeProvider>(peer.GetPattern(PatternInterface.Invoke)).Invoke();
        Drain();
    }

    private static void Run(Action<Scope> action) => WpfTestThread.Run(() =>
    {
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        using var scope = new Scope();
        action(scope);
    });

    private static void Drain()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
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

    private sealed class Scope : IDisposable
    {
        public ContentDialogHost Host { get; } = new() { IsAnimationEnabled = false };
        public Border Background { get; } = new();
        public Window Window { get; }

        public Scope()
        {
            var root = new Grid();
            root.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml"),
            });
            root.Children.Add(Background);
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

        public Dialog CreateDialog() => new(Host) { Title = "Dialog", Content = "Content" };
        public void Dispose()
        {
            Window.Close();
            Drain();
        }
    }

    private sealed class TitleSelector(DataTemplate template) : DataTemplateSelector
    {
        public object? Item { get; private set; }
        public DependencyObject? Container { get; private set; }
        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            Item = item;
            Container = container;
            return template;
        }
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
