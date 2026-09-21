// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Threading;
using NSubstitute;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Layouts;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies deferred service acquisition and separates active state from catalog discovery.
/// </summary>
public class SoftKeyboardLoadingTests
{
    /// <summary>
    /// Construction, configuration, focus and collapsed loading must not acquire input services.
    /// Opening observes active state; only explicit requests enumerate the catalog.
    /// </summary>
    [Fact]
    public Task CollapsedLoadDefersServicesAndShowingReadsOnlyActiveProfile()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            var clock = new ManualInputObservationScheduler();
            var backend = new IdleBackend();
            var candidates = Substitute.For<ISoftKeyboardCandidateSource>();
            var candidateSession = Substitute.For<ISoftKeyboardCandidateSession>();
            candidates.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(candidateSession);
            var profile = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409,
                "Active", true);
            var session = Substitute.For<ISoftKeyboardActiveInputMethodSession>();
            session.GetActiveInputMethod().Returns(profile);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([profile], profile));
            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            source.CreateSession().Returns(session);
            var keyboard = new SoftKeyboard(new SoftKeyboardPlatformServices(
                backend, backend, backend, candidates, clock))
            {
                InputMethodSource = source,
                ExtensionProvider = EmptySoftKeyboardExtensionProvider.Default,
                Visibility = Visibility.Collapsed,
            };
            var layouts = Substitute.For<ISoftKeyboardLayoutProvider>();
            layouts.CreateLayout(Arg.Any<SoftKeyboardLayoutContext>()).Returns(SoftKeyboardLayout.Empty);
            keyboard.LayoutProvider = layouts;
            var editor = new TextBox();
            keyboard.Target = editor;
            var panel = new StackPanel();
            panel.Children.Add(editor);
            panel.Children.Add(keyboard);
            var window = new Window { Content = panel, Width = 600, Height = 400, ShowInTaskbar = false };
            try
            {
                window.Show();
                editor.Focus();
                await Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                clock.Advance(500);
                Assert.True(keyboard.IsLoaded);
                Assert.Equal(Visibility.Collapsed, keyboard.Visibility);
                source.DidNotReceive().CreateSession();
                candidates.DidNotReceive().CreateSession(Arg.Any<SoftKeyboardCandidateHost>());
                Assert.Equal(0, backend.Reads);
                layouts.DidNotReceive().CreateLayout(Arg.Any<SoftKeyboardLayoutContext>());
                Assert.Same(SoftKeyboardLayout.Empty, keyboard.CurrentLayout);
                Assert.Empty(keyboard.AvailableSupplementalLayouts);

                keyboard.Visibility = Visibility.Visible;
                clock.Advance(100);
                Assert.Same(profile, keyboard.ActiveInputMethod);
                session.Received().GetActiveInputMethod();
                session.DidNotReceive().GetSnapshot();
                Assert.Empty(keyboard.InputMethods);

                Assert.True(keyboard.RefreshInputMethods());
                session.Received(1).GetSnapshot();
                Assert.Same(profile, Assert.Single(keyboard.InputMethods));
                keyboard.OnSessionInputMethodStateInvalidated();
                clock.Advance(100);
                session.Received(1).GetSnapshot();

                keyboard.Visibility = Visibility.Collapsed;
                session.ClearReceivedCalls();
                keyboard.OnSessionFocusChanged(editor);
                keyboard.OnSessionInputMethodStateInvalidated();
                clock.Advance(500);
                session.DidNotReceive().GetActiveInputMethod();
                session.DidNotReceive().GetSnapshot();

                // Explicit catalog requests remain usable while closed and finish their window.
                keyboard.RefreshInputMethods();
                clock.Advance(100);
                session.Received(1).GetSnapshot();
                Assert.Same(profile, keyboard.ActiveInputMethod);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Explicit discovery before loading completes without implicitly creating a candidate session.
    /// </summary>
    [Fact]
    public void ExplicitCatalogRefreshWorksBeforeLoaded()
    {
        WpfTestThread.Run(() =>
        {
            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<ISoftKeyboardInputMethodSession>();
            var profile = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409,
                "Explicit", true);
            source.CreateSession().Returns(session);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([profile], profile));
            var keyboard = new SoftKeyboard { InputMethodSource = source };
            source.DidNotReceive().CreateSession();
            keyboard.RefreshInputMethods();
            session.Received(1).GetSnapshot();
            Assert.Same(profile, Assert.Single(keyboard.InputMethods));
            Assert.False(keyboard.IsLoaded);
            keyboard.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
        });
    }
    /// <summary>
    /// Invisible changes coalesce; explicit refresh and visibility publish the latest context.
    /// </summary>
    [Fact]
    public void LayoutsAreDeferredUntilVisibleOrExplicitRefresh()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var contexts = new List<SoftKeyboardLayoutContext>();
            var provider = Substitute.For<ISoftKeyboardLayoutProvider>();
            provider.CreateLayout(Arg.Any<SoftKeyboardLayoutContext>()).Returns(call =>
            {
                var context = call.Arg<SoftKeyboardLayoutContext>();
                contexts.Add(context);
                return new SoftKeyboardLayout([SoftKeyboardKey.FromText(context.Page)]);
            });
            var extensions = Substitute.For<ISoftKeyboardExtensionProvider>();
            extensions.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>())
                .Returns(Array.Empty<SoftKeyboardExtension>());
            var keyboard = new SoftKeyboard { LayoutProvider = provider, ExtensionProvider = extensions };
            keyboard.ShowPage("first");
            keyboard.ShowPage("latest");
            Assert.Empty(contexts);
            extensions.DidNotReceive().GetExtensions(Arg.Any<SoftKeyboardExtensionContext>());
            Assert.Same(SoftKeyboardLayout.Empty, keyboard.CurrentLayout);
            Assert.Equal(1, keyboard.CurrentLayout.ColumnCount);
            Assert.Empty(keyboard.AvailableSupplementalLayouts);

            var window = new Window { Content = keyboard, ShowInTaskbar = false };
            try
            {
                window.Show();
                Assert.Equal("latest", Assert.Single(contexts).Page);
                Assert.Equal("latest", Assert.Single(keyboard.CurrentLayout.Keys).Text);
                var previous = keyboard.CurrentLayout;
                keyboard.Visibility = Visibility.Collapsed;
                keyboard.ShowPage("closed");
                Assert.Same(previous, keyboard.CurrentLayout);
                Assert.Single(contexts);
                keyboard.RefreshLayout();
                Assert.Equal(2, contexts.Count);
                Assert.Equal("closed", Assert.Single(keyboard.CurrentLayout.Keys).Text);
                Assert.Equal(2, keyboard.AvailableSupplementalLayouts.Count);

                keyboard.ShowPage("reopened");
                Assert.Equal(2, contexts.Count);
                keyboard.Visibility = Visibility.Visible;
                Assert.Equal(3, contexts.Count);
                Assert.Equal("reopened", Assert.Single(keyboard.CurrentLayout.Keys).Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// A provider's recursive request cannot keep forcing automatic builds after closing.
    /// </summary>
    [Fact]
    public void RecursiveLayoutRefreshDoesNotEscapeInvisibleStateDeferral()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var provider = Substitute.For<ISoftKeyboardLayoutProvider>();
            provider.CreateLayout(Arg.Any<SoftKeyboardLayoutContext>()).Returns(_ =>
            {
                keyboard.RefreshLayout();
                return SoftKeyboardLayout.Empty;
            });
            keyboard.LayoutProvider = provider;
            keyboard.RefreshLayout();
            keyboard.ShowPage(SoftKeyboardPages.Symbols);
            provider.Received(1).CreateLayout(Arg.Any<SoftKeyboardLayoutContext>());
        });
    }

    /// <summary>
    /// Keeps the loading test independent of native input and records accidental discovery.
    /// </summary>
    private sealed class IdleBackend : ISoftKeyboardInputBackend, ISoftKeyboardInputPaneBackend,
        ISoftKeyboardInputMethodBackend
    {
        internal int Reads { get; private set; }

        /// <inheritdoc />
        public void Refresh(ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active)
        {
            Reads++;
            active = null;
        }

        /// <inheritdoc />
        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            return false;
        }

        /// <inheritdoc />
        public bool SendVirtualKey(Key key, ModifierKeys modifiers)
        {
            return false;
        }

        /// <inheritdoc />
        public bool SendUnicode(string text)
        {
            return false;
        }

        /// <inheritdoc />
        public bool IsHostForeground(IntPtr host)
        {
            return false;
        }

        /// <inheritdoc />
        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            return SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
        }

        /// <inheritdoc />
        public bool TryShow(IntPtr host)
        {
            return false;
        }
    }

}
