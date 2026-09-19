// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Accumulates notification data and schedules bounded activation and short click refreshes.
/// </summary>
/// <remarks>
/// This class owns no native state or UI. Its consumer validates profile, source and editor
/// identity before publishing. Cancellation advances the generation even if a callback is queued.
/// Activation windows merge bursts. After a window closes, state and presentation notifications
/// still request publication without opening another activation window.
/// Pending publications can coalesce, including the language-bar icon's 30 ms settling delay.
/// </remarks>
internal sealed class InputMethodObservationCoordinator(
    IInputObservationScheduler scheduler,
    Action publish,
    Action? activationStarted = null) : IDisposable
{
    private IDisposable? _refresh;
    private IDisposable? _clickRefresh;
    private IDisposable? _activationWork;
    private long         _started;
    private long         _refreshDue;
    private long?        _presentationDue;
    private int          _generation;
    private bool         _completionAttempted;

    /// <summary>
    /// Gets the identity used to reject operations started for an earlier activation.
    /// </summary>
    internal int Generation => _generation;

    /// <summary>
    /// Gets whether the last committed surface must remain unchanged.
    /// </summary>
    internal bool IsObserving { get; private set; }

    /// <summary>
    /// Gets immutable raw data captured for the current generation.
    /// </summary>
    internal SoftKeyboardInputObservation Observation { get; private set; } = SoftKeyboardInputObservation.Empty;

    /// <summary>
    /// Begins an empty activation generation, replacing every pending refresh.
    /// </summary>
    internal void Begin()
    {
        Cancel();

        IsObserving = true;
        _started    = scheduler.Now;

        Debug.WriteLine($"[SoftKeyboard.InputMode.Observation] WINDOW started; generation={_generation}.");

        if (activationStarted is not null)
        {
            var generation = _generation;

            _activationWork = scheduler.Schedule(0, () =>
            {
                _activationWork = null;
                if (generation == _generation)
                {
                    activationStarted();
                }
            });
        }

        ScheduleRefresh(100);
    }

    /// <summary>
    /// Records TSF and IMM values in arrival order using the same open and conversion fields.
    /// </summary>
    /// <remarks>
    /// Every open/conversion notification has read both native values before entering this method.
    /// During activation, an already observed equal pair keeps the existing deadline; the
    /// first observation, including a failed read, and changed values wait another 100 ms,
    /// subject to the activation deadline.
    /// After activation, even equal values request publication without starting a new window.
    /// Language-bar notifications allow 30 ms for icon readiness and preserve longer pending
    /// activation waits. Every activation extension remains subject to the 500 ms limit.
    /// </remarks>
    internal void Observe(InputMethodChange change)
    {
        var unchanged = false;

        if (change.State is { } state
            && change.Kind is InputMethodChangeKind.ImeOpen or InputMethodChangeKind.ImeConversion)
        {
            unchanged =
                (!state.OpenObserved
                    || (Observation.OpenObserved && Observation.OpenValue == state.OpenValue))
                &&
                (!state.ConversionObserved
                    || (Observation.ConversionObserved && Observation.ConversionValue == state.ConversionValue));

            Observation = new SoftKeyboardInputObservation(
                openObserved: Observation.OpenObserved || state.OpenObserved,
                openValue: state.OpenObserved ? state.OpenValue : Observation.OpenValue,
                conversionObserved: Observation.ConversionObserved || state.ConversionObserved,
                conversionValue: state.ConversionObserved ? state.ConversionValue : Observation.ConversionValue
            );
        }
        else if (change.Kind == InputMethodChangeKind.InputModePresentation)
        {
            _presentationDue = scheduler.Now + 30;

            var due = IsObserving
                ? Math.Min(_started + 500, Math.Max(_refreshDue, _presentationDue.Value))
                : _presentationDue.Value;

            ScheduleRefresh((int)Math.Max(0, due - scheduler.Now));
            return;
        }

        if (IsObserving && unchanged)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Observation] Equal VALUE recorded; "
                + $"generation={_generation}, kind={change.Kind}, deadlineExtended=False."
            );

            return;
        }

        RequestRefresh();
    }

    /// <summary>
    /// Tries one settled fallback per generation, accepting only interpretable state.
    /// </summary>
    /// <remarks>
    /// Missing values are supplemented first. If the retained combination cannot be interpreted,
    /// a complete replacement sample can repair it. Successful existing fields otherwise win.
    /// Reads never extend the window or schedule another publication. Reentrant notifications,
    /// profile changes and editor changes invalidate the entire in-flight fallback.
    /// </remarks>
    internal void CompleteObservation(
        Func<bool, SoftKeyboardInputObservation> read,
        Func<SoftKeyboardInputObservation, bool> canInterpret,
        Func<bool> isCurrent)
    {
        if (IsObserving || _completionAttempted)
        {
            return;
        }

        _completionAttempted = true;

        var generation = _generation;
        var retained   = Observation;

        bool IsCurrent()
        {
            return generation == _generation && ReferenceEquals(retained, Observation) && isCurrent();
        }

        var interpreted = canInterpret(retained);

        if (!IsCurrent() || (interpreted && retained.OpenValue is not null
            && retained.ConversionValue is not null))
        {
            return;
        }

        // @formatter:off
        // TSF is sampled first; IMM is a second chance through the focused HWND's own context.
        foreach (var useImm in new[] { false, true })
        {
            // @formatter:on
            var sample = read(useImm);

            if (!IsCurrent())
            {
                return;
            }

            var candidate = new SoftKeyboardInputObservation(
                retained.OpenObserved || sample.OpenObserved,
                retained.OpenValue ?? sample.OpenValue,
                retained.ConversionObserved || sample.ConversionObserved,
                retained.ConversionValue ?? sample.ConversionValue
            );

            var accepted = canInterpret(candidate);
            if (!accepted && !interpreted)
            {
                candidate = sample;
                accepted  = canInterpret(candidate);
            }

            if (!IsCurrent())
            {
                return;
            }

            var applied = accepted
                && (candidate.OpenValue != retained.OpenValue
                    || candidate.ConversionValue != retained.ConversionValue);

            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Observation] FALLBACK sampled; "
                + $"generation={generation}, api={(useImm ? "IMM" : "TSF")}, "
                + $"open={sample.OpenValue}, conversion={sample.ConversionValue}, "
                + $"interpreted={accepted}, applied={applied}."
            );

            if (applied)
            {
                Observation = candidate;
                return;
            }
        }
    }

    /// <summary>
    /// Requests publication within the current activation window or asynchronously after it.
    /// </summary>
    /// <remarks>
    /// Only Begin opens an activation window. Later notifications remain eligible for refresh,
    /// including notifications received after the fixed deadline. A pending language-bar delay
    /// is retained so another notification cannot cause an early icon read.
    /// </remarks>
    internal void RequestRefresh()
    {
        var now = scheduler.Now;
        var due = Math.Max(now + (IsObserving ? 100 : 0), _presentationDue ?? now);

        if (IsObserving)
        {
            due = Math.Min(due, _started + 500);
        }

        ScheduleRefresh((int)Math.Max(0, due - now));
    }

    /// <summary>
    /// Requests a final platform refresh 30 ms after a click, even if earlier updates published.
    /// </summary>
    /// <remarks>
    /// A pending language-bar readiness delay can defer this refresh so its icon has time to
    /// become available. The final recheck merges with that notification's pending publication.
    /// </remarks>
    internal void AfterClick()
    {
        if (IsObserving)
        {
            return;
        }

        _clickRefresh?.Dispose();

        var generation = _generation;

        _clickRefresh = scheduler.Schedule(30, () =>
        {
            _clickRefresh = null;
            if (generation != _generation)
            {
                return;
            }

            // Merge the click recheck with any icon notification that is still settling.
            RequestRefresh();
        });
    }

    /// <summary>
    /// Replaces the pending publication and closes an active window when it executes.
    /// The consumer can perform a single fallback read before publishing the settled state.
    /// </summary>
    private void ScheduleRefresh(int delay)
    {
        _refresh?.Dispose();
        _refreshDue = scheduler.Now + delay;

        var generation = _generation;

        _refresh = scheduler.Schedule(delay, () =>
        {
            _refresh = null;

            if (generation != _generation)
            {
                return;
            }

            var wasObserving = IsObserving;

            IsObserving      = false;
            _presentationDue = null;

            if (wasObserving)
            {
                Debug.WriteLine(
                    "[SoftKeyboard.InputMode.Observation] WINDOW completed; "
                    + $"generation={generation}, elapsedMs={scheduler.Now - _started}, "
                    + $"open={Observation.OpenValue}, conversion={Observation.ConversionValue}."
                );
            }

            publish();
        });
    }

    /// <summary>
    /// Invalidates the generation and releases all delayed work and retained observations.
    /// </summary>
    internal void Cancel()
    {
        if (_refresh is not null || _clickRefresh is not null || IsObserving)
        {
            Debug.WriteLine(
                $"[SoftKeyboard.InputMode.Observation] Pending work CANCELED; generation={_generation}.");
        }

        _generation++;
        _activationWork?.Dispose();
        _activationWork = null;
        _refresh?.Dispose();
        _clickRefresh?.Dispose();
        _refresh             = null;
        _clickRefresh        = null;
        _presentationDue     = null;
        _refreshDue          = 0;
        _completionAttempted = false;
        IsObserving          = false;
        Observation          = SoftKeyboardInputObservation.Empty;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Cancel();
    }
}