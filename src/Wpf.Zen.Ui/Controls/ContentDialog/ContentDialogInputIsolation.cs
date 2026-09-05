// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Owns one modal constraint on an enabled property without replacing its binding, resource,
/// or animation. Multiple hosts may independently constrain the same element.
/// </summary>
internal sealed class ContentDialogInputIsolation(DependencyObject target, DependencyProperty property)
{
    private static readonly object                      MetadataLock        = new();
    private static readonly HashSet<DependencyProperty> InstalledProperties = [];

    private static readonly ConditionalWeakTable
        <
            DependencyObject, Dictionary<DependencyProperty, int>
        >
        Constraints = new();

    private static readonly FieldInfo? CoerceCallbackField =
        typeof(DependencyProperty).GetField(
            "_designerCoerceValueCallback",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private bool _active;

    internal DependencyObject Target { get; } = target;

    /// <summary>
    /// Registers ownership before coercing: IsEnabledChanged may synchronously end the host session.
    /// The host must store this lease before calling this method so that reentrant cleanup sees it.
    /// </summary>
    internal void Activate()
    {
        EnsureCoercionSupported(property);

        lock (MetadataLock)
        {
            var properties = Constraints.GetOrCreateValue(Target);

            properties.TryGetValue(property, out int count);
            properties[property] = count + 1;

            _active = true;
        }

        Target.CoerceValue(property);
    }

    /// <summary>
    /// Removes ownership before notifying WPF. Even if an application callback throws, no constraint
    /// remains registered, and the next session can establish its own independent lease.
    /// </summary>
    internal void Release()
    {
        lock (MetadataLock)
        {
            if (!_active)
            {
                return;
            }

            _active = false;

            if (Constraints.TryGetValue(Target, out var properties))
            {
                if (--properties[property] == 0)
                {
                    properties.Remove(property);
                }

                if (properties.Count == 0)
                {
                    Constraints.Remove(Target);
                }
            }
        }

        Target.CoerceValue(property);
    }

    /// <summary>
    /// Adds an instance-aware constraint after WPF's ordinary metadata coercion.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WPF provides no public per-instance coercion API. <see cref="DependencyProperty.OverrideMetadata"/>
    /// affects an entire type and cannot be used after its metadata has been consumed. Ordinary
    /// metadata coercion also skips <see cref="DependencyObject.ClearValue(DependencyProperty)"/>
    /// when a property falls back to its default value. <see cref="DependencyObject.SetCurrentValue"/>
    /// can be superseded by binding updates, while animations would compete with application clocks.
    /// </para>
    /// <para>
    /// The cached reflection target is <c>DependencyProperty._designerCoerceValueCallback</c> in
    /// WindowsBase. WPF invokes this final coercion hook even when a default value is restored.
    /// This implementation does not modify metadata: WPF first runs its original coercion, including
    /// derived-control callbacks and parent or <c>IsEnabledCore</c> checks, then invokes any existing
    /// final callback before this bridge applies the instance-specific constraint.
    /// </para>
    /// <para>
    /// The permanent bridge retains neither hosts nor elements. Weak, reference-counted entries opt
    /// individual properties into isolation. If the internal field is unavailable or cannot be written,
    /// presentation fails through <c>ShowAsync</c>'s normal cleanup path before a constraint is acquired;
    /// the dialog never silently opens without input isolation.
    /// </para>
    /// <para>
    /// This relies on WPF's final-coercion ordering rather than on a control template, and preserves
    /// normal WPF coercion semantics for <see cref="DependencyObject.SetCurrentValue"/>. Do not replace
    /// this bridge with a type-wide <c>false</c> default or an override of application metadata.
    /// </para>
    /// </remarks>
    private static void EnsureCoercionSupported(DependencyProperty property)
    {
        lock (MetadataLock)
        {
            if (InstalledProperties.Contains(property))
            {
                return;
            }

            if (CoerceCallbackField is null)
            {
                throw new NotSupportedException(
                    "This WPF version does not support content dialog input isolation."
                );
            }

            try
            {
                var original = (CoerceValueCallback?)CoerceCallbackField.GetValue(property);

                CoerceValueCallback callback = (d, value) =>
                {
                    var coerced = original is null ? value : original(d, value);
                    lock (MetadataLock)
                    {
                        return Constraints.TryGetValue(d, out var properties)
                               && properties.ContainsKey(property)
                            ? false
                            : coerced;
                    }
                };

                CoerceCallbackField.SetValue(property, callback);
            }
            catch (Exception exception) when (
                exception is MemberAccessException
                    or ArgumentException
                    or System.Security.SecurityException
            )
            {
                throw new NotSupportedException(
                    "This WPF runtime cannot establish content dialog input isolation.",
                    exception
                );
            }

            InstalledProperties.Add(property);
        }
    }
}
