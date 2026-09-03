// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Appearance;

/// <summary>
/// Hosts Wpf.Zen.Ui's process-wide theme in WPF's internal system-theme resource table when no
/// <see cref="Application"/> exists.
/// </summary>
/// <remarks>
/// <para>
/// WPF deliberately stops implicit-style lookup at <see cref="Application.Resources"/>. That is normally the
/// correct boundary, but it leaves WinForms <c>ElementHost</c> applications without a process-wide resource scope.
/// A dictionary installed on the ElementHost root cannot replace that scope because navigation controls such as
/// <see cref="Frame"/> use <see cref="InheritanceBehavior.SkipToAppNow"/> and therefore skip resources above the
/// navigation boundary.
/// </para>
/// <para>
/// Default control styles follow a different path: WPF asks its internal <c>SystemResources</c> table for a style
/// keyed by <see cref="FrameworkElement.DefaultStyleKey"/>. This host adds an overlay at that final dictionary
/// boundary. Native WPF type keys are consequently resolved as real theme styles, while Wpf.Zen.Ui component resource
/// keys resolve from the same active Light/Dark dictionary without depending on an Application or a logical tree.
/// </para>
/// <para>
/// Every reflected member is resolved before the first mutation. Installation is transactional, is performed under
/// WPF's own theme-dictionary lock, and uses WPF's own resource invalidation routine afterward. If a future WPF
/// version changes any required internal contract, construction or installation fails and <see cref="ThemeManager"/>
/// falls back to the Application resource host instead of leaving partially modified framework state.
/// </para>
/// </remarks>
internal sealed class SystemThemeResourceHost
{
    private const BindingFlags StaticNonPublic   = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

    private readonly FieldInfo    _dictionariesField;
    private readonly FieldInfo    _resourceCacheField;
    private readonly FieldInfo    _themeStyleCacheField;
    private readonly PropertyInfo _themeDictionaryLockProperty;
    private readonly MethodInfo   _findResourceInternalMethod;
    private readonly MethodInfo   _ensureDictionarySlotMethod;
    private readonly MethodInfo   _invalidateResourcesMethod;
    private readonly MethodInfo   _invalidateOnResourcesChangeMethod;
    private readonly PropertyInfo _catastrophicDictionaryChangeInfoProperty;
    private readonly PropertyInfo _themeChangeInfoProperty;
    private readonly FieldInfo    _themedDictionaryField;
    private readonly FieldInfo    _themedLoadedField;
    private readonly PropertyInfo _isThemeDictionaryProperty;
    private readonly MethodInfo   _themeStyleCacheClearMethod;

    private object?             _presentationFrameworkSlot;
    private object?             _wpfUiSlot;
    private ResourceDictionary? _presentationFrameworkBaseDictionary;
    private ResourceDictionary? _installedPresentationFrameworkOverlay;
    private ResourceDictionary? _installedWpfUiOverlay;

    private SystemThemeResourceHost()
    {
        var presentationFramework = typeof(FrameworkElement).Assembly;

        var systemResourcesType = presentationFramework.GetType(
            "System.Windows.SystemResources",
            throwOnError: true
        )!;

        var resourceDictionariesType = GetRequiredNestedType(
            systemResourcesType,
            "ResourceDictionaries"
        );

        _dictionariesField    = GetRequiredField(systemResourcesType, "_dictionaries", StaticNonPublic);
        _resourceCacheField   = GetRequiredField(systemResourcesType, "_resourceCache", StaticNonPublic);
        _themeStyleCacheField = GetRequiredField(systemResourcesType, "_themeStyleCache", StaticNonPublic);
        _themeDictionaryLockProperty = GetRequiredProperty(
            systemResourcesType,
            "ThemeDictionaryLock",
            StaticNonPublic
        );
        _findResourceInternalMethod = GetRequiredMethod(
            systemResourcesType,
            "FindResourceInternal",
            StaticNonPublic,
            typeof(object)
        );
        _ensureDictionarySlotMethod = GetRequiredMethod(
            systemResourcesType,
            "EnsureDictionarySlot",
            StaticNonPublic,
            typeof(Assembly)
        );
        _invalidateResourcesMethod = GetRequiredMethod(
            systemResourcesType,
            "InvalidateResources",
            StaticNonPublic,
            typeof(bool)
        );

        // SystemResources exposes only two notifications: an operating-system theme change and a system
        // colors/settings change. Neither describes Wpf.Zen.Ui replacing an arbitrary palette dictionary. Reuse WPF's
        // own dictionary-change tree walk with its broadest ResourceChangeInfo so DynamicResource expressions inside
        // an unchanged ThemeStyle and ControlTemplate are invalidated without replacing either object.
        var treeWalkHelperType = presentationFramework.GetType(
            "System.Windows.TreeWalkHelper",
            throwOnError: true
        )!;
        var resourcesChangeInfoType = presentationFramework.GetType(
            "System.Windows.ResourcesChangeInfo",
            throwOnError: true
        )!;
        _invalidateOnResourcesChangeMethod = GetRequiredMethod(
            treeWalkHelperType,
            "InvalidateOnResourcesChange",
            StaticNonPublic,
            typeof(FrameworkElement),
            typeof(FrameworkContentElement),
            resourcesChangeInfoType
        );
        _catastrophicDictionaryChangeInfoProperty = GetRequiredProperty(
            resourcesChangeInfoType,
            "CatastrophicDictionaryChangeInfo",
            StaticNonPublic
        );
        _themeChangeInfoProperty = GetRequiredProperty(
            resourcesChangeInfoType,
            "ThemeChangeInfo",
            StaticNonPublic
        );

        _themedDictionaryField = GetRequiredField(
            resourceDictionariesType,
            "_themedDictionary",
            InstanceNonPublic
        );
        _themedLoadedField = GetRequiredField(
            resourceDictionariesType,
            "_themedLoaded",
            InstanceNonPublic
        );
        _isThemeDictionaryProperty = GetRequiredProperty(
            typeof(ResourceDictionary),
            "IsThemeDictionary",
            InstanceNonPublic
        );

        var themeStyleCacheType = _themeStyleCacheField.FieldType;
        _themeStyleCacheClearMethod = GetRequiredMethod(
            themeStyleCacheType,
            nameof(IDictionary.Clear),
            BindingFlags.Instance | BindingFlags.Public
        );
    }

    /// <summary>
    /// Attempts to resolve the complete reflection contract without modifying WPF state.
    /// </summary>
    /// <param name="host">Receives a usable host when all required internals are available.</param>
    /// <returns><see langword="true"/> when the system-theme backend can be attempted.</returns>
    internal static bool TryCreate(out SystemThemeResourceHost? host)
    {
        try
        {
            host = new SystemThemeResourceHost();
            return true;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            TraceReflectionFailure("resolve", exception);
            host = null;
            return false;
        }
    }

    /// <summary>
    /// Installs <paramref name="activeResources"/> as the current process-wide WPF theme overlay.
    /// </summary>
    /// <param name="activeResources">
    /// The fully composed Wpf.Zen.Ui theme dictionary selected by explicit theme-manager initialization.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when installation and invalidation succeeded; otherwise <see langword="false"/>.
    /// On failure the method restores the framework dictionaries before returning.
    /// </returns>
    internal bool TryApply(ResourceDictionary activeResources)
    {
        if (activeResources is null)
        {
            throw new ArgumentNullException(nameof(activeResources));
        }

        SlotState presentationFrameworkState = default;
        SlotState wpfUiState                 = default;
        var       stateCaptured              = false;

        try
        {
            // PresentationFramework's real Windows theme dictionary must be loaded before the overlay is composed,
            // so use normal resource lookup for that assembly. Do not use the same trick for Wpf.Zen.Ui: its ThemeInfo
            // declares ThemeDictionaryLocation.None, which makes a lookup miss continue into Generic.xaml. Loading
            // Generic.xaml merely to allocate the slot can strand dispatcher-affine values on the first STA that
            // configures ThemeManager and later break otherwise valid multi-STA hosts.
            var presentationFrameworkSlot = EnsureLoadedDictionarySlot(
                typeof(TextBox).Assembly,
                typeof(TextBox)
            );

            var themeLock = _themeDictionaryLockProperty.GetValue(null)
                            ?? throw new InvalidOperationException("WPF returned a null theme dictionary lock.");
            object wpfUiSlot;
            lock (themeLock)
            {
                // EnsureDictionarySlot is the precise private primitive needed here: it allocates only the assembly's
                // ResourceDictionaries record. WPF's source documents that callers must hold ThemeDictionaryLock.
                // Reflecting it avoids both Generic.xaml loading and duplicating the framework's slot bookkeeping.
                wpfUiSlot = EnsureDictionarySlotWithoutLoading(typeof(ThemeVariant).Assembly);
            }

            var currentFrameworkDictionary =
                _themedDictionaryField.GetValue(presentationFrameworkSlot) as ResourceDictionary;
            var frameworkThemeSlotWasReset = !ReferenceEquals(
                currentFrameworkDictionary,
                _installedPresentationFrameworkOverlay
            );

            // A real WM_THEMECHANGED clears every themed slot. When that has happened, EnsureDictionarySlot above
            // has just loaded the new Windows dictionary and it becomes the next overlay's base. During an ordinary
            // Wpf.Zen.Ui theme switch the current value is our overlay, so retain the previously captured system base.
            if (frameworkThemeSlotWasReset)
            {
                _presentationFrameworkBaseDictionary = currentFrameworkDictionary;
            }

            if (_presentationFrameworkBaseDictionary is null)
            {
                throw new InvalidOperationException(
                    "WPF did not load the PresentationFramework themed resource dictionary."
                );
            }

            var presentationFrameworkOverlay = CreateThemeOverlay(
                _presentationFrameworkBaseDictionary,
                activeResources
            );
            var wpfUiOverlay = CreateThemeOverlay(activeResources);

            lock (themeLock)
            {
                presentationFrameworkState = CaptureSlotState(presentationFrameworkSlot);
                wpfUiState = CaptureSlotState(wpfUiSlot);
                stateCaptured = true;

                SetThemedDictionary(
                    presentationFrameworkSlot,
                    presentationFrameworkOverlay,
                    loaded: true
                );
                SetThemedDictionary(wpfUiSlot, wpfUiOverlay, loaded: true);
                ClearSystemResourceCaches();
            }

            if (frameworkThemeSlotWasReset)
            {
                // First installation replaces Windows' native ThemeStyles, and WM_THEMECHANGED can later replace the
                // base dictionary underneath this overlay. Those are genuine style transitions and must re-evaluate
                // ThemeStyle once. Use the direct tree walk rather than SystemResources.InvalidateResources: WPF's
                // latter helper only queues ThemeChangeInfo for the initiating Dispatcher, while this process-wide
                // overlay may already be serving presentation sources on several STA threads.
                InvalidateThemeStyleResources();

                // A ThemeChangeInfo walk is not sufficient when a Wpf.Zen.Ui control was created before explicit
                // theme-manager activation. Its ThemeStyle comes from Generic.xaml both before and after installation, so
                // WPF retains that Style object and may leave DynamicResource expressions inside it associated with
                // Generic.xaml's built-in Light dictionary. Replaying an ordinary dictionary change after the style
                // transition makes those expressions resolve against the newly installed system overlay. Without
                // this second walk, the first Dark switch updates direct resource references but leaves values such
                // as FluentWindow.Background and template brushes Light until the next palette switch.
                InvalidatePaletteResources();
            }
            else
            {
                // Light/Dark is a palette replacement, not an operating-system theme transition. A ThemeChangeInfo
                // walk re-evaluates ThemeStyle and can destroy every generated template subtree. Catastrophic
                // dictionary invalidation instead refreshes DynamicResource dependents within the existing style and
                // template, preserving VisualState clocks, focus, Popup state, and control-owned template state.
                InvalidatePaletteResources();
            }

            _presentationFrameworkSlot = presentationFrameworkSlot;
            _wpfUiSlot = wpfUiSlot;
            _installedPresentationFrameworkOverlay = presentationFrameworkOverlay;
            _installedWpfUiOverlay = wpfUiOverlay;
            return true;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            if (stateCaptured)
            {
                TryRestoreCapturedState(presentationFrameworkState, wpfUiState);
            }

            TraceReflectionFailure("install", exception);
            return false;
        }
    }

    /// <summary>
    /// Removes an installed overlay and restores WPF's dictionaries before switching to the Application backend.
    /// </summary>
    internal void Restore()
    {
        if (_installedPresentationFrameworkOverlay is null
            || _installedWpfUiOverlay is null
            || _presentationFrameworkSlot is null
            || _wpfUiSlot is null)
        {
            return;
        }

        try
        {
            var themeLock = _themeDictionaryLockProperty.GetValue(null)
                            ?? throw new InvalidOperationException("WPF returned a null theme dictionary lock.");

            lock (themeLock)
            {
                if (ReferenceEquals(
                        _themedDictionaryField.GetValue(_presentationFrameworkSlot),
                        _installedPresentationFrameworkOverlay
                    ))
                {
                    SetThemedDictionary(
                        _presentationFrameworkSlot,
                        _presentationFrameworkBaseDictionary,
                        loaded: true
                    );
                }

                if (ReferenceEquals(
                        _themedDictionaryField.GetValue(_wpfUiSlot),
                        _installedWpfUiOverlay
                    ))
                {
                    // Wpf.Zen.Ui declares ThemeDictionaryLocation.None, so restoring the slot means allowing the normal
                    // generic-dictionary fallback to run again rather than claiming that a null theme was loaded.
                    SetThemedDictionary(_wpfUiSlot, null, loaded: false);
                }

                ClearSystemResourceCaches();
            }

            InvalidateThemeResources();
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            // Restore is a best-effort cleanup used only while falling back. The Application dictionary installed by
            // ThemeManager still provides the requested resources even if a future WPF version rejects this cleanup.
            TraceReflectionFailure("restore", exception);
        }
        finally
        {
            _installedPresentationFrameworkOverlay = null;
            _installedWpfUiOverlay = null;
        }
    }

    /// <summary>
    /// Replays WPF's palette-resource invalidation after a new presentation source is connected.
    /// </summary>
    /// <returns><see langword="true"/> when the invalidation request was accepted.</returns>
    /// <remarks>
    /// Controls can fetch their initial ThemeStyle while still detached from every window. WPF cannot discover those
    /// objects during the installation-time PresentationSource walk. Once an ElementHost root is loaded, replaying the
    /// official invalidation path lets Frame and the other presentation-island controls forward the change to that
    /// formerly detached content.
    /// </remarks>
    internal bool TryInvalidatePresentationSources()
    {
        try
        {
            InvalidatePaletteResources();
            return true;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            TraceReflectionFailure("invalidate", exception);
            return false;
        }
    }

    /// <summary>
    /// Initializes ThemeStyle for a presentation root that was constructed before the system overlay was installed.
    /// </summary>
    /// <param name="root">The newly connected root visual owned by the current dispatcher.</param>
    /// <returns><see langword="true"/> when WPF accepted the targeted invalidation.</returns>
    /// <remarks>
    /// This is intentionally a ThemeChangeInfo walk, but it is limited to the new root. Existing windows must not be
    /// retemplated merely because another ElementHost or Popup acquired an HwndSource. Wpf.Zen.Ui controls already use the
    /// assembly's stable Generic.xaml Style instances; native controls may need this one transition from Windows'
    /// original ThemeStyle to the configured Wpf.Zen.Ui native style.
    /// </remarks>
    internal bool TryInitializePresentationSource(FrameworkElement root)
    {
        if (root is null)
        {
            throw new ArgumentNullException(nameof(root));
        }

        try
        {
            var changeInfo = _themeChangeInfoProperty.GetValue(null)
                             ?? throw new InvalidOperationException("WPF returned a null theme-change descriptor.");
            InvalidateRoot(root, changeInfo);
            return true;
        }
        catch (Exception exception) when (!IsCriticalException(exception))
        {
            TraceReflectionFailure("initialize a presentation source", exception);
            return false;
        }
    }

    private ResourceDictionary CreateThemeOverlay(
        ResourceDictionary presentationFrameworkBase,
        ResourceDictionary activeResources)
    {
        var overlay = new ResourceDictionary();

        // Merged dictionaries are searched in reverse order. The active Wpf.Zen.Ui dictionary therefore overrides native
        // type keys supplied by PresentationFramework while every untouched WPF control keeps its original theme style.
        overlay.MergedDictionaries.Add(presentationFrameworkBase);
        overlay.MergedDictionaries.Add(activeResources);

        // ResourceDictionary normally receives this flag only while SystemResources parses a theme BAML stream. The
        // flag seals Style/Template values and detaches them from their construction Dispatcher, which is required for
        // the same cache and cross-thread semantics as genuine framework theme dictionaries.
        _isThemeDictionaryProperty.SetValue(overlay, true);

        return overlay;
    }

    private ResourceDictionary CreateThemeOverlay(ResourceDictionary activeResources)
    {
        var overlay = new ResourceDictionary();
        overlay.MergedDictionaries.Add(activeResources);

        // The themed slot contains palette/component resources only. A missing Wpf.Zen.Ui control type key deliberately
        // falls through to the assembly's one normal Generic.xaml dictionary, keeping every ThemeStyle and Template
        // object stable for the lifetime of the process.
        _isThemeDictionaryProperty.SetValue(overlay, true);
        return overlay;
    }

    private object EnsureLoadedDictionarySlot(Assembly assembly, object lookupKey)
    {
        _ = _findResourceInternalMethod.Invoke(null, [lookupKey]);

        if (_dictionariesField.GetValue(null) is not IDictionary dictionaries)
        {
            throw new InvalidOperationException("WPF did not initialize its theme dictionary table.");
        }

        return dictionaries[assembly]
               ?? throw new InvalidOperationException(
                   $"WPF did not create a theme dictionary slot for '{assembly.GetName().Name}'."
               );
    }

    private object EnsureDictionarySlotWithoutLoading(Assembly assembly)
    {
        var slot = _ensureDictionarySlotMethod.Invoke(null, [assembly]);
        return slot
               ?? throw new InvalidOperationException(
                   $"WPF did not create a theme dictionary slot for '{assembly.GetName().Name}'."
               );
    }

    private SlotState CaptureSlotState(object slot)
    {
        return new SlotState(
            slot,
            _themedDictionaryField.GetValue(slot) as ResourceDictionary,
            _themedLoadedField.GetValue(slot) is true
        );
    }

    private void SetThemedDictionary(object slot, ResourceDictionary? dictionary, bool loaded)
    {
        _themedDictionaryField.SetValue(slot, dictionary);
        _themedLoadedField.SetValue(slot, loaded);
    }

    private void ClearSystemResourceCaches()
    {
        if (_resourceCacheField.GetValue(null) is not IDictionary resourceCache)
        {
            throw new InvalidOperationException("WPF returned an unsupported system resource cache.");
        }

        resourceCache.Clear();

        var themeStyleCache = _themeStyleCacheField.GetValue(null)
                              ?? throw new InvalidOperationException("WPF returned a null theme-style cache.");

        _ = _themeStyleCacheClearMethod.Invoke(themeStyleCache, null);
    }

    /// <summary>
    /// Invalidates arbitrary resource references without telling WPF that the operating-system theme changed.
    /// </summary>
    /// <remarks>
    /// <c>SystemResources.InvalidateResources(false)</c> uses <c>ThemeChangeInfo</c>, whose contract includes
    /// re-evaluating ThemeStyle. Wpf.Zen.Ui changes only values in the palette dictionary, so the matching WPF semantic
    /// is <c>CatastrophicDictionaryChangeInfo</c>: every resource key may have changed, while styles and templates
    /// themselves have not. PresentationSource enumeration also reaches PopupRoot and ElementHost HWND islands that
    /// are outside a normal logical-tree resource walk.
    /// </remarks>
    private void InvalidatePaletteResources()
    {
        var changeInfo = _catastrophicDictionaryChangeInfoProperty.GetValue(null)
                         ?? throw new InvalidOperationException("WPF returned a null dictionary-change descriptor.");

        InvalidatePresentationSources(changeInfo, "invalidate a secondary dispatcher palette");
    }

    /// <summary>
    /// Re-evaluates ThemeStyle across all connected presentation roots for first installation or a real Windows
    /// theme-dictionary replacement.
    /// </summary>
    private void InvalidateThemeStyleResources()
    {
        var changeInfo = _themeChangeInfoProperty.GetValue(null)
                         ?? throw new InvalidOperationException("WPF returned a null theme-change descriptor.");

        InvalidatePresentationSources(changeInfo, "invalidate a secondary dispatcher theme style");
    }

    private void InvalidatePresentationSources(object changeInfo, string secondaryDispatcherOperation)
    {
        var sources = new List<PresentationSource>();
        foreach (PresentationSource source in PresentationSource.CurrentSources)
        {
            if (!source.IsDisposed)
            {
                sources.Add(source);
            }
        }

        foreach (var source in sources)
        {
            var dispatcher = source.Dispatcher;
            if (dispatcher.CheckAccess())
            {
                InvalidatePresentationSource(source, changeInfo);
                continue;
            }

            _ = dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(() =>
                {
                    try
                    {
                        InvalidatePresentationSource(source, changeInfo);
                    }
                    catch (Exception exception) when (!IsCriticalException(exception))
                    {
                        // A process-wide SystemResources table can be consumed by several STA dispatchers. The
                        // initiating dispatcher has already completed transactionally; a later dispatcher failure is
                        // isolated and traced rather than crashing that unrelated UI thread.
                        TraceReflectionFailure(secondaryDispatcherOperation, exception);
                    }
                })
            );
        }
    }

    private void InvalidatePresentationSource(PresentationSource source, object changeInfo)
    {
        if (source.IsDisposed || source.RootVisual is not FrameworkElement root)
        {
            return;
        }

        InvalidateRoot(root, changeInfo);
    }

    private void InvalidateRoot(FrameworkElement root, object changeInfo)
    {
        _ = _invalidateOnResourcesChangeMethod.Invoke(null, [root, null, changeInfo]);
    }

    /// <summary>
    /// Uses WPF's real theme-change path only while restoring the framework's original themed dictionaries.
    /// </summary>
    private void InvalidateThemeResources()
    {
        _ = _invalidateResourcesMethod.Invoke(null, [false]);
    }

    private void TryRestoreCapturedState(
        SlotState presentationFrameworkState,
        SlotState wpfUiState)
    {
        try
        {
            var themeLock = _themeDictionaryLockProperty.GetValue(null)
                            ?? throw new InvalidOperationException("WPF returned a null theme dictionary lock.");

            lock (themeLock)
            {
                SetThemedDictionary(
                    presentationFrameworkState.Slot,
                    presentationFrameworkState.Dictionary,
                    presentationFrameworkState.Loaded
                );
                SetThemedDictionary(
                    wpfUiState.Slot,
                    wpfUiState.Dictionary,
                    wpfUiState.Loaded
                );
                ClearSystemResourceCaches();
            }

            InvalidateThemeResources();
        }
        catch (Exception restoreException) when (!IsCriticalException(restoreException))
        {
            TraceReflectionFailure("rollback", restoreException);
        }
    }

    private static Type GetRequiredNestedType(Type declaringType, string name)
    {
        return declaringType.GetNestedType(name, BindingFlags.Public | BindingFlags.NonPublic)
               ?? throw new MissingMemberException(declaringType.FullName, name);
    }

    private static FieldInfo GetRequiredField(Type declaringType, string name, BindingFlags flags)
    {
        return declaringType.GetField(name, flags)
               ?? throw new MissingFieldException(declaringType.FullName, name);
    }

    private static PropertyInfo GetRequiredProperty(
        Type         declaringType,
        string       name,
        BindingFlags flags)
    {
        return declaringType.GetProperty(name, flags)
               ?? throw new MissingMemberException(declaringType.FullName, name);
    }

    private static MethodInfo GetRequiredMethod(
        Type          declaringType,
        string        name,
        BindingFlags  flags,
        params Type[] parameterTypes)
    {
        return declaringType.GetMethod(name, flags, binder: null, parameterTypes, modifiers: null)
               ?? throw new MissingMethodException(declaringType.FullName, name);
    }

    private static bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException
            or StackOverflowException
            or AccessViolationException;
    }

    private static void TraceReflectionFailure(string operation, Exception exception)
    {
        var failure = exception;
        if (exception is TargetInvocationException { InnerException: { } innerException })
        {
            failure = innerException;
        }

        Trace.TraceWarning(
            "Wpf.Zen.Ui could not {0} the applicationless SystemResources theme host and will use the "
            + "Application resource host instead. {1}: {2}",
            operation,
            failure.GetType().Name,
            failure.Message
        );
    }

    private readonly record struct SlotState(
        object              Slot,
        ResourceDictionary? Dictionary,
        bool                Loaded
    );
}