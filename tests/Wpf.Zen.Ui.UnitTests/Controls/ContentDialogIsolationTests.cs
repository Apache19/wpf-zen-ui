// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Wpf.Zen.Ui.Controls;
using Dialog = Wpf.Zen.Ui.Controls.ContentDialog;
using NativeButton = System.Windows.Controls.Button;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    [Fact]
    public void Isolation_OverlappingOwnersDoNotReleaseEachOthersConstraint() => WpfTestThread.Run(() =>
    {
        var element = new Border();
        var first = new ContentDialogInputIsolation(element, UIElement.IsEnabledProperty);
        var second = new ContentDialogInputIsolation(element, UIElement.IsEnabledProperty);
        try
        {
            first.Activate();
            second.Activate();
            first.Release();
            Assert.False(element.IsEnabled);
            element.IsEnabled = false;
            second.Release();
            Assert.False(element.IsEnabled);
            element.IsEnabled = true;
            Assert.True(element.IsEnabled);
        }
        finally
        {
            first.Release();
            second.Release();
        }
    });

    [Fact]
    public void Isolation_PreservesDerivedControlCoercion() => WpfTestThread.Run(() =>
    {
        var element = new CoercingIsolationBorder();
        var original = UIElement.IsEnabledProperty.GetMetadata(element).CoerceValueCallback;
        var isolation = new ContentDialogInputIsolation(element, UIElement.IsEnabledProperty);
        try
        {
            isolation.Activate();
            Assert.False(element.IsEnabled);
            element.AllowInput = false;
            isolation.Release();
            Assert.False(element.IsEnabled);
            element.AllowInput = true;
            element.CoerceValue(UIElement.IsEnabledProperty);
            Assert.True(element.IsEnabled);
            Assert.Same(original, UIElement.IsEnabledProperty.GetMetadata(element).CoerceValueCallback);
        }
        finally
        {
            isolation.Release();
        }
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Isolation_KeepsBindingUpdatesAndRestoresLatestValue(bool finalValue) => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        var model = new EnableModel();
        body.SetBinding(UIElement.IsEnabledProperty, new Binding(nameof(EnableModel.Enabled)) { Source = model });
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(body.IsEnabled);

        model.Enabled = false;
        Drain();
        model.Enabled = finalValue;
        Drain();
        Assert.False(body.IsEnabled);
        Assert.True(BindingOperations.IsDataBound(body, UIElement.IsEnabledProperty));

        dialog.Close();
        Pump(task);
        Assert.Equal(finalValue, body.IsEnabled);
        Assert.True(BindingOperations.IsDataBound(body, UIElement.IsEnabledProperty));
    });

    [Fact]
    public void Isolation_PreservesApplicationAnimationAndLocalValue() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        body.IsEnabled = false;
        var animation = new BooleanAnimationUsingKeyFrames
        {
            Duration = Duration.Forever,
            KeyFrames = { new DiscreteBooleanKeyFrame(true, KeyTime.FromTimeSpan(TimeSpan.Zero)) },
        };
        body.BeginAnimation(UIElement.IsEnabledProperty, animation);
        Drain();
        Assert.True(body.IsEnabled);

        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(body.IsEnabled);
        Assert.True(body.HasAnimatedProperties);
        dialog.Close();
        Pump(task);
        Assert.True(body.IsEnabled);
        Assert.True(body.HasAnimatedProperties);
        body.BeginAnimation(UIElement.IsEnabledProperty, null);
        Assert.False(body.IsEnabled);
    });

    [Fact]
    public void Isolation_ClearValueCannotBypassConstraint() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        body.IsEnabled = false;
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        body.ClearValue(UIElement.IsEnabledProperty);
        Assert.False(body.IsEnabled);
        Assert.Equal(DependencyProperty.UnsetValue, body.ReadLocalValue(UIElement.IsEnabledProperty));
        dialog.Close();
        Pump(task);
        Assert.True(body.IsEnabled);
    });

    [Fact]
    public void Isolation_DynamicResourceUpdatesRemainEffectiveAfterClosure() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        body.Resources["BusinessEnabled"] = true;
        body.SetResourceReference(UIElement.IsEnabledProperty, "BusinessEnabled");
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        body.Resources["BusinessEnabled"] = false;
        Drain();
        body.Resources["BusinessEnabled"] = true;
        Drain();
        Assert.False(body.IsEnabled);
        body.Resources["BusinessEnabled"] = false;
        dialog.Close();
        Pump(task);
        Assert.False(body.IsEnabled);
        body.Resources["BusinessEnabled"] = true;
        Assert.True(body.IsEnabled);
    });

    [Fact]
    public void Isolation_NewBranchCallbackFailureFaultsTaskAndReleasesExistingBranches() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        var panel = (StackPanel)scope.Host.Content;
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var added = new Border();
        var expected = new InvalidOperationException("Application isolation callback failed.");
        added.IsEnabledChanged += (_, e) =>
        {
            if (!(bool)e.NewValue)
            {
                throw expected;
            }
        };
        panel.Children.Add(added);
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Pump(task)));
        Assert.True(body.IsEnabled);
        Assert.True(added.IsEnabled);
        Assert.Null(scope.Host.CurrentDialog);
        Assert.Null(dialog.Parent);
    });

    [Fact]
    public void Isolation_TracksAddedRemovedAndReplacedContent() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        var panel = (StackPanel)scope.Host.Content;
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var added = new NativeButton();
        panel.Children.Add(added);
        Drain();
        Assert.False(added.IsEnabled);
        panel.Children.Remove(added);
        Drain();
        Assert.True(added.IsEnabled);

        var replacement = new Border();
        scope.Host.Content = replacement;
        Drain();
        Assert.False(replacement.IsEnabled);
        Assert.True(body.IsEnabled);
        Assert.True(((Wpf.Zen.Ui.Controls.TitleBar)panel.Children[0]).IsUserContentEnabled);

        dialog.Close();
        Pump(task);
        Assert.True(replacement.IsEnabled);
    });

    [Fact]
    public void Isolation_UpdatesWhenTitleBarPathChanges() => Run(scope =>
    {
        var panel = new StackPanel();
        var body = new Border();
        panel.Children.Add(body);
        scope.Host.Content = panel;
        Drain();
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var titleBar = new Wpf.Zen.Ui.Controls.TitleBar();
        panel.Children.Add(titleBar);
        Drain();
        Assert.True(titleBar.IsEnabled);
        Assert.False(titleBar.IsUserContentEnabled);
        Assert.False(body.IsEnabled);
        panel.Children.Remove(titleBar);
        Drain();
        Assert.True(titleBar.IsUserContentEnabled);
        Assert.False(body.IsEnabled);
        dialog.Close();
        Pump(task);
        Assert.True(body.IsEnabled);
    });

    [Fact]
    public void Isolation_ReentrantLayoutRefreshesTheFinalTree() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        var panel = (StackPanel)scope.Host.Content;
        var added = new Border();
        body.IsEnabledChanged += (_, e) =>
        {
            if (!(bool)e.NewValue && !panel.Children.Contains(added))
            {
                panel.Children.Add(added);
                // Nested layout raises LayoutUpdated while the first branch snapshot is being
                // applied. The newly introduced branch must receive a subsequent reconciliation.
                panel.UpdateLayout();
            }
        };
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(added.IsEnabled);
        dialog.Close();
        Pump(task);
        Assert.True(added.IsEnabled);
    });

    [Fact]
    public void Isolation_ReentrantHideDoesNotReattachOrLeaveInputSubscriptions() => Run(scope =>
    {
        var body = scope.Background;
        DependencyPropertyChangedEventHandler hide = (_, e) =>
        {
            if (!(bool)e.NewValue)
            {
                scope.Host.Visibility = Visibility.Hidden;
            }
        };
        body.IsEnabledChanged += hide;
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Pump(task);
        body.IsEnabledChanged -= hide;
        Assert.False(dialog.IsOpen);
        Assert.Null(dialog.Parent);
        Assert.True(body.IsEnabled);

        scope.Host.Visibility = Visibility.Visible;
        Drain();
        var next = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(body.IsEnabled);
        dialog.Close();
        Pump(next);
        Assert.True(body.IsEnabled);
    });

    [Fact]
    public void Isolation_RestoreExceptionStillReleasesEveryBranchAndAllowsReuse() => Run(scope =>
    {
        var body = AddTitleBarShell(scope);
        var second = new Border();
        ((StackPanel)scope.Host.Content).Children.Add(second);
        Drain();
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var expected = new InvalidOperationException("Application enabled callback failed.");
        DependencyPropertyChangedEventHandler fail = (_, e) =>
        {
            if ((bool)e.NewValue)
            {
                throw expected;
            }
        };
        body.IsEnabledChanged += fail;
        dialog.Close();
        Assert.Same(expected, Assert.Throws<InvalidOperationException>(() => Pump(task)));
        body.IsEnabledChanged -= fail;
        Assert.False(dialog.IsOpen);
        Assert.Null(dialog.Parent);
        Assert.True(second.IsEnabled);

        var next = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.False(body.IsEnabled);
        Assert.False(second.IsEnabled);
        dialog.Close();
        Pump(next);
        Assert.True(body.IsEnabled);
        Assert.True(second.IsEnabled);
    });

    [Theory]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void DialogVisibility_HidingEndsSessionAndAllowsReuse(Visibility visibility) => Run(async scope =>
    {
        var dialog = scope.CreateDialog();
        int closed = 0;
        dialog.Closed += (_, _) => closed++;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Visibility = visibility;
        Pump(task);
        Assert.Null(await task);
        Assert.Equal(1, closed);
        Assert.True(scope.Background.IsEnabled);
        Assert.Null(dialog.Parent);

        dialog.Visibility = Visibility.Visible;
        var next = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Close("accepted");
        dialog.Visibility = visibility;
        Pump(next);
        Assert.Equal("accepted", await next);
        Assert.Equal(2, closed);
    });

    [Fact]
    public void DialogVisibility_TransientHideDoesNotEndSession() => Run(scope =>
    {
        var dialog = scope.CreateDialog();
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        dialog.Visibility = Visibility.Hidden;
        dialog.Visibility = Visibility.Visible;
        Drain();
        Assert.False(task.IsCompleted);
        dialog.Close();
        Pump(task);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PresenterTemplateReplacement_CompletesRequestedTransition(bool closing) => Run(async scope =>
    {
        scope.Host.IsAnimationEnabled = true;
        scope.Host.OpenDuration = closing ? TimeSpan.Zero : TimeSpan.FromSeconds(30);
        scope.Host.CloseDuration = TimeSpan.FromSeconds(30);
        var dialog = scope.CreateDialog();
        int opened = 0;
        dialog.Opened += (_, _) => opened++;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        var presenter = (ContentDialogTransitionPresenter)scope.Host.Template.FindName("PART_DialogPresenter", scope.Host);
        if (closing)
        {
            dialog.Close("accepted");
        }

        presenter.Template = (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                xmlns:ui="clr-namespace:Wpf.Zen.Ui.Controls;assembly=Wpf.Zen.Ui"
                TargetType="{x:Type ui:ContentDialogTransitionPresenter}">
                <Grid>
                    <Border x:Name="PART_Backdrop" />
                    <Border x:Name="PART_DialogRoot">
                        <ContentPresenter Content="{TemplateBinding Dialog}" />
                    </Border>
                </Grid>
            </ControlTemplate>
            """);
        presenter.ApplyTemplate();
        Drain();
        Assert.Equal(1, opened);
        Assert.False(presenter.HasAnimatedProperties);
        if (closing)
        {
            Pump(task);
            Assert.Equal("accepted", await task);
        }
        else
        {
            var surface = (Border)presenter.Template.FindName("PART_DialogRoot", presenter);
            Assert.Equal(1.0, surface.Opacity);
            Assert.False(task.IsCompleted);
            scope.Host.IsAnimationEnabled = false;
            dialog.Close();
            Pump(task);
        }
    });

    private static Border AddTitleBarShell(Scope scope)
    {
        var panel = new StackPanel();
        panel.Children.Add(new Wpf.Zen.Ui.Controls.TitleBar());
        var body = new Border();
        panel.Children.Add(body);
        scope.Host.Content = panel;
        Drain();
        return body;
    }

    private sealed class CoercingIsolationBorder : Border
    {
        internal bool AllowInput { get; set; } = true;

        static CoercingIsolationBorder()
        {
            var frameworkCoerce = IsEnabledProperty.GetMetadata(typeof(Border)).CoerceValueCallback;
            IsEnabledProperty.OverrideMetadata(typeof(CoercingIsolationBorder), new UIPropertyMetadata(
                true,
                null,
                (d, value) => (bool)frameworkCoerce(d, value) && ((CoercingIsolationBorder)d).AllowInput));
        }
    }

    private static T FindDialogTemplateElement<T>(Dialog dialog, Func<T, bool> predicate)
        where T : FrameworkElement
    {
        // ContentSource and AutomationId are the relevant contracts. Unnamed template elements
        // do not need artificial x:Name values just to make test discovery convenient.
        return Assert.Single(Descendants(dialog).OfType<T>(), element =>
            ReferenceEquals(element.TemplatedParent, dialog) && predicate(element));

        static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                var child = VisualTreeHelper.GetChild(root, index);
                yield return child;
                foreach (var descendant in Descendants(child))
                {
                    yield return descendant;
                }
            }
        }
    }
}
