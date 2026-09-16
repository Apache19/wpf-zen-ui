// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using NSubstitute;
using NSubstitute.Extensions;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Exercises asynchronous source transitions, alternate routing, resource cleanup, and template
/// animation behavior.
/// </summary>
public class SoftKeyboardRegressionTests
{
    private static readonly SoftKeyboardInputMode NativeMode =
        new("native", true);

    private static readonly SoftKeyboardInputMode DirectMode =
        new("direct", false);

    private static SoftKeyboardInputModeContribution ModeContribution(
        SoftKeyboardInputMode mode)
    {
        return new SoftKeyboardInputModeContribution(
            mode: SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode)
        );
    }

    /// <summary>
    /// Menu activation runs after popup teardown and expires when its source or keyboard lifetime
    /// changes.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task ProfileMenuSelection_WaitsForCloseAndRejectsStaleRequests(int invalidation)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var keyboard = scope.Keyboard;
            var source = CreateSource(scope.Method, out var session);
            keyboard.InputMethodSource = source;
            await scope.DrainAsync();
            keyboard.ShowInputMethodMenu();
            await scope.DrainAsync();
            var menu = (ContextMenu)typeof(SoftKeyboard).GetField(
                "_inputMethodMenu", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(keyboard)!;
            Assert.True(menu.IsOpen);
            session.TryActivate(scope.Method).Returns(_ =>
            {
                Assert.False(menu.IsOpen);
                Assert.Same(scope.Editor, Keyboard.FocusedElement);
                return true;
            });

            SoftKeyboardCommands.ActivateInputMethod.Execute(scope.Method, keyboard);
            Assert.False(menu.IsOpen);
            session.DidNotReceive().TryActivate(Arg.Any<SoftKeyboardInputMethod>());
            if (invalidation == 1)
            {
                keyboard.InputMethodSource = CreateSource(scope.Method, out _);
            }
            else if (invalidation == 2)
            {
                keyboard.IsOpen = false;
            }

            await scope.DrainAsync();
            session.Received(invalidation == 0 ? 1 : 0).TryActivate(scope.Method);
        });
    }

    /// <summary>
    /// A profile change must select its state adapter before publishing the new layout.
    /// </summary>
    [Fact]
    public Task ProfileObservation_UsesNewProfileAdapterOnTheFirstRead()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var next = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0804, "Next IME", true);
            var source = CreateSource(scope.Method, out var session);
            var active = scope.Method;
            session.GetSnapshot().Returns(_ => new SoftKeyboardInputMethodSnapshot([scope.Method, next], active));
            scope.Keyboard.InputMethodSource = source;
            var firstAdapter = Substitute.For<ISoftKeyboardInputModeAdapter>();
            var nextAdapter = Substitute.For<ISoftKeyboardInputModeAdapter>();
            firstAdapter.GetContribution(scope.Method).Returns(ModeContribution(DirectMode));
            nextAdapter.GetContribution(next).Returns(ModeContribution(NativeMode));
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(call =>
                new[]
                {
                    new SoftKeyboardExtension(inputModeAdapter:
                        call.Arg<SoftKeyboardExtensionContext>().Layout.InputMethod == next
                            ? nextAdapter : firstAdapter)
                });
            scope.Keyboard.ExtensionProvider = provider;
            Assert.True(scope.Keyboard.RefreshInputMethods());

            active = next;
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(next, scope.Keyboard.ActiveInputMethod);
            Assert.Same(NativeMode, scope.Keyboard.InputMode);
            firstAdapter.DidNotReceive().GetContribution(next);

            active = scope.Method;
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
            nextAdapter.DidNotReceive().GetContribution(scope.Method);
        });
    }

    /// <summary>
    /// A source mutation without a notification does not fabricate another refresh.
    /// </summary>
    [Fact]
    public Task ProfileObservation_DoesNotRetryWithoutNotification()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();

            var next = new SoftKeyboardInputMethod(
                Guid.NewGuid(),
                Guid.NewGuid(),
                0x0411,
                "Next IME",
                true
            );
            var source = CreateSource(scope.Method, out var session);
            var active = scope.Method;
            session.GetSnapshot().Returns(_ =>
                new SoftKeyboardInputMethodSnapshot([scope.Method, next], active));

            var adapter = Substitute.For<ISoftKeyboardInputModeAdapter>();
            adapter.GetContribution(Arg.Any<SoftKeyboardInputMethod>())
                .Returns(ModeContribution(NativeMode));
            var provider = Substitute.For<ISoftKeyboardExtensionProvider>();
            provider.GetExtensions(Arg.Any<SoftKeyboardExtensionContext>()).Returns(
                [new SoftKeyboardExtension(inputModeAdapter: adapter)]
            );

            scope.Keyboard.InputMethodSource = source;
            scope.Keyboard.ExtensionProvider = provider;
            await scope.DrainAsync();
            Assert.Same(scope.Method, scope.Keyboard.ActiveInputMethod);

            adapter.GetContribution(scope.Method).Returns(_ =>
            {
                active = next;
                return ModeContribution(DirectMode);
            });
            adapter.GetContribution(next).Returns(ModeContribution(DirectMode));

            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(scope.Method, scope.Keyboard.ActiveInputMethod);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// Profile activation writes no mode and publishes the observation before returning.
    /// </summary>
    [Fact]
    public Task InputMethodActivation_OnlySelectsProfileAndPublishesOneObservation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var keyboard = scope.Keyboard;
            var source = CreateSource(scope.Method, out var session);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            keyboard.InputMethodSource = source;
            var next = new SoftKeyboardInputMethod(
                Guid.NewGuid(), Guid.NewGuid(), 0x0804, "Chinese", false);
            var active = scope.Method;
            var mode = DirectMode;
            session.GetSnapshot().Returns(_ =>
                new SoftKeyboardInputMethodSnapshot([scope.Method, next], active));
            session.GetInputMode(Arg.Any<SoftKeyboardInputMethod>()).Returns(_ => mode);
            Assert.True(keyboard.RefreshInputMethods());
            session.TryActivate(next).Returns(_ =>
            {
                keyboard.OnSessionInputLanguageChanged();
                active = next;
                keyboard.OnSessionInputLanguageChanged();
                return true;
            });
            var contexts = new List<SoftKeyboardLayoutContext>();
            var provider = Substitute.For<ISoftKeyboardLayoutProvider>();
            provider.CreateLayout(Arg.Any<SoftKeyboardLayoutContext>()).Returns(call =>
            {
                contexts.Add(call.Arg<SoftKeyboardLayoutContext>());
                return null;
            });
            keyboard.LayoutProvider = provider;
            contexts.Clear();

            Assert.True(keyboard.TryActivateInputMethod(next));
            session.Received(1).TryActivate(next);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
            var context = Assert.Single(contexts);
            Assert.Same(next, context.InputMethod);
            Assert.Same(DirectMode, context.InputMode);
        });
    }

    /// <summary>
    /// Publishing an observation retains the gate and rejects target changes from layout code.
    /// </summary>
    [Fact]
    public Task InputMethodObservation_RejectsTargetChangeFromLayout()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var keyboard = scope.Keyboard;
            var source = CreateSource(scope.Method, out var session);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            keyboard.InputMethodSource = source;
            var provider = Substitute.For<ISoftKeyboardLayoutProvider>();
            provider.CreateLayout(Arg.Any<SoftKeyboardLayoutContext>()).Returns(call =>
            {
                if (call.Arg<SoftKeyboardLayoutContext>().InputMode?.IsNativeInput == true)
                {
                    Assert.False(keyboard.TryActivateInputMethod(scope.Method));
                    keyboard.Target = scope.OtherEditor;
                }

                return null;
            });
            keyboard.LayoutProvider = provider;
            session.GetInputMode(scope.Method).Returns(NativeMode);

            Assert.False(keyboard.RefreshInputMethods());
            session.DidNotReceive().TryActivate(Arg.Any<SoftKeyboardInputMethod>());
        });
    }

    /// <summary>
    /// The focusless input-method menu keeps observing the editor's current input state.
    /// </summary>
    [Fact]
    public Task InputMethodMenu_RemainsFocuslessWhileEditorStateRefreshes()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.GetInputMode(scope.Method).Returns(NativeMode);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            scope.Keyboard.ShowInputMethodMenu();
            var menu = Assert.IsType<ContextMenu>(GetMenu(scope.Keyboard));
            Assert.False(menu.Focus());
            Assert.Same(scope.Editor, Keyboard.FocusedElement);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(DirectMode, scope.Keyboard.InputMode);

            menu.IsOpen = false;
            await scope.DrainAsync();
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
            Assert.Same(scope.Editor, Keyboard.FocusedElement);
        });
    }

    /// <summary>
    /// A remembered editor leaves the focusless menu visible but cannot enable its commands.
    /// </summary>
    [Fact]
    public Task InputMethodMenu_OutsideScopeOpensDisabledWithoutRestoringRememberedTarget()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.AutoHideMode = SoftKeyboardAutoHideMode.Manual;
            scope.Keyboard.TargetScope = scope.Editor;

            Assert.True(scope.OtherEditor.Focus());
            Assert.Same(scope.Editor, scope.Keyboard.ActiveTarget);

            scope.Keyboard.ShowInputMethodMenu();
            await scope.DrainAsync();

            var menu = Assert.IsType<ContextMenu>(GetMenu(scope.Keyboard));
            Assert.True(menu.IsOpen);
            Assert.All(menu.Items.Cast<MenuItem>(), static item => Assert.False(item.IsEnabled));
            Assert.Same(scope.OtherEditor, Keyboard.FocusedElement);
        });
    }

    /// <summary>
    /// Selecting an ordinary keyboard succeeds without writing an IME compartment.
    /// Stale On values supplied by the backend cannot expose a mode switch for this entry.
    /// </summary>
    [Fact]
    public Task OrdinaryKeyboard_ActivatesWithoutInputModeTransitions()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var method = new SoftKeyboardInputMethod(Guid.Empty, Guid.Empty, 0x0409, "English", true);
            scope.Keyboard.InputMethodSource = CreateSource(method, out var session);
            session.GetInputMode(method).Returns((SoftKeyboardInputMode?)null);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Null(scope.Keyboard.InputMode);
            Assert.DoesNotContain(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);

            Assert.True(scope.Keyboard.TryActivateInputMethod(method));
            session.Received(1).TryActivate(method);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
            Assert.Null(scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// Capability updates rebuild the layout, while transient failures retain the last valid mode.
    /// </summary>
    [Fact]
    public Task ModeSwitch_TracksCapabilityAndStateRecovery()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var method = scope.Method;
            scope.Keyboard.InputMethodSource = CreateSource(method, out var session);
            session.GetInputMode(method).Returns(DirectMode);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Equal("A", Assert.Single(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode).Content);

            var buttonSession = Assert.IsAssignableFrom<ISoftKeyboardInputModeButtonSession>(session);
            buttonSession.GetInputModeButton(method).Returns((SoftKeyboardInputModeButton?)null);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.DoesNotContain(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);

            session.GetInputMode(method).Returns(_ => throw new InvalidOperationException("State unavailable"));
            Assert.False(scope.Keyboard.RefreshInputMethods());
            Assert.Same(DirectMode, scope.Keyboard.InputMode);

            session.Configure().GetInputMode(method).Returns(DirectMode);
            buttonSession.GetInputModeButton(method).Returns(
                new SoftKeyboardInputModeButton("default", "A", "Input mode")
            );
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Equal("A", Assert.Single(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode).Content);
        });
    }

    /// <summary>
    /// Reentrant source and target transitions invalidate the whole synchronous activation
    /// transaction.
    /// </summary>
    [Theory]
    [InlineData("source")]
    [InlineData("close")]
    [InlineData("target")]
    [InlineData("unload")]
    public Task Activation_RejectsReentrantContextChange(string transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var keyboard = scope.Keyboard;
            keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var next = session;
            session.TryActivate(scope.Method).Returns(_ =>
            {
                switch (transition)
                {
                    case "source":
                        keyboard.InputMethodSource = CreateSource(scope.Method, out next);
                        session.DidNotReceive().Dispose();
                        break;
                    case "close":
                        keyboard.IsOpen = false;
                        break;
                    case "target":
                        keyboard.Target = scope.OtherEditor;
                        break;
                    case "unload":
                        scope.Panel.Children.Remove(keyboard);
                        keyboard.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(transition));
                }

                return true;
            });

            Assert.False(keyboard.TryActivateInputMethod(scope.Method));
            next.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
            if (transition == "source")
            {
                session.Received(1).Dispose();
                await scope.DrainAsync();
                Assert.Same(scope.Method, keyboard.ActiveInputMethod);
            }
        });
    }

    /// <summary>
    /// Reentrant activation is rejected without interrupting the operation already owning native
    /// state.
    /// </summary>
    [Fact]
    public Task Activation_RejectsNestedMutation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.TryActivate(scope.Method).Returns(_ =>
            {
                Assert.False(scope.Keyboard.TryActivateInputMethod(scope.Method));
                Assert.False(scope.Keyboard.RefreshInputMethods());
                return true;
            });

            Assert.True(scope.Keyboard.TryActivateInputMethod(scope.Method));
            session.Received(1).TryActivate(scope.Method);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
        });
    }

    /// <summary>
    /// Activation never consults or writes the current input mode.
    /// </summary>
    [Fact]
    public Task Activation_IgnoresInputModeWriter()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            ).Returns(_ =>
            {
                scope.Keyboard.IsOpen = false;
                return true;
            });

            Assert.True(scope.Keyboard.TryActivateInputMethod(scope.Method));
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
        });
    }

    /// <summary>
    /// A current activation writes only the command and waits for source invalidation before read.
    /// </summary>
    [Fact]
    public Task Activation_WaitsForStateInvalidation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = CreateSource(scope.Method, out var session);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            scope.Keyboard.InputMethodSource = source;
            session.ClearReceivedCalls();
            session.GetInputMode(scope.Method).Returns(NativeMode);
            Assert.True(scope.Keyboard.TryActivateInputMethod(scope.Method));
            session.Received(1).TryActivate(scope.Method);
            session.DidNotReceive().GetSnapshot();
            session.DidNotReceive().GetInputMode(scope.Method);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);

            scope.Keyboard.OnSessionInputMethodStateInvalidated();
            session.Received(1).GetSnapshot();
            session.Received(1).GetInputMode(scope.Method);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
            Assert.Same(NativeMode, scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// Switching invokes the observed command route and publishes only after source invalidation.
    /// </summary>
    [Fact]
    public Task InputModeSwitch_WaitsForStateInvalidation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            session.ClearReceivedCalls();
            session.GetInputMode(scope.Method).Returns(DirectMode);

            typeof(SoftKeyboard).GetMethod(
                "SwitchInputModeCore",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(scope.Keyboard, null);

            var buttonSession = Assert.IsAssignableFrom<ISoftKeyboardInputModeButtonSession>(session);
            buttonSession.Received(1).TryInvokeInputModeButton(scope.Method);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
            session.DidNotReceive().TryActivate(Arg.Any<SoftKeyboardInputMethod>());
            session.DidNotReceive().GetSnapshot();
            session.DidNotReceive().GetInputMode(scope.Method);
            Assert.Same(NativeMode, scope.Keyboard.InputMode);

            scope.Keyboard.OnSessionInputMethodStateInvalidated();
            session.Received(1).GetSnapshot();
            session.Received(1).GetInputMode(scope.Method);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// A notification raised inside a native command is drained after the command releases the
    /// operation gate.
    /// </summary>
    [Fact]
    public Task InputModeSwitch_DrainsReentrantStateInvalidation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var buttonSession = Assert.IsAssignableFrom<ISoftKeyboardInputModeButtonSession>(session);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            buttonSession.TryInvokeInputModeButton(scope.Method).Returns(_ =>
            {
                scope.Keyboard.OnSessionInputMethodStateInvalidated();
                return true;
            });
            session.ClearReceivedCalls();

            typeof(SoftKeyboard).GetMethod(
                "SwitchInputModeCore",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(scope.Keyboard, null);

            buttonSession.Received(1).TryInvokeInputModeButton(scope.Method);
            session.Received(1).GetSnapshot();
            session.Received(1).GetInputMode(scope.Method);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// A native command can publish an icon and handle clicks without fabricating semantic state.
    /// </summary>
    [Fact]
    public Task NativeInputModeButton_IsIndependentFromInterpretedState()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();

            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<
                ISoftKeyboardInputMethodSession,
                ISoftKeyboardInputModeButtonSession
            >();
            var buttonSession = (ISoftKeyboardInputModeButtonSession)session;
            var presentation = new SoftKeyboardInputModeButton(
                "native-command",
                "icon",
                "Input mode"
            );

#pragma warning disable IDISP004 // The keyboard owns and disposes the configured session.
            source.CreateSession().Returns(session);
#pragma warning restore IDISP004
            session.GetSnapshot().Returns(
                new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method)
            );
            session.GetInputMode(scope.Method).Returns((SoftKeyboardInputMode?)null);
            buttonSession.GetInputModeButton(scope.Method).Returns(presentation);
            buttonSession.TryInvokeInputModeButton(scope.Method).Returns(true);

            scope.Keyboard.InputMethodSource = source;
            Assert.True(scope.Keyboard.RefreshInputMethods());

            Assert.Null(scope.Keyboard.InputMode);
            Assert.Equal(presentation, scope.Keyboard.InputModeButton);
            Assert.Equal(
                "icon",
                Assert.Single(
                    scope.Keyboard.CurrentLayout.Keys,
                    key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode
                ).Content
            );

            session.ClearReceivedCalls();
            typeof(SoftKeyboard).GetMethod(
                "SwitchInputModeCore",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(scope.Keyboard, null);

            buttonSession.Received(1).TryInvokeInputModeButton(scope.Method);
            session.DidNotReceive().TrySwitchInputMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputMode>()
            );
        });
    }

    /// <summary>
    /// An adapter can contribute semantic state while leaving presentation and invocation to the
    /// platform-default provider.
    /// </summary>
    [Fact]
    public Task InputModeAdapter_UnhandledFacetsUsePlatformDefaults()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();

            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<
                ISoftKeyboardInputMethodSession,
                ISoftKeyboardInputModeButtonSession
            >();
            var buttonSession = (ISoftKeyboardInputModeButtonSession)session;
            var adapter = Substitute.For<ISoftKeyboardInputModeAdapter>();
            var presentation = new SoftKeyboardInputModeButton(
                "platform-command",
                "platform-icon",
                "Platform input mode"
            );

#pragma warning disable IDISP004 // The keyboard owns and disposes the configured session.
            source.CreateSession().Returns(session);
#pragma warning restore IDISP004
            session.GetSnapshot().Returns(
                new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method)
            );
            buttonSession.GetInputModeButton(scope.Method).Returns(presentation);
            buttonSession.TryInvokeInputModeButton(scope.Method).Returns(true);
            adapter.GetContribution(scope.Method).Returns(ModeContribution(NativeMode));

            scope.Keyboard.InputModeAdapter = adapter;
            scope.Keyboard.InputMethodSource = source;
            Assert.True(scope.Keyboard.RefreshInputMethods());

            Assert.Same(NativeMode, scope.Keyboard.InputMode);
            Assert.Equal(presentation, scope.Keyboard.InputModeButton);

            session.ClearReceivedCalls();
            adapter.ClearReceivedCalls();
            typeof(SoftKeyboard).GetMethod(
                "SwitchInputModeCore",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(scope.Keyboard, null);

            buttonSession.Received(1).TryInvokeInputModeButton(scope.Method);
            adapter.DidNotReceive().TrySwitchMode(
                Arg.Any<SoftKeyboardInputMethod>(),
                Arg.Any<SoftKeyboardInputModeTransition>()
            );
        });
    }

    /// <summary>
    /// A handled empty presentation suppresses the platform default without claiming unrelated
    /// semantic or transition facets.
    /// </summary>
    [Fact]
    public Task InputModeAdapter_ExplicitNoneStopsPresentationFallback()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();

            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<
                ISoftKeyboardInputMethodSession,
                ISoftKeyboardInputModeButtonSession
            >();
            var buttonSession = (ISoftKeyboardInputModeButtonSession)session;
            var adapter = Substitute.For<ISoftKeyboardInputModeAdapter>();

#pragma warning disable IDISP004 // The keyboard owns and disposes the configured session.
            source.CreateSession().Returns(session);
#pragma warning restore IDISP004
            session.GetSnapshot().Returns(
                new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method)
            );
            buttonSession.GetInputModeButton(scope.Method).Returns(
                new SoftKeyboardInputModeButton("platform", "icon", "Platform input mode")
            );
            adapter.GetContribution(scope.Method).Returns(
                new SoftKeyboardInputModeContribution(
                    mode: SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(NativeMode),
                    presentation: SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.None
                )
            );

            scope.Keyboard.InputModeAdapter = adapter;
            scope.Keyboard.InputMethodSource = source;
            Assert.True(scope.Keyboard.RefreshInputMethods());

            Assert.Same(NativeMode, scope.Keyboard.InputMode);
            Assert.Null(scope.Keyboard.InputModeButton);
            Assert.DoesNotContain(
                scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode
            );
        });
    }

    /// <summary>
    /// Interpreting service-specific state does not replace the independently discovered button.
    /// </summary>
    [Theory]
    [InlineData(false, "platform-icon")]
    [InlineData(true, "extension-icon")]
    public Task InputModeAdapter_ReplacesPlatformButtonOnlyWhenExplicit(
        bool replaceButton,
        string expectedContent)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();

            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<
                ISoftKeyboardInputMethodSession,
                ISoftKeyboardInputModeButtonSession
            >();
            var buttonSession = (ISoftKeyboardInputModeButtonSession)session;
            var adapter = Substitute.For<ISoftKeyboardInputModeAdapter>();
            var replacement = new SoftKeyboardInputModeButton(
                "extension-command",
                "extension-icon",
                "Extension input mode"
            );
            var mode = new SoftKeyboardInputMode("vendor-native", true);
            var transition = new SoftKeyboardInputModeTransition("extension-transition");
            var presentation = new SoftKeyboardInputModeButton(
                "native-command",
                "platform-icon",
                "Input mode"
            );

#pragma warning disable IDISP004 // The keyboard owns and disposes the configured session.
            source.CreateSession().Returns(session);
#pragma warning restore IDISP004
            session.GetSnapshot().Returns(
                new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method)
            );
            buttonSession.GetInputModeButton(scope.Method).Returns(presentation);
            adapter.GetContribution(scope.Method).Returns(
                new SoftKeyboardInputModeContribution(
                    SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode),
                    replaceButton
                        ? SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.FromValue(
                            replacement
                        )
                        : SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.Unhandled,
                    SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition>.FromValue(
                        transition
                    )
                )
            );
            adapter.TrySwitchMode(scope.Method, transition).Returns(true);

            scope.Keyboard.InputModeAdapter = adapter;
            scope.Keyboard.InputMethodSource = source;
            Assert.True(scope.Keyboard.RefreshInputMethods());

            Assert.Same(mode, scope.Keyboard.InputMode);
            Assert.Equal(replaceButton ? replacement : presentation, scope.Keyboard.InputModeButton);
            Assert.Equal(
                expectedContent,
                Assert.Single(
                    scope.Keyboard.CurrentLayout.Keys,
                    key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode
                ).Content
            );

            session.ClearReceivedCalls();
            adapter.ClearReceivedCalls();
            typeof(SoftKeyboard).GetMethod(
                "SwitchInputModeCore",
                BindingFlags.Instance | BindingFlags.NonPublic
            )!.Invoke(scope.Keyboard, null);

            adapter.Received(1).TrySwitchMode(scope.Method, transition);
            buttonSession.DidNotReceive().TryInvokeInputModeButton(scope.Method);
        });
    }

    /// <summary>
    /// An unchanged observation retains both the catalog instance and the generated layout.
    /// </summary>
    [Fact]
    public Task InputMethodObservation_DoesNotPublishUnchangedState()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out _);
            var catalog = scope.Keyboard.InputMethods;
            var layout = scope.Keyboard.CurrentLayout;

            Assert.True(scope.Keyboard.RefreshInputMethods());

            Assert.Same(catalog, scope.Keyboard.InputMethods);
            Assert.Same(layout, scope.Keyboard.CurrentLayout);
        });
    }

    /// <summary>
    /// Every custom-source invalidation is observed before its event handler returns.
    /// </summary>
    [Fact]
    public Task ObservableInputMethodSource_RefreshesEveryInvalidationImmediately()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = Substitute.For<ISoftKeyboardInputMethodSource>();
            var session = Substitute.For<ISoftKeyboardInputMethodObservableSession>();
#pragma warning disable IDISP004 // The control owns and disposes the configured session.
            source.CreateSession().Returns(session);
#pragma warning restore IDISP004
            var mode = NativeMode;
            session.GetSnapshot().Returns(
                new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method)
            );
            session.GetInputMode(scope.Method).Returns(_ => mode);
            scope.Keyboard.InputMethodSource = source;
            session.ClearReceivedCalls();
            mode = DirectMode;

            session.StateInvalidated += Raise.Event<EventHandler>(session, EventArgs.Empty);
            session.StateInvalidated += Raise.Event<EventHandler>(session, EventArgs.Empty);
            session.Received(2).GetSnapshot();
            session.Received(2).GetInputMode(scope.Method);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
        });
    }

    /// <summary>
    /// A notification raised during a read repeats the observation before the call returns.
    /// </summary>
    [Fact]
    public Task InputMethodObservation_DiscardsSnapshotCrossedByNotification()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var next = new SoftKeyboardInputMethod(
                Guid.NewGuid(), Guid.NewGuid(), 0x0411, "Japanese", true
            );
            var invalidate = true;
            session.GetSnapshot().Returns(_ =>
            {
                if (invalidate)
                {
                    invalidate = false;
                    scope.Keyboard.OnSessionInputMethodStateInvalidated();
                }

                return new SoftKeyboardInputMethodSnapshot([next], next);
            });

            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(next, scope.Keyboard.ActiveInputMethod);
        });
    }

    /// <summary>
    /// A transient state read failure preserves the last valid profile, mode, and layout.
    /// </summary>
    [Fact]
    public Task InputMethodObservation_FailurePreservesLastValidState()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.GetInputMode(scope.Method).Returns(DirectMode);
            Assert.True(scope.Keyboard.RefreshInputMethods());
            var catalog = scope.Keyboard.InputMethods;
            var profile = scope.Keyboard.ActiveInputMethod;
            var layout = scope.Keyboard.CurrentLayout;
            var error = new InvalidOperationException("temporary state failure");
            session.GetInputMode(scope.Method).Returns(_ => throw error);

            Assert.False(scope.Keyboard.RefreshInputMethods());

            Assert.Same(error, scope.Keyboard.InputMethodError);
            Assert.Same(catalog, scope.Keyboard.InputMethods);
            Assert.Same(profile, scope.Keyboard.ActiveInputMethod);
            Assert.Same(DirectMode, scope.Keyboard.InputMode);
            Assert.Same(layout, scope.Keyboard.CurrentLayout);
            Assert.Contains(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);
        });
    }

    /// <summary>
    /// Catalog bindings receive complete immutable snapshots and retain stable older values.
    /// </summary>
    [Fact]
    public Task InputMethodCatalog_ReplacesSnapshotAtomically()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var previous = scope.Keyboard.InputMethods;
            var observer = new TextBlock();
            observer.SetBinding(
                FrameworkElement.TagProperty,
                new System.Windows.Data.Binding(nameof(SoftKeyboard.InputMethods)) { Source = scope.Keyboard });
            var fresh = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "fresh", true);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([fresh], fresh));
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Same(scope.Method, Assert.Single(previous));
            Assert.Same(fresh, Assert.Single(scope.Keyboard.InputMethods));
            Assert.Same(scope.Keyboard.InputMethods, observer.Tag);
        });
    }

    /// <summary>
    /// A failure is observable and a later successful snapshot clears it.
    /// </summary>
    [Fact]
    public Task InputMethodCatalog_ReportsFailureAndRecovers()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            var error = new InvalidOperationException("catalog failed");
            session.GetSnapshot().Returns(_ => throw error);
            Assert.False(scope.Keyboard.RefreshInputMethods());
            Assert.Same(error, scope.Keyboard.InputMethodError);
            session.Configure().GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method));
            Assert.True(scope.Keyboard.RefreshInputMethods());
            Assert.Null(scope.Keyboard.InputMethodError);
        });
    }

    /// <summary>
    /// Source creation may reenter; its returned session must not attach to the replacement source.
    /// </summary>
    [Fact]
    public Task InputMethodCreation_RejectsReentrantReplacement()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var replacement = CreateSource(scope.Method, out var current);
            var source = CreateSource(scope.Method, out var obsolete);
#pragma warning disable IDISP004 // Configure ownership transfer from the substitute factory.
            source.Configure().CreateSession().Returns(_ =>
            {
                scope.Keyboard.InputMethodSource = replacement;
                Assert.Empty(scope.Keyboard.InputMethods);
                return obsolete;
            });
#pragma warning restore IDISP004
            scope.Keyboard.InputMethodSource = source;
            obsolete.Received(1).Dispose();
            obsolete.DidNotReceive().GetSnapshot();
            await scope.DrainAsync();
            Assert.Same(scope.Method, Assert.Single(scope.Keyboard.InputMethods));
            current.DidNotReceive().Dispose();
        });
    }

    /// <summary>
    /// Reentrant candidate registration releases the detached source and permits fresh
    /// registration.
    /// </summary>
    [Fact]
    public Task CandidateCreation_RejectsReentrantReplacement()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = Substitute.For<ISoftKeyboardCandidateSource>();
            var replacement = Substitute.For<ISoftKeyboardCandidateSource>();
            var obsolete = Substitute.For<ISoftKeyboardCandidateSession>();
            var current = Substitute.For<ISoftKeyboardCandidateSession>();
            current.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
#pragma warning disable IDISP004 // Configure sessions whose lifetime belongs to the keyboard.
            replacement.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(current);
            source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(_ =>
            {
                scope.Keyboard.CandidateSource = replacement;
                return obsolete;
            });
#pragma warning restore IDISP004
            scope.Keyboard.CandidateSource = source;
            obsolete.Received(1).Dispose();
            await obsolete.DidNotReceive().RefreshAsync(Arg.Any<CancellationToken>());
            await scope.Keyboard.RefreshCandidatesAsync();
            current.DidNotReceive().Dispose();
            Assert.Null(scope.Keyboard.CandidateError);
        });
    }

    /// <summary>
    /// Cleanup callbacks may replace sources, but new registration waits until old cleanup returns.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SourceCleanup_DefersReentrantRegistration(bool candidate)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            if (candidate)
            {
                var source = Substitute.For<ISoftKeyboardCandidateSource>();
                var replacement = Substitute.For<ISoftKeyboardCandidateSource>();
                var obsolete = Substitute.For<ISoftKeyboardCandidateSession>();
                var current = Substitute.For<ISoftKeyboardCandidateSession>();
                obsolete.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
                current.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
#pragma warning disable IDISP004 // The control owns factory results and cleanup.
                source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(obsolete);
                replacement.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(current);
#pragma warning restore IDISP004
                scope.Keyboard.CandidateSource = source;
                obsolete.When(value => value.Dispose()).Do(_ =>
                {
                    scope.Keyboard.CandidateSource = replacement;
#pragma warning disable IDISP004 // Verifies that no registration occurred inside the old cleanup callback.
                    replacement.DidNotReceive().CreateSession(Arg.Any<SoftKeyboardCandidateHost>());
#pragma warning restore IDISP004
                });
                scope.Keyboard.CandidateSource = Substitute.For<ISoftKeyboardCandidateSource>();
                await scope.Keyboard.RefreshCandidatesAsync();
                current.DidNotReceive().Dispose();
                Assert.Null(scope.Keyboard.CandidateError);
            }
            else
            {
                scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var obsolete);
                var replacement = CreateSource(scope.Method, out var current);
                obsolete.When(value => value.Dispose()).Do(_ =>
                {
                    scope.Keyboard.InputMethodSource = replacement;
#pragma warning disable IDISP004 // Verifies that no source opened inside the old cleanup callback.
                    replacement.DidNotReceive().CreateSession();
#pragma warning restore IDISP004
                });
                scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out _);
                Assert.True(scope.Keyboard.RefreshInputMethods());
                current.DidNotReceive().Dispose();
                Assert.Null(scope.Keyboard.InputMethodError);
            }
        });
    }

    /// <summary>
    /// Cleanup must dispose once even when presentation release fails, preserving both failures
    /// when necessary.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CandidateCleanup_DisposesAfterReleaseFailure(bool disposeThrows)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = Substitute.For<ISoftKeyboardCandidateSource>();
            var session = Substitute.For<ISoftKeyboardCandidateSession>();
            var releaseError = new InvalidOperationException("release failed");
            var disposeError = new InvalidOperationException("dispose failed");
#pragma warning disable IDISP004 // Configures a substitute; the keyboard owns and disposes the returned session.
            source.CreateSession(Arg.Any<SoftKeyboardCandidateHost>()).Returns(session);
#pragma warning restore IDISP004
            session.RefreshAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
            session.When(value => value.ReleasePresentation()).Do(_ => throw releaseError);
            if (disposeThrows)
            {
                session.When(value => value.Dispose()).Do(_ => throw disposeError);
            }

            scope.Keyboard.CandidateSource = source;
            await scope.Keyboard.RefreshCandidatesAsync();
            scope.Keyboard.IsOpen = false;
            session.Received(1).Dispose();
            if (disposeThrows)
            {
                var error = Assert.IsType<AggregateException>(scope.Keyboard.CandidateError);
                Assert.Equal([releaseError, disposeError], error.InnerExceptions);
            }
            else
            {
                Assert.Same(releaseError, scope.Keyboard.CandidateError);
            }
        });
    }

    /// <summary>
    /// An action implementing only the asynchronous alternate interface receives the selected value
    /// without Unicode fallback.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AlternateRequest_InvokesAsyncOnlyAction(bool failAfterClosing)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var action = Substitute.For<IAsyncSoftKeyboardAlternateKeyAction>();
            var pending = new TaskCompletionSource<bool>();
            action.ExecuteAlternateAsync(
                Arg.Any<SoftKeyboardActionContext>(), "alt", Arg.Any<CancellationToken>()
            ).Returns(pending.Task);
            var key = new SoftKeyboardKey(
                SoftKeyboardKeyKind.Custom, "a", "a", alternates: ["alt"], action: action
            );
            scope.Keyboard.LayoutProvider = new SoftKeyboardLayoutRule(
                _ => true, _ => new SoftKeyboardLayout([key])
            );
            scope.Window.UpdateLayout();
            var cap = Assert.Single(FindDescendants<SoftKeyboardKeyControl>(scope.Keyboard));
            cap.RaiseEvent(new RoutedEventArgs(SoftKeyboardKeyControl.InteractionStartedEvent, cap));
            cap.RaiseEvent(new SoftKeyboardInputRequestedEventArgs(
                SoftKeyboardKeyControl.InputRequestedEvent, cap, key, "alt"
            ));

            Assert.Single(action.ReceivedCalls(), call => call.GetMethodInfo().Name == "ExecuteAlternateAsync");
            Assert.Empty(scope.Input.Text);
            if (failAfterClosing)
            {
                scope.Keyboard.IsOpen = false;
                pending.SetException(new InvalidOperationException("late action failure"));
            }
            else
            {
                pending.SetResult(true);
            }

            await scope.DrainAsync();
            Assert.Null(scope.Keyboard.ExtensionError);
        });
    }

    /// <summary>
    /// The menu consumes the current snapshot immediately, including an initially empty catalog.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InputMethodMenu_ReadsFreshCatalog(bool initiallyEmpty)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            var source = CreateSource(scope.Method, out var session);
            if (initiallyEmpty)
            {
                session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([], null));
            }

            scope.Keyboard.InputMethodSource = source;
            var fresh = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "fresh", true);
            session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([fresh], fresh));
            scope.Keyboard.ShowInputMethodMenu();

            var menu = Assert.IsType<ContextMenu>(GetMenu(scope.Keyboard));
            Assert.True(menu.IsOpen);
            var item = Assert.IsType<MenuItem>(Assert.Single(menu.Items.Cast<object>()));
            Assert.Same(fresh, item.CommandParameter);
        });
    }

    /// <summary>
    /// Native reentrancy during catalog discovery cannot reopen a closed or replaced menu request.
    /// </summary>
    [Theory]
    [InlineData("close")]
    [InlineData("source")]
    [InlineData("newer")]
    public Task InputMethodMenu_RejectsReentrantOpen(string transition)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out var session);
            session.GetSnapshot().Returns(call =>
            {
                if (transition == "close")
                {
                    scope.Keyboard.IsOpen = false;
                }
                else if (transition == "source")
                {
                    scope.Keyboard.InputMethodSource = CreateSource(scope.Method, out _);
                }
                else
                {
                    scope.Keyboard.ShowInputMethodMenu();
                }

                return new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method);
            });

            scope.Keyboard.ShowInputMethodMenu();
            Assert.NotEqual(true, GetMenu(scope.Keyboard)?.IsOpen);
            // Stop provoking reentrancy before the next explicit observation.
            session.Configure().GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([scope.Method], scope.Method));
        });
    }

    /// <summary>
    /// Opening animation relinquishes opacity so disabling works regardless of transition order or
    /// reopening.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OpeningAnimation_PreservesDisabledOpacity(bool disableBeforeOpening)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            await scope.DrainAsync();
            scope.Keyboard.IsOpen = false;
            scope.Keyboard.IsEnabled = !disableBeforeOpening;
            scope.Keyboard.IsOpen = true;
            await Task.Delay(250);
            scope.Keyboard.IsEnabled = false;
            var root = Assert.IsType<Border>(scope.Keyboard.Template.FindName("KeyboardRoot", scope.Keyboard));
            Assert.Equal(0.55, root.Opacity, 3);
            scope.Keyboard.IsOpen = false;
            scope.Keyboard.IsOpen = true;
            await Task.Delay(250);
            Assert.Equal(0.55, root.Opacity, 3);
            scope.Keyboard.IsEnabled = true;
            Assert.Equal(1, root.Opacity, 3);
        });
    }

    /// <summary>
    /// Creates an immediate source whose callbacks can simulate native reentrancy.
    /// </summary>
    private static ISoftKeyboardInputMethodSource CreateSource(
        SoftKeyboardInputMethod method,
        out ISoftKeyboardInputMethodSession session)
    {
        var source = Substitute.For<ISoftKeyboardInputMethodSource>();
        session = Substitute.For<
            ISoftKeyboardInputMethodSession,
            ISoftKeyboardInputModeButtonSession
        >();
        var buttonSession = (ISoftKeyboardInputModeButtonSession)session;
#pragma warning disable IDISP004 // Configures a substitute; the keyboard owns and disposes the returned session.
        source.CreateSession().Returns(session);
#pragma warning restore IDISP004
        session.GetSnapshot().Returns(new SoftKeyboardInputMethodSnapshot([method], method));
        session.GetInputMode(method).Returns(NativeMode);
        buttonSession.GetInputModeButton(method).Returns(
            method.ClassId == Guid.Empty
                ? null
                : new SoftKeyboardInputModeButton("default", "A", "Input mode")
        );
        buttonSession.TryInvokeInputModeButton(method).Returns(true);
        session.TrySwitchInputMode(
            Arg.Any<SoftKeyboardInputMethod>(),
            Arg.Any<SoftKeyboardInputMode>()
        ).Returns(true);
        session.TryActivate(Arg.Any<SoftKeyboardInputMethod>()).Returns(true);
        return source;
    }

    private static ContextMenu? GetMenu(SoftKeyboard keyboard)
    {
        return (ContextMenu?)typeof(SoftKeyboard).GetField(
            "_inputMethodMenu", BindingFlags.Instance | BindingFlags.NonPublic
        )!.GetValue(keyboard);
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
