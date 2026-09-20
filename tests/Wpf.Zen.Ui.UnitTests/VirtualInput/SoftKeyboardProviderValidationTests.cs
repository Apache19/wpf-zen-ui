// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using NSubstitute;
using Wpf.Zen.Ui.VirtualInput.Controls;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies required providers remain non-null through every WPF property assignment path.
/// </summary>
public class SoftKeyboardProviderValidationTests
{
    /// <summary>
    /// CLR wrappers use the same validation and exception contract as direct WPF assignments.
    /// </summary>
    [Fact]
    public void ClrSetters_RejectNull()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();

            Assert.Throws<ArgumentException>(() => keyboard.CandidateSource = null!);
            Assert.Throws<ArgumentException>(() => keyboard.InputMethodSource = null!);
            Assert.Throws<ArgumentException>(() => keyboard.ExtensionProvider = null!);
            Assert.Throws<ArgumentException>(() => keyboard.SettingsProvider = null!);
        });
    }

    /// <summary>
    /// Rejected local writes preserve the existing provider; clearing restores its default.
    /// </summary>
    /// <param name="propertyName">The required provider property to exercise.</param>
    [Theory]
    [InlineData(nameof(SoftKeyboard.CandidateSource))]
    [InlineData(nameof(SoftKeyboard.InputMethodSource))]
    [InlineData(nameof(SoftKeyboard.ExtensionProvider))]
    [InlineData(nameof(SoftKeyboard.SettingsProvider))]
    public void DirectWrites_RejectNullAndPreserveProvider(string propertyName)
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var property = GetProviderProperty(propertyName);
            var defaultProvider = keyboard.GetValue(property);
            var provider = Substitute.For([property.PropertyType], []);

            Assert.NotNull(defaultProvider);
            keyboard.SetValue(property, provider);
            Assert.Same(provider, keyboard.GetValue(property));

            Assert.Throws<ArgumentException>(() => keyboard.SetValue(property, null));
            Assert.Same(provider, keyboard.GetValue(property));
            Assert.Throws<ArgumentException>(() => keyboard.SetCurrentValue(property, null));
            Assert.Same(provider, keyboard.GetValue(property));

            keyboard.ClearValue(property);
            Assert.Same(defaultProvider, keyboard.GetValue(property));
        });
    }

    /// <summary>
    /// Styles reject null providers and supply valid providers after a local value is cleared.
    /// </summary>
    /// <param name="propertyName">The required provider property to exercise.</param>
    [Theory]
    [InlineData(nameof(SoftKeyboard.CandidateSource))]
    [InlineData(nameof(SoftKeyboard.InputMethodSource))]
    [InlineData(nameof(SoftKeyboard.ExtensionProvider))]
    [InlineData(nameof(SoftKeyboard.SettingsProvider))]
    public void Styles_ValidateProviders(string propertyName)
    {
        WpfTestThread.Run(() =>
        {
            var property = GetProviderProperty(propertyName);
            var invalidStyle = new Style(typeof(SoftKeyboard));
            invalidStyle.Setters.Add(new Setter(property, null));
            Assert.Throws<ArgumentException>(() => invalidStyle.Seal());

            var provider = Substitute.For([property.PropertyType], []);
            var style = new Style(typeof(SoftKeyboard));
            style.Setters.Add(new Setter(property, provider));
            var keyboard = new SoftKeyboard { Style = style };
            Assert.Same(provider, keyboard.GetValue(property));

            keyboard.SetValue(property, property.GetMetadata(typeof(SoftKeyboard)).DefaultValue);
            keyboard.ClearValue(property);
            Assert.Same(provider, keyboard.GetValue(property));
        });
    }

    /// <summary>
    /// Null binding results use WPF fallback and later valid source updates still propagate.
    /// </summary>
    /// <param name="propertyName">The required provider property to exercise.</param>
    /// <param name="useFallback">Whether the binding supplies a fallback provider.</param>
    [Theory]
    [InlineData(nameof(SoftKeyboard.CandidateSource), false)]
    [InlineData(nameof(SoftKeyboard.CandidateSource), true)]
    [InlineData(nameof(SoftKeyboard.InputMethodSource), false)]
    [InlineData(nameof(SoftKeyboard.InputMethodSource), true)]
    [InlineData(nameof(SoftKeyboard.ExtensionProvider), false)]
    [InlineData(nameof(SoftKeyboard.ExtensionProvider), true)]
    [InlineData(nameof(SoftKeyboard.SettingsProvider), false)]
    [InlineData(nameof(SoftKeyboard.SettingsProvider), true)]
    public void Bindings_NullUsesFallbackAndValidUpdatesRecover(string propertyName, bool useFallback)
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var property = GetProviderProperty(propertyName);
            var defaultProvider = keyboard.GetValue(property);
            var provider = Substitute.For([property.PropertyType], []);
            var fallback = useFallback
                ? Substitute.For([property.PropertyType], [])
                : defaultProvider;
            var source = new ContentControl();
            var binding = new Binding(nameof(ContentControl.Content))
            {
                Source = source,
                Mode = BindingMode.OneWay,
                FallbackValue = useFallback ? fallback : DependencyProperty.UnsetValue
            };

            var expression = BindingOperations.SetBinding(keyboard, property, binding);
            Assert.Same(fallback, keyboard.GetValue(property));

            source.Content = provider;
            expression.UpdateTarget();
            Assert.Same(provider, keyboard.GetValue(property));

            source.Content = null;
            expression.UpdateTarget();
            Assert.Same(fallback, keyboard.GetValue(property));
            Assert.Same(expression, BindingOperations.GetBindingExpression(keyboard, property));

            source.Content = provider;
            expression.UpdateTarget();
            Assert.Same(provider, keyboard.GetValue(property));

            BindingOperations.ClearBinding(keyboard, property);
            Assert.Same(defaultProvider, keyboard.GetValue(property));
        });
    }

    /// <summary>
    /// Resolves the registered property without passing dispatcher-affine objects as test data.
    /// </summary>
    private static DependencyProperty GetProviderProperty(string propertyName)
    {
        return propertyName switch
        {
            nameof(SoftKeyboard.CandidateSource) => SoftKeyboard.CandidateSourceProperty,
            nameof(SoftKeyboard.InputMethodSource) => SoftKeyboard.InputMethodSourceProperty,
            nameof(SoftKeyboard.ExtensionProvider) => SoftKeyboard.ExtensionProviderProperty,
            nameof(SoftKeyboard.SettingsProvider) => SoftKeyboard.SettingsProviderProperty,
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName))
        };
    }
}
