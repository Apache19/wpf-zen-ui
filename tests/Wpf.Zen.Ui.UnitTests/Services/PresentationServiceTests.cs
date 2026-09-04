// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Services;

public class PresentationServiceTests
{
    [Fact]
    public void Services_AreBoundToOneImmutableHost()
    {
        WpfTestThread.Run(() =>
        {
            var firstPresenter = new SnackbarPresenter();
            var secondPresenter = new SnackbarPresenter();
            var firstDialogHost = new ContentDialogHost();
            var secondDialogHost = new ContentDialogHost();
            var snackbarService = new SnackbarService();
            var dialogService = new ContentDialogService();

            snackbarService.Attach(firstPresenter);
            dialogService.Attach(firstDialogHost);

            // Repeating attachment for idempotent composition paths is safe, but silently
            // redirecting an already injected service to a later host is architecturally unsafe.
            snackbarService.Attach(firstPresenter);
            dialogService.Attach(firstDialogHost);

            Assert.Throws<InvalidOperationException>(() => snackbarService.Attach(secondPresenter));
            Assert.Throws<InvalidOperationException>(() => dialogService.Attach(secondDialogHost));
        });
    }

    [Fact]
    public void SnackbarPresenters_AndQueues_AreIsolatedPerWindow()
    {
        WpfTestThread.Run(() =>
        {
            var (firstWindow, _, firstPresenter, _) = CreateWindow();
            var (secondWindow, _, secondPresenter, _) = CreateWindow();

            try
            {
                firstWindow.Show();
                secondWindow.Show();
                DrainDispatcher();

                var firstService = new SnackbarService();
                var secondService = new SnackbarService();
                firstService.Attach(firstPresenter);
                secondService.Attach(secondPresenter);

                firstService.Show(
                    "First",
                    "Only the first window receives this notification.",
                    ControlAppearance.Normal,
                    null,
                    TimeSpan.FromMinutes(1)
                );

                Assert.Equal("First", firstPresenter.Content?.Title);
                Assert.Null(secondPresenter.Content);
            }
            finally
            {
                firstWindow.Close();
                secondWindow.Close();
            }
        });
    }

    [Fact]
    public void MultiplePresentationScopes_CanShareOneWindow()
    {
        WpfTestThread.Run(() =>
        {
            var firstPresenter = new SnackbarPresenter();
            var secondPresenter = new SnackbarPresenter();
            var firstRegion = CreateRegion(firstPresenter, new ContentDialogHost());
            var secondRegion = CreateRegion(secondPresenter, new ContentDialogHost());
            var root = new StackPanel();
            _ = root.Children.Add(firstRegion);
            _ = root.Children.Add(secondRegion);
            var window = CreateHiddenWindow(root);

            try
            {
                window.Show();
                DrainDispatcher();

                var firstService = new SnackbarService();
                var secondService = new SnackbarService();
                firstService.Attach(firstPresenter);
                secondService.Attach(secondPresenter);

                firstService.Show(
                    "Plug-in one",
                    "Only the explicitly attached region receives this notification.",
                    ControlAppearance.Normal,
                    null,
                    TimeSpan.FromMinutes(1)
                );

                Assert.Equal("Plug-in one", firstPresenter.Content?.Title);
                Assert.Null(secondPresenter.Content);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContentDialogService_UsesAttachedHostAndDisablesActualSiblings()
    {
        WpfTestThread.Run(() =>
        {
            // A running WPF application installs this context before UI event handlers execute.
            // The isolated STA helper intentionally does not run Dispatcher.Run, so install the
            // production-equivalent context for ContentDialog's asynchronous close continuation.
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            (Window window, UIElement applicationContent, _, ContentDialogHost dialogHost) =
                CreateWindow();

            try
            {
                window.Show();
                DrainDispatcher();

                var service = new ContentDialogService();
                service.Attach(dialogHost);
                // A dialog may be associated explicitly before it is passed to the service. The
                // service must accept the same host without trying to assign it a second time.
                var dialog = new ContentDialog(dialogHost);

                Task<ContentDialogResult> resultTask = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );

                Assert.Same(dialog, dialogHost.Content);
                Assert.False(applicationContent.IsEnabled);

                dialog.Hide(ContentDialogResult.Primary);
                PumpUntilCompleted(resultTask);

                Assert.True(resultTask.IsCompletedSuccessfully);
                // ReSharper disable once AsyncApostle.AsyncWait
                Assert.Equal(ContentDialogResult.Primary, resultTask.Result);
                Assert.Null(dialogHost.Content);
                Assert.True(applicationContent.IsEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContentDialog_CannotShowTheSameInstanceConcurrently()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            (Window window, _, _, ContentDialogHost dialogHost) = CreateWindow();

            try
            {
                window.Show();
                DrainDispatcher();

                var service = new ContentDialogService();
                service.Attach(dialogHost);
                var dialog = new ContentDialog();

                Task<ContentDialogResult> firstShow = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );
                Task<ContentDialogResult> secondShow = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );

                _ = Assert.Throws<InvalidOperationException>(
                    () => secondShow.GetAwaiter().GetResult()
                );

                dialog.Hide(ContentDialogResult.Primary);
                PumpUntilCompleted(firstShow);

                // ReSharper disable once AsyncApostle.AsyncWait
                Assert.Equal(ContentDialogResult.Primary, firstShow.Result);

                // Reuse remains supported after the complete close lifecycle has released the
                // instance; only overlapping presentations are rejected.
                Task<ContentDialogResult> thirdShow = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );
                dialog.Hide(ContentDialogResult.Secondary);
                PumpUntilCompleted(thirdShow);

                // ReSharper disable once AsyncApostle.AsyncWait
                Assert.Equal(ContentDialogResult.Secondary, thirdShow.Result);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContentDialog_CompletesWhenItsHostIsUnloaded()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            (Window window, _, _, ContentDialogHost dialogHost) = CreateWindow();
            window.Show();
            DrainDispatcher();

            var service = new ContentDialogService();
            service.Attach(dialogHost);
            Task<ContentDialogResult> resultTask = service.ShowAsync(
                new ContentDialog(),
                CancellationToken.None
            );

            // WPF unloads the visual tree when a Window closes, but it deliberately retains
            // ContentControl.Content. The dialog operation must therefore observe the host's
            // lifetime instead of waiting for Content to be cleared by application code.
            window.Close();
            PumpUntilCompleted(resultTask);

            // ReSharper disable once AsyncApostle.AsyncWait
            Assert.Equal(ContentDialogResult.None, resultTask.Result);
            Assert.Null(dialogHost.Content);
        });
    }

    [Fact]
    public void ContentDialog_ConstrainsSizeWithoutChangingConfiguredMaximums()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            (Window window, _, _, ContentDialogHost dialogHost) = CreateWindow();

            try
            {
                window.Show();
                DrainDispatcher();

                var service = new ContentDialogService();
                service.Attach(dialogHost);
                var dialog = new ContentDialog
                {
                    Content = new Border { Width = 2_000D, Height = 2_000D },
                    MaxWidth = 100D,
                    MaxHeight = 80D,
                };

                Task<ContentDialogResult> resultTask = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );
                DrainDispatcher();

                Assert.Equal(100D, dialog.MaxWidth);
                Assert.Equal(80D, dialog.MaxHeight);
                Assert.InRange(dialog.ActualWidth, 0D, dialog.MaxWidth);
                Assert.InRange(dialog.ActualHeight, 0D, dialog.MaxHeight);

                dialog.Hide();
                PumpUntilCompleted(resultTask);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ContentDialogHost_PreservesDisabledTitleBarContent()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            var titleBar = new TitleBar { IsUserContentEnabled = false };
            var dialogHost = new ContentDialogHost { IsDisableSiblingsEnabled = true };
            var root = new Grid();
            _ = root.Children.Add(titleBar);
            _ = root.Children.Add(dialogHost);
            var window = CreateHiddenWindow(root);

            try
            {
                window.Show();
                DrainDispatcher();

                var service = new ContentDialogService();
                service.Attach(dialogHost);
                var dialog = new ContentDialog();
                Task<ContentDialogResult> resultTask = service.ShowAsync(
                    dialog,
                    CancellationToken.None
                );

                Assert.False(titleBar.IsUserContentEnabled);

                dialog.Hide();
                PumpUntilCompleted(resultTask);

                Assert.False(titleBar.IsUserContentEnabled);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void PresentationServices_WorkInHwndSourceWithoutWpfWindow()
    {
        WpfTestThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );

            var applicationContent = new Border();
            var snackbarPresenter = new SnackbarPresenter();
            var dialogHost = new ContentDialogHost { IsDisableSiblingsEnabled = true };
            var root = new Grid();
            _ = root.Children.Add(applicationContent);
            _ = root.Children.Add(snackbarPresenter);
            _ = root.Children.Add(dialogHost);

            var parameters = new HwndSourceParameters("PresentationServiceTests")
            {
                Width = 240,
                Height = 160,
                PositionX = -10_000,
                PositionY = -10_000,
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP
            };

            using var source = new HwndSource(parameters) { RootVisual = root };
            DrainDispatcher();

            Assert.Null(Window.GetWindow(root));
            Assert.True(dialogHost.IsLoaded);
            Assert.True(snackbarPresenter.IsLoaded);

            var snackbarService = new SnackbarService();
            snackbarService.Attach(snackbarPresenter);
            snackbarService.Show(
                "Hosted WPF",
                "No WPF Window is required.",
                ControlAppearance.Normal,
                null,
                TimeSpan.FromMinutes(1)
            );
            Assert.Equal("Hosted WPF", snackbarPresenter.Content?.Title);

            var dialogService = new ContentDialogService();
            dialogService.Attach(dialogHost);
            var dialog = new ContentDialog();
            Task<ContentDialogResult> resultTask = dialogService.ShowAsync(
                dialog,
                CancellationToken.None
            );

            Assert.Same(dialog, dialogHost.Content);
            Assert.False(applicationContent.IsEnabled);

            dialog.Hide(ContentDialogResult.Secondary);
            PumpUntilCompleted(resultTask);

            // ReSharper disable once AsyncApostle.AsyncWait
            Assert.Equal(ContentDialogResult.Secondary, resultTask.Result);
            Assert.True(applicationContent.IsEnabled);
        });
    }

    private static (
        Window Window,
        UIElement ApplicationContent,
        SnackbarPresenter SnackbarPresenter,
        ContentDialogHost DialogHost
    ) CreateWindow()
    {
        var applicationContent = new Border();
        var snackbarPresenter = new SnackbarPresenter();
        var dialogHost = new ContentDialogHost { IsDisableSiblingsEnabled = true };
        var root = new Grid();
        _ = root.Children.Add(applicationContent);
        _ = root.Children.Add(snackbarPresenter);
        _ = root.Children.Add(dialogHost);

        Window window = CreateHiddenWindow(root);

        return (window, applicationContent, snackbarPresenter, dialogHost);
    }

    private static Grid CreateRegion(
        SnackbarPresenter snackbarPresenter,
        ContentDialogHost dialogHost
    )
    {
        var root = new Grid();
        _ = root.Children.Add(new Border());
        _ = root.Children.Add(snackbarPresenter);
        _ = root.Children.Add(dialogHost);
        return root;
    }

    private static Window CreateHiddenWindow(object content)
    {
        return new Window
        {
            Content = content,
            Width = 240D,
            Height = 160D,
            Left = -10_000D,
            Top = -10_000D,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
        };
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        _ = Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        Dispatcher.PushFrame(frame);
    }

    private static void PumpUntilCompleted(Task task)
    {
        Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        _ = task.ContinueWith(
            _ =>
                dispatcher.BeginInvoke(
                    DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false)
                ),
            TaskScheduler.Default
        );
        Dispatcher.PushFrame(frame);
        task.GetAwaiter().GetResult();
    }
}
