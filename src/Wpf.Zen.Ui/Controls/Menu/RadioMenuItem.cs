// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a menu item that behaves like a radio button within a menu group.
/// </summary>
public class RadioMenuItem : MenuItem
{
    private static readonly Dictionary<string, List<WeakReference<RadioMenuItem>>> RegisteredGroups =
        new(StringComparer.Ordinal);

    private static readonly object RegisteredGroupsLock = new();

    private bool    _isInvokingClick;
    private string? _currentlyRegisteredGroupName;

    static RadioMenuItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RadioMenuItem),
            new FrameworkPropertyMetadata(typeof(RadioMenuItem))
        );

        IsCheckableProperty.OverrideMetadata(
            typeof(RadioMenuItem),
            new FrameworkPropertyMetadata(
                true,
                null,
                static (d, _) => !((RadioMenuItem)d)._isInvokingClick
            )
        );
    }

    public RadioMenuItem()
    {
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Identifies the <see cref="GroupName"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty GroupNameProperty =
        DependencyProperty.Register(
            nameof(GroupName),
            typeof(string),
            typeof(RadioMenuItem),
            new FrameworkPropertyMetadata(string.Empty, OnGroupNameChanged)
        );

    /// <summary>
    /// Gets or sets the name that determines which <see cref="RadioMenuItem"/> controls are mutually exclusive.
    /// </summary>
    [DefaultValue("")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    protected override void OnClick()
    {
        SetCurrentValue(IsCheckedProperty, true);

        _isInvokingClick = true;
        CoerceValue(IsCheckableProperty);

        try
        {
            base.OnClick();
        }
        finally
        {
            _isInvokingClick = false;
            CoerceValue(IsCheckableProperty);
        }
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        UpdateRadioMenuItemGroup();
        base.OnChecked(e);
    }

    private static void OnGroupNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var radioMenuItem = (RadioMenuItem)d;
        var groupName     = e.NewValue as string ?? string.Empty;

        radioMenuItem.UpdateRegisteredGroup(groupName);

        if (radioMenuItem.IsChecked)
        {
            radioMenuItem.UpdateRadioMenuItemGroup();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateRegisteredGroup(GroupName);

        if (IsChecked)
        {
            UpdateRadioMenuItemGroup();
        }
    }

    private void UpdateRegisteredGroup(string groupName)
    {
        if (string.Equals(_currentlyRegisteredGroupName, groupName, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrEmpty(_currentlyRegisteredGroupName))
        {
            Unregister(_currentlyRegisteredGroupName!, this);
        }

        if (!string.IsNullOrEmpty(groupName))
        {
            Register(groupName, this);
        }

        _currentlyRegisteredGroupName = groupName;
    }

    private static void Register(string groupName, RadioMenuItem radioMenuItem)
    {
        lock (RegisteredGroupsLock)
        {
            if (!RegisteredGroups.TryGetValue(groupName, out var elements))
            {
                elements                    = [];
                RegisteredGroups[groupName] = elements;
            }

            PurgeDead(elements, radioMenuItem);
            elements.Add(new WeakReference<RadioMenuItem>(radioMenuItem));
        }
    }

    private static void Unregister(string groupName, RadioMenuItem radioMenuItem)
    {
        lock (RegisteredGroupsLock)
        {
            if (!RegisteredGroups.TryGetValue(groupName, out var elements))
            {
                return;
            }

            PurgeDead(elements, radioMenuItem);

            if (elements.Count == 0)
            {
                RegisteredGroups.Remove(groupName);
            }
        }
    }

    private static void PurgeDead(
        List<WeakReference<RadioMenuItem>> elements,
        RadioMenuItem?                     elementToRemove
    )
    {
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            if (
                !elements[i].TryGetTarget(out var target)
             || ReferenceEquals(target, elementToRemove)
            )
            {
                elements.RemoveAt(i);
            }
        }
    }

    private void UpdateRadioMenuItemGroup()
    {
        if (!string.IsNullOrEmpty(GroupName))
        {
            UpdateNamedGroup();
            return;
        }

        UpdateSiblingGroup();
    }

    private void UpdateNamedGroup()
    {
        var currentRoot = GetRootScope(this);

        lock (RegisteredGroupsLock)
        {
            if (!RegisteredGroups.TryGetValue(GroupName, out var elements))
            {
                return;
            }

            for (int i = elements.Count - 1; i >= 0; i--)
            {
                if (!elements[i].TryGetTarget(out var menuItem))
                {
                    elements.RemoveAt(i);
                    continue;
                }

                if (
                    !ReferenceEquals(menuItem, this)
                 && menuItem.IsChecked
                 && ReferenceEquals(GetRootScope(menuItem), currentRoot)
                )
                {
                    menuItem.UncheckRadioMenuItem();
                }
            }
        }
    }

    private void UpdateSiblingGroup()
    {
        if (Parent is not DependencyObject parent)
        {
            return;
        }

        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (
                child is RadioMenuItem menuItem
             && !ReferenceEquals(menuItem, this)
             && string.IsNullOrEmpty(menuItem.GroupName)
             && menuItem.IsChecked
            )
            {
                menuItem.UncheckRadioMenuItem();
            }
        }
    }

    private void UncheckRadioMenuItem()
    {
        SetCurrentValue(IsCheckedProperty, false);
    }

    private static DependencyObject GetRootScope(RadioMenuItem radioMenuItem)
    {
        if (PresentationSource.FromDependencyObject(radioMenuItem)?.RootVisual is DependencyObject rootVisual)
        {
            return rootVisual;
        }

        DependencyObject  root    = radioMenuItem;
        DependencyObject? current = radioMenuItem;

        while ((current = LogicalTreeHelper.GetParent(current)) is not null)
        {
            root = current;
        }

        return root;
    }
}