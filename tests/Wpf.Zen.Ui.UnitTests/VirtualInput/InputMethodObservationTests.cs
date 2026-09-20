// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Layouts;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies observation timing, Dispatcher scheduling, and real WPF presentation.
/// </summary>
public class InputMethodObservationTests
{
    /// <summary>
    /// Due observation work remains asynchronous but runs ahead of queued input processing.
    /// </summary>
    [Fact]
    public Task DuePublication_PrecedesPendingInput()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var scheduler = new DispatcherInputObservationScheduler(dispatcher);
            var published = false;
            var input = dispatcher.InvokeAsync(() => Assert.True(published), DispatcherPriority.Input);

            using var scheduled = scheduler.Schedule(0, () => published = true);
            Assert.False(published);

            await input;
        });
    }

    /// <summary>
    /// Either notification supplies a pair; only a change in that pair extends activation.
    /// </summary>
    /// <param name="openTrigger">Whether the only native trigger is an open notification.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PairedNotificationsMergeOnceAndCompareBothFields(bool openTrigger)
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        var kind = openTrigger ? InputMethodChangeKind.ImeOpen : InputMethodChangeKind.ImeConversion;
        coordinator.Begin();
        coordinator.Observe(new InputMethodChange(kind,
            State: new SoftKeyboardInputObservation(true, 1, true, 1025)));
        clock.Advance(90);
        // The trigger's own value stays equal, but the companion value changes.
        coordinator.Observe(new InputMethodChange(kind, State: new SoftKeyboardInputObservation(
            true, openTrigger ? 1 : 0, true, openTrigger ? 0 : 1025)));
        clock.Advance(90);
        coordinator.Observe(new InputMethodChange(kind, State: coordinator.Observation));
        clock.Advance(9);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.Equal(openTrigger ? 1 : 0, coordinator.Observation.OpenValue);
        Assert.Equal(openTrigger ? 0 : 1025, coordinator.Observation.ConversionValue);
    }

    /// <summary>
    /// Settled completion fills a failed read without replacing a successful notified field.
    /// Repeated refreshes do not poll native state, and later notifications remain authoritative.
    /// </summary>
    [Fact]
    public void CompletionPreservesValuesAndRunsOncePerGeneration()
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        var reads = 0;
        SoftKeyboardInputObservation Read(bool useImm)
        {
            reads++;
            return new SoftKeyboardInputObservation(true, 0, true, 1025);
        }

        coordinator.Begin();
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.ImeOpen,
            State: new SoftKeyboardInputObservation(true, 1, true, null)));
        coordinator.CompleteObservation(Read, CanInterpretPinyin, () => true);
        Assert.Equal(0, reads);
        clock.Advance(100);
        coordinator.CompleteObservation(Read, CanInterpretPinyin, () => true);
        Assert.Equal(1, reads);
        Assert.Equal(1, coordinator.Observation.OpenValue);
        Assert.Equal(1025, coordinator.Observation.ConversionValue);
        coordinator.CompleteObservation(Read, CanInterpretPinyin, () => true);
        Assert.Equal(1, reads);
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.ImeOpen,
            State: new SoftKeyboardInputObservation(true, 1, true, 0)));
        clock.Advance(0);
        Assert.Equal(0, coordinator.Observation.ConversionValue);
        coordinator.Begin();
        clock.Advance(100);
        coordinator.CompleteObservation(Read, CanInterpretPinyin, () => true);
        Assert.Equal(2, reads);
    }

    /// <summary>
    /// Uninterpretable TSF data falls back to IMM; rejected candidates never become observations.
    /// </summary>
    /// <param name="validImm">Whether the final native sample can be interpreted.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CompletionTriesImmOnlyAfterUnusableTsf(bool validImm)
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.ImeOpen,
            State: new SoftKeyboardInputObservation(true, 2, true, -1)));
        clock.Advance(100);
        var calls = new List<bool>();
        coordinator.CompleteObservation(useImm =>
        {
            calls.Add(useImm);
            return new SoftKeyboardInputObservation(true, useImm && validImm ? 1 : 2, true, 1025);
        }, CanInterpretPinyin, () => true);
        Assert.Equal(new[] { false, true }, calls);
        Assert.Equal(validImm ? 1 : 2, coordinator.Observation.OpenValue);
        Assert.Equal(validImm ? 1025 : -1, coordinator.Observation.ConversionValue);
    }

    /// <summary>
    /// Unavailable TSF fields do not prevent an IMM attempt even when retained data is decodable.
    /// </summary>
    [Fact]
    public void CompletionContinuesWhenTsfAddsNoMissingValue()
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 1025));
        clock.Advance(100);
        var calls = new List<bool>();
        coordinator.CompleteObservation(useImm =>
        {
            calls.Add(useImm);
            return useImm ? new SoftKeyboardInputObservation(true, 1, true, 0)
                : new SoftKeyboardInputObservation(true, null, true, null);
        }, CanInterpretPinyin, () => true);
        Assert.Equal(new[] { false, true }, calls);
        Assert.Equal(1, coordinator.Observation.OpenValue);
        Assert.Equal(1025, coordinator.Observation.ConversionValue);
    }

    /// <summary>
    /// Native reads cannot overwrite a reentrant notification, generation change or lost owner.
    /// </summary>
    /// <param name="invalidation">Which ownership boundary changes during the read.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void CompletionRejectsReentrantState(int invalidation)
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        clock.Advance(100);
        var current = true;
        coordinator.CompleteObservation(useImm =>
        {
            if (invalidation == 0)
            {
                coordinator.Observe(new InputMethodChange(InputMethodChangeKind.ImeOpen,
                    State: new SoftKeyboardInputObservation(true, 1, true, 0)));
            }
            else if (invalidation == 1)
            {
                coordinator.Begin();
            }
            else
            {
                current = false;
            }

            return new SoftKeyboardInputObservation(true, 1, true, 1025);
        }, CanInterpretPinyin, () => current);
        Assert.Equal(invalidation == 0 ? 0 : (int?)null, coordinator.Observation.ConversionValue);
    }

    /// <summary>
    /// A full interpretable pair bypasses all settled native reads.
    /// </summary>
    [Fact]
    public void CompleteNotificationPairNeedsNoFallback()
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.ImeOpen,
            State: new SoftKeyboardInputObservation(true, 1, true, 1025)));
        clock.Advance(100);
        coordinator.CompleteObservation(_ => throw new InvalidOperationException("Unexpected read"),
            CanInterpretPinyin, () => true);
        Assert.Equal(1025, coordinator.Observation.ConversionValue);
    }

    /// <summary>
    /// Uses the real extension interpretation contract to judge fallback candidates.
    /// </summary>
    private static bool CanInterpretPinyin(SoftKeyboardInputObservation observation)
    {
        var profile = new SoftKeyboardInputMethod(KnownTextServices.MicrosoftPinyin,
            Guid.NewGuid(), 0x0804, "Pinyin", true);
        return ChineseInputModeAdapter.Default.GetContribution(profile, observation).Button is not null;
    }

    /// <summary>
    /// Initial discovery and window reactivation recover a Pinyin button without any notification.
    /// The visible key is refreshed from the new generation's sample rather than old cached state.
    /// </summary>
    [Fact]
    public Task InitialDiscoveryAndWindowReactivationReadMissingState()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var profile = new SoftKeyboardInputMethod(KnownTextServices.MicrosoftPinyin,
                Guid.NewGuid(), 0x0804, "Pinyin", true);
            var sample = new SoftKeyboardInputObservation(true, 1, true, 1025);
            var backend = new Backend(profile) { StateReader = (_, _) => sample };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            scope.Keyboard.InputModeAdapter = ChineseInputModeAdapter.Default;
            Assert.Equal(0, backend.StateReads);
            await scope.DrainAsync();
            Assert.Equal("Pinyin", scope.Keyboard.ActiveInputMethod?.DisplayName);
            Assert.Equal("中", ModeCap(scope.Keyboard).Content);
            Assert.Equal(1, backend.StateReads);

            sample = new SoftKeyboardInputObservation(true, 1, true, 0);
            typeof(SoftKeyboard).GetMethod("OnWindowDeactivated",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scope.Keyboard, new object?[] { scope.Window, EventArgs.Empty });
            typeof(SoftKeyboard).GetMethod("OnWindowActivated",
                BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(scope.Keyboard, new object?[] { scope.Window, EventArgs.Empty });
            await scope.DrainAsync();
            Assert.Equal("A", ModeCap(scope.Keyboard).Content);
            Assert.Equal(2, backend.StateReads);

            // A regular publication does not become a native polling loop.
            scope.Keyboard.RefreshInputMethods();
            Assert.Equal(2, backend.StateReads);
        });
    }

    /// <summary>
    /// An open-only native notification supplies conversion and shows the Pinyin fallback key.
    /// </summary>
    [Fact]
    public Task OpenOnlyNotificationResolvesPinyinWithoutSettledRead()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var profile = new SoftKeyboardInputMethod(KnownTextServices.MicrosoftPinyin,
                Guid.NewGuid(), 0x0804, "Pinyin", true);
            var backend = new Backend(profile)
            {
                StateReader = (_, _) => throw new InvalidOperationException("Unexpected fallback"),
            };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            scope.Keyboard.InputModeAdapter = ChineseInputModeAdapter.Default;
            Activate(scope.Keyboard, profile);
            scope.Keyboard.OnSessionInputMethodChanged(new InputMethodChange(InputMethodChangeKind.ImeOpen,
                State: new SoftKeyboardInputObservation(true, 1, true, 1025)));
            await scope.DrainAsync();
            Assert.Null(scope.Keyboard.InputMethodError);
            Assert.Equal("中", ModeCap(scope.Keyboard).Content);
            Assert.Equal(0, backend.StateReads);
        });
    }

    /// <summary>
    /// Keeps missing conversion unknown after activation until its own notification arrives.
    /// </summary>
    /// <param name="conversion">The later notified value, or null when its read fails.</param>
    /// <param name="content">The expected Pinyin button content, or null when unavailable.</param>
    [Theory]
    [InlineData(1025, "中")]
    [InlineData(0, "A")]
    [InlineData(null, null)]
    public void MissingConversionWaitsForNotificationAfterActivation(int? conversion, string? content)
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 1));
        clock.Advance(100);
        var profile = new SoftKeyboardInputMethod(KnownTextServices.MicrosoftPinyin,
            Guid.NewGuid(), 0x0804, "Pinyin", true);
        Assert.False(coordinator.Observation.ConversionObserved);
        Assert.Null(ChineseInputModeAdapter.Default
            .GetContribution(profile, coordinator.Observation).Button);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, conversion));
        clock.Advance(0);
        Assert.False(coordinator.IsObserving);
        Assert.True(coordinator.Observation.ConversionObserved);
        Assert.Equal(conversion, coordinator.Observation.ConversionValue);
        Assert.Equal(content, ChineseInputModeAdapter.Default
            .GetContribution(profile, coordinator.Observation).Button?.Content);
    }

    /// <summary>
    /// Equal values retain the activation deadline, including repeated unavailable reads.
    /// </summary>
    /// <param name="open">Whether to observe the open field instead of conversion.</param>
    /// <param name="value">The value repeated by subsequent notifications.</param>
    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 1025)]
    [InlineData(true, null)]
    [InlineData(false, null)]
    public void EqualValuesDoNotExtendActivation(bool open, int? value)
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        clock.Advance(50);
        var change = StateChange(open ? InputMethodChangeKind.ImeOpen : InputMethodChangeKind.ImeConversion, value);
        coordinator.Observe(change);
        clock.Advance(49);
        coordinator.Observe(change);
        clock.Advance(50);
        coordinator.Observe(change);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.False(coordinator.IsObserving);
        // The same value after commitment still requests publication without reopening a window.
        coordinator.Observe(change);
        clock.Advance(0);
        Assert.Equal(2, publications);
        Assert.False(coordinator.IsObserving);
    }

    /// <summary>
    /// The two fields have independent presence and value comparisons during activation.
    /// </summary>
    [Fact]
    public void EqualNumbersInDifferentFieldsStillExtendActivation()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 1));
        clock.Advance(90);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 1));
        clock.Advance(99);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
    }

    /// <summary>
    /// Language-bar notifications wait 30 ms while retaining any longer activation wait.
    /// </summary>
    /// <param name="arrival">The language-bar notification time after activation.</param>
    /// <param name="due">The expected publication time after activation.</param>
    [Theory]
    [InlineData(10, 100)]
    [InlineData(90, 120)]
    public void LanguageBarUsesShortActivationExtension(int arrival, int due)
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        clock.Advance(arrival);
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.InputModePresentation));
        clock.Advance(due - arrival - 1);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.False(coordinator.IsObserving);
    }

    /// <summary>
    /// A burst of language-bar notifications remains bounded by the activation deadline.
    /// </summary>
    [Fact]
    public void LanguageBarBurstStopsAtActivationDeadline()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        for (var i = 0; i < 24; i++)
        {
            clock.Advance(20);
            coordinator.Observe(new InputMethodChange(InputMethodChangeKind.InputModePresentation));
        }

        clock.Advance(19);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.False(coordinator.IsObserving);
    }

    /// <summary>
    /// State notifications and the click recheck preserve a pending language-bar readiness delay.
    /// </summary>
    [Fact]
    public void LanguageBarDelaySurvivesStateNotificationsAndClickRecheck()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        clock.Advance(100);
        coordinator.AfterClick();
        clock.Advance(10);
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.InputModePresentation));
        clock.Advance(10);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 1));
        clock.Advance(19);
        Assert.Equal(1, publications);
        Assert.False(coordinator.IsObserving);
        clock.Advance(1);
        Assert.Equal(2, publications);
        Assert.Equal(1, coordinator.Observation.OpenValue);
        coordinator.Observe(new InputMethodChange(InputMethodChangeKind.InputModePresentation));
        clock.Advance(10);
        coordinator.Cancel();
        clock.Advance(30);
        Assert.Equal(2, publications);
    }

    /// <summary>
    /// Shared open and conversion notifications update the visible Pinyin key in arrival order.
    /// </summary>
    [Fact]
    public Task PinyinNotificationsUpdateSharedValuesAndVisibleKey()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var profile = new SoftKeyboardInputMethod(KnownTextServices.MicrosoftPinyin,
                Guid.NewGuid(), 0x0804, "Pinyin", true);
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(new Backend(profile));
            scope.Keyboard.InputModeAdapter = ChineseInputModeAdapter.Default;
            Notify(scope.Keyboard, InputMethodChangeKind.ImeOpen, 1);
            scope.Keyboard.OnSessionInputMethodChanged(StateChange(InputMethodChangeKind.ImeConversion, 1025));
            await scope.DrainAsync();
            Assert.Equal("中", scope.Keyboard.InputModeButton?.Content);
            Assert.Equal("中", ModeCap(scope.Keyboard).Content);
            scope.Keyboard.OnSessionInputMethodChanged(StateChange(InputMethodChangeKind.ImeConversion, 0));
            scope.Clock.Advance(0);
            Assert.Equal("A", scope.Keyboard.InputModeButton?.Content);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeConversion, 1025);
            scope.Clock.Advance(0);
            Assert.Equal("中", scope.Keyboard.InputModeButton?.Content);
        });
    }

    /// <summary>
    /// Records the latest notified values and clears both fields when a generation is replaced.
    /// </summary>
    [Fact]
    public void SharedValuesUpdateInArrivalOrderAndResetWithGeneration()
    {
        var clock = new ManualInputObservationScheduler();
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { });
        coordinator.Begin();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 1025));
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 1));
        Assert.Equal(1025, coordinator.Observation.ConversionValue);
        Assert.Equal(1, coordinator.Observation.OpenValue);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 0));
        Assert.Equal(0, coordinator.Observation.OpenValue);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 0));
        Assert.Equal(0, coordinator.Observation.ConversionValue);
        clock.Advance(100);
        coordinator.Begin();
        Assert.Same(SoftKeyboardInputObservation.Empty, coordinator.Observation);
    }

    /// <summary>
    /// Waits for quiet while preserving missing fields and replacing failed notification reads.
    /// </summary>
    [Fact]
    public void Activation_WaitsForQuietAndRecordsOnlyNotifiedFields()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        clock.Advance(90);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 2));
        clock.Advance(99);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.Equal(2, coordinator.Observation.OpenValue);
        Assert.False(coordinator.Observation.ConversionObserved);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, null));
        clock.Advance(0);
        Assert.True(coordinator.Observation.OpenObserved);
        Assert.Null(coordinator.Observation.OpenValue);
        Assert.Equal(2, publications);
    }

    /// <summary>
    /// Bounds a continuous notification burst and accepts notifications arriving after commitment.
    /// </summary>
    [Fact]
    public void Activation_ContinuousNotificationsStopAtFiveHundredMilliseconds()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        for (var i = 0; i < 5; i++)
        {
            clock.Advance(90);
            coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, i));
        }

        clock.Advance(49);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.False(coordinator.IsObserving);
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 9));
        clock.Advance(0);
        Assert.Equal(2, publications);
    }

    /// <summary>
    /// Replaces raw observations on activation and cancels every outstanding delayed callback.
    /// </summary>
    [Fact]
    public void Activation_NoNotificationsAndReplacementNeverInventValues()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeConversion, 1025));
        clock.Advance(50);
        coordinator.Begin();
        clock.Advance(99);
        Assert.Equal(0, publications);
        clock.Advance(1);
        Assert.Equal(1, publications);
        Assert.Same(SoftKeyboardInputObservation.Empty, coordinator.Observation);
        coordinator.AfterClick();
        coordinator.Cancel();
        clock.Advance(1000);
        Assert.Equal(1, publications);
    }

    /// <summary>
    /// Updates the actual key control from icon A to B without a notification after the
    /// click delay.
    /// </summary>
    [Fact]
    public Task NativeButton_ClickRefreshesChangedIconAfterThirtyMillisecondsWithoutNotification()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("A", 20) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            await scope.DrainAsync();
            var oldLayout = scope.Keyboard.CurrentLayout;
            var oldButton = scope.Keyboard.InputModeButton;
            var reads = backend.Reads;
            var oldCap = ModeCap(scope.Keyboard);
            Assert.Same(oldButton!.Content, oldCap.Content);
            oldCap.Invoke();
            Assert.Equal(1, backend.Invocations);
            scope.Clock.Advance(20);
            backend.Button = Icon("B", 200);
            scope.Clock.Advance(9);
            Assert.Same(oldButton, scope.Keyboard.InputModeButton);
            Assert.Same(oldLayout, scope.Keyboard.CurrentLayout);
            Assert.Equal(reads, backend.Reads);
            scope.Clock.Advance(1);
            Assert.Equal(reads + 1, backend.Reads);
            Assert.Same(backend.Button, scope.Keyboard.InputModeButton);
            Assert.Same(backend.Button.Content, Assert.Single(scope.Keyboard.CurrentLayout.Keys,
                key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode).Content);
            Assert.Same(backend.Button.Content, ModeCap(scope.Keyboard).Content);
            var changedLayout = scope.Keyboard.CurrentLayout;
            Click(scope.Keyboard);
            scope.Clock.Advance(30);
            Assert.Same(changedLayout, scope.Keyboard.CurrentLayout);
        });
    }

    /// <summary>
    /// Keeps the mandatory click recheck even when an earlier notification already refreshed UI.
    /// </summary>
    [Fact]
    public Task NotificationBeforeClickDeadlineDoesNotCancelFinalRecheckAndLateChangesStillPublish()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("A", 20) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            await scope.DrainAsync();
            Click(scope.Keyboard);
            scope.Clock.Advance(10);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeOpen, 1);
            scope.Clock.Advance(0);
            var reads = backend.Reads;
            backend.Button = Icon("B", 200);
            scope.Clock.Advance(20);
            Assert.Equal(reads + 1, backend.Reads);
            Assert.Equal("B", scope.Keyboard.InputModeButton?.Id);
            backend.Button = Icon("C", 250);
            Notify(scope.Keyboard, InputMethodChangeKind.InputModePresentation);
            scope.Clock.Advance(29);
            Assert.Equal("B", scope.Keyboard.InputModeButton?.Id);
            scope.Clock.Advance(1);
            Assert.Equal("C", scope.Keyboard.InputModeButton?.Id);
        });
    }

    /// <summary>
    /// Falls back only when platform discovery is unavailable, including when invocation fails.
    /// </summary>
    [Theory]
    [InlineData(SoftKeyboardInputModeCommandResult.Accepted, 0)]
    [InlineData(SoftKeyboardInputModeCommandResult.Failed, 0)]
    [InlineData(SoftKeyboardInputModeCommandResult.Unavailable, 1)]
    public Task Click_UsesFreshPlatformRouteAndOnlyFallsBackWhenUnavailable(
        SoftKeyboardInputModeCommandResult result, int fallbackCount)
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("A", 20), Result = result };
            var adapter = new Adapter();
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            scope.Keyboard.InputModeAdapter = adapter;
            await scope.DrainAsync();
            Click(scope.Keyboard);
            Assert.Equal(1, backend.Invocations);
            Assert.Equal(fallbackCount, adapter.Invocations);
            Assert.True(backend.Bounds.Width > 0);
        });
    }

    /// <summary>
    /// Preserves Japanese caps over native icons and updates them only from interpretable
    /// observations.
    /// </summary>
    [Fact]
    public Task JapaneseOverrideSurvivesClickRefreshAndTracksSubsequentObservation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var profile = new SoftKeyboardInputMethod(
                new Guid("03B5835F-F03C-411B-9CE2-AA23E1171E36"), Guid.NewGuid(), 0x0411,
                "Japanese", true);
            var backend = new Backend(profile) { Button = Icon("native", 30) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            scope.Keyboard.InputModeAdapter = MicrosoftJapaneseInputModeAdapter.Default;
            Notify(scope.Keyboard, InputMethodChangeKind.ImeOpen, 1);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeConversion, 9);
            await scope.DrainAsync();
            Assert.Equal("あ", scope.Keyboard.InputModeButton?.Content);
            Assert.Contains(scope.Keyboard.CurrentLayout.Keys, key => key.Text == "ー");
            Click(scope.Keyboard);
            Assert.Equal(1, backend.Invocations);
            backend.Button = Icon("changed-native", 200);
            scope.Clock.Advance(30);
            Assert.Equal("あ", scope.Keyboard.InputModeButton?.Content);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeConversion, 11);
            scope.Clock.Advance(0);
            Assert.Equal("カ", scope.Keyboard.InputModeButton?.Content);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeConversion, 12345);
            scope.Clock.Advance(0);
            Assert.Same(backend.Button, scope.Keyboard.InputModeButton);
        });
    }

    /// <summary>
    /// Retains the committed surface during observation and rebuilds once while normal input
    /// continues.
    /// </summary>
    [Fact]
    public Task ActivationRetainsSurfaceIgnoresClicksAndPublishesLatestProfileOnce()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("old", 20) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            await scope.DrainAsync();
            var oldLayout = scope.Keyboard.CurrentLayout;
            var next = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(), 0x0409, "Next", true);
            var rebuilds = 0;
            var layoutDescriptor = System.ComponentModel.DependencyPropertyDescriptor.FromProperty(
                SoftKeyboard.CurrentLayoutProperty, typeof(SoftKeyboard));
            void LayoutChanged(object? sender, EventArgs args)
            {
                rebuilds++;
            }

            layoutDescriptor.AddValueChanged(scope.Keyboard, LayoutChanged);
            backend.Profile = next;
            backend.Button = Icon("new", 200);
            Activate(scope.Keyboard, next);
            Notify(scope.Keyboard, InputMethodChangeKind.ImeOpen, 1);
            scope.Keyboard.RefreshInputMethods();
            scope.Keyboard.RefreshLayout();
            Click(scope.Keyboard);
            Assert.Equal(0, backend.Invocations);
            Assert.True(scope.Keyboard.SendActionText("x"));
            Assert.Contains("x", scope.Input.Text);
            Assert.Same(oldLayout, scope.Keyboard.CurrentLayout);
            Assert.Same(scope.Method, scope.Keyboard.ActiveInputMethod);
            scope.Clock.Advance(100);
            Assert.Same(next, scope.Keyboard.ActiveInputMethod);
            Assert.Equal("new", scope.Keyboard.InputModeButton?.Id);
            var layout = scope.Keyboard.CurrentLayout;
            scope.Clock.Advance(500);
            Assert.Same(layout, scope.Keyboard.CurrentLayout);
            Assert.Equal(1, rebuilds);
            layoutDescriptor.RemoveValueChanged(scope.Keyboard, LayoutChanged);
        });
    }

    /// <summary>
    /// Rejects the delayed click refresh after an editor or source is replaced.
    /// </summary>
    [Fact]
    public Task ClickRefreshIsCanceledWhenEditorOrSourceChanges()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("A", 20) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            await scope.DrainAsync();
            Click(scope.Keyboard);
            var reads = backend.Reads;
            scope.Keyboard.Target = scope.OtherEditor;
            scope.Clock.Advance(30);
            Assert.Equal(reads, backend.Reads);
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(new Backend(scope.Method));
            scope.Clock.Advance(100);
            Assert.Null(scope.Keyboard.InputModeButton);
        });
    }

    /// <summary>
    /// Defers activation cleanup beyond the native callback and rejects superseded generations.
    /// </summary>
    [Fact]
    public void ActivationCleanupRunsOutsideNotificationAndCanceledGenerationsNeverRun()
    {
        var clock = new ManualInputObservationScheduler();
        var cleanups = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => { }, () => cleanups++);
        coordinator.Begin();
        var firstGeneration = coordinator.Generation;
        Assert.True(coordinator.IsObserving);
        Assert.Equal(0, cleanups);
        coordinator.Begin();
        Assert.NotEqual(firstGeneration, coordinator.Generation);
        clock.Advance(0);
        Assert.Equal(1, cleanups);
        coordinator.Begin();
        coordinator.Cancel();
        clock.Advance(1000);
        Assert.Equal(1, cleanups);
    }

    /// <summary>
    /// Restarts the click deadline while allowing independent notification-driven refreshes.
    /// </summary>
    [Fact]
    public void RepeatedClickResetsShortDeadlineAndKeepsNotificationRefresh()
    {
        var clock = new ManualInputObservationScheduler();
        var publications = 0;
        using var coordinator = new InputMethodObservationCoordinator(clock, () => publications++);
        coordinator.Begin();
        clock.Advance(100);
        coordinator.AfterClick();
        clock.Advance(20);
        coordinator.AfterClick();
        coordinator.Observe(StateChange(InputMethodChangeKind.ImeOpen, 1));
        clock.Advance(0);
        Assert.Equal(2, publications);
        clock.Advance(29);
        Assert.Equal(2, publications);
        clock.Advance(1);
        Assert.Equal(3, publications);
        clock.Advance(100);
        Assert.Equal(3, publications);
    }

    /// <summary>
    /// Rechecks the actual profile at execution time and cancels pending work when unloaded.
    /// </summary>
    [Fact]
    public Task ClickDetectsSilentProfileChangeAndUnloadCancelsObservation()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Button = Icon("old", 20) };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            await scope.DrainAsync();
            var oldButton = scope.Keyboard.InputModeButton;
            backend.Profile = new SoftKeyboardInputMethod(Guid.NewGuid(), Guid.NewGuid(),
                0x0409, "Silently changed", true);
            backend.Button = Icon("new", 200);
            Click(scope.Keyboard);
            Assert.Equal(0, backend.Invocations);
            Assert.Same(oldButton, scope.Keyboard.InputModeButton);
            scope.Clock.Advance(99);
            Assert.Same(oldButton, scope.Keyboard.InputModeButton);
            scope.Clock.Advance(1);
            Assert.Same(backend.Button, scope.Keyboard.InputModeButton);
            Click(scope.Keyboard);
            var reads = backend.Reads;
            scope.Window.Close();
            scope.Clock.Advance(1000);
            Assert.Equal(reads, backend.Reads);
        });
    }

    /// <summary>
    /// Keeps explicit presentation overrides independent from command availability.
    /// </summary>
    [Fact]
    public Task ExplicitOverrideCanPresentWithoutPromisingAnExecutableCommand()
    {
        return WpfApplicationTestHost.RunAsync(async () =>
        {
            using var scope = new SoftKeyboardRegressionScope();
            var backend = new Backend(scope.Method) { Result = SoftKeyboardInputModeCommandResult.Unavailable };
            var cap = new SoftKeyboardInputModeButton("appearance", "X", "Extension appearance");
            var adapter = new Adapter
            {
                Contribution = new SoftKeyboardInputModeContribution(button: cap, overridePlatformButton: true),
            };
            scope.Keyboard.InputMethodSource = new WindowsSoftKeyboardInputMethodSource(backend);
            scope.Keyboard.InputModeAdapter = adapter;
            await scope.DrainAsync();
            Assert.Same(cap, scope.Keyboard.InputModeButton);
            Click(scope.Keyboard);
            Assert.Equal(0, adapter.Invocations);
            scope.Clock.Advance(30);
            Assert.Same(cap, scope.Keyboard.InputModeButton);
        });
    }

    private static SoftKeyboardKeyControl ModeCap(SoftKeyboard keyboard)
    {
        keyboard.UpdateLayout();
        return Assert.Single(Descendants(keyboard).OfType<SoftKeyboardKeyControl>(),
            cap => cap.Key?.Kind == SoftKeyboardKeyKind.SwitchInputMode);
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static void Click(SoftKeyboard keyboard)
    {
        typeof(SoftKeyboard).GetMethod("SwitchInputMode", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(keyboard, null);
    }

    private static void Notify(SoftKeyboard keyboard, InputMethodChangeKind kind, int? value = null)
    {
        keyboard.OnSessionInputMethodChanged(StateChange(kind, value));
    }

    /// <summary>
    /// Supplies partial samples to exercise each field's merge behavior independently.
    /// Native callbacks supply both attempted fields in a single change.
    /// </summary>
    private static InputMethodChange StateChange(InputMethodChangeKind kind, int? value)
    {
        return new InputMethodChange(kind, State: new SoftKeyboardInputObservation(
            kind == InputMethodChangeKind.ImeOpen, value,
            kind == InputMethodChangeKind.ImeConversion, value));
    }

    private static void Activate(SoftKeyboard keyboard, SoftKeyboardInputMethod profile)
    {
        keyboard.OnSessionInputMethodChanged(new InputMethodChange(InputMethodChangeKind.Profile,
            new InputProcessorProfileChange(1, profile.LanguageId, profile.ClassId,
                new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31"), profile.ProfileId, IntPtr.Zero, true)));
    }

    private static SoftKeyboardInputModeButton Icon(string id, byte value)
    {
        var image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null,
            new byte[] { value, value, value, 255 }, 4);
        image.Freeze();
        return new SoftKeyboardInputModeButton(id, image, "Mode command");
    }

    private sealed class Backend(SoftKeyboardInputMethod profile) :
        ISoftKeyboardInputMethodBackend, ISoftKeyboardInputModeButtonBackend,
        ISoftKeyboardInputStateBackend
    {
        internal SoftKeyboardInputMethod Profile { get; set; } = profile;
        internal SoftKeyboardInputModeButton? Button { get; set; }
        internal SoftKeyboardInputModeCommandResult Result { get; set; } = SoftKeyboardInputModeCommandResult.Accepted;
        internal int Invocations { get; private set; }
        internal int Reads { get; private set; }
        internal Rect Bounds { get; private set; }
        internal Func<bool, IntPtr, SoftKeyboardInputObservation>? StateReader { get; set; }
        internal int StateReads { get; private set; }

        /// <inheritdoc />
        public SoftKeyboardInputObservation ReadState(bool useImm, IntPtr window)
        {
            StateReads++;
            return StateReader?.Invoke(useImm, window) ?? SoftKeyboardInputObservation.Empty;
        }

        /// <inheritdoc />
        public void Refresh(ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active)
        {
            destination.Clear();
            destination.Add(Profile);
            active = Profile;
        }
        /// <inheritdoc />
        public bool TryActivate(SoftKeyboardInputMethod inputMethod)
        {
            Profile = inputMethod;
            return true;
        }
        /// <inheritdoc />
        public SoftKeyboardInputModeButton? GetInputModeButton(SoftKeyboardInputMethod inputMethod)
        {
            Reads++;
            return Button;
        }
        /// <inheritdoc />
        public SoftKeyboardInputModeCommandResult TryInvokeInputModeButton(
            SoftKeyboardInputMethod inputMethod, Rect screenBounds)
        {
            Invocations++;
            Bounds = screenBounds;
            return Result;
        }
    }

    private sealed class Adapter : ISoftKeyboardInputModeAdapter
    {
        internal int Invocations { get; private set; }
        internal SoftKeyboardInputModeContribution? Contribution { get; set; }
        /// <inheritdoc />
        public SoftKeyboardInputModeContribution GetContribution(SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputObservation observation)
        {
            return Contribution ?? new SoftKeyboardInputModeContribution(
                new SoftKeyboardInputModeButton("extension", "X", "Extension command"),
                new SoftKeyboardInputModeTransition("toggle"));
        }
        /// <inheritdoc />
        public bool TrySwitchMode(SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputObservation observation, SoftKeyboardInputModeTransition transition)
        {
            Invocations++;
            return true;
        }
    }
}
