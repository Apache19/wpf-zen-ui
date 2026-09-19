// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Wpf.Zen.Ui.VirtualInput.Interop;
using Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Verifies private-member dispatch, optional binding failures, and thread-affine object lifetimes.
/// </summary>
public class WpfReflectionBridgeTests
{
    /// <summary>
    /// Unsupported bindings are attempted once, including when compilation throws.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnavailableBinding_IsCached(bool throws)
    {
        var attempts = 0;
        var binding = WpfReflectionBridge.Create<Func<object>>("Test", () =>
        {
            attempts++;
            if (throws)
            {
                throw new MemberAccessException();
            }

            return null;
        });

        Assert.Null(binding.Value);
        Assert.Null(binding.Value);
        Assert.Equal(1, attempts);
    }

    /// <summary>
    /// Both operations acquire a fresh pane and dispose it on success, rejection, and failure.
    /// Direct dispatch preserves the original exception instead of a reflection wrapper.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InputPane_DisposesEveryRequest(bool show)
    {
        var invoke = SystemInputPaneController.CreateInvoker(typeof(TestPane));
        Assert.NotNull(invoke);
        TestPane.Failure = null;
        TestPane.Accepted = true;
        Assert.True(invoke(null!, show));
        var first = TestPane.Last!;
        Assert.Equal(show, first.ShowRequested);
        Assert.Equal(1, first.DisposeCount);

        TestPane.Accepted = false;
        Assert.False(invoke(null!, show));
        Assert.NotSame(first, TestPane.Last);
        Assert.Equal(1, TestPane.Last!.DisposeCount);

        TestPane.Failure = new COMException("Test native failure");
        try
        {
            Assert.Same(TestPane.Failure, Assert.Throws<COMException>(() => invoke(null!, show)));
            Assert.Equal(1, TestPane.Last!.DisposeCount);
        }
        finally
        {
            TestPane.Failure = null;
        }
    }

    /// <summary>
    /// A missing or incompatible WPF contract does not publish a partially usable bridge.
    /// </summary>
    [Fact]
    public void InputPane_RejectsMissingContract()
    {
        Assert.Null(SystemInputPaneController.CreateInvoker(null));
        Assert.Null(SystemInputPaneController.CreateInvoker(typeof(object)));
        Assert.Null(WpfTextServicesAccessor.CreateAccessor(null, null, null));
    }

    /// <summary>
    /// Compiles against the installed WPF private contract without displaying system input UI.
    /// </summary>
    [Fact]
    public void InputPane_RuntimeContractCompiles()
    {
        var paneType = typeof(HwndSource).Assembly.GetType(
            "MS.Internal.WindowsRuntime.Windows.UI.ViewManagement.InputPane");
        Assert.NotNull(paneType);
        Assert.NotNull(SystemInputPaneController.CreateInvoker(paneType));
    }

    /// <summary>
    /// A single cached delegate reads fresh state on each thread and short-circuits null owners.
    /// </summary>
    [Fact]
    public void TextServices_ReadsLiveThreadState()
    {
        const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
        var accessor = WpfTextServicesAccessor.CreateAccessor(
            typeof(TestStore).GetProperty("Current", BindingFlags.Static | BindingFlags.NonPublic),
            typeof(TestStore).GetProperty("Host", instance),
            typeof(TestHost).GetProperty("Manager", instance));
        Assert.NotNull(accessor);

        for (var i = 0; i < 2; i++)
        {
            WpfTestThread.Run(() =>
            {
                Assert.Null(accessor());
                var store = new TestStore();
                TestStore.Local = store;
                Assert.Null(accessor());
                var first = new object();
                store.Host = new TestHost { Manager = first };
                Assert.Same(first, accessor());
                var second = new object();
                store.Host = new TestHost { Manager = second };
                Assert.Same(second, accessor());
                store.Host.Manager = null;
                Assert.Null(accessor());
                store.Host = null;
                Assert.Null(accessor());
            });
        }
    }

    /// <summary>
    /// Checks real WPF getter access without activating TSF or substituting its thread manager.
    /// </summary>
    [Fact]
    public void TextServices_RuntimeContractMatchesReflection()
    {
        var assembly = typeof(System.Windows.Controls.TextBox).Assembly;
        var storeProperty = assembly.GetType("System.Windows.Documents.TextEditor")!
            .GetProperty("_ThreadLocalStore", BindingFlags.Static | BindingFlags.NonPublic)!;
        var hostProperty = assembly.GetType("System.Windows.Documents.TextEditorThreadLocalStore")!
            .GetProperty("TextServicesHost", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var managerProperty = assembly.GetType("System.Windows.Documents.TextServicesHost")!
            .GetProperty("ThreadManager", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var accessor = WpfTextServicesAccessor.CreateAccessor(
            storeProperty, hostProperty, managerProperty);
        Assert.NotNull(accessor);

        WpfTestThread.Run(() =>
        {
            var store = storeProperty.GetValue(null);
            Assert.NotNull(store);
            var host = hostProperty.GetValue(store);
            var expected = host is null ? null : managerProperty.GetValue(host);
            Assert.Same(expected, accessor());
            Assert.Same(host, hostProperty.GetValue(store));
        });
    }

    private sealed class TestPane : IDisposable
    {
        internal static TestPane? Last;
        internal static bool Accepted;
        internal static Exception? Failure;
        internal bool ShowRequested;
        internal int DisposeCount;

        private static TestPane GetForWindow(HwndSource source)
        {
            Last = new TestPane();
            return Last;
        }

        private bool TryShow()
        {
            ShowRequested = true;
            return Complete();
        }

        private bool TryHide()
        {
            ShowRequested = false;
            return Complete();
        }

        private static bool Complete()
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            return Accepted;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class TestStore
    {
        [ThreadStatic]
        internal static TestStore? Local;

        private static TestStore? Current => Local;
        internal TestHost? Host { get; set; }
    }

    private sealed class TestHost
    {
        internal object? Manager { get; set; }
    }
}
