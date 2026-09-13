// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Resolves per-control extensions and their supplemental-layout catalog.
/// </summary>
public partial class SoftKeyboard
{
    private IReadOnlyList<SoftKeyboardExtension>          _activeExtensions       = [];
    private IReadOnlyList<SoftKeyboardSupplementalLayout> _supplementalLayouts    = [];
    private IReadOnlyList<SoftKeyboardLayoutOption>       _availableLayoutOptions = [];

    /// <summary>
    /// Identifies the <see cref="ExtensionProvider"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ExtensionProviderProperty
        = DependencyProperty.Register(
            nameof(ExtensionProvider),
            typeof(ISoftKeyboardExtensionProvider),
            typeof(SoftKeyboard),
            new PropertyMetadata(EmptySoftKeyboardExtensionProvider.Default, OnExtensionProviderChanged)
        );

    /// <summary>
    /// Gets or sets the ordered, instance-local input-method extension provider.
    /// </summary>
    /// <remarks>
    /// The core default contributes no language or vendor behavior. Applications may use a
    /// composite provider to apply several independent extension packages in declaration order.
    /// </remarks>
    public ISoftKeyboardExtensionProvider ExtensionProvider
    {
        get => (ISoftKeyboardExtensionProvider)GetValue(ExtensionProviderProperty);
        set => SetValue(
            ExtensionProviderProperty,
            value ?? throw new ArgumentNullException(nameof(value))
        );
    }

    private static readonly DependencyPropertyKey ExtensionErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(ExtensionError),
            typeof(Exception),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="ExtensionError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ExtensionErrorProperty
        = ExtensionErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the most recent extension discovery or catalog error, or null after clean resolution.
    /// </summary>
    public Exception? ExtensionError => (Exception?)GetValue(ExtensionErrorProperty);

    internal IReadOnlyList<SoftKeyboardExtension> ResolveExtensions()
    {
        return _activeExtensions;
    }

    private void ResolveExtensions(SoftKeyboardLayoutContext context)
    {
        var                     errors = new List<Exception>();
        SoftKeyboardExtension[] extensions;

        try
        {
            extensions = ExtensionProvider
                    .GetExtensions(new SoftKeyboardExtensionContext(context))
                    .ToArray()
                ?? throw new InvalidOperationException(
                    "The extension provider returned a null sequence."
                );

            if (extensions.Any(static extension => extension is null))
            {
                throw new InvalidOperationException(
                    "An extension provider returned a null extension."
                );
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            errors.Add(exception);
            extensions = [];
        }

        _activeExtensions = extensions;

        var registrations = new List<SoftKeyboardSupplementalLayout>
        {
            new SoftKeyboardSupplementalLayout(
                SoftKeyboardSupplementalLayouts.Standard,
                "标准键盘",
                StandardSoftKeyboardLayoutProvider.Default
            )
        };

        var identifiers = new HashSet<string>(StringComparer.Ordinal)
        {
            SoftKeyboardSupplementalLayouts.Standard
        };

        foreach (var extension in extensions)
        {
            if (extension.SupplementalLayouts is null)
            {
                continue;
            }

            try
            {
                var supplied = extension.SupplementalLayouts.GetLayouts(context)
                    ?? throw new InvalidOperationException(
                        "A supplemental-layout provider returned a null sequence."
                    );

                foreach (var registration in supplied)
                {
                    if (registration is null)
                    {
                        throw new InvalidOperationException(
                            "A supplemental-layout provider returned a null registration."
                        );
                    }

                    bool isApplicable;
                    try
                    {
                        isApplicable = registration.IsApplicable(context);
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        errors.Add(exception);
                        continue;
                    }

                    if (!isApplicable)
                    {
                        continue;
                    }

                    if (!identifiers.Add(registration.Id))
                    {
                        errors.Add(
                            new InvalidOperationException(
                                $"The supplemental layout identifier '{registration.Id}' is duplicated."
                            )
                        );
                        continue;
                    }

                    registrations.Add(registration);
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                errors.Add(exception);
            }
        }

        _supplementalLayouts = registrations;
        _availableLayoutOptions =
        [
            new SoftKeyboardLayoutOption(
                SoftKeyboardSupplementalLayouts.Automatic,
                "自动（输入法首选）"
            ),
            .. registrations.Select(
                static registration => new SoftKeyboardLayoutOption(
                    registration.Id,
                    registration.DisplayName
                )
            )
        ];

        SetValue(
            ExtensionErrorPropertyKey,
            errors.Count switch
            {
                0 => null,
                1 => errors[0],
                _ => new AggregateException("One or more keyboard extensions failed.", errors),
            }
        );
    }

    private SoftKeyboardSupplementalLayout ResolveSupplementalLayout(string selection)
    {
        if (selection != SoftKeyboardSupplementalLayouts.Automatic)
        {
            return _supplementalLayouts.FirstOrDefault(layout => layout.Id == selection)
                ?? _supplementalLayouts[0];
        }

        return _supplementalLayouts.FirstOrDefault(static layout => layout.IsAutomaticPreferred)
            ?? _supplementalLayouts[0];
    }

    private ISoftKeyboardInputStateAdapter? ResolveInputStateAdapter()
    {
        if (InputStateAdapter is not null)
        {
            return InputStateAdapter;
        }

        return _activeExtensions.Select(static extension => extension.InputStateAdapter)
            .FirstOrDefault(static adapter => adapter is not null);
    }

    private void ReportExtensionError(Exception exception)
    {
        if (GetValue(ExtensionErrorProperty) is Exception previous)
        {
            SetValue(
                ExtensionErrorPropertyKey,
                new AggregateException("One or more keyboard extensions failed.", previous, exception)
            );
        }
        else
        {
            SetValue(ExtensionErrorPropertyKey, exception);
        }
    }

    private static void OnExtensionProviderChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)dependencyObject;

        keyboard.CancelCandidateOperations();
        keyboard.ReleaseCandidatePresentation();
        keyboard.RebuildLayout();
        keyboard.RefreshImeState();
        // Extension registrations can change available settings without changing the input state.
        keyboard.RefreshSettings();

        if (keyboard.IsLoaded && keyboard.IsOpen)
        {
            keyboard.RequestCandidateRefresh();
        }
    }
}