# Popup shadow

`PopupShadow.PopupShadowChrome` renders a popup shadow in a separate transparent HWND. Use it when the popup content should keep its real size and the shadow should follow the popup as a faithful background window.

This is an alternative to `OverflowTolerantPopup`. Use one approach or the other:

- `OverflowTolerantPopup` keeps the shadow inside the popup content and allows the transparent effect margin to overflow before WPF nudges visible content.
- `PopupShadow.PopupShadowChrome` keeps the popup content free of any shadow margin and creates a detached background window sized from the popup HWND plus `ShadowThickness`.

When using `PopupShadow.PopupShadowChrome`, the popup's first `Border` is used as the source for corner radius, border thickness, and padding. The shadow window draws its own neutral gray border inset by `0.5` device-independent pixels and applies the configured `DropShadowEffect`, so the visible popup border is not doubled by another transparent border edge.

The shadow starts transparent to avoid a first-frame flash. `AnimationMode` controls how it reacts to WPF `PopupAnimation`:

- `DynamicContentBounds` is the default. During slide or scroll animations, it tracks the popup content's currently visible bounds and fades the shadow in.
- `DeferredFadeIn` follows fade animations, but waits for slide or scroll animations to finish before fading the shadow in.

```xml
<Popup
    AllowsTransparency="True"
    IsOpen="{Binding IsOpen}"
    Placement="Bottom"
    controls:PopupShadow.PopupShadowChrome="{StaticResource FlyoutShadowChrome}">
    <Border
        Background="{DynamicResource AcrylicBackgroundFillColorDefaultBrush}"
        BorderBrush="{DynamicResource SurfaceStrokeColorFlyoutBrush}"
        BorderThickness="1"
        CornerRadius="8">
        <!-- Popup content without shadow margin or DropShadowEffect. -->
    </Border>
</Popup>
```

```xml
<controls:PopupShadowChrome
    x:Key="FlyoutShadowChrome"
    ShadowThickness="30"
    BlurRadius="20"
    Direction="270"
    AnimationMode="DynamicContentBounds"
    Opacity="0.135"
    ShadowDepth="10"
    Color="#202020" />
```

The same attached property can be set on `ContextMenu`, because WPF displays a context menu through a popup HWND.
