// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;
using System.Windows.Markup;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Selects an existing palette brush using the templated control's Interaction.State.
/// </summary>
/// <remarks>
/// Use on Brush dependency properties inside a ControlTemplate. By default the palette comes
/// from Interaction.Palette on the templated parent. Set Palette to share a separate palette
/// for a stroke or foreground. Bindings observe individual entries so resource replacement,
/// palette replacement, and changes to unfrozen palettes update the target automatically.
/// This extension adds no visual elements and does not mutate or clone brushes.
/// </remarks>
[MarkupExtensionReturnType(typeof(Brush))]
public sealed class InteractionBrushExtension : MarkupExtension
{
    /// <summary>
    /// Gets or sets an explicit palette. Null uses the templated parent's attached palette.
    /// </summary>
    public InteractionBrushSet? Palette { get; set; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new MultiBinding
        {
            Mode      = BindingMode.OneWay,
            Converter = BrushConverter.Instance,
        };

        var statePath = new PropertyPath("(0)", Interaction.StateProperty);

        binding.Bindings.Add(new Binding
        {
            Path           = statePath,
            RelativeSource = RelativeSource.TemplatedParent,
            Mode           = BindingMode.OneWay,
        });

        AddBrushBinding(binding, nameof(InteractionBrushSet.Normal));
        AddBrushBinding(binding, nameof(InteractionBrushSet.PointerOver));
        AddBrushBinding(binding, nameof(InteractionBrushSet.Pressed));
        AddBrushBinding(binding, nameof(InteractionBrushSet.Disabled));
        return binding.ProvideValue(serviceProvider);
    }

    // Observe each DP rather than only the palette reference: theme resources can replace
    // an entry without replacing its containing palette.
    private void AddBrushBinding(MultiBinding target, string member)
    {
        var binding = new Binding
        {
            Mode = BindingMode.OneWay
        };

        if (Palette is { } palette)
        {
            binding.Source = palette;
            binding.Path   = new PropertyPath(member);
        }
        else
        {
            binding.RelativeSource = RelativeSource.TemplatedParent;
            binding.Path           = new PropertyPath("(0)." + member, Interaction.PaletteProperty);
        }

        target.Bindings.Add(binding);
    }

    private sealed class BrushConverter : IMultiValueConverter
    {
        internal static readonly BrushConverter Instance = new();

        /// <inheritdoc />
        public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var state = values[0] is InteractionState current ? current : InteractionState.Normal;
            var index = state switch
            {
                InteractionState.PointerOver => 2,
                InteractionState.Pressed     => 3,
                InteractionState.Disabled    => 4,
                _                            => 1,
            };

            var brush  = values[index] as Brush;
            var normal = values[1] as Brush;

            return brush ?? normal;
        }

        /// <inheritdoc />
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("Interaction brushes are one-way template bindings.");
        }
    }
}