// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Draws a passive status label and font icon with a rounded background and border.
/// </summary>
/// <remarks>
/// Text and Icon are clipped inside BorderThickness and Padding. IconSize and IconForeground
/// default to the text's FontSize and Foreground. IconPlacement selects the icon's side.
/// Icon and unwrapped text are vertically centered within
/// a shared row; content alignment positions that row as a whole and defaults to Center.
/// No child visual is created; use InfoBadge when arbitrary content or templates are required.
/// </remarks>
[ContentProperty(nameof(Text))]
public class Badge : Decorator
{
    /// <summary>
    /// Tracks the source's optional dynamic font resource independently of text typography.
    /// WPF invalidates this value when resources or the host's resource scope change.
    /// </summary>
    private static readonly DependencyProperty IconFontFamilyProperty
        = DependencyProperty.Register(
            "IconFontFamily", typeof(FontFamily), typeof(Badge),
            new FrameworkPropertyMetadata(null, OnTextChanged)
        );

    private FormattedText?  _formattedText;
    private FormattedText?  _formattedIcon;
    private StreamGeometry? _backgroundGeometry;
    private StreamGeometry? _borderGeometry;
    private Size            _geometrySize;
    private Thickness       _geometryThickness;
    private CornerRadius?   _geometryRadius;
    private bool            _geometryValid;

    static Badge()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(typeof(Badge))
        );

        FocusableProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(false, null, CoerceFalse)
        );

        IsHitTestVisibleProperty.OverrideMetadata(
            typeof(Badge),
            new UIPropertyMetadata(false, null, CoerceFalse)
        );

        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(false, null, CoerceFalse)
        );

        FlowDirectionProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(OnTextChanged)
        );

        LanguageProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(OnTextChanged)
        );

        TextOptions.TextFormattingModeProperty.OverrideMetadata(
            typeof(Badge),
            new FrameworkPropertyMetadata(OnTextChanged)
        );
    }

    /// <summary>
    /// Identifies the <see cref="Text"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextProperty
        = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(Badge),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the text to draw alongside Icon. The default is an empty string.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty
        = DependencyProperty.Register(
            nameof(Icon), typeof(FontIconSource), typeof(Badge),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender,
                OnIconChanged
            )
        );

    /// <summary>
    /// Gets or sets the immutable font icon to draw alongside Text. The default is null.
    /// </summary>
    public FontIconSource? Icon
    {
        get => (FontIconSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Identifies the shared <see cref="IconSize"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconSizeProperty
        = IconPresenter.IconSizeProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(double.NaN,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnIconSizeChanged
            )
        );

    /// <summary>
    /// Gets or sets the icon font size in DIPs. The default NaN follows FontSize.
    /// </summary>
    /// <remarks>
    /// Shares IconPresenter.IconSize, including inherited values. A concrete size affects only
    /// the icon; setting NaN restores FontSize fallback even when an ancestor supplies a size.
    /// </remarks>
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>
    /// Identifies the shared <see cref="IconForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconForegroundProperty
        = IconPresenter.IconForegroundProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
                OnIconForegroundChanged
            )
        );

    /// <summary>
    /// Gets or sets the icon brush. The default null follows Foreground.
    /// </summary>
    /// <remarks>
    /// Shares IconPresenter.IconForeground, including inherited values. A concrete brush affects
    /// only the icon. Local values take precedence over inherited presentation settings.
    /// </remarks>
    public Brush? IconForeground
    {
        get => (Brush?)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconPlacement"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconPlacementProperty
        = DependencyProperty.Register(
            nameof(IconPlacement), typeof(BadgeIconPlacement), typeof(Badge),
            new FrameworkPropertyMetadata(BadgeIconPlacement.Left,
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidIconPlacement
        );

    /// <summary>
    /// Gets or sets the icon side relative to text. The default is Left.
    /// </summary>
    /// <remarks>
    /// As with InfoBadge, Left precedes text and Right follows it in reading order.
    /// Right-to-left flow mirrors the physical sides. Single-content badges are unaffected.
    /// </remarks>
    public BadgeIconPlacement IconPlacement
    {
        get => (BadgeIconPlacement)GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IconSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconSpacingProperty
        = DependencyProperty.Register(
            nameof(IconSpacing), typeof(double), typeof(Badge),
            new FrameworkPropertyMetadata(4.0,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidIconSpacing
        );

    /// <summary>
    /// Gets or sets the gap in DIPs between icon and text layout boxes. The default is 4.
    /// </summary>
    /// <remarks>
    /// Must be finite and nonnegative. No gap is reserved when text or the icon glyph is empty.
    /// Changing the gap updates layout without rebuilding the cached text and icon shaping.
    /// </remarks>
    public double IconSpacing
    {
        get => (double)GetValue(IconSpacingProperty);
        set => SetValue(IconSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Severity"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SeverityProperty
        = DependencyProperty.Register(
            nameof(Severity), typeof(BadgeSeverity), typeof(Badge),
            new FrameworkPropertyMetadata(
                BadgeSeverity.Informational,
                FrameworkPropertyMetadataOptions.None
            ),
            IsValidSeverity
        );

    /// <summary>
    /// Gets or sets the semantic status used by style triggers. The default is Informational.
    /// </summary>
    public BadgeSeverity Severity
    {
        get => (BadgeSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Background"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BackgroundProperty
        = DependencyProperty.Register(
            nameof(Background), typeof(Brush), typeof(Badge),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the background inside the border. The default is null.
    /// </summary>
    public Brush? Background
    {
        get => (Brush?)GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="BorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BorderBrushProperty
        = DependencyProperty.Register(
            nameof(BorderBrush), typeof(Brush), typeof(Badge),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the brush for the border ring. The default is null.
    /// </summary>
    public Brush? BorderBrush
    {
        get => (Brush?)GetValue(BorderBrushProperty);
        set => SetValue(BorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="BorderThickness"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BorderThicknessProperty
        = DependencyProperty.Register(
            nameof(BorderThickness), typeof(Thickness), typeof(Badge),
            new FrameworkPropertyMetadata(
                new Thickness(),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidThickness
        );

    /// <summary>
    /// Gets or sets nonnegative border widths. The default is zero on every side.
    /// </summary>
    public Thickness BorderThickness
    {
        get => (Thickness)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Padding"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PaddingProperty
        = DependencyProperty.Register(
            nameof(Padding), typeof(Thickness), typeof(Badge),
            new FrameworkPropertyMetadata(
                new Thickness(),
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidThickness
        );

    /// <summary>
    /// Gets or sets nonnegative spacing inside the border. The default is zero.
    /// </summary>
    public Thickness Padding
    {
        get => (Thickness)GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty
        = DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius?), typeof(Badge),
            new FrameworkPropertyMetadata(null,
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidCornerRadius
        );

    /// <summary>
    /// Gets or sets each border centerline radius, as in Border. The default is null.
    /// </summary>
    /// <remarks>
    /// Null draws a capsule whose outer radius is half the smaller rendered dimension.
    /// The shape adapts to resizing, becoming circular for a square surface. An explicit zero
    /// produces square corners; other explicit values retain Border's centerline semantics.
    /// </remarks>
    public CornerRadius? CornerRadius
    {
        get => (CornerRadius?)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="HorizontalContentAlignment"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HorizontalContentAlignmentProperty
        = DependencyProperty.Register(
            nameof(HorizontalContentAlignment),
            typeof(HorizontalAlignment),
            typeof(Badge),
            new FrameworkPropertyMetadata(
                HorizontalAlignment.Center,
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidHorizontalAlignment
        );

    /// <summary>
    /// Gets or sets horizontal content alignment. Center is the default; Stretch uses Center.
    /// </summary>
    public HorizontalAlignment HorizontalContentAlignment
    {
        get => (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="VerticalContentAlignment"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VerticalContentAlignmentProperty
        = DependencyProperty.Register(
            nameof(VerticalContentAlignment),
            typeof(VerticalAlignment),
            typeof(Badge),
            new FrameworkPropertyMetadata(
                VerticalAlignment.Center,
                FrameworkPropertyMetadataOptions.AffectsRender
            ),
            IsValidVerticalAlignment
        );

    /// <summary>
    /// Gets or sets vertical content alignment. Center is the default; Stretch uses Center.
    /// </summary>
    public VerticalAlignment VerticalContentAlignment
    {
        get => (VerticalAlignment)GetValue(VerticalContentAlignmentProperty);
        set => SetValue(VerticalContentAlignmentProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Foreground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ForegroundProperty
        = TextElement.ForegroundProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                Brushes.Black,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender,
                OnForegroundChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited brush; the default is black.
    /// </summary>
    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FontFamily"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FontFamilyProperty
        = TextElement.FontFamilyProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                SystemFonts.MessageFontFamily,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited font family; the default is the system message font.
    /// </summary>
    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FontSize"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FontSizeProperty
        = TextElement.FontSizeProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                SystemFonts.MessageFontSize,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited font size in DIPs; the default is the system message font size.
    /// </summary>
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FontWeight"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FontWeightProperty
        = TextElement.FontWeightProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                FontWeights.Normal,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited text weight; the default is Normal.
    /// </summary>
    public FontWeight FontWeight
    {
        get => (FontWeight)GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FontStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FontStyleProperty
        = TextElement.FontStyleProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                FontStyles.Normal,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited text style; the default is Normal.
    /// </summary>
    public FontStyle FontStyle
    {
        get => (FontStyle)GetValue(FontStyleProperty);
        set => SetValue(FontStyleProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FontStretch"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FontStretchProperty
        = TextElement.FontStretchProperty.AddOwner(
            typeof(Badge),
            new FrameworkPropertyMetadata(
                FontStretches.Normal,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsRender,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the inherited text stretch; the default is Normal.
    /// </summary>
    public FontStretch FontStretch
    {
        get => (FontStretch)GetValue(FontStretchProperty);
        set => SetValue(FontStretchProperty, value);
    }

    /// <summary>
    /// Gets the absent child. Assigning a non-null child is not supported by this drawing element.
    /// </summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public override UIElement Child
    {
        get => null!;
        set
        {
            if (value is not null)
            {
                throw new InvalidOperationException("Badge draws Text and Icon and cannot host a child.");
            }
        }
    }

    /// <summary>
    /// Measures the cached text layout plus border widths and padding.
    /// </summary>
    protected override Size MeasureOverride(Size constraint)
    {
        var text    = EnsureText();
        var icon    = EnsureIcon();
        var size    = GetContentSize(text, icon);
        var border  = GetBorderThickness();
        var padding = Padding;
        var width   = size.Width;
        var height  = size.Height;

        width  += border.Left + border.Right + padding.Left + padding.Right;
        height += border.Top + border.Bottom + padding.Top + padding.Bottom;

        return new Size(Math.Min(width, constraint.Width), Math.Min(height, constraint.Height));
    }

    /// <summary>
    /// Reserves the final drawing surface without creating child visuals.
    /// </summary>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        return arrangeSize;
    }

    /// <summary>
    /// Draws cached border contours and the icon/text row inside the padded content rectangle.
    /// </summary>
    protected override void OnRender(DrawingContext drawingContext)
    {
        var border = GetBorderThickness();
        EnsureGeometry(border);

        if (_borderGeometry is not null && BorderBrush is not null)
        {
            drawingContext.DrawGeometry(BorderBrush, null, _borderGeometry);
        }

        if (_backgroundGeometry is not null && Background is not null)
        {
            drawingContext.DrawGeometry(Background, null, _backgroundGeometry);
        }

        var bounds  = new Rect(RenderSize);
        var inner   = BadgeGeometry.Deflate(bounds, border);
        var content = BadgeGeometry.Deflate(inner, Padding);
        var text    = EnsureText();
        var icon    = EnsureIcon();

        if ((text is null && icon is null) || content.Width <= 0 || content.Height <= 0)
        {
            return;
        }

        var size  = GetContentSize(text, icon);
        var width = size.Width;

        var x = HorizontalContentAlignment switch
        {
            HorizontalAlignment.Left  => content.Left,
            HorizontalAlignment.Right => content.Right - width,
            _                         => content.Left + (content.Width - width) / 2,
        };

        var y = VerticalContentAlignment switch
        {
            VerticalAlignment.Top    => content.Top,
            VerticalAlignment.Bottom => content.Bottom - size.Height,
            _                        => content.Top + (content.Height - size.Height) / 2,
        };

        var clip = new RectangleGeometry(content);
        clip.Freeze();
        drawingContext.PushClip(clip);

        var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
        var iconOnLeft  = IconPlacement == BadgeIconPlacement.Left;
        if (rightToLeft)
        {
            iconOnLeft = !iconOnLeft;
        }

        var iconWidth = icon?.WidthIncludingTrailingWhitespace ?? 0;
        var spacing   = text is not null && icon is not null ? IconSpacing : 0;
        if (icon is not null)
        {
            var iconX = iconOnLeft ? x : x + width - iconWidth;
            var iconY = y + (size.Height - icon.Height) / 2;
            drawingContext.DrawText(icon, new Point(iconX, iconY));
        }

        if (text is not null)
        {
            // FormattedText's RTL origin is the right edge of its own text box.
            var textX = iconOnLeft ? x + iconWidth + spacing : x;
            if (rightToLeft)
            {
                textX += text.WidthIncludingTrailingWhitespace;
            }

            var textY = y + (size.Height - text.Height) / 2;
            drawingContext.DrawText(text, new Point(textX, textY));
        }

        drawingContext.Pop();
    }

    /// <summary>
    /// Rebuilds text metrics and rounded geometry when moving between monitors.
    /// </summary>
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);

        _formattedText = null;
        _formattedIcon = null;
        _geometryValid = false;

        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>
    /// Creates text layout only when its content, typography, culture, or DPI has changed.
    /// </summary>
    private FormattedText? EnsureText()
    {
        if (_formattedText is not null)
        {
            return _formattedText;
        }

        var value = Text;
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        _formattedText = CreateText(value, typeface, FlowDirection, FontSize, Foreground);
        return _formattedText;
    }

    /// <summary>
    /// Shapes the icon independently so its font and metrics remain separate from the label.
    /// </summary>
    private FormattedText? EnsureIcon()
    {
        if (_formattedIcon is not null)
        {
            return _formattedIcon;
        }

        var icon  = Icon;
        var glyph = icon?.Glyph;
        if (icon is null || string.IsNullOrEmpty(glyph))
        {
            return null;
        }

        var resourceFont = (FontFamily?)GetValue(IconFontFamilyProperty);
        var family       = resourceFont ?? icon.FontFamily;
        var typeface     = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var size         = IconSize;
        if (double.IsNaN(size))
        {
            size = FontSize;
        }

        var foreground = IconForeground ?? Foreground;
        // todo: glyph 可能为 null
        _formattedIcon = CreateText(glyph, typeface, FlowDirection.LeftToRight, size, foreground);
        return _formattedIcon;
    }

    /// <summary>
    /// Creates a layout with independent typography and the host's culture, formatting and DPI.
    /// </summary>
    private FormattedText CreateText(
        string value, Typeface typeface, FlowDirection direction, double size, Brush foreground)
    {
        var culture    = Language.GetSpecificCulture();
        var dpi        = VisualTreeHelper.GetDpi(this);
        var formatting = TextOptions.GetTextFormattingMode(this);

        return new FormattedText(
            value, culture, direction, typeface, size, foreground, null, formatting, dpi.PixelsPerDip
        );
    }

    /// <summary>
    /// Shares row measurement between layout and drawing; empty content reserves no spacing.
    /// </summary>
    private Size GetContentSize(FormattedText? text, FormattedText? icon)
    {
        var textWidth  = text?.WidthIncludingTrailingWhitespace ?? 0;
        var iconWidth  = icon?.WidthIncludingTrailingWhitespace ?? 0;
        var textHeight = text?.Height ?? 0;
        var iconHeight = icon?.Height ?? 0;
        var spacing    = text is not null && icon is not null ? IconSpacing : 0;

        var width  = textWidth + iconWidth + spacing;
        var height = Math.Max(textHeight, iconHeight);

        return new Size(width, height);
    }

    /// <summary>
    /// Keeps geometry independent of brushes, so theme changes can reuse the frozen contours.
    /// </summary>
    private void EnsureGeometry(Thickness border)
    {
        var size   = RenderSize;
        var radius = CornerRadius;

        if (_geometryValid && _geometrySize == size && _geometryThickness == border
            && _geometryRadius == radius)
        {
            return;
        }

        BadgeGeometry.Create(size, border, radius, out _backgroundGeometry, out _borderGeometry);

        _geometrySize      = size;
        _geometryThickness = border;
        _geometryRadius    = radius;
        _geometryValid     = true;
    }

    /// <summary>
    /// Applies device-pixel rounding consistently to the layout and drawing border widths.
    /// </summary>
    private Thickness GetBorderThickness()
    {
        var border = BorderThickness;
        if (!UseLayoutRounding)
        {
            return border;
        }

        var dpi = VisualTreeHelper.GetDpi(this);

        var left   = Math.Round(border.Left * dpi.DpiScaleX) / dpi.DpiScaleX;
        var top    = Math.Round(border.Top * dpi.DpiScaleY) / dpi.DpiScaleY;
        var right  = Math.Round(border.Right * dpi.DpiScaleX) / dpi.DpiScaleX;
        var bottom = Math.Round(border.Bottom * dpi.DpiScaleY) / dpi.DpiScaleY;

        return new Thickness(left, top, right, bottom);
    }

    /// <summary>
    /// Attaches the new source's optional font resource and releases the previous expression.
    /// Third-party sources use their FontFamily directly without a library resource dependency.
    /// </summary>
    private static void OnIconChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var badge       = (Badge)sender;
        var source      = (FontIconSource?)args.NewValue;
        var resourceKey = source?.FontFamilyResourceKey;

        if (resourceKey is not null)
        {
            badge.SetResourceReference(IconFontFamilyProperty, resourceKey);
        }
        else
        {
            badge.ClearValue(IconFontFamilyProperty);
        }

        OnTextChanged(sender, args);
    }

    /// <summary>
    /// Invalidates cached shaping, including changes arriving through property inheritance.
    /// </summary>
    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var badge = (Badge)sender;

        badge._formattedText = null;
        badge._formattedIcon = null;

        badge.InvalidateMeasure();
        badge.InvalidateVisual();
    }

    /// <summary>
    /// Updates the cached brush without reshaping text or invalidating its layout metrics.
    /// </summary>
    private static void OnForegroundChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var badge = (Badge)sender;
        badge._formattedText?.SetForegroundBrush((Brush)args.NewValue);
        badge.UpdateIconForeground();
    }

    /// <summary>
    /// Invalidates only icon shaping when a local or inherited icon size changes.
    /// </summary>
    private static void OnIconSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var badge = (Badge)sender;
        badge._formattedIcon = null;
    }

    /// <summary>
    /// Applies icon-specific brush changes without rebuilding glyph metrics.
    /// </summary>
    private static void OnIconForegroundChanged(
        DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var badge = (Badge)sender;
        badge.UpdateIconForeground();
    }

    /// <summary>
    /// Resolves the current icon brush, including fallback after an override is cleared.
    /// </summary>
    private void UpdateIconForeground()
    {
        var foreground = IconForeground ?? Foreground;
        _formattedIcon?.SetForegroundBrush(foreground);
    }

    /// <summary>
    /// Restricts placement to the two supported horizontal sides.
    /// </summary>
    private static bool IsValidIconPlacement(object value)
    {
        return value is BadgeIconPlacement.Left or BadgeIconPlacement.Right;
    }

    /// <summary>
    /// Keeps the display element outside keyboard navigation and input hit testing.
    /// </summary>
    private static object CoerceFalse(DependencyObject sender, object value)
    {
        return false;
    }

    private static bool IsValidSeverity(object value)
    {
        return value is BadgeSeverity.Attention or BadgeSeverity.Informational
            or BadgeSeverity.Success or BadgeSeverity.Caution or BadgeSeverity.Critical;
    }

    /// <summary>
    /// Rejects gaps that cannot participate in finite, nonnegative layout calculations.
    /// </summary>
    private static bool IsValidIconSpacing(object value)
    {
        var spacing = (double)value;
        return IsFiniteNonnegative(spacing);
    }

    private static bool IsValidHorizontalAlignment(object value)
    {
        return value is HorizontalAlignment.Left or HorizontalAlignment.Center
            or HorizontalAlignment.Right or HorizontalAlignment.Stretch;
    }

    private static bool IsValidVerticalAlignment(object value)
    {
        return value is VerticalAlignment.Top or VerticalAlignment.Center
            or VerticalAlignment.Bottom or VerticalAlignment.Stretch;
    }

    private static bool IsValidThickness(object value)
    {
        var thickness = (Thickness)value;
        return IsFiniteNonnegative(thickness.Left) && IsFiniteNonnegative(thickness.Top)
            && IsFiniteNonnegative(thickness.Right) && IsFiniteNonnegative(thickness.Bottom);
    }

    private static bool IsValidCornerRadius(object value)
    {
        if (value is null)
        {
            return true;
        }

        var radius = (CornerRadius)value;

        return IsFiniteNonnegative(radius.TopLeft) && IsFiniteNonnegative(radius.TopRight)
            && IsFiniteNonnegative(radius.BottomLeft) && IsFiniteNonnegative(radius.BottomRight);
    }

    private static bool IsFiniteNonnegative(double value)
    {
        return value >= 0 && !double.IsInfinity(value);
    }
}