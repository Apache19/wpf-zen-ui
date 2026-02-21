using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Wpf.Ui.Extensions;

[MarkupExtensionReturnType(typeof(object))]
public sealed class LibraryInitializerExtension : MarkupExtension
{
    static LibraryInitializerExtension()
    {
        LibraryInitializer.EnsureInitialized();
    }

    public override object? ProvideValue(IServiceProvider serviceProvider)
    {
        return null;
    }
}
