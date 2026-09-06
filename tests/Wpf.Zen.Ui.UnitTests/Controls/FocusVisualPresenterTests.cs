// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using UiButton = Wpf.Zen.Ui.Controls.Button;
using UiToggleSwitch = Wpf.Zen.Ui.Controls.ToggleSwitch;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class FocusVisualPresenterTests
{
    [Fact]
    public void LoadedPresenter_DiscoversTargetFromAdornerAncestor()
    {
        Run(() =>
        {
            var target    = new Border { Width = 80, Height = 30 };
            var decorator = new AdornerDecorator { Child = target };
            var window = new Window
            {
                Width         = 160,
                Height        = 100,
                ShowInTaskbar = false,
                Content       = decorator,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var layer   = Assert.IsType<AdornerLayer>(AdornerLayer.GetAdornerLayer(target));
                var adorner = new PresenterAdorner(target);

                layer.Add(adorner);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.Loaded);

                Assert.Same(target, adorner.Presenter.AdornedElement);

                layer.Remove(adorner);
                window.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.Loaded);

                Assert.Null(adorner.Presenter.AdornedElement);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ControlWithoutCornerRadius_UsesFallbackValue()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiToggleSwitch();

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(4), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void UnconfiguredConventionalCornerRadius_UsesFallbackValue()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiButton();

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(4), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void ConventionalCornerRadius_IsResolvedAndUpdated()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiButton { CornerRadius = new CornerRadius(8, 7, 6, 5) };

            presenter.SetAdornedElement(target);

            Assert.Equal(target.CornerRadius, presenter.CornerRadius);

            target.CornerRadius = new CornerRadius(3);

            Assert.Equal(new CornerRadius(3), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void ExplicitZeroConventionalCornerRadius_RemainsSquare()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiButton { CornerRadius = new CornerRadius(0) };

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(0), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void ConfiguredBorderCornerRadius_IsUsedWhenNoConventionalPropertyExists()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiToggleSwitch();
            target.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(6), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void ExplicitFocusCornerRadius_TakesPrecedenceAndCanRestoreAutomaticResolution()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new UiButton { CornerRadius = new CornerRadius(7) };
            FocusVisual.SetCornerRadius(target, new CornerRadius(2));

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(2), presenter.CornerRadius);

            FocusVisual.SetCornerRadius(target, null);

            Assert.Equal(new CornerRadius(7), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void CustomFallbackValue_IsUsedWhenNoRadiusSourceExists()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter { FallbackCornerRadius = new CornerRadius(9) };

            presenter.SetAdornedElement(new UiToggleSwitch());

            Assert.Equal(new CornerRadius(9), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    [Fact]
    public void ConventionalCornerRadius_TakesPrecedenceOverBorderAttachedValue()
    {
        Run(() =>
        {
            var presenter = new FocusVisualPresenter();
            var target    = new SeparateCornerRadiusControl { CornerRadius = new CornerRadius(7) };
            target.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));

            presenter.SetAdornedElement(target);

            Assert.Equal(new CornerRadius(7), presenter.CornerRadius);
            presenter.SetAdornedElement(null);
        });
    }

    private static void Run(Action action)
    {
        WpfTestThread.Run(action);
    }

    private sealed class SeparateCornerRadiusControl : Control
    {
        public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(SeparateCornerRadiusControl),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }
    }

    private sealed class PresenterAdorner : Adorner
    {
        public PresenterAdorner(UIElement adornedElement)
            : base(adornedElement)
        {
            Presenter = new FocusVisualPresenter();
            AddVisualChild(Presenter);
        }

        public FocusVisualPresenter Presenter { get; }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index)
        {
            return index == 0 ? Presenter : throw new ArgumentOutOfRangeException(nameof(index));
        }

        protected override Size MeasureOverride(Size constraint)
        {
            Presenter.Measure(AdornedElement.RenderSize);
            return AdornedElement.RenderSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Presenter.Arrange(new Rect(finalSize));
            return finalSize;
        }
    }
}
