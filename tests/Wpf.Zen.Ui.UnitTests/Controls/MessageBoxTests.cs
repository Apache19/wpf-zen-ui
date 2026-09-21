// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

extern alias UiNative;

using System.ComponentModel;
using System.Linq;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using UiMessageBox = Wpf.Zen.Ui.Controls.MessageBox;
using UiButton = Wpf.Zen.Ui.Controls.Button;
using Native = UiNative::Windows.Win32.PInvoke;
using HWND = UiNative::Windows.Win32.Foundation.HWND;

namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(ThemeManagerTestCollection))]
public class MessageBoxTests
{
    public MessageBoxTests()
    {
        WpfApplicationTestHost.EnsureApplication();
    }

    [Theory]
    [InlineData(MessageBoxButton.OK, MessageBoxResult.OK)]
    [InlineData(MessageBoxButton.OKCancel, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.YesNoCancel, MessageBoxResult.Cancel)]
    public void CaptionClose_ReturnsNativeDismissalResult(MessageBoxButton buttons, MessageBoxResult expected)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var window = Create(buttons);
            window.Loaded += (_, _) => window.Close();
            window.ShowDialog();
            Assert.Equal(expected, window.Result);
        });
    }

    [Theory]
    [InlineData(MessageBoxButton.OK, MessageBoxResult.OK)]
    [InlineData(MessageBoxButton.OKCancel, MessageBoxResult.Cancel)]
    [InlineData(MessageBoxButton.YesNo, MessageBoxResult.No)]
    [InlineData(MessageBoxButton.YesNoCancel, MessageBoxResult.Yes)]
    public void Show_IsSynchronousModalAndRestoresOwner(MessageBoxButton buttons, MessageBoxResult choice)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var owner = new Window { Width = 200, Height = 100, ShowInTaskbar = false };
            owner.Show();
            var ownerHandle = new HWND(new WindowInteropHelper(owner).Handle);
            bool returned = false;
            Exception? failure = null;
            owner.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var window = HwndSource.CurrentSources.OfType<HwndSource>()
                    .Select(source => source.RootVisual).OfType<MessageBoxWindow>().Single();
                try
                {
                    Assert.False(returned);
                    Assert.False((bool)Native.IsWindowEnabled(ownerHandle));
                    Assert.Same(owner, window.Owner);
                    var actions = ((ContentDialogActionBar)window.FindName("ButtonsPanel")).Items.OfType<UiButton>().ToArray();
                    Assert.Equal(choice, actions.Single(action => action.IsDefault).Tag);
                    actions.Single(action => Equals(action.Tag, choice))
                        .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                }
                catch (Exception exception)
                {
                    failure = exception;
                    window.Complete(choice);
                }
            }));
            try
            {
                MessageBoxResult result = UiMessageBox.Show(owner, "Message", "Caption", buttons,
                    MessageBoxImage.Information, choice);
                returned = true;
                Assert.Null(failure);
                Assert.Equal(choice, result);
                Assert.True((bool)Native.IsWindowEnabled(ownerHandle));
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public void YesNo_RejectsCloseAndEscape()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var window = Create(MessageBoxButton.YesNo);
            bool remainedOpen = false;
            window.Loaded += (_, _) =>
            {
                window.Close();
                var key = new KeyEventArgs(Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(window), 0, Key.Escape)
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                window.RaiseEvent(key);
                remainedOpen = window.IsVisible && window.Result == MessageBoxResult.None && key.Handled;
                window.Complete(MessageBoxResult.No);
            };
            window.ShowDialog();
            Assert.True(remainedOpen);
            Assert.Equal(MessageBoxResult.No, window.Result);
        });
    }

    [Theory]
    [InlineData("zh-CN", "确定(_O)")]
    [InlineData("zh-TW", "確定(_O)")]
    [InlineData("en-US", "_OK")]
    [InlineData("es-ES", "_OK")]
    public void Labels_UseCultureFallbackAndIndependentOverrides(string culture, string expected)
    {
        var labels = new MessageBoxButtonText { Yes = "_Save", OK = "" };
        Assert.Equal(expected, MessageBoxWindow.GetLabel(MessageBoxResult.OK, labels, new CultureInfo(culture)));
        Assert.Equal("_Save", MessageBoxWindow.GetLabel(MessageBoxResult.Yes, labels, new CultureInfo(culture)));
    }

    [Fact]
    public void UnavailableDefault_SelectsFirstButtonLikeWpf()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var window = Create(MessageBoxButton.YesNo, MessageBoxResult.Cancel);
            var actions = ((ContentDialogActionBar)window.FindName("ButtonsPanel")).Items.OfType<UiButton>();
            Assert.Equal(MessageBoxResult.Yes, actions.Single(action => action.IsDefault).Tag);
            window.Complete(MessageBoxResult.Yes);
        });
    }

    [Fact]
    public void InvalidArguments_AreRejectedBeforeOpeningWindow()
    {
        Assert.Throws<InvalidEnumArgumentException>(() => UiMessageBox.Show("text", "title", (MessageBoxButton)2));
        Assert.Throws<InvalidEnumArgumentException>(() => UiMessageBox.Show("text", "title", MessageBoxButton.OK,
            (MessageBoxImage)1));
        Assert.Throws<InvalidEnumArgumentException>(() => UiMessageBox.Show("text", "title", MessageBoxButton.OK,
            MessageBoxImage.None, (MessageBoxResult)3));
        Assert.Throws<InvalidEnumArgumentException>(() => UiMessageBox.Show("text", "title", MessageBoxButton.OK,
            MessageBoxImage.None, MessageBoxResult.None, (MessageBoxOptions)1));
        Assert.Throws<ArgumentNullException>(() => UiMessageBox.Show((Window)null!, "text"));
        Assert.Throws<NotSupportedException>(() => UiMessageBox.Show("text", "title", MessageBoxButton.OK,
            MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.ServiceNotification,
            new MessageBoxButtonText()));
    }

    [Fact]
    public void RightToLeftAndAlignment_AreAppliedIndependently()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var window = new MessageBoxWindow("text", "caption", MessageBoxButton.OK, MessageBoxImage.None,
                MessageBoxResult.None, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign, null);
            Assert.Equal(FlowDirection.RightToLeft, window.FlowDirection);
            Assert.Equal(TextAlignment.Right, ((System.Windows.Controls.TextBlock)window.FindName("MessageText")).TextAlignment);
            window.Close();
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void FluentLayout_KeepsActionsVisibleAndFocusesDefault(bool dark, bool longMessage)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var window = new MessageBoxWindow(
                longMessage ? string.Join("\n", Enumerable.Repeat("这是一段较长的消息，用于检查滚动内容和按钮布局。", 80))
                    : "是否保存对文档的修改？\n未保存的修改将会丢失。",
                "未保存的修改", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning,
                MessageBoxResult.Cancel, MessageBoxOptions.None,
                new MessageBoxButtonText { Yes = "保存(_S)", No = "不保存(_N)", Cancel = "取消" });
            ThemeManager.Initialize();
            Theme.SetRequested(window, dark ? Wpf.Zen.Ui.Appearance.ThemePreference.Dark
                : Wpf.Zen.Ui.Appearance.ThemePreference.Light);
            Exception? failure = null;
            window.ContentRendered += (_, _) =>
            {
                try
                {
                    var panel = (ContentDialogActionBar)window.FindName("ButtonsPanel");
                    var actions = panel.Items.OfType<UiButton>().ToArray();
                    Assert.True(actions.Single(action => action.IsDefault).IsKeyboardFocused);
                    Assert.All(actions, action => Assert.True(action.ActualWidth >= 96));
                    double expectedWidth = (panel.ActualWidth
                        - panel.ColumnSpacing * (actions.Length - 1)) / actions.Length;
                    Assert.All(actions, action => Assert.Equal(expectedWidth, action.ActualWidth, 1));
                    Assert.True(panel.TransformToAncestor(window).Transform(new Point()).Y + panel.ActualHeight
                        <= window.ActualHeight);
                    if (longMessage)
                    {
                        var scroller = (System.Windows.Controls.ScrollViewer)window.FindName("MessageScroller");
                        Assert.True(scroller.ExtentHeight > scroller.ViewportHeight);
                    }

                    // Optional render artifacts support manual layout review without screen capture.
                    string? directory = Environment.GetEnvironmentVariable("WPFUI_MESSAGEBOX_RENDER_DIR");
                    if (!string.IsNullOrEmpty(directory))
                    {
                        System.IO.Directory.CreateDirectory(directory);
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                            (int)window.ActualWidth, (int)window.ActualHeight, 96, 96,
                            System.Windows.Media.PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                        using var stream = System.IO.File.Create(System.IO.Path.Combine(directory,
                            $"messagebox-{(dark ? "dark" : "light")}-{longMessage}.png"));
                        encoder.Save(stream);
                    }
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
                finally
                {
                    window.Complete(MessageBoxResult.Cancel);
                }
            };
            window.ShowDialog();
            Assert.Null(failure);
        });
    }
    [Fact]
    public void OwnerlessShow_AcceptsOnlyTextAndCustomLabel()
    {
        WpfApplicationTestHost.Run(() =>
        {
            string? actualLabel = null;
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var window = HwndSource.CurrentSources.OfType<HwndSource>()
                    .Select(source => source.RootVisual).OfType<MessageBoxWindow>().Single();
                var action = ((ContentDialogActionBar)window.FindName("ButtonsPanel")).Items.OfType<UiButton>().Single();
                actualLabel = ((AccessText)action.Content).Text;
                window.Close();
            }));
            Assert.Equal(MessageBoxResult.OK,
                UiMessageBox.Show("Complete", buttonText: new MessageBoxButtonText { OK = "Got it" }));
            Assert.Equal("Got it", actualLabel);
        });
    }
    private static MessageBoxWindow Create(MessageBoxButton buttons, MessageBoxResult defaultResult = MessageBoxResult.None)
    {
        return new MessageBoxWindow("Message", "Caption", buttons, MessageBoxImage.None,
            defaultResult, MessageBoxOptions.None, null);
    }
}
