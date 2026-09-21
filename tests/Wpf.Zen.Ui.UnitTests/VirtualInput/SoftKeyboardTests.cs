// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.AutomationPeers;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardTests
{
    [Fact]
    public void KeyModels_RejectMissingInputPayloadsButAllowLabeledWhitespace()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey, "Key", "Key"
            )
        );

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey, "Key", "Key", key: (Key)int.MaxValue
            )
        );

        Assert.Throws<ArgumentException>(
            () => new SoftKeyboardKey(
                SoftKeyboardKeyKind.Text, "Text", "Text"
            )
        );

        Assert.Throws<ArgumentException>(
            () => new SoftKeyboardKey(
                SoftKeyboardKeyKind.Text, "Text", "Text", text: string.Empty
            )
        );

        foreach (var invalid in new[]
            {
                null!, string.Empty
            })
        {
            Assert.Throws<ArgumentException>(
                () => new SoftKeyboardKey(
                    SoftKeyboardKeyKind.Text, "Text", "Text", text: "x", alternates: [invalid]
                )
            );
        }

        var whitespace = new SoftKeyboardKey(
            SoftKeyboardKeyKind.Text, "Space", "Space", text: " ", alternates: ["\t"]
        );

        Assert.Equal(" ", whitespace.Text);
        Assert.Equal("\t", Assert.Single(whitespace.Alternates));
    }

    [Theory]
    [InlineData("target")]
    [InlineData("targetRoundTrip")]
    [InlineData("closeRoundTrip")]
    [InlineData("disableRoundTrip")]
    [InlineData("hideRoundTrip")]
    public void DeferredInputModeSwitch_IsDiscardedWhenItsInputSessionEnds(string change)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var other = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(other);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();

                Assert.True(editor.Focus());
                PumpDispatcher();

                var composition = new TextComposition(
                    InputManager.Current, editor, string.Empty, TextCompositionAutoComplete.Off
                );

                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(
                    keyboard,
                    new SoftKeyboardKey(SoftKeyboardKeyKind.SwitchInputMode, "IME", "Switch input mode")
                );

                composition.Complete();

                switch (change)
                {
                    case "target":
                    case "targetRoundTrip":
                        keyboard.Target = other;
                        Assert.True(other.Focus());
                        if (change == "targetRoundTrip")
                        {
                            keyboard.Target = editor;
                            Assert.True(editor.Focus());
                PumpDispatcher();
                        }

                        break;
                    case "closeRoundTrip":
                        keyboard.Visibility = Visibility.Collapsed;
                        keyboard.Visibility = Visibility.Visible;
                        break;
                    case "disableRoundTrip":
                        keyboard.IsEnabled = false;
                        keyboard.IsEnabled = true;
                        break;
                    case "hideRoundTrip":
                        keyboard.Visibility = Visibility.Hidden;
                        keyboard.Visibility = Visibility.Visible;
                        break;
                }

                PumpDispatcher();
                Assert.True(keyboard.InputModeButton?.Id == "native");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ReplacingTargetAdapter_RebuildsScopeForTheSameEditor()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 700,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                InputScopeNameValue? scope = null;
                keyboard.LayoutProvider = new SoftKeyboardLayoutRule(_ => true, context =>
                {
                    scope = context.InputScope;
                    return new SoftKeyboardLayout([]);
                });
                keyboard.TargetAdapter = new NumberScopeAdapter();
                Assert.Same(editor, keyboard.ActiveTarget);
                Assert.Equal(InputScopeNameValue.Number, scope);
                keyboard.TargetAdapter = null;
                Assert.Equal(InputScopeNameValue.Default, scope);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class NumberScopeAdapter : ISoftKeyboardTargetAdapter
    {
        public bool IsSupported(IInputElement target) => target is System.Windows.Controls.TextBox;
        public bool CanReceiveInput(IInputElement target) => true;
        public InputScopeNameValue GetInputScope(IInputElement target) => InputScopeNameValue.Number;
        public bool TryFocus(IInputElement target) => ReferenceEquals(Keyboard.Focus(target), target);
    }

    [Fact]
    public void CustomAction_UsesGuardedInputAndExpiresAfterInvocation()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            keyboard.Target = editor;
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            var action = new CapturingKeyboardAction();
            var key = SoftKeyboardKey.FromAction(action, "Fn", "Custom input");
            var invoke = typeof(SoftKeyboard).GetMethod("InvokeKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                invoke.Invoke(keyboard, [key]);
                Assert.Equal(1, action.Calls);
                Assert.Contains("Text:custom", input.Operations);
                Assert.Throws<InvalidOperationException>(() => action.Context!.TrySendText("late"));
                editor.IsReadOnly = true;
                invoke.Invoke(keyboard, [key]);
                Assert.Equal(1, action.Calls);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class CapturingKeyboardAction : ISoftKeyboardKeyAction
    {
        internal int Calls { get; private set; }
        internal SoftKeyboardActionContext? Context { get; private set; }

        public void Execute(SoftKeyboardActionContext context)
        {
            Calls++;
            Context = context;
            Assert.True(context.TrySendText("custom"));
        }
    }

    [Fact]
    public void AsyncAction_KeepsContextUntilCompletionAndCancelsOnSessionChange()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            keyboard.Target = editor;
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            var action = new DeferredKeyboardAction();

            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                InvokeKey(keyboard, SoftKeyboardKey.FromAction(action, "Async", "Async action"));
                SoftKeyboardActionContext context = Assert.IsType<SoftKeyboardActionContext>(action.Context);
                ModifierKeys modifiers = ModifierKeys.Control
                    | ModifierKeys.Shift
                    | ModifierKeys.Alt
                    | ModifierKeys.Windows;

                Assert.True(context.TrySendKey(Key.K, modifiers));
                Assert.Contains((Key.K, modifiers), input.Chords);

                keyboard.Visibility = Visibility.Collapsed;
                Assert.True(action.CancellationToken.IsCancellationRequested);
                Assert.Throws<OperationCanceledException>(() => context.TrySendText("obsolete"));

                action.Complete();
                PumpDispatcher();
                Assert.True(action.Finished.IsCompleted);
                Assert.Throws<InvalidOperationException>(() => context.TrySendText("expired"));
                Assert.Null(keyboard.ExtensionError);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AsyncAlternateAction_ReceivesSelectedValue()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            keyboard.Target = editor;
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            var action = new CapturingAlternateAction();

            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                typeof(SoftKeyboard)
                    .GetMethod("ExecuteCustomAction", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [action, "variant"]);
                PumpDispatcher();

                Assert.Equal("variant", action.Alternative);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class DeferredKeyboardAction : IAsyncSoftKeyboardKeyAction
    {
        private readonly TaskCompletionSource<bool> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        private readonly TaskCompletionSource<bool> _finished = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        internal SoftKeyboardActionContext? Context { get; private set; }

        internal CancellationToken CancellationToken { get; private set; }

        internal Task Finished => _finished.Task;

        public async Task ExecuteAsync(
            SoftKeyboardActionContext context,
            CancellationToken cancellationToken)
        {
            Context = context;
            CancellationToken = cancellationToken;
            try
            {
                await _completion.Task.ConfigureAwait(true);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                _finished.TrySetResult(true);
            }
        }

        internal void Complete()
        {
            _completion.TrySetResult(true);
        }
    }

    private sealed class CapturingAlternateAction : IAsyncSoftKeyboardAlternateKeyAction
    {
        internal string? Alternative { get; private set; }

        public Task ExecuteAsync(
            SoftKeyboardActionContext context,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task ExecuteAlternateAsync(
            SoftKeyboardActionContext context,
            string alternative,
            CancellationToken cancellationToken)
        {
            Alternative = alternative;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void CandidateSelection_ScrollsInBothDirectionsWithoutMovingEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 640,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                foreach (var selectedIndex in new[]
                    {
                        19, 0
                    })
                {
                    keyboard.ReplaceCandidates(
                        Enumerable.Range(0, 20).Select(index =>
                            new SoftKeyboardCandidate((uint)index, $"Candidate {index}")),
                        SoftKeyboardCandidatePresentationState.Embedded,
                        selectedCandidateIndex: (uint)selectedIndex);
                    window.UpdateLayout();
                    PumpDispatcher();
                    window.UpdateLayout();

                    var items = (SoftKeyboardCandidateList)keyboard.Template.FindName("PART_Candidates", keyboard);
                    var container = (FrameworkElement)items.ItemContainerGenerator.ContainerFromIndex(selectedIndex);
                    var viewer =
                        Descendants<DynamicScrollViewer>(
                                (SoftKeyboardCandidateList)keyboard.Template.FindName("PART_Candidates", keyboard))
                            .Single();
                    var viewport = Descendants<ScrollContentPresenter>(viewer).First();
                    var bounds = container.TransformToAncestor(viewport)
                        .TransformBounds(new Rect(container.RenderSize));
                    Assert.True(viewer.ScrollableWidth > 0);
                    Assert.True(bounds.Left >= -1);
                    Assert.True(bounds.Right <= viewer.ViewportWidth + 1);
                    Assert.Same(editor, Keyboard.FocusedElement);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateList_OperatesIndependentlyAndClearsReplacedContainers()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var candidates = new ObservableCollection<SoftKeyboardCandidate>(
                Enumerable.Range(100, 20)
                    .Select(index => new SoftKeyboardCandidate((uint)index, $"Candidate {index}")));
            var list = new SoftKeyboardCandidateList
            {
                ItemsSource = candidates,
                SelectedCandidateIndex = 119
            };
            var window = new Window
            {
                Content = list,
                Width = 320,
                Height = 120,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();
                PumpDispatcher();
                window.UpdateLayout();
                var container = (SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(19);
                Assert.True(container.IsSelected);
                Assert.False(container.Focusable);
                Assert.True(Descendants<DynamicScrollViewer>(list).Single().HorizontalOffset > 0);

                var calls = 0;
                list.CandidateInvoked += (_, e) =>
                {
                    calls++;
                    Assert.Same(candidates[19], e.Candidate);
                };
                container.Content = "Presentation override";
                var peer = UIElementAutomationPeer.CreatePeerForElement(container)!;
                Assert.Equal("Candidate 119", peer.GetName());
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)!).Invoke();
                PumpDispatcher();
                Assert.Equal(1, calls);
                Assert.Equal(119U, list.SelectedCandidateIndex);

                list.SelectedCandidateIndex = 100;
                window.UpdateLayout();
                PumpDispatcher();
                Assert.Same(container, list.ItemContainerGenerator.ContainerFromIndex(19));
                Assert.False(container.IsSelected);
                Assert.True(((SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(0)).IsSelected);

                list.Template = new ControlTemplate(typeof(SoftKeyboardCandidateList));
                window.UpdateLayout();
                list.ClearValue(Control.TemplateProperty);
                window.UpdateLayout();
                PumpDispatcher();
                Assert.True(((SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(0)).IsSelected);

                list.Visibility = Visibility.Collapsed;
                list.SelectedCandidateIndex = 119;
                window.UpdateLayout();
                PumpDispatcher();
                list.Visibility = Visibility.Visible;
                window.UpdateLayout();
                PumpDispatcher();
                window.UpdateLayout();
                var viewer = Descendants<DynamicScrollViewer>(list).Single();
                Assert.True(viewer.HorizontalOffset > 0);

                // Collapse only the replaceable items panel: the list remains loaded and visible, so
                // recovery must complete the pending request through the ordinary layout retry.
                var host = Descendants<StackPanel>(list)
                    .Single(panel => ItemsControl.GetItemsOwner(panel) == list);
                host.Visibility = Visibility.Collapsed;
                list.SelectedCandidateIndex = 100;
                window.UpdateLayout();
                PumpDispatcher();
                host.Visibility = Visibility.Visible;
                window.UpdateLayout();
                PumpDispatcher();
                window.UpdateLayout();
                Assert.Equal(0, viewer.HorizontalOffset);

                var current = (SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                candidates.Clear();
                window.UpdateLayout();
                PumpDispatcher();
                Assert.False(current.IsSelected);
                current.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, current));
                Assert.Equal(1, calls);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateList_AllowsCustomPanelAndDerivedContainers()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var candidates = new ObservableCollection<SoftKeyboardCandidate>
            {
                new(1, "one"),
                new(2, "two"),
            };
            var list = new TrackingCandidateList
            {
                ItemsSource = candidates,
                ItemsPanel = new ItemsPanelTemplate(
                    new FrameworkElementFactory(typeof(WrapPanel))
                ),
            };
            var window = new Window
            {
                Content = list,
                Width = 100,
                ShowInTaskbar = false
            };

            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();

                Assert.Single(
                    Descendants<WrapPanel>(list),
                    panel => ItemsControl.GetItemsOwner(panel) == list
                );
                Assert.Equal(2, list.Prepared);
                Assert.All(
                    candidates,
                    candidate => Assert.IsType<TrackingCandidateItem>(
                        list.ItemContainerGenerator.ContainerFromItem(candidate)
                    )
                );

                candidates.RemoveAt(0);
                window.UpdateLayout();
                Assert.Equal(1, list.Cleared);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class TrackingCandidateList : SoftKeyboardCandidateList
    {
        internal int Prepared { get; private set; }

        internal int Cleared { get; private set; }

        protected override SoftKeyboardCandidateItem CreateCandidateContainer()
        {
            return new TrackingCandidateItem();
        }

        protected override void OnCandidateContainerPrepared(
            SoftKeyboardCandidateItem container,
            SoftKeyboardCandidate candidate)
        {
            Prepared++;
        }

        protected override void OnCandidateContainerClearing(
            SoftKeyboardCandidateItem container,
            SoftKeyboardCandidate candidate)
        {
            Cleared++;
        }
    }

    private sealed class TrackingCandidateItem : SoftKeyboardCandidateItem
    {
    }

    [Fact]
    public void CandidateHighlightUpdate_PreservesItemsAndContainers()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var window = new Window
            {
                Content = keyboard,
                Width = 640,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                keyboard.ReplaceCandidates(
                    [new SoftKeyboardCandidate(40, "first"), new SoftKeyboardCandidate(41, "second")],
                    SoftKeyboardCandidatePresentationState.Embedded,
                    selectedCandidateIndex: 40);
                window.UpdateLayout();
                var list = (SoftKeyboardCandidateList)keyboard.Template.FindName("PART_Candidates", keyboard);
                var first = keyboard.Candidates[0];
                var container = (SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(0);

                keyboard.ReplaceCandidates(
                    [new SoftKeyboardCandidate(40, "first"), new SoftKeyboardCandidate(41, "second")],
                    SoftKeyboardCandidatePresentationState.Embedded,
                    selectedCandidateIndex: 41);
                window.UpdateLayout();
                Assert.Same(first, keyboard.Candidates[0]);
                Assert.Same(container, list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.False(container.IsSelected);
                Assert.True(((SoftKeyboardCandidateItem)list.ItemContainerGenerator.ContainerFromIndex(1)).IsSelected);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Defaults_UseStandardVisibilityAndRemainNonFocusable()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            Assert.Equal(Visibility.Visible, keyboard.Visibility);
            Assert.False(keyboard.Focusable);
            Assert.False(keyboard.IsTabStop);
            Assert.Equal(4, keyboard.RowSpacing);
            Assert.Equal(4, keyboard.ColumnSpacing);
            Assert.Same(SoftKeyboardLayout.Empty, keyboard.CurrentLayout);
            Assert.Empty(keyboard.AvailableSupplementalLayouts);
        });
    }

    [Fact]
    public void LayoutProvider_UsesInputScopeAndPage()
    {
        WpfTestThread.Run(() =>
        {
            ISoftKeyboardLayoutProvider provider = DefaultSoftKeyboardLayoutProvider.Default;
            var phone = Assert.IsType<SoftKeyboardLayout>(provider.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.TelephoneNumber,
                    SoftKeyboardPages.Automatic,
                    SoftKeyboardShiftState.Off
                )
            ));
            var symbols = Assert.IsType<SoftKeyboardLayout>(provider.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.Default,
                    SoftKeyboardPages.Symbols,
                    SoftKeyboardShiftState.Off
                )
            ));

            Assert.Contains(phone.Keys, key => key.Text == "+");
            Assert.Contains(phone.Keys, key => key.Text == "0");
            Assert.DoesNotContain(phone.Keys, key => key.Kind == SoftKeyboardKeyKind.SwitchInputMode);
            Assert.Contains(phone.Keys, key => key.Kind == SoftKeyboardKeyKind.InputMethods);
            Assert.Contains(symbols.Keys, key => key.Text == "$");
        });
    }

    /// <summary>
    /// Every physical character family consumes one-shot Shift but preserves locked Shift.
    /// </summary>
    /// <param name="character">
    /// A letter, digit or OEM character key sent through the control.
    /// </param>
    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.D3)]
    [InlineData(Key.D0)]
    [InlineData(Key.OemMinus)]
    [InlineData(Key.OemComma)]
    [InlineData(Key.Oem7)]
    [InlineData(Key.Oem102)]
    public void ShiftOnce_IsConsumedByOneCharacterAndCapsLockPersists(Key character)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;

            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            Assert.True(editor.Focus());
            PumpDispatcher();

            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            Assert.Equal(SoftKeyboardShiftState.Once, keyboard.ShiftState);
            var characterKey = new SoftKeyboardKey(
                SoftKeyboardKeyKind.VirtualKey, character.ToString(), character.ToString(), character);
            InvokeKey(keyboard, characterKey);

            Assert.Equal((character, true), Assert.Single(input.VirtualKeys));
            Assert.Equal(SoftKeyboardShiftState.Off, keyboard.ShiftState);

            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
            Assert.Equal(SoftKeyboardShiftState.Locked, keyboard.ShiftState);
            InvokeKey(keyboard, characterKey);
            Assert.Equal(SoftKeyboardShiftState.Locked, keyboard.ShiftState);

            window.Close();
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Backspace_WithLatchedShift_SendsUnmodifiedKeyAndPreservesShift(bool locked, bool composing)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            TextComposition? composition = null;
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
                if (locked)
                {
                    InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.Shift));
                }

                var expectedShift = locked ? SoftKeyboardShiftState.Locked : SoftKeyboardShiftState.Once;
                Assert.Equal(expectedShift, keyboard.ShiftState);
                if (composing)
                {
                    composition = new TextComposition(
                        InputManager.Current, editor, string.Empty, TextCompositionAutoComplete.Off);
                    _ = TextCompositionManager.StartComposition(composition);
                }

                // Repeated requests must also remain unmodified, without inserting Escape or
                // consuming the casing latch when the IME owns the current text transaction.
                InvokeKey(keyboard, FindKey(keyboard, Key.Back));
                InvokeKey(keyboard, FindKey(keyboard, Key.Back));

                Assert.Equal([(Key.Back, false), (Key.Back, false)], input.VirtualKeys);
                Assert.Equal(expectedShift, keyboard.ShiftState);
                Assert.Same(editor, Keyboard.FocusedElement);

                composition?.Complete();
                composition = null;
                InvokeKey(keyboard, FindKey(keyboard, Key.A));
                Assert.Equal((Key.A, true), input.VirtualKeys.Last());
                Assert.Equal(locked ? SoftKeyboardShiftState.Locked : SoftKeyboardShiftState.Off, keyboard.ShiftState);
            }
            finally
            {
                composition?.Complete();
                window.Close();
            }
        });
    }

    [Fact]
    public void HeldNavigationKey_RepeatsWithoutMovingFocusToKeyboard()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            Assert.True(editor.Focus());
            PumpDispatcher();

            window.UpdateLayout();
            var cap = FindCap(keyboard, Key.Left);
            cap.BeginPress();
            cap.AdvanceHold();
            cap.AdvanceHold();

            Assert.Equal([(Key.Left, false), (Key.Left, false)], input.VirtualKeys);
            Assert.Same(editor, Keyboard.FocusedElement);
            keyboard.Visibility = Visibility.Collapsed;
            window.Close();
        });
    }

    [Fact]
    public void GeneratedLetterKey_InvokeRoutesInputWithoutTakingEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();

            SoftKeyboardKey letter = FindKey(keyboard, Key.A);
            SoftKeyboardKeyControl button = Assert.Single(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                item => ReferenceEquals(item.DataContext, letter)
            );
            button.Invoke();

            Assert.Equal((Key.A, false), Assert.Single(input.VirtualKeys));
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void EmbeddedHwndSource_UsesEditorHostForGuardedInputWithoutWpfWindow()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            input.RequireNonZeroForegroundHost = true;
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor,
                TargetAdapter = new NumberScopeAdapter()
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            var owner = new Window
            {
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };

            try
            {
                owner.Show();
                var ownerHandle = new WindowInteropHelper(owner).Handle;
                using var source = new HwndSource(new HwndSourceParameters("Embedded soft keyboard")
                {
                    ParentWindow = ownerHandle,
                    Width = 600,
                    Height = 400,
                    WindowStyle = 0x50000000
                })
                {
                    RootVisual = root
                };
                PumpDispatcher();

                Assert.True(keyboard.IsLoaded);
                Assert.Null(Window.GetWindow(keyboard));
                Assert.True(editor.Focus());
                PumpDispatcher();

                InvokeKey(
                    keyboard,
                    new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, "1", "Digit 1", key: Key.D1)
                );

                Assert.Equal((Key.D1, false), Assert.Single(input.VirtualKeys));
                Assert.Equal(source.Handle, input.LastForegroundHost);
                Assert.Equal(source.Handle, keyboard.HostWindowHandle);
            }
            finally
            {
                owner.Close();
            }
        });
    }

    [Fact]
    public void StandaloneHwndSource_UsesPresentationRootAsDefaultTargetScope()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out RecordingInputBackend input, out _, out _);
            input.RequireNonZeroForegroundHost = true;
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor,
                TargetAdapter = new NumberScopeAdapter()
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            using var source = new HwndSource(new HwndSourceParameters("Standalone soft keyboard")
            {
                Width = 600,
                Height = 400
            })
            {
                RootVisual = root
            };
            PumpDispatcher();

            Assert.Null(Window.GetWindow(keyboard));
            Assert.Same(editor, keyboard.ActiveTarget);
            Assert.True(
                (bool)typeof(SoftKeyboard)
                    .GetMethod("IsWindowForeground", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, null)!
            );
            Assert.Equal(source.Handle, input.LastForegroundHost);
            Assert.Equal(source.Handle, keyboard.HostWindowHandle);
        });
    }

    // Real SendInput requires an interactive foreground desktop, just like the installed-IME test.
    [Fact(Explicit = true)]
    public void GeneratedLetterKey_InvokeInsertsThroughWindowsInputStream()
    {
        WpfApplicationTestHost.Run(() =>
        {
            Key receivedKey = Key.None;
            var editor = new System.Windows.Controls.TextBox();
            editor.PreviewKeyDown += (_, eventArgs) => receivedKey = eventArgs.Key;
            var keyboard = new SoftKeyboard
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            Assert.True(window.Activate());
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();
            Assert.True(
                (bool)typeof(SoftKeyboard)
                    .GetMethod("IsWindowForeground", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, null)!
            );

            SoftKeyboardKey letter = FindKey(keyboard, Key.A);
            SoftKeyboardKeyControl button = Assert.Single(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                item => ReferenceEquals(item.DataContext, letter)
            );
            button.Invoke();
            PumpDispatcher();

            Assert.Equal(Key.A, receivedKey);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact(Explicit = true)]
    public void InstalledMicrosoftPinyin_PublishesCandidatesToEmbeddedSurfaceWhenAvailable()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            Assert.True(window.Activate());
            window.UpdateLayout();
            PumpDispatcher();

            SoftKeyboardInputMethod? pinyin = keyboard.InputMethods.FirstOrDefault(inputMethod =>
                inputMethod.ClassId == new Guid("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E")
            );
            if (pinyin is null)
            {
                window.Close();
                return;
            }

            Assert.True(editor.Focus());
            PumpDispatcher();
            typeof(SoftKeyboard)
                .GetMethod("ActivateInputMethod", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [pinyin]);
            PumpDispatcher();
            if (keyboard.InputModeButton?.Id == "direct")
            {
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                PumpDispatcher();
            }

            InvokeKey(keyboard, FindKey(keyboard, Key.N));
            InvokeKey(keyboard, FindKey(keyboard, Key.I));
                PumpDispatcher();

            object? textEditor = typeof(TextBoxBase)
                .GetField("_textEditor", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(editor);
            object? textStore = textEditor?.GetType()
                .GetField("_textstore", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(textEditor);

            Assert.True(
                keyboard.CandidatePresentationState == SoftKeyboardCandidatePresentationState.Embedded,
                $"Candidate state was {keyboard.CandidatePresentationState}; text was '{editor.Text}'; "
                + $"Input mode was {keyboard.InputModeButton}; conversion was {InputMethod.Current.ImeConversionMode}; "
                + $"language was {InputLanguageManager.Current.CurrentInputLanguage.Name}; "
                + $"active profile was {keyboard.ActiveInputMethod?.DisplayName ?? "<none>"}; "
                + $"WPF TextStore was {(textStore is null ? "missing" : "attached")}."
            );
            Assert.NotEmpty(keyboard.Candidates);

            keyboard.ShowPage(SoftKeyboardPages.Symbols);
                PumpDispatcher();
            Assert.Equal(string.Empty, editor.Text);
            Assert.False(keyboard.HasCandidates);

            InvokeKey(keyboard, keyboard.CurrentLayout.Keys.First(key => key.Text == "1"));
            PumpDispatcher();
            Assert.Equal("1", editor.Text);

            keyboard.ShowPage(SoftKeyboardPages.Letters);
            InvokeKey(keyboard, FindKey(keyboard, Key.N));
            InvokeKey(keyboard, FindKey(keyboard, Key.I));
                PumpDispatcher();

            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, keyboard.CandidatePresentationState);
            Assert.NotEmpty(keyboard.Candidates);
            Assert.True(
                keyboard.CanGoToNextCandidatePage,
                $"Microsoft Pinyin exposed only {keyboard.CandidatePageCount} candidate page(s) for 'ni'."
            );
            uint pageBeforeNavigation = keyboard.CandidatePageNumber;
            Wpf.Zen.Ui.Controls.Button nextPage = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_NextCandidatePageButton"
            );
            nextPage.RaiseEvent(
                new RoutedEventArgs(
                    ButtonBase.ClickEvent,
                    nextPage
                )
            );
            PumpDispatcher();

            Assert.Equal(pageBeforeNavigation + 1U, keyboard.CandidatePageNumber);
            Assert.NotEmpty(keyboard.Candidates);

            SoftKeyboardCandidate candidate = keyboard.Candidates[0];
            typeof(SoftKeyboard)
                .GetMethod("CommitCandidate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, [candidate]);
            PumpDispatcher();

            Assert.Equal("1" + candidate.Text, editor.Text);
            Assert.False(keyboard.HasCandidates);
            window.Close();
        });
    }

    [Fact]
    public void NativeInputStructure_MatchesWindowsAbiOnCurrentArchitecture()
    {
        int expectedSize = IntPtr.Size == 8 ? 40 : 28;

        Assert.Equal(
            expectedSize,
            System.Runtime.InteropServices.Marshal.SizeOf<Windows.Win32.UI.Input.KeyboardAndMouse.INPUT>()
        );
    }

    [Fact]
    public void InputModeKey_SwitchesModeWithoutTakingEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();
            Assert.True(keyboard.InputModeButton?.Id == "native");

            SoftKeyboardKeyControl button = Assert.Single(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                item => item.DataContext is SoftKeyboardKey { Kind: SoftKeyboardKeyKind.SwitchInputMode }
            );
            button.Invoke();
            PumpDispatcher();
            keyboard.OnSessionInputMethodStateInvalidated();            PumpDispatcher();

            Assert.False(keyboard.InputModeButton?.Id == "native");
            Assert.Equal(SoftKeyboardPages.Automatic, keyboard.Page);
            Assert.Same(editor, Keyboard.FocusedElement);

            InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
            PumpDispatcher();
            keyboard.OnSessionInputMethodStateInvalidated();            PumpDispatcher();
            Assert.True(keyboard.InputModeButton?.Id == "native");
            window.Close();
        });
    }

    [Fact]
    public void ImeKey_IsAbsentAndCannotActivateAnotherTipFromDirectKeyboardLayout()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _, out FakeInputMethodBackend inputMethods);
            inputMethods.UseDirectKeyboardLayout();
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();

            Assert.DoesNotContain(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                item => item.DataContext is SoftKeyboardKey { Kind: SoftKeyboardKeyKind.SwitchInputMode }
            );
            InvokeKey(
                keyboard,
                new SoftKeyboardKey(SoftKeyboardKeyKind.SwitchInputMode, "A", "Switch input mode")
            );

            Assert.Equal(0, inputMethods.ActivationCount);
            Assert.Null(keyboard.InputModeButton);
            Assert.Null(keyboard.ActiveInputMethod);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void HeldCharacter_OpensImmutableAccentAlternatives()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var window = new Window
            {
                Content = keyboard,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();
                var cap = FindCap(keyboard, Key.A);
                cap.BeginPress();
                cap.AdvanceHold();
                Assert.True(cap.IsAlternatePopupOpen);
                Assert.Contains("á", cap.Key!.Alternates);
                Assert.Contains("ä", cap.Key.Alternates);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ExplicitTarget_OutsideScopeIsRejected()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var scope = new Grid();
            var external = new System.Windows.Controls.TextBox();
            var window = new Window
            {
                Content = scope,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            keyboard.TargetScope = scope;
            keyboard.Target = external;

            Assert.Null(keyboard.ActiveTarget);

            scope.Children.Add(external);
            keyboard.Target = null;
            keyboard.Target = external;
            Assert.Same(external, keyboard.ActiveTarget);
            window.Close();
        });
    }

    /// <summary>
    /// Focus tracking respects the host's visibility binding for every WPF visibility value.
    /// </summary>
    /// <param name="visibility">The initial presentation selected by the host.</param>
    [Theory]
    [InlineData(Visibility.Visible)]
    [InlineData(Visibility.Hidden)]
    [InlineData(Visibility.Collapsed)]
    public void Focus_TracksSupportedTargetsWithoutChangingVisibility(Visibility visibility)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var scope = new Grid();
            var editor = new System.Windows.Controls.TextBox();
            scope.Children.Add(editor);
            var keyboard = new SoftKeyboard { TargetScope = scope };
            var state = new Border { Visibility = visibility };
            System.Windows.Data.BindingOperations.SetBinding(
                keyboard, UIElement.VisibilityProperty,
                new System.Windows.Data.Binding(nameof(UIElement.Visibility)) { Source = state });
            scope.Children.Add(keyboard);
            var window = new Window { Content = scope, ShowInTaskbar = false };
            try
            {
                window.Show();
                PumpDispatcher();

                keyboard.OnSessionFocusChanged(editor);
                Assert.Equal(visibility, keyboard.Visibility);
                Assert.Same(editor, keyboard.ActiveTarget);
                keyboard.OnSessionFocusChanged(new System.Windows.Controls.Button());
                Assert.Equal(visibility, keyboard.Visibility);

                state.Visibility = Visibility.Visible;
                Assert.True(keyboard.IsVisible);
                keyboard.OnSessionFocusChanged(editor);
                keyboard.OnSessionFocusChanged(new System.Windows.Controls.Button());
                Assert.Equal(Visibility.Visible, keyboard.Visibility);

                state.Visibility = Visibility.Collapsed;
                Assert.False(keyboard.IsVisible);
                Assert.True(System.Windows.Data.BindingOperations.IsDataBound(
                    keyboard, UIElement.VisibilityProperty));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CandidateBackend_ControlsStateAndCommitWithoutNativeTsf()
    {
        WpfTestThread.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var keyboard = new SoftKeyboard(services);
            candidates.Register(keyboard);
            candidates.Publish(keyboard, 2, 3, "first", "second");

            Assert.Equal(SoftKeyboardCandidatePresentationState.Embedded, keyboard.CandidatePresentationState);
            Assert.Equal(2, keyboard.Candidates.Count);
            Assert.True(keyboard.HasCandidates);
            Assert.Equal(2U, keyboard.CandidatePageNumber);
            Assert.Equal(3U, keyboard.CandidatePageCount);
            Assert.True(keyboard.CanGoToPreviousCandidatePage);
            Assert.True(keyboard.CanGoToNextCandidatePage);

            Assert.True(candidates.TryCommit(keyboard, keyboard.Candidates[1].Index));
            Assert.Equal(1U, candidates.LastCommittedIndex);
        });
    }

    [Fact]
    public void CandidatePagingButtons_AreTouchInvokableAndKeepEditorFocus()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 640,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();
            candidates.Publish(keyboard, 2, 3, "first", "second");
            window.UpdateLayout();
            PumpDispatcher();

            Wpf.Zen.Ui.Controls.Button previous = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_PreviousCandidatePageButton"
            );
            Wpf.Zen.Ui.Controls.Button next = Assert.Single(
                Descendants<Wpf.Zen.Ui.Controls.Button>(keyboard),
                button => button.Name == "PART_NextCandidatePageButton"
            );

            Assert.True(SoftKeyboardCommands.MoveToNextCandidatePage.CanExecute(null, keyboard));
            Assert.True(previous.IsEnabled);
            Assert.True(next.IsEnabled);
            SoftKeyboardCommands.MoveToNextCandidatePage.Execute(null, keyboard);
            Assert.Equal(1, candidates.LastPageDelta);
            Assert.Same(editor, Keyboard.FocusedElement);

            Assert.True(SoftKeyboardCommands.MoveToPreviousCandidatePage.CanExecute(null, keyboard));
            SoftKeyboardCommands.MoveToPreviousCandidatePage.Execute(null, keyboard);
            Assert.Equal(-1, candidates.LastPageDelta);
            Assert.Same(editor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void CandidateBar_ReservesHeightWhileActiveImeHasNoCandidates()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 640,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();

            var candidateBar = Assert.IsType<DockPanel>(keyboard.Template.FindName("CandidateBar", keyboard));
            var previous = Assert.IsType<Wpf.Zen.Ui.Controls.Button>(
                keyboard.Template.FindName("PART_PreviousCandidatePageButton", keyboard)
            );
            Assert.Equal(Visibility.Hidden, candidateBar.Visibility);
            // The hidden candidate surface reserves its height to avoid moving the key rows.
            Assert.True(((FrameworkElement)candidateBar.Parent).ActualHeight > 0);
            Assert.Equal(Visibility.Collapsed, previous.Visibility);
            double emptyHeight = keyboard.ActualHeight;

            candidates.Publish(keyboard, 1, 2, "candidate");
            window.UpdateLayout();
            Assert.Equal(Visibility.Visible, candidateBar.Visibility);
            Assert.Equal(Visibility.Visible, previous.Visibility);

            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            window.UpdateLayout();
            Assert.Equal(Visibility.Hidden, candidateBar.Visibility);
            Assert.Equal(emptyHeight, keyboard.ActualHeight);

            SoftKeyboardKeyControl imeToggle = Assert.Single(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                button => button.DataContext is SoftKeyboardKey { Kind: SoftKeyboardKeyKind.SwitchInputMode }
            );
            imeToggle.Invoke();
            window.UpdateLayout();
            Assert.Equal(Visibility.Hidden, candidateBar.Visibility);
            Assert.Equal(emptyHeight, keyboard.ActualHeight);
            window.Close();
        });
    }

    [Fact]
    public void EqualCandidateSnapshots_PreserveItemsForPointerClickCompletion()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                Width = 640,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            Assert.True(editor.Focus());
            PumpDispatcher();
            candidates.Publish(keyboard, 1, 1, "first", "second");
            window.UpdateLayout();
            SoftKeyboardCandidate firstItem = keyboard.Candidates[0];
            int snapshotVersion = keyboard.CandidateSnapshotVersion;
            SoftKeyboardCandidateItem firstButton = Assert.Single(
                Descendants<SoftKeyboardCandidateItem>(keyboard),
                button => ReferenceEquals(button.Candidate, firstItem)
            );

            Assert.True(editor.Focus());
            PumpDispatcher();
            candidates.Publish(keyboard, 1, 1, "first", "second");
            window.UpdateLayout();

            Assert.Same(firstItem, keyboard.Candidates[0]);
            Assert.Equal(snapshotVersion, keyboard.CandidateSnapshotVersion);
            Assert.Same(
                firstButton,
                Assert.Single(
                    Descendants<SoftKeyboardCandidateItem>(keyboard),
                    button => ReferenceEquals(button.Candidate, firstItem)
                )
            );

            // Sharing data does not make an unrelated list the keyboard's candidate surface.
            var unrelatedList = new SoftKeyboardCandidateList
            {
                ItemsSource = keyboard.Candidates
            };
            keyboard.RaiseEvent(new SoftKeyboardCandidateInvokedEventArgs(unrelatedList, firstItem));
            Assert.Null(candidates.LastCommittedIndex);
            var candidateList = (SoftKeyboardCandidateList)keyboard.Template.FindName("PART_Candidates", keyboard);
            // A different enumerable of the same current candidate objects remains a valid presentation.
            candidateList.ItemsSource = keyboard.Candidates.ToArray();
            window.UpdateLayout();
            firstButton = Descendants<SoftKeyboardCandidateItem>(candidateList)
                .Single(button => button.Candidate == firstItem);
            firstButton.Content = "Custom caption";
            firstButton.RaiseEvent(
                new RoutedEventArgs(ButtonBase.ClickEvent, firstButton)
            );
            Assert.Equal(0U, candidates.LastCommittedIndex);
            window.Close();
        });
    }

    [Fact]
    public void CollapsingKeyboard_ReleasesEmbeddedCandidatePresentation()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var window = new Window { Content = keyboard, ShowInTaskbar = false };
            try
            {
                window.Show();
                // Explicit acquisition supplies a candidate session without an editor.
                keyboard.RefreshCandidatesAsync().GetAwaiter().GetResult();
                candidates.Publish(keyboard, 1, 1, "candidate");

                var releases = candidates.ReleasePresentationCount;
                keyboard.Visibility = Visibility.Collapsed;

                Assert.Equal(releases + 1, candidates.ReleasePresentationCount);
                Assert.Empty(keyboard.Candidates);
                Assert.Equal(SoftKeyboardCandidatePresentationState.None, keyboard.CandidatePresentationState);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Local and ancestor visibility release candidates and reacquire them when shown.
    /// </summary>
    /// <param name="visibility">The hidden state applied by the host.</param>
    /// <param name="hideAncestor">Whether the host hides the keyboard's parent.</param>
    [Theory]
    [InlineData(Visibility.Collapsed, false)]
    [InlineData(Visibility.Hidden, false)]
    [InlineData(Visibility.Collapsed, true)]
    [InlineData(Visibility.Hidden, true)]
    public void HidingKeyboard_ReleasesCandidatesAndShowingItRefreshesTheBridge(
        Visibility visibility, bool hideAncestor)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var keyboardHost = new Grid();
            keyboardHost.Children.Add(keyboard);
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboardHost);
            var window = new Window
            {
                Content = root,
                Width = 640,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            candidates.Publish(keyboard, 1, 1, "candidate");
            int releasesBeforeHide = candidates.ReleasePresentationCount;
            int refreshesBeforeHide = candidates.RefreshCount;

            UIElement surface = hideAncestor ? keyboardHost : keyboard;
            surface.Visibility = visibility;
            Assert.False(keyboard.IsVisible);

            Assert.True(candidates.ReleasePresentationCount > releasesBeforeHide);
            Assert.Empty(keyboard.Candidates);

            surface.Visibility = Visibility.Visible;
            Assert.True(keyboard.IsVisible);
            window.UpdateLayout();

            Assert.True(candidates.RefreshCount > refreshesBeforeHide);
            window.Close();
        });
    }

    [Fact]
    public void FocusedEditor_RetriesCandidateBridgeAfterWpfFocusProcessing()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out FakeCandidateBackend candidates, out _);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new Grid();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.TargetScope = root;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            int refreshCountBeforeFocus = candidates.RefreshCount;

            Assert.True(editor.Focus());
                PumpDispatcher();

            Assert.Same(editor, keyboard.ActiveTarget);
            Assert.True(candidates.RefreshCount > refreshCountBeforeFocus);
            window.Close();
        });
    }

    [Fact]
    public void AutomationPeer_ExposesPaneWithoutVisibilityCommands()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var peer = new SoftKeyboardAutomationPeer(keyboard);
            Assert.Equal(AutomationControlType.Pane, peer.GetAutomationControlType());
            Assert.Null(peer.GetPattern(PatternInterface.ExpandCollapse));
            Assert.False(keyboard.Focusable);
        });
    }

    [Fact]
    public void PlatformFailureStates_DoNotDisableLayoutOrModeSwitching()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            pane.Result = SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();

            Assert.Equal(
                SoftKeyboardSystemKeyboardSuppressionState.Unavailable,
                keyboard.SystemKeyboardSuppressionState
            );

            keyboard.ShowPage(SoftKeyboardPages.Symbols);
            Assert.Equal(SoftKeyboardPages.Symbols, keyboard.Page);
            Assert.NotEmpty(keyboard.CurrentLayout.Keys);
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_RequiresOpenVisibleLoadedFocusedTargetAndRestoresConservatively()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var editor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };

            // Visibility can be bound before the control enters a visual tree. With no HWND and no
            // focused target there must be no native InputPane call.
            Assert.Equal(0, pane.HideCount);
            Assert.Equal(
                SoftKeyboardSystemKeyboardSuppressionState.NotRequested,
                keyboard.SystemKeyboardSuppressionState
            );

            var keyboardHost = new Grid();
            keyboardHost.Children.Add(keyboard);
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboardHost);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();

            Assert.True(pane.HideCount > 0);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Hidden, keyboard.SystemKeyboardSuppressionState);
            int hideCountBeforeCollapse = pane.HideCount;

            keyboardHost.Visibility = Visibility.Collapsed;

            Assert.Equal(hideCountBeforeCollapse, pane.HideCount);
            Assert.Equal(1, pane.ShowCount);
            Assert.Equal(
                SoftKeyboardSystemKeyboardSuppressionState.NotRequested,
                keyboard.SystemKeyboardSuppressionState
            );

            typeof(SoftKeyboard)
                .GetMethod("RequestSystemKeyboardHide", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, null);
            Assert.Equal(hideCountBeforeCollapse, pane.HideCount);

            keyboardHost.Visibility = Visibility.Visible;
            window.UpdateLayout();
            Assert.True(pane.HideCount > hideCountBeforeCollapse);
            Assert.Equal(SoftKeyboardSystemKeyboardSuppressionState.Hidden, keyboard.SystemKeyboardSuppressionState);

            keyboard.Visibility = Visibility.Collapsed;

            Assert.Equal(2, pane.ShowCount);
            Assert.Equal(
                SoftKeyboardSystemKeyboardSuppressionState.NotRequested,
                keyboard.SystemKeyboardSuppressionState
            );

            int hideCountAfterClose = pane.HideCount;
            typeof(SoftKeyboard)
                .GetMethod("RequestSystemKeyboardHide", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(keyboard, null);
            Assert.Equal(hideCountAfterClose, pane.HideCount);
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_DoesNotShowAfterRejectedHideOrWhileAnotherKeyboardNeedsIt()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var editor = new System.Windows.Controls.TextBox();
            var firstKeyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var secondKeyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                Target = editor
            };
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(firstKeyboard);
            root.Children.Add(secondKeyboard);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();
            Assert.True(editor.Focus());
            PumpDispatcher();
            Assert.True(pane.HideCount >= 2);

            firstKeyboard.Visibility = Visibility.Collapsed;

            Assert.Equal(0, pane.ShowCount);
            secondKeyboard.Visibility = Visibility.Collapsed;
            Assert.Equal(1, pane.ShowCount);

            pane.Result = SoftKeyboardSystemKeyboardSuppressionState.Rejected;
            firstKeyboard.Visibility = Visibility.Visible;
            Assert.Equal(
                SoftKeyboardSystemKeyboardSuppressionState.Rejected,
                firstKeyboard.SystemKeyboardSuppressionState
            );

            firstKeyboard.Visibility = Visibility.Collapsed;
            Assert.Equal(1, pane.ShowCount);
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_HidesAgainAfterFocusReturnsAndAfterDelayedSystemShow()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var firstEditor = new System.Windows.Controls.TextBox();
            var secondEditor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible
            };
            var root = new StackPanel();
            root.Children.Add(firstEditor);
            root.Children.Add(secondEditor);
            root.Children.Add(keyboard);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();
                Assert.True(firstEditor.Focus());
                int immediateHideCount = pane.HideCount;

                // Model UIElement.Focus's Show after GotKeyboardFocus and a second fast focus move.
                // The deferred hide must survive the immediate-request throttle.
                Assert.True(secondEditor.Focus());
                pane.IsVisible = true;
                PumpDispatcher();
                Assert.False(pane.IsVisible);
                Assert.True(pane.HideCount > immediateHideCount);

                // Windows focus tracking can finish even later, with no further WPF input events.
                pane.IsVisible = true;
                for (int attempt = 0; attempt < 10 && pane.IsVisible; attempt++)
                {
                    PumpDispatcher();
                }

                Assert.False(pane.IsVisible);
                keyboard.Visibility = Visibility.Collapsed;
                int finalHideCount = pane.HideCount;
                pane.IsVisible = true;
                    PumpDispatcher();
                Assert.True(pane.IsVisible);
                Assert.Equal(finalHideCount, pane.HideCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void InputPaneSuppression_DeferredRequestCannotEscapeItsScope()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out FakeInputPaneBackend pane);
            var editor = new System.Windows.Controls.TextBox();
            var outsideEditor = new System.Windows.Controls.TextBox();
            var scope = new StackPanel();
            var keyboard = new SoftKeyboard(services)
            {
                Visibility = Visibility.Visible,
                TargetScope = scope
            };
            scope.Children.Add(editor);
            scope.Children.Add(keyboard);
            var root = new StackPanel();
            root.Children.Add(scope);
            root.Children.Add(outsideEditor);
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();
                Assert.True(editor.Focus());
                PumpDispatcher();
                Assert.True(outsideEditor.Focus());
                int hideCount = pane.HideCount;
                pane.IsVisible = true;
                    PumpDispatcher();
                Assert.True(pane.IsVisible);
                Assert.Equal(hideCount, pane.HideCount);
                Assert.Equal(0, pane.ShowCount);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// WPF visibility preserves Hidden layout space and removes Collapsed layout space.
    /// </summary>
    /// <param name="visibility">The initial visibility before template creation.</param>
    [Theory]
    [InlineData(Visibility.Collapsed)]
    [InlineData(Visibility.Hidden)]
    public void DefaultTemplate_UsesWpfVisibilityLayoutAndKeepsKeysNonFocusable(Visibility visibility)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var services = CreateServices(out _, out _, out _);
            var keyboard = new SoftKeyboard(services) { Visibility = visibility };
            var window = new Window
            {
                Content = keyboard,
                Width = 340,
                SizeToContent = SizeToContent.Height,
                ShowInTaskbar = false,
            };
            window.Show();
            PumpDispatcher();
            window.UpdateLayout();

            var initialHeight = keyboard.ActualHeight;
            if (visibility == Visibility.Collapsed)
            {
                Assert.Equal(0d, initialHeight);
            }
            else
            {
                Assert.True(initialHeight >= 44d);
                Assert.NotEmpty(keyboard.CurrentLayout.Keys);
            }

            keyboard.Visibility = Visibility.Visible;
            window.UpdateLayout();
            Assert.True(keyboard.ActualHeight >= 44d);
            if (visibility == Visibility.Hidden)
            {
                Assert.Equal(initialHeight, keyboard.ActualHeight);
            }

            Assert.All(
                Descendants<SoftKeyboardKeyControl>(keyboard),
                button =>
                {
                    Assert.False(button.Focusable);
                    Assert.False(button.IsTabStop);
                }
            );
            window.Close();
        });
    }

    [Fact]
    public void InputPaneSuppression_DoesNotDetachWpfSharedWinRtObjects()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var firstEditor = new System.Windows.Controls.TextBox();
            var secondEditor = new System.Windows.Controls.TextBox();
            var keyboard = new SoftKeyboard();
            var scope = new StackPanel();
            scope.Children.Add(firstEditor);
            scope.Children.Add(secondEditor);
            scope.Children.Add(keyboard);
            var window = new Window
            {
                Content = scope,
                ShowInTaskbar = false
            };
            window.Show();
            PumpDispatcher();

            // Focusing first lets WPF acquire its own cached InputPane activation factory. The
            // keyboard must not manually release that shared COM identity when requesting TryHide.
            Assert.True(firstEditor.Focus());
            keyboard.Target = firstEditor;
            keyboard.Visibility = Visibility.Visible;

            Assert.True(secondEditor.Focus());
            Assert.Same(secondEditor, Keyboard.FocusedElement);
            window.Close();
        });
    }

    [Fact]
    public void ExternalInputStateChange_RebuildsSymbolsWithoutChangingSurfaceOrHeight()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _, out var methods))
            {
                Visibility = Visibility.Visible
            };
            // This test exercises Chinese native punctuation, independently of the test machine's
            // installed/current keyboard language. State and page changes still come from the control.
            keyboard.LayoutProvider = new SoftKeyboardLayoutRule(
                _ => true,
                context =>
                    CreateExtensionLayout(
                        ChineseSoftKeyboardExtensionProvider.Default,
                        new SoftKeyboardLayoutContext(
                            CultureInfo.GetCultureInfo("zh-CN"),
                            context.InputScope,
                            context.Page,
                            context.ShiftState,
                            context.InputMethod,
                            context.KeyboardLayoutHandle,
                            inputModeButton: context.InputModeButton
                        )
                    )
            );

            var window = new Window
            {
                Content = keyboard,
                Width = 720,
                SizeToContent = SizeToContent.Height,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                keyboard.ShowPage(SoftKeyboardPages.MoreSymbols);
                window.UpdateLayout();
                double height = keyboard.ActualHeight;
                Assert.Contains(keyboard.CurrentLayout.Keys, key => key.Text == "【");
                methods.SetNativeInput(false);
                typeof(SoftKeyboard).GetMethod("RefreshInputMode", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, null);
                PumpDispatcher();
                window.UpdateLayout();
                Assert.Equal(SoftKeyboardPages.MoreSymbols, keyboard.Page);
                Assert.Contains(keyboard.CurrentLayout.Keys, key => key.Text == "[");
                Assert.Equal(height, keyboard.ActualHeight);
                foreach (string mode in SoftKeyboardPages.BuiltInPages)
                {
                    keyboard.ShowPage(mode);
                    window.UpdateLayout();
                    Assert.Equal(height, keyboard.ActualHeight);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void AlternatePopup_IsAnchoredAndCommitsOnceWithoutResizingKeyboard()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 720,
                SizeToContent = SizeToContent.Height,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                editor.Focus();
                window.UpdateLayout();
                double height = keyboard.ActualHeight;
                PumpDispatcher();
                window.UpdateLayout();
                var button = FindCap(keyboard, Key.E);
                button.BeginPress();
                button.AdvanceHold();
                var popup = (Popup)button.Template.FindName("PART_AlternatePopup", button);
                ((FrameworkElement)popup.Child).UpdateLayout();
                Assert.True(popup.IsOpen);
                Assert.Same(button, popup.PlacementTarget);
                Assert.Same(editor, Keyboard.FocusedElement);
                window.UpdateLayout();
                Assert.Equal(height, keyboard.ActualHeight);
                button.CompletePress(true);
                PumpDispatcher();
                Assert.True(popup.IsOpen);
                Assert.False(popup.StaysOpen);
                var alternate = Descendants<Wpf.Zen.Ui.Controls.Button>(popup.Child)
                    .First(item => Equals(item.DataContext, "é"));
                alternate.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, alternate));
                Assert.False(popup.IsOpen);
                Assert.False(button.IsAlternatePopupOpen);
                Assert.Equal("Text:é", Assert.Single(input.Operations));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void InputMethodMenu_LeavesLayoutInPlaceAndClosesWithKeyboard()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var window = new Window
            {
                Content = keyboard,
                Width = 720,
                SizeToContent = SizeToContent.Height,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();

                var layout = keyboard.CurrentLayout;
                double height = keyboard.ActualHeight;

                keyboard.ShowInputMethodMenu();

                var menu = (ContextMenu)typeof(SoftKeyboard).GetField(
                        "_inputMethodMenu",
                        BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(keyboard)!;

                Assert.True(menu.IsOpen);
                Assert.Single(menu.Items);
                Assert.False(Assert.IsType<MenuItem>(menu.Items[0]).IsEnabled);
                Assert.Same(layout, keyboard.CurrentLayout);
                Assert.Equal(height, keyboard.ActualHeight);

                keyboard.Visibility = Visibility.Collapsed;

                Assert.False(menu.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static SoftKeyboardPlatformServices CreateServices(
        out RecordingInputBackend input,
        out FakeCandidateBackend candidates,
        out FakeInputPaneBackend inputPane
    ) => CreateServices(out input, out candidates, out inputPane, out _);

    private static SoftKeyboardPlatformServices CreateServices(
        out RecordingInputBackend input,
        out FakeCandidateBackend candidates,
        out FakeInputPaneBackend inputPane,
        out FakeInputMethodBackend inputMethods
    )
    {
        input = new RecordingInputBackend();
        candidates = new FakeCandidateBackend();
        inputPane = new FakeInputPaneBackend();
        inputMethods = new FakeInputMethodBackend();
        return new SoftKeyboardPlatformServices(
            input,
            inputPane,
            inputMethods,
            new BackendSoftKeyboardCandidateSource(candidates)
        );
    }

    private static SoftKeyboardKey FindKey(SoftKeyboard keyboard, Key key) =>
        keyboard.CurrentLayout.Keys.First(item => item.Key == key);

    private static SoftKeyboardKey FindKey(SoftKeyboard keyboard, SoftKeyboardKeyKind kind) =>
        keyboard.CurrentLayout.Keys.First(item => item.Kind == kind);

    [Fact]
    public void NumericScope_ExplicitLettersAndSymbolsOverrideAutomaticLayout()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            InputMethod.SetInputScope(editor, new InputScope
            {
                Names =
                {
                    new InputScopeName(InputScopeNameValue.Number)
                },
            });
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                Assert.Equal(SoftKeyboardPages.Automatic, keyboard.Page);
                Assert.DoesNotContain(keyboard.CurrentLayout.Keys, key => key.Key == Key.A);
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.ChangePage));
                Assert.NotNull(FindKey(keyboard, Key.A));
                keyboard.ShowPage(SoftKeyboardPages.Symbols);
                Assert.Contains(keyboard.CurrentLayout.Keys, key => key.Text == "#");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(SoftKeyboardPages.Symbols)]
    public void SymbolSurface_PreservesCandidateRegionAndProvidesPureNavigation(string mode)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _, out var inputMethods))
            {
                Visibility = Visibility.Visible,
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                candidates.Publish(keyboard, 1, 1, "candidate");
                int released = candidates.ReleasePresentationCount;
                keyboard.ShowPage(mode);
                window.UpdateLayout();
                Assert.True(keyboard.CanPresentCandidates);
                Assert.True(keyboard.HasCandidates);
                Assert.Equal(released, candidates.ReleasePresentationCount);
                SoftKeyboardInputModeButton? inputMode = keyboard.InputModeButton;
                int activations = inputMethods.ActivationCount;
                var back = Assert.Single(
                    Descendants<SoftKeyboardKeyControl>(keyboard),
                    button => button.IsVisible && button.DataContext is SoftKeyboardKey
                    {
                        Kind: SoftKeyboardKeyKind.ChangePage, Page: SoftKeyboardPages.Letters
                    });
                back.Invoke();
                Assert.Equal(SoftKeyboardPages.Letters, keyboard.Page);
                Assert.Equal(inputMode, keyboard.InputModeButton);
                Assert.Equal(activations, inputMethods.ActivationCount);
                Assert.Same(editor, Keyboard.FocusedElement);

                keyboard.ShowPage(mode);
                window.UpdateLayout();
                keyboard.Visibility = Visibility.Collapsed;
                Assert.Equal(Visibility.Collapsed, keyboard.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void InterruptedHold_DoesNotSendFurtherInput(bool disable)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                window.UpdateLayout();
                var cap = FindCap(keyboard, Key.Back);
                cap.BeginPress();
                cap.AdvanceHold();
                int count = input.VirtualKeys.Count;
                if (disable)
                {
                    keyboard.IsEnabled = false;
                    InvokeKey(keyboard, FindKey(keyboard, Key.Back));
                }
                else
                {
                    cap.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
                    {
                        RoutedEvent = Mouse.LostMouseCaptureEvent,
                    });
                }

                cap.AdvanceHold();
                Assert.Equal(count, input.VirtualKeys.Count);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void FocusOutsideScope_ReleasesCandidatesAndRejectsStaleCommit()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var other = new System.Windows.Controls.TextBox();
            var scope = new StackPanel();
            scope.Children.Add(editor);
            scope.Children.Add(keyboard);
            keyboard.TargetScope = scope;
            keyboard.Target = editor;
            var root = new StackPanel();
            root.Children.Add(scope);
            root.Children.Add(other);
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                Assert.True(keyboard.CanPresentCandidates);
                candidates.Publish(keyboard, 1, 1, "candidate");
                SoftKeyboardCandidate stale = keyboard.Candidates[0];
                Assert.True(other.Focus());
                Assert.False(keyboard.CanPresentCandidates);
                Assert.False(keyboard.HasCandidates);
                Assert.False(keyboard.CommitCandidateAsync(stale).GetAwaiter().GetResult());
                Assert.Null(candidates.LastCommittedIndex);
                Assert.Same(other, Keyboard.FocusedElement);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static SoftKeyboardLayout? CreateExtensionLayout(
        ISoftKeyboardExtensionProvider provider, SoftKeyboardLayoutContext context)
    {
        // This fixture's fake platform names its two button states; production layout decoding
        // belongs to service-specific observation adapters.
        return context.InputModeButton?.Id == "native"
            ? ChineseCompactLayoutProvider.Default.CreateLayout(context)
            : DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(context);

    }

    [Fact]
    public void KeyContent_AcceptsPresentationDataAndRejectsMissingTextContent()
    {
        var content = new FluentIconSource(SymbolRegular.Backspace24);
        var key = new SoftKeyboardKey(
            SoftKeyboardKeyKind.VirtualKey,
            content,
            "Backspace",
            Key.Back
        );

        Assert.Same(content, key.Content);
        Assert.Throws<ArgumentNullException>(() =>
            new SoftKeyboardKey(SoftKeyboardKeyKind.Text, null!, "Missing", text: "x"));
        Assert.Throws<ArgumentException>(() =>
            new SoftKeyboardKey(SoftKeyboardKeyKind.Text, " ", "Missing", text: "x"));
    }

    [Fact]
    public void LayoutProvider_UsesFluentIconSourcesForSpecialKeyCaps()
    {
        SoftKeyboardLayout layout = Assert.IsType<SoftKeyboardLayout>(
            DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.Default,
                    SoftKeyboardPages.Letters,
                    SoftKeyboardShiftState.Off
                )
            ));
        SoftKeyboardLayout lockedLayout = Assert.IsType<SoftKeyboardLayout>(
            DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                new SoftKeyboardLayoutContext(
                    CultureInfo.GetCultureInfo("en-US"),
                    InputScopeNameValue.Default,
                    SoftKeyboardPages.Letters,
                    SoftKeyboardShiftState.Locked
                )
            ));
        IReadOnlyList<SoftKeyboardKey> keys = layout.Keys;
        IReadOnlyList<SoftKeyboardKey> lockedKeys = lockedLayout.Keys;

        Assert.Equal(
            SymbolRegular.KeyboardShift24,
            Assert.IsType<FluentIconSource>(Assert.Single(keys, key => key.Kind == SoftKeyboardKeyKind.Shift).Content)
                .Symbol
        );
        Assert.Equal(
            SymbolRegular.Backspace24,
            Assert.IsType<FluentIconSource>(Assert.Single(keys, key => key.Key == Key.Back).Content).Symbol
        );
        Assert.Equal(
            SymbolRegular.ArrowLeft24,
            Assert.IsType<FluentIconSource>(Assert.Single(keys, key => key.Key == Key.Left).Content).Symbol
        );
        Assert.Equal(
            SymbolRegular.ArrowRight24,
            Assert.IsType<FluentIconSource>(Assert.Single(keys, key => key.Key == Key.Right).Content).Symbol
        );
        Assert.Equal(
            SymbolRegular.ArrowEnterLeft24,
            Assert.IsType<FluentIconSource>(Assert.Single(keys, key => key.Key == Key.Enter).Content).Symbol
        );

        FluentIconSource lockedShift = Assert.IsType<FluentIconSource>(
            Assert.Single(lockedKeys, key => key.Kind == SoftKeyboardKeyKind.Shift).Content
        );
        Assert.Equal(SymbolRegular.KeyboardShiftUppercase24, lockedShift.Symbol);
        Assert.True(lockedShift.Filled);
    }

    [Fact]
    public void GeneratedSpecialKey_UsesTheImplicitFluentIconTemplateAndKeyboardIconSize()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard
            {
                Visibility = Visibility.Visible
            };
            var window = new Window
            {
                Content = keyboard,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false,
            };

            try
            {
                window.Show();
                PumpDispatcher();
                window.UpdateLayout();
                SoftKeyboardKey backspace = FindKey(keyboard, Key.Back);
                SoftKeyboardKeyControl button = Assert.Single(
                    Descendants<SoftKeyboardKeyControl>(keyboard),
                    item => ReferenceEquals(item.DataContext, backspace)
                );
                FluentIcon icon = Assert.Single(Descendants<FluentIcon>(button));

                Assert.Same(backspace.Content, button.Content);
                Assert.Equal(SymbolRegular.Backspace24, icon.Symbol);
                Assert.Equal(20d, icon.IconSize);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SurfaceTransition_CancelsActiveCompositionWithEscape()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                keyboard.ShowPage(SoftKeyboardPages.Symbols);

                Assert.Equal(SoftKeyboardPages.Symbols, keyboard.Page);
                Assert.Contains((Key.Escape, false), input.VirtualKeys);
                composition.Complete();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DirectUnicodeInput_WaitsForCompositionCancellationBeforeText()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                keyboard.ShowPage(SoftKeyboardPages.Symbols);
                input.Operations.Clear();
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(
                    keyboard,
                    new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "★", "Star", text: "★")
                );

                Assert.Equal(["Key:Escape"], input.Operations);
                composition.Complete();
                var frame = new System.Windows.Threading.DispatcherFrame();
                _ = keyboard.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                    new Action(() => frame.Continue = false));
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                Assert.Equal(["Key:Escape", "Text:★"], input.Operations);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void HostCollapse_RequestsCompositionCancellationOnlyOnce()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                keyboard.Visibility = Visibility.Collapsed;

                Assert.Equal(Visibility.Collapsed, keyboard.Visibility);
                Assert.Equal(1, input.VirtualKeys.Count(item => item == (Key.Escape, false)));
                composition.Complete();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void InputModeSwitch_WaitsForCompositionCancellationToComplete()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                Assert.True(keyboard.InputModeButton?.Id == "native");
                var composition = new TextComposition(
                    InputManager.Current,
                    editor,
                    string.Empty,
                    TextCompositionAutoComplete.Off
                );
                _ = TextCompositionManager.StartComposition(composition);

                InvokeKey(
                    keyboard,
                    new SoftKeyboardKey(SoftKeyboardKeyKind.SwitchInputMode, "中", "Switch input mode")
                );

                Assert.Contains((Key.Escape, false), input.VirtualKeys);
                Assert.True(keyboard.InputModeButton?.Id == "native");
                composition.Complete();
                PumpDispatcher();
                keyboard.OnSessionInputMethodStateInvalidated();                PumpDispatcher();
                Assert.False(keyboard.InputModeButton?.Id == "native");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void InputModeSwitch_CompletesWhenTextStoreUsesDifferentCompositionObjects(
        bool richText,
        bool unrelatedCompletion)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            TextBoxBase editor = richText
                ? new System.Windows.Controls.RichTextBox()
                : new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };

            // TextStore creates a new framework composition for each event record. Its constructors
            // are internal, so reproduce that event contract here without requiring an installed TIP.
            void Observe(RoutedEvent routedEvent, IInputElement source)
            {
                Type type = richText
                    ? typeof(System.Windows.Documents.FrameworkRichTextComposition)
                    : typeof(System.Windows.Documents.FrameworkTextComposition);
                var composition = (TextComposition)Activator.CreateInstance(
                    type,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    [InputManager.Current, source, new object()],
                    null)!;
                keyboard.OnSessionTextComposition(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = routedEvent,
                    Source = source,
                });
            }

            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                Observe(TextCompositionManager.PreviewTextInputStartEvent, editor);
                Observe(TextCompositionManager.PreviewTextInputUpdateEvent, editor);
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                Assert.Contains((Key.Escape, false), input.VirtualKeys);
                Assert.True(keyboard.InputModeButton?.Id == "native");

                if (unrelatedCompletion)
                {
                    // A final event from another editor must not release the pending cancellation.
                    Observe(TextCompositionManager.PreviewTextInputEvent, new System.Windows.Controls.RichTextBox());
                    PumpDispatcher();
                    Assert.True(keyboard.InputModeButton?.Id == "native");
                    InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                }

                Observe(TextCompositionManager.PreviewTextInputEvent, editor);
                PumpDispatcher();
                keyboard.OnSessionInputMethodStateInvalidated();                PumpDispatcher();
                Assert.False(keyboard.InputModeButton?.Id == "native");
                Assert.Same(editor, Keyboard.FocusedElement);

                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                PumpDispatcher();
                keyboard.OnSessionInputMethodStateInvalidated();                PumpDispatcher();
                Assert.True(keyboard.InputModeButton?.Id == "native");
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                PumpDispatcher();
                keyboard.OnSessionInputMethodStateInvalidated();                PumpDispatcher();
                Assert.False(keyboard.InputModeButton?.Id == "native");
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void WindowReactivation_QueuesCandidateBridgeRefresh()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out _, out var candidates, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                Assert.True(editor.Focus());
                PumpDispatcher();
                typeof(SoftKeyboard).GetMethod("OnWindowDeactivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [window, EventArgs.Empty]);
                int refreshesBeforeActivation = candidates.RefreshCount;

                typeof(SoftKeyboard).GetMethod("OnWindowActivated", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [window, EventArgs.Empty]);
                PumpDispatcher();

                Assert.True(candidates.RefreshCount > refreshesBeforeActivation);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void InvokeKey(SoftKeyboard keyboard, SoftKeyboardKey key)
    {
        typeof(SoftKeyboard)
            .GetMethod("InvokeKey", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(keyboard, [key]);
    }

    [Theory]
    [InlineData("nextKey")]
    [InlineData("target")]
    [InlineData("layout")]
    [InlineData("close")]
    [InlineData("deactivate")]
    public void KeyboardContextChange_CancelsKeyOwnedAlternatives(string change)
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard(CreateServices(out var input, out _, out _))
            {
                Visibility = Visibility.Visible
            };
            var editor = new System.Windows.Controls.TextBox();
            var other = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(other);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                Width = 700,
                Height = 500,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                editor.Focus();
                PumpDispatcher();
                window.UpdateLayout();
                var cap = FindCap(keyboard, Key.E);
                cap.BeginPress();
                cap.AdvanceHold();
                Assert.True(cap.IsAlternatePopupOpen);
                switch (change)
                {
                    case "nextKey": FindCap(keyboard, Key.A).BeginPress(); break;
                    case "target": keyboard.Target = other; break;
                    case "layout": keyboard.ShowPage(SoftKeyboardPages.Symbols); break;
                    case "close": keyboard.Visibility = Visibility.Collapsed; break;
                    case "deactivate":
                        typeof(SoftKeyboard).GetMethod(
                                "OnWindowDeactivated",
                                BindingFlags.Instance | BindingFlags.NonPublic
                            )!
                            .Invoke(keyboard, [window, EventArgs.Empty]);
                        break;
                }

                cap.SelectAlternative("é");
                cap.CompletePress(true);
                Assert.False(cap.IsAlternatePopupOpen);
                Assert.Empty(input.Operations);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static SoftKeyboardKeyControl FindCap(SoftKeyboard keyboard, Key key) =>
        Descendants<SoftKeyboardKeyControl>(keyboard).Single(cap => cap.Key?.Key == key);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T result)
            {
                yield return result;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void PumpDispatcher()
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromMilliseconds(150),
            System.Windows.Threading.DispatcherPriority.ApplicationIdle,
            (_, _) => frame.Continue = false,
            System.Windows.Threading.Dispatcher.CurrentDispatcher
        );
        timer.Start();
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    private sealed class RecordingInputBackend : ISoftKeyboardInputBackend
    {
        internal bool RequireNonZeroForegroundHost { get; set; }

        internal IntPtr LastForegroundHost { get; private set; }

        internal List<(Key Key, bool Shift)> VirtualKeys { get; } = [];

        internal List<(Key Key, ModifierKeys Modifiers)> Chords { get; } = [];

        internal List<string> Operations { get; } = [];

        public bool SendVirtualKey(Key key, ModifierKeys modifiers)
        {
            VirtualKeys.Add((key, (modifiers & ModifierKeys.Shift) != 0));
            Chords.Add((key, modifiers));
            Operations.Add($"Key:{key}");
            return true;
        }

        public bool SendUnicode(string text)
        {
            Operations.Add($"Text:{text}");
            return true;
        }

        public bool IsHostForeground(IntPtr host)
        {
            LastForegroundHost = host;
            return !RequireNonZeroForegroundHost || host != IntPtr.Zero;
        }
    }

    private sealed class FakeInputPaneBackend : ISoftKeyboardInputPaneBackend
    {
        internal bool IsVisible { get; set; }

        internal SoftKeyboardSystemKeyboardSuppressionState Result { get; set; } =
            SoftKeyboardSystemKeyboardSuppressionState.Hidden;

        internal int HideCount { get; private set; }

        internal int ShowCount { get; private set; }

        public SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr host)
        {
            HideCount++;
            if (Result == SoftKeyboardSystemKeyboardSuppressionState.Hidden)
            {
                IsVisible = false;
            }

            return Result;
        }

        public bool TryShow(IntPtr host)
        {
            ShowCount++;
            return true;
        }
    }

    [Fact]
    public void CustomInputModeAdapter_RejectsTransitionsAndPreservesModeOnReadFailure()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var adapter = new TestInputModeAdapter();
            var keyboard = new SoftKeyboard(CreateServices(out _, out _, out _))
            {
                Visibility = Visibility.Visible,
                InputModeAdapter = adapter,
            };
            var editor = new System.Windows.Controls.TextBox();
            var root = new StackPanel();
            root.Children.Add(editor);
            root.Children.Add(keyboard);
            keyboard.Target = editor;
            var window = new Window
            {
                Content = root,
                ShowInTaskbar = false
            };
            try
            {
                window.Show();
                PumpDispatcher();
                editor.Focus();
                PumpDispatcher();
                Assert.Equal("direct", keyboard.InputModeButton?.Id);
                InvokeKey(keyboard, FindKey(keyboard, SoftKeyboardKeyKind.SwitchInputMode));
                Assert.False(adapter.SwitchRequested);
                Assert.Equal("direct", keyboard.InputModeButton?.Id);
                adapter.ThrowOnRead = true;
                typeof(SoftKeyboard).GetMethod("RefreshInputMode", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, null);
                PumpDispatcher();
                Assert.Equal("direct", keyboard.InputModeButton?.Id);
                Assert.IsType<InvalidOperationException>(keyboard.InputMethodError);
                keyboard.InputModeAdapter = null;
                PumpDispatcher();
                Assert.Equal("direct", keyboard.InputModeButton?.Id);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private sealed class TestInputModeAdapter : ISoftKeyboardInputModeAdapter
    {
        private static readonly SoftKeyboardInputModeTransition Transition = new("test-switch");

        internal bool ThrowOnRead { get; set; }

        internal bool SwitchRequested { get; private set; }

        public SoftKeyboardInputModeContribution GetContribution(
            SoftKeyboardInputMethod inputMethod, SoftKeyboardInputObservation observation)
        {
            if (ThrowOnRead)
            {
                throw new InvalidOperationException("Unavailable");
            }

            return new SoftKeyboardInputModeContribution(
                new SoftKeyboardInputModeButton("direct", "A", "Direct input"),
                Transition, overridePlatformButton: true);

        }

        public bool TrySwitchMode(
            SoftKeyboardInputMethod inputMethod,
            SoftKeyboardInputObservation observation,
            SoftKeyboardInputModeTransition transition)
        {
            SwitchRequested = true;
            return false;
        }
    }

    private sealed class FakeInputMethodBackend :
        ISoftKeyboardInputMethodBackend,
        ISoftKeyboardInputModeButtonBackend
    {
        private static readonly Guid FakeClassId = new("D9400B6B-9E48-4D79-90A6-62A3C085B3C2");
        private static readonly Guid FakeProfileId = new("451C2C15-CE41-49D1-A05E-D6474366F501");
        private bool _native = true;
        private bool _profileActive = true;

        internal int ActivationCount { get; private set; }

        public void Refresh(
            ObservableCollection<SoftKeyboardInputMethod> destination,
            out SoftKeyboardInputMethod? active
        )
        {
            destination.Clear();
            var profile = new SoftKeyboardInputMethod(FakeClassId, FakeProfileId, 0x0804, "Test IME", _profileActive);
            destination.Add(profile);
            active = _profileActive ? profile : null;
        }

        public bool TryActivate(SoftKeyboardInputMethod profile)
        {
            ActivationCount++;
            _profileActive = true;
            return true;
        }

        public SoftKeyboardInputModeButton GetInputModeButton(
            SoftKeyboardInputMethod inputMethod)
        {
            return new SoftKeyboardInputModeButton(
                _native ? "native" : "direct",
                _native ? "中" : "A",
                _native ? "Native input" : "Direct input"
            );
        }

        public SoftKeyboardInputModeCommandResult TryInvokeInputModeButton(
            SoftKeyboardInputMethod inputMethod, Rect screenBounds)
        {
            _native = !_native;
            return SoftKeyboardInputModeCommandResult.Accepted;
        }

        internal void SetNativeInput(bool native)
        {
            _native = native;
        }

        internal void UseDirectKeyboardLayout()
        {
            _native = false;
            _profileActive = false;
        }
    }

    private sealed class FakeCandidateBackend : ISoftKeyboardCandidateBackend
    {
        internal uint? LastCommittedIndex { get; private set; }

        internal int? LastPageDelta { get; private set; }

        internal int RefreshCount { get; private set; }

        internal int ReleasePresentationCount { get; private set; }

        public void Register(SoftKeyboard keyboard)
        {
        }

        public void Unregister(SoftKeyboard keyboard)
        {
        }

        public void Refresh(SoftKeyboard keyboard)
        {
            RefreshCount++;
        }

        public void ReleasePresentation(SoftKeyboard keyboard)
        {
            ReleasePresentationCount++;
            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
        }

        public bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            LastPageDelta = delta;
            return true;
        }

        public bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            LastCommittedIndex = index;
            return true;
        }

        internal void Publish(SoftKeyboard keyboard, uint pageNumber, uint pageCount, params string[] values)
        {
            keyboard.ReplaceCandidates(
                values.Select((value, index) => new SoftKeyboardCandidate((uint)index, value)),
                SoftKeyboardCandidatePresentationState.Embedded,
                pageNumber,
                pageCount,
                values.Length > 0 ? 0U : null
            );
        }
    }
}
