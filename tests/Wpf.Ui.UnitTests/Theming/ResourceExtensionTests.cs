// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Markup;
using Wpf.Ui.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.UnitTests.Theming;

public class ResourceExtensionTests
{
    private static readonly (Type DynamicType, Type StaticType, Type TokenType)[] ExtensionFamilies =
    [
        (typeof(DynamicStyleExtension), typeof(StaticStyleExtension), typeof(StyleToken)),
        (typeof(DynamicBrushExtension), typeof(StaticBrushExtension), typeof(BrushToken)),
        (typeof(DynamicColorExtension), typeof(StaticColorExtension), typeof(ColorToken)),
        (typeof(DynamicValueExtension), typeof(StaticValueExtension), typeof(ValueToken)),
        (typeof(DynamicTemplateExtension), typeof(StaticTemplateExtension), typeof(TemplateToken)),
    ];

    [Fact]
    public void EveryPublishedToken_MapsToExactlyOnePublishedKey()
    {
        AssertKeyCoverage(
            typeof(StyleToken),
            typeof(StyleKeys),
            token => new DynamicStyleExtension((StyleToken)token).ResourceKey,
            token => new StaticStyleExtension((StyleToken)token).ResourceKey
        );
        AssertKeyCoverage(
            typeof(BrushToken),
            typeof(BrushKeys),
            token => new DynamicBrushExtension((BrushToken)token).ResourceKey,
            token => new StaticBrushExtension((BrushToken)token).ResourceKey
        );
        AssertKeyCoverage(
            typeof(ColorToken),
            typeof(ColorKeys),
            token => new DynamicColorExtension((ColorToken)token).ResourceKey,
            token => new StaticColorExtension((ColorToken)token).ResourceKey
        );
        AssertKeyCoverage(
            typeof(ValueToken),
            typeof(ValueKeys),
            token => new DynamicValueExtension((ValueToken)token).ResourceKey,
            token => new StaticValueExtension((ValueToken)token).ResourceKey
        );
        AssertKeyCoverage(
            typeof(TemplateToken),
            typeof(TemplateKeys),
            token => new DynamicTemplateExtension((TemplateToken)token).ResourceKey,
            token => new StaticTemplateExtension((TemplateToken)token).ResourceKey
        );
    }

    [Fact]
    public void Extensions_ExposeEnumTypedXamlContractsForTokenCompletion()
    {
        foreach (var (dynamicType, staticType, tokenType) in ExtensionFamilies)
        {
            AssertExtensionContract(dynamicType, tokenType, typeof(DynamicResourceExtension));
            AssertExtensionContract(staticType, tokenType, typeof(StaticResourceExtension));
        }
    }

    [Fact]
    public void NoneToken_IsRejectedByEveryExtension()
    {
        Action[] constructors =
        [
            () => _ = new DynamicStyleExtension(StyleToken.None),
            () => _ = new StaticStyleExtension(StyleToken.None),
            () => _ = new DynamicBrushExtension(BrushToken.None),
            () => _ = new StaticBrushExtension(BrushToken.None),
            () => _ = new DynamicColorExtension(ColorToken.None),
            () => _ = new StaticColorExtension(ColorToken.None),
            () => _ = new DynamicValueExtension(ValueToken.None),
            () => _ = new StaticValueExtension(ValueToken.None),
            () => _ = new DynamicTemplateExtension(TemplateToken.None),
            () => _ = new StaticTemplateExtension(TemplateToken.None),
        ];

        foreach (var constructor in constructors)
        {
            Assert.Throws<ArgumentOutOfRangeException>(constructor);
        }
    }

    [Fact]
    public void TokensWithFrameworkSpecificKeyNames_MapToTheirExpectedKeys()
    {
        Assert.Same(
            BrushKeys.SystemColorWindowTextBrush,
            new DynamicBrushExtension(BrushToken.SystemWindowText).ResourceKey
        );
        Assert.Same(
            ColorKeys.SystemColorWindowTextColor,
            new DynamicColorExtension(ColorToken.SystemWindowText).ResourceKey
        );
        Assert.Same(
            StyleKeys.DynamicScrollPageStyle,
            new StaticStyleExtension(StyleToken.DynamicScrollPage).ResourceKey
        );
        Assert.Same(
            TemplateKeys.DefaultValidationTemplate,
            new StaticTemplateExtension(TemplateToken.DefaultValidation).ResourceKey
        );
    }

    private static void AssertKeyCoverage(
        Type tokenType,
        Type keysType,
        Func<object, object> getDynamicKey,
        Func<object, object> getStaticKey
    )
    {
        var tokens = Enum.GetValues(tokenType).Cast<object>().Skip(1).ToArray();
        var publishedKeys = keysType
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(property => property.PropertyType == typeof(ComponentResourceKey))
            .Select(property => Assert.IsType<ComponentResourceKey>(property.GetValue(null)))
            .ToHashSet();
        var mappedKeys = new HashSet<ComponentResourceKey>();

        foreach (var token in tokens)
        {
            var dynamicKey = Assert.IsType<ComponentResourceKey>(getDynamicKey(token));
            var staticKey = Assert.IsType<ComponentResourceKey>(getStaticKey(token));

            // Both lookup lifetimes must share one authoritative token-to-key mapping. Otherwise a token
            // could silently target different resources merely because a caller changed Static to Dynamic.
            Assert.Same(dynamicKey, staticKey);
            Assert.True(mappedKeys.Add(dynamicKey), $"Token '{token}' maps to a duplicate resource key.");
        }

        Assert.Equal(publishedKeys.Count, tokens.Length);
        Assert.True(
            publishedKeys.SetEquals(mappedKeys),
            $"The {tokenType.Name} mapping and {keysType.Name} public key surface have diverged."
        );
    }

    private static void AssertExtensionContract(Type extensionType, Type tokenType, Type baseType)
    {
        Assert.True(baseType.IsAssignableFrom(extensionType));

        var tokenProperty = extensionType.GetProperty(nameof(DynamicStyleExtension.Token));
        Assert.NotNull(tokenProperty);
        Assert.Equal(tokenType, tokenProperty!.PropertyType);

        var constructorArgument = tokenProperty.GetCustomAttribute<ConstructorArgumentAttribute>();
        Assert.NotNull(constructorArgument);
        Assert.Equal("token", constructorArgument!.ArgumentName);

        var contentProperty = extensionType.GetCustomAttribute<ContentPropertyAttribute>();
        Assert.NotNull(contentProperty);
        Assert.Equal(tokenProperty.Name, contentProperty!.Name);

        var constructor = Assert.Single(extensionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        var parameter = Assert.Single(constructor.GetParameters());
        Assert.Equal(tokenType, parameter.ParameterType);
    }
}
