using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using NativeComboBox = System.Windows.Controls.ComboBox;
using PagingBoxControl = Wpf.Zen.Ui.Controls.PagingBox;

namespace Wpf.Zen.Ui.UnitTests.Controls
{
    /// <summary>
    /// 验证 <see cref="PagingBox"/> 的分页、命令和 WPF 控件契约。
    /// </summary>
    public class PagingBoxTests
    {
        [Fact]
        public void Command_EventHandlerCommitsAnotherPage_StillReceivesRequestedPage()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(static _ => true);
                var control = new TestPagingBox
                {
                    CurrentPage = 1,
                    PageCount = 10,
                    Command = command
                };
                control.PageRequested += (_, _) => control.CurrentPage = 3;

                GetRangeProvider(control).SetValue(4);

                Assert.Equal([4], command.ExecutedParameters);
                Assert.Equal(3, control.CommandParameter);
            });
        }

        [Fact]
        public void CanExecute_IsEvaluatedForEachRequestedPage_WithoutDisablingContainer()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(parameter => parameter is int page && page > 1);
                var control = new TestPagingBox
                {
                    CurrentPage = 1,
                    PageCount = 10,
                    Command = command
                };

                Assert.IsAssignableFrom<ICommandSource>(control);
                Assert.True(control.IsEnabled);
                Assert.False(control.CanGoPrevious);
                Assert.True(control.CanGoNext);
                Assert.False(control.PageList.Single(item => item.Page == 1).IsEnabled);
                Assert.True(control.PageList.Single(item => item.Page == 2).IsEnabled);
            });
        }

        [Fact]
        public void CanExecuteChanged_RefreshesEveryVisibleAction()
        {
            WpfTestThread.Run(() =>
            {
                var allowRequests = false;
                var command = new TestCommand(_ => allowRequests);
                var control = new TestPagingBox
                {
                    CurrentPage = 2,
                    PageCount = 5,
                    Command = command
                };

                Assert.False(control.CanGoPrevious);
                Assert.False(control.CanGoNext);
                Assert.All(control.PageList, item => Assert.False(item.IsEnabled));

                allowRequests = true;
                command.RaiseCanExecuteChanged();

                Assert.True(control.CanGoPrevious);
                Assert.True(control.CanGoNext);
                Assert.True(control.PageList.Single(item => item.Page == 1).IsEnabled);
                Assert.False(control.PageList.Single(item => item.Page == 2).IsEnabled);
                Assert.True(control.PageList.Single(item => item.Page == 3).IsEnabled);
            });
        }

        [Fact]
        public void PageList_ExtremeConfiguration_RemainsBoundedOrderedAndUnique()
        {
            WpfTestThread.Run(() =>
            {
                var control = new TestPagingBox
                {
                    PageCount = 1000,
                    CurrentPage = 100,
                    MaxVisiblePages = int.MaxValue,
                    EllipsisStep = int.MaxValue
                };

                var pages = control.PageList.Select(item => item.Page).ToArray();

                Assert.Equal(PagingBoxControl.MaximumVisiblePages, control.MaxVisiblePages);
                Assert.InRange(pages.Length, 1, PagingBoxControl.MaximumVisiblePages);
                Assert.Equal(pages.OrderBy(page => page), pages);
                Assert.Equal(pages.Length, pages.Distinct().Count());
                Assert.All(pages, page => Assert.InRange(page, 1, control.PageCount));
            });
        }

        [Fact]
        public void PageList_NormalRanges_AlwaysSatisfyNavigationInvariants()
        {
            WpfTestThread.Run(() =>
            {
                foreach (var maxVisiblePages in new[] { 5, 7, 9, 11, 20 })
                {
                    for (var pageCount = 0; pageCount <= 100; pageCount++)
                    {
                        for (var currentPage = 1; currentPage <= Math.Max(1, pageCount); currentPage++)
                        {
                            var control = new TestPagingBox
                            {
                                MaxVisiblePages = maxVisiblePages,
                                PageCount = pageCount,
                                CurrentPage = currentPage
                            };
                            var pages = control.PageList.Select(item => item.Page).ToArray();

                            Assert.InRange(pages.Length, 0, Math.Min(control.MaxVisiblePages, pageCount));
                            Assert.Equal(pages.OrderBy(page => page), pages);
                            Assert.Equal(pages.Length, pages.Distinct().Count());
                            Assert.All(pages, page => Assert.InRange(page, 1, pageCount));
                            Assert.Equal(
                                pageCount == 0 ? 0 : 1,
                                control.PageList.Count(item => item.IsCurrent));
                        }
                    }
                }
            });
        }

        [Fact]
        public void PageSizeOptions_ObservableCollectionChange_ResynchronizesSelection()
        {
            WpfTestThread.Run(() =>
            {
                var options = new ObservableCollection<int> { 10 };
                var control = new TestPagingBox
                {
                    PageSize = 20,
                    PageSizeOptions = options,
                    Template = CreateTemplate(typeof(NativeComboBox), "PART_PageSizeComboBox")
                };

                Assert.True(control.ApplyTemplate());
                var comboBox = Assert.IsType<NativeComboBox>(
                    control.Template.FindName("PART_PageSizeComboBox", control));
                Assert.Null(comboBox.SelectedItem);

                options.Add(20);

                Assert.Equal(20, comboBox.SelectedItem);
            });
        }

        [Fact]
        public void PageSize_ExternalUpdateOnlySynchronizesState()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(static _ => true);
                var control = new TestPagingBox
                {
                    Command = command
                };

                control.PageSize = 25;

                Assert.Equal(25, control.PageSize);
                Assert.Equal(0, control.PageSizeChangedHookCount);
                Assert.Empty(command.ExecutedParameters);
            });
        }

        [Fact]
        public void MaximumPageSize_CoercesPageSizeAndRejectsInvalidLimit()
        {
            WpfTestThread.Run(() =>
            {
                var control = new TestPagingBox();

                Assert.False(control.IsPageSizeEditable);
                Assert.Equal(PagingBoxControl.DefaultMaximumPageSize, control.MaximumPageSize);

                control.MaximumPageSize = 100;
                control.PageSize = 250;

                Assert.Equal(100, control.PageSize);
                Assert.Throws<ArgumentException>(() => control.MaximumPageSize = 0);
            });
        }

        [Fact]
        public void PageSizeSelector_UserSelectionCommitsAndRequestsFirstPage()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(static _ => true);
                var control = new TestPagingBox
                {
                    Command = command,
                    PageSize = 20,
                    PageSizeOptions = new[] { 10, 20, 30 },
                    Template = CreateTemplate(typeof(NativeComboBox), "PART_PageSizeComboBox")
                };
                var window = ShowInWindow(control);

                try
                {
                    var comboBox = Assert.IsType<NativeComboBox>(
                        control.Template.FindName("PART_PageSizeComboBox", control));

                    comboBox.SelectedItem = 30;

                    Assert.Equal(30, control.PageSize);
                    Assert.Equal(1, control.PageSizeChangedHookCount);
                    Assert.Equal([1], command.ExecutedParameters);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void EditablePageSize_EnterCommitsValidTextAndRestoresInvalidText()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(static _ => true);
                var control = new TestPagingBox
                {
                    Command = command,
                    IsPageSizeEditable = true,
                    MaximumPageSize = 100,
                    PageSize = 20,
                    PageSizeOptions = new[] { 10, 20, 30, 50 },
                    Template = CreateTemplate(typeof(NativeComboBox), "PART_PageSizeComboBox")
                };
                var window = ShowInWindow(control);

                try
                {
                    var comboBox = Assert.IsType<NativeComboBox>(
                        control.Template.FindName("PART_PageSizeComboBox", control));
                    comboBox.IsEditable = true;

                    InvokePageSizeTextCommit(control, "25");

                    Assert.Equal(25, control.PageSize);
                    Assert.Equal(1, control.PageSizeChangedHookCount);
                    Assert.Equal([1], command.ExecutedParameters);

                    comboBox.Text = "101";
                    InvokePageSizeTextCommit(control, "101");

                    Assert.Equal(25, control.PageSize);
                    Assert.Equal("25", comboBox.Text);
                    Assert.Equal(1, control.PageSizeChangedHookCount);
                    Assert.Equal([1], command.ExecutedParameters);
                }
                finally
                {
                    window.Close();
                }
            });
        }

        [Fact]
        public void TemplateReplacement_DetachesOldPartHandlersImmediately()
        {
            WpfTestThread.Run(() =>
            {
                var command = new TestCommand(static _ => true);
                var control = new TestPagingBox
                {
                    CurrentPage = 2,
                    PageCount = 3,
                    Command = command,
                    Template = CreateTemplate(typeof(ButtonBase), "PART_PreviousButton")
                };

                Assert.True(control.ApplyTemplate());
                var oldButton = Assert.IsType<ButtonBase>(
                    control.Template.FindName("PART_PreviousButton", control));
                oldButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, oldButton));
                Assert.Equal([1], command.ExecutedParameters);

                command.ExecutedParameters.Clear();
                control.Template = CreateTemplate(typeof(Border), "ReplacementRoot");
                oldButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, oldButton));

                Assert.Empty(command.ExecutedParameters);
            });
        }

        [Fact]
        public void AutomationPeer_ExposesRangeValueAndRaisesUserRequest()
        {
            WpfTestThread.Run(() =>
            {
                var control = new TestPagingBox
                {
                    CurrentPage = 2,
                    PageCount = 8,
                    EllipsisStep = 4
                };
                var peer = control.CreatePeer();
                var provider = Assert.IsAssignableFrom<IRangeValueProvider>(
                    peer.GetPattern(PatternInterface.RangeValue));

                Assert.Equal(typeof(PagingBoxControl).Name, peer.GetClassName());
                Assert.Equal(AutomationControlType.Group, peer.GetAutomationControlType());
                Assert.Equal(1, provider.Minimum);
                Assert.Equal(8, provider.Maximum);
                Assert.Equal(2, provider.Value);
                Assert.Equal(4, provider.LargeChange);

                provider.SetValue(6);

                Assert.Equal([6], control.RequestedPages);
                Assert.Equal(2, control.CurrentPage);
            });
        }

        [Fact]
        public void AutomationPeer_RangeValueChangeMethodsUseDoubleValues()
        {
            var peerType = typeof(PagingBoxControl).Assembly.GetType(
                "Mining_EntClient.Presentation.AutomationPeers.PagingBoxAutomationPeer",
                throwOnError: true)!;

            var currentPageMethod = peerType.GetMethod(
                "RaiseCurrentPageChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var pageCountMethod = peerType.GetMethod(
                "RaisePageCountChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(currentPageMethod);
            Assert.NotNull(pageCountMethod);
            Assert.All(
                currentPageMethod.GetParameters(),
                parameter => Assert.Equal(typeof(double), parameter.ParameterType));
            Assert.All(
                pageCountMethod.GetParameters(),
                parameter => Assert.Equal(typeof(double), parameter.ParameterType));
        }

        [Fact]
        public void PageItemAutomationFormat_UpdatesGeneratedDescriptions()
        {
            WpfTestThread.Run(() =>
            {
                var control = new TestPagingBox
                {
                    PageCount = 3,
                    CurrentPage = 2,
                    PageItemAutomationNameFormat = "Page #{0}",
                    CurrentPageItemAutomationNameFormat = "Selected page #{0}"
                };

                Assert.Equal("Page #1", control.PageList.Single(item => item.Page == 1).Description);
                Assert.Equal("Selected page #2", control.PageList.Single(item => item.Page == 2).Description);
            });
        }

        [Fact]
        public void PageSizeChanged_ProtectedHookCanBeSpecialized()
        {
            WpfTestThread.Run(() =>
            {
                var control = new TestPagingBox();
                RoutedPropertyChangedEventArgs<int>? routedArgs = null;
                control.PageSizeChanged += (_, e) => routedArgs = e;

                control.RaisePageSizeChanged(20, 50);

                Assert.Equal(1, control.PageSizeChangedHookCount);
                Assert.NotNull(routedArgs);
                Assert.Equal(20, routedArgs.OldValue);
                Assert.Equal(50, routedArgs.NewValue);
            });
        }

#pragma warning disable CS0618
        private static ControlTemplate CreateTemplate(Type elementType, string name)
        {
            return new ControlTemplate(typeof(PagingBoxControl))
            {
                VisualTree = new FrameworkElementFactory(elementType, name)
            };
        }
#pragma warning restore CS0618

        private static IRangeValueProvider GetRangeProvider(TestPagingBox control)
        {
            return Assert.IsAssignableFrom<IRangeValueProvider>(
                control.CreatePeer().GetPattern(PatternInterface.RangeValue));
        }

        private static Window ShowInWindow(PagingBoxControl control)
        {
            var window = new Window
            {
                ShowInTaskbar = false,
                Width = 400,
                Height = 100,
                Content = control
            };
            window.Show();
            control.ApplyTemplate();
            return window;
        }

        private static void InvokePageSizeTextCommit(PagingBoxControl control, string text)
        {
            var method = typeof(PagingBoxControl).GetMethod(
                "CommitPageSizeText",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);
            method.Invoke(control, [text]);
        }

        private sealed class TestPagingBox : PagingBoxControl
        {
            internal List<int> RequestedPages { get; } = [];

            internal int PageSizeChangedHookCount { get; private set; }

            internal AutomationPeer CreatePeer()
            {
                return OnCreateAutomationPeer();
            }

            internal void RaisePageSizeChanged(int oldValue, int newValue)
            {
                OnPageSizeChanged(
                    new RoutedPropertyChangedEventArgs<int>(
                        oldValue,
                        newValue,
                        PageSizeChangedEvent));
            }

            protected override void OnPageRequested(PageRequestedEventArgs e)
            {
                RequestedPages.Add(e.Page);
                base.OnPageRequested(e);
            }

            protected override void OnPageSizeChanged(RoutedPropertyChangedEventArgs<int> e)
            {
                PageSizeChangedHookCount++;
                base.OnPageSizeChanged(e);
            }
        }

        private sealed class TestCommand(Func<object?, bool> canExecute) : ICommand
        {
            internal List<object?> ExecutedParameters { get; } = [];

            public event EventHandler? CanExecuteChanged;

            public bool CanExecute(object? parameter)
            {
                return canExecute(parameter);
            }

            public void Execute(object? parameter)
            {
                ExecutedParameters.Add(parameter);
            }

            internal void RaiseCanExecuteChanged()
            {
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
