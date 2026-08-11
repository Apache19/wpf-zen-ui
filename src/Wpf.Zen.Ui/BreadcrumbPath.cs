// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Represents a XAML-declarable complete semantic path for a navigated page.
/// </summary>
/// <remarks>
/// <para>
/// The collection is a <see cref="Freezable"/> so bindings used by its segments receive the page's
/// inheritance context even though the path is not part of the visual tree. Changes to segment
/// bindings invalidate the active location without creating a journal entry.
/// </para>
/// <para>
/// Use this type for exceptional fixed or bindable paths. Application-wide default hierarchy
/// belongs in <see cref="NavigationMap"/>; complex runtime rules still belong in
/// <see cref="INavigationLocationSource"/>.
/// </para>
/// </remarks>
public sealed class BreadcrumbPath : FreezableCollection<BreadcrumbPathSegment>
{
    /// <summary>Initializes a new instance of the <see cref="BreadcrumbPath"/> class.</summary>
    public BreadcrumbPath() { }

    /// <summary>Creates an empty instance used by WPF's clone and freeze infrastructure.</summary>
    /// <returns>A new <see cref="BreadcrumbPath"/> that preserves the derived collection type.</returns>
    /// <remarks>
    /// <see cref="FreezableCollection{T}"/> creates a plain base collection by default. Overriding
    /// this factory is therefore required: resource cloning, <see cref="Freezable.Clone"/>, and
    /// <see cref="Freezable.GetAsFrozen"/> must not silently lose the attached property's declared
    /// <see cref="BreadcrumbPath"/> type.
    /// </remarks>
    protected override Freezable CreateInstanceCore() => new BreadcrumbPath();

    internal IReadOnlyList<BreadcrumbSegment> CreateSnapshot()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("A declared breadcrumb path cannot be empty.");
        }

        var result = new BreadcrumbSegment[Count];
        for (int index = 0; index < Count; index++)
        {
            BreadcrumbPathSegment declaration = this[index]
                ?? throw new InvalidOperationException(
                    "A declared breadcrumb path cannot contain null segments."
                );
            result[index] = declaration.CreateSegment();
        }

        return Array.AsReadOnly(result);
    }
}
