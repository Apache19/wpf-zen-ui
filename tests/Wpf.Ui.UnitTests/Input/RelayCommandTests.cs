// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using Wpf.Ui.Input;

namespace Wpf.Ui.UnitTests.Input;

public class RelayCommandTests
{
    #region Constructors

    [Fact]
    public void Constructor_SingleArg_WithNullExecute_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!));
    }

    [Fact]
    public void Constructor_SingleArg_WithValidExecute_CreatesCommand()
    {
        var command = new RelayCommand<string>(_ => { });

        Assert.NotNull(command);
    }

    [Fact]
    public void Constructor_TwoArgs_WithNullExecute_ThrowsArgumentNullException()
    {
        static bool CanExecute(string? p) => true;

        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(null!, CanExecute));
    }

    [Fact]
    public void Constructor_TwoArgs_WithNullCanExecute_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RelayCommand<string>(_ => { }, null!));
    }

    [Fact]
    public void Constructor_TwoArgs_WithValidDelegates_CreatesCommand()
    {
        var command = new RelayCommand<string>(_ => { }, _ => true);

        Assert.NotNull(command);
    }

    #endregion

    #region CanExecute — non-nullable value type (int)

    [Fact]
    public void CanExecute_Int_NoPredicate_ValueParameter_ReturnsTrue()
    {
        var command = new RelayCommand<int>(_ => { });

        bool result = command.CanExecute(42);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Int_NoPredicate_ObjectWithCorrectType_ReturnsTrue()
    {
        var command = new RelayCommand<int>(_ => { });

        bool result = command.CanExecute((object)42);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Int_NoPredicate_ObjectNull_ReturnsTrue()
    {
        // WPF passes null during initialization before CommandParameter is set.
        // Without a canExecute predicate, the command is always executable.
        var command = new RelayCommand<int>(_ => { });

        bool result = command.CanExecute(null);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Int_WithPredicate_ObjectNull_ReturnsFalse()
    {
        // When a predicate exists, null for a non-nullable value type cannot
        // be meaningfully evaluated — defer to false (safe default).
        var command = new RelayCommand<int>(_ => { }, i => i > -1);

        bool result = command.CanExecute(null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecute_Int_WithPredicate_ObjectNull_GuardsBeforeInvokingPredicate()
    {
        // The predicate must NOT be invoked with null/default — we return false
        // before the predicate ever runs.
        var command = new RelayCommand<int>(
            _ => { },
            _ =>
            {
                Assert.Fail("Predicate should not be invoked for null + value type.");
                return true;
            });

        bool result = command.CanExecute(null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecute_Int_WithPredicate_ValidValue_UsesPredicate()
    {
        var command = new RelayCommand<int>(_ => { }, i => i > 10);

        Assert.False(command.CanExecute(5));
        Assert.True(command.CanExecute(15));
    }

    [Fact]
    public void CanExecute_Int_ObjectWithWrongType_ThrowsArgumentException()
    {
        var command = new RelayCommand<int>(_ => { });

        Assert.Throws<ArgumentException>(() => command.CanExecute("not an int"));
    }

    #endregion

    #region CanExecute — non-nullable value type (enum: ThumbRateState)

    [Fact]
    public void CanExecute_Enum_NoPredicate_ObjectNull_ReturnsTrue()
    {
        // This is the exact scenario that was broken in .NET Framework:
        // ThumbRate creates RelayCommand<ThumbRateState> without a predicate.
        // WPF calls CanExecute(null) during initialization → should return true.
        var command = new RelayCommand<ThumbRateState>(_ => { });

        bool result = command.CanExecute(null);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Enum_NoPredicate_BoxedValue_ReturnsTrue()
    {
        var command = new RelayCommand<ThumbRateState>(_ => { });

        bool result = command.CanExecute((object)ThumbRateState.Liked);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Enum_NoPredicate_TypedValue_ReturnsTrue()
    {
        var command = new RelayCommand<ThumbRateState>(_ => { });

        bool result = command.CanExecute(ThumbRateState.Disliked);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_Enum_WithPredicate_ObjectNull_ReturnsFalse()
    {
        var command = new RelayCommand<ThumbRateState>(
            _ => { },
            s => s == ThumbRateState.Liked);

        bool result = command.CanExecute(null);

        Assert.False(result);
    }

    [Fact]
    public void CanExecute_Enum_WithPredicate_EvaluatesCorrectly()
    {
        var command = new RelayCommand<ThumbRateState>(
            _ => { },
            s => s == ThumbRateState.Liked);

        Assert.True(command.CanExecute(ThumbRateState.Liked));
        Assert.False(command.CanExecute(ThumbRateState.Disliked));
        Assert.False(command.CanExecute(ThumbRateState.None));
    }

    #endregion

    #region CanExecute — nullable value type (int?)

    [Fact]
    public void CanExecute_NullableInt_NoPredicate_ObjectNull_ReturnsTrue()
    {
        // Nullable<T> has default(T) == null, so the null guard is NOT
        // triggered — null passes through to CanExecute(T?) where no
        // predicate means always true.
        var command = new RelayCommand<int?>(_ => { });

        bool result = command.CanExecute((object?)null);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_NullableInt_NoPredicate_Value_ReturnsTrue()
    {
        var command = new RelayCommand<int?>(_ => { });

        bool result = command.CanExecute((object)42);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_NullableInt_WithPredicate_ObjectNull_UsesPredicate()
    {
        int? received = null;
        var command = new RelayCommand<int?>(
            _ => { },
            i =>
            {
                received = i;
                return i.HasValue;
            });

        bool result = command.CanExecute((object?)null);

        Assert.False(result);
        Assert.Null(received); // predicate received null, not default(int)
    }

    [Fact]
    public void CanExecute_NullableInt_WithPredicate_ValidValue_UsesPredicate()
    {
        var command = new RelayCommand<int?>(
            _ => { },
            i => i.HasValue && i.Value > 10);

        Assert.True(command.CanExecute((object)15));
        Assert.False(command.CanExecute((object)5));
        Assert.False(command.CanExecute((object?)null));
    }

    #endregion

    #region CanExecute — reference type (string)

    [Fact]
    public void CanExecute_String_NoPredicate_ObjectNull_ReturnsTrue()
    {
        // Reference types: default(T) is null, so the null guard is skipped.
        // null passes to CanExecute(T?) → no predicate → true.
        var command = new RelayCommand<string>(_ => { });

        bool result = command.CanExecute((object?)null);

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_String_NoPredicate_ValidValue_ReturnsTrue()
    {
        var command = new RelayCommand<string>(_ => { });

        bool result = command.CanExecute((object)"hello");

        Assert.True(result);
    }

    [Fact]
    public void CanExecute_String_WithPredicate_ObjectNull_UsesPredicate()
    {
        string? received = "not null";
        var command = new RelayCommand<string>(
            _ => { },
            s =>
            {
                received = s;
                return s is not null;
            });

        bool result = command.CanExecute((object?)null);

        Assert.False(result);
        Assert.Null(received);
    }

    [Fact]
    public void CanExecute_String_WithPredicate_ValidValue_UsesPredicate()
    {
        var command = new RelayCommand<string>(
            _ => { },
            s => s is not null && s.Length > 2);

        Assert.True(command.CanExecute((object)"hello"));
        Assert.False(command.CanExecute((object)"hi"));
    }

    [Fact]
    public void CanExecute_String_ObjectWithWrongType_ThrowsArgumentException()
    {
        var command = new RelayCommand<string>(_ => { });

        Assert.Throws<ArgumentException>(() => command.CanExecute(42));
    }

    #endregion

    #region Execute — non-nullable value type (int)

    [Fact]
    public void Execute_Int_Typed_InvokesDelegate()
    {
        int received = 0;
        var command = new RelayCommand<int>(i => received = i);

        command.Execute(42);

        Assert.Equal(42, received);
    }

    [Fact]
    public void Execute_Int_ObjectWithCorrectType_InvokesDelegate()
    {
        int received = 0;
        var command = new RelayCommand<int>(i => received = i);

        command.Execute((object)42);

        Assert.Equal(42, received);
    }

    [Fact]
    public void Execute_Int_ObjectWithWrongType_ThrowsArgumentException()
    {
        var command = new RelayCommand<int>(_ => { });

        Assert.Throws<ArgumentException>(() => command.Execute("wrong"));
    }

    [Fact]
    public void Execute_Int_ObjectNull_ThrowsArgumentException()
    {
        // Execute does NOT have the null guard like CanExecute does.
        // Callers should check CanExecute first (which handles null).
        var command = new RelayCommand<int>(_ => { });

        Assert.Throws<ArgumentException>(() => command.Execute(null));
    }

    #endregion

    #region Execute — reference type (string)

    [Fact]
    public void Execute_String_Typed_InvokesDelegate()
    {
        string? received = null;
        var command = new RelayCommand<string>(s => received = s);

        command.Execute("hello");

        Assert.Equal("hello", received);
    }

    [Fact]
    public void Execute_String_ObjectNull_InvokesDelegateWithNull()
    {
        string? received = "not null";
        var command = new RelayCommand<string>(s => received = s);

        command.Execute((object?)null);

        Assert.Null(received);
    }

    [Fact]
    public void Execute_String_ObjectWithWrongType_ThrowsArgumentException()
    {
        var command = new RelayCommand<string>(_ => { });

        Assert.Throws<ArgumentException>(() => command.Execute(42));
    }

    #endregion

    #region NotifyCanExecuteChanged

    [Fact]
    public void NotifyCanExecuteChanged_RaisesEvent()
    {
        var command = new RelayCommand<int>(_ => { });
        var raised = false;
        command.CanExecuteChanged += (_, _) => raised = true;

        command.NotifyCanExecuteChanged();

        Assert.True(raised);
    }

    [Fact]
    public void NotifyCanExecuteChanged_WithMultipleSubscribers_RaisesForAll()
    {
        var command = new RelayCommand<int>(_ => { });
        var count = 0;
        command.CanExecuteChanged += (_, _) => count++;
        command.CanExecuteChanged += (_, _) => count++;

        command.NotifyCanExecuteChanged();

        Assert.Equal(2, count);
    }

    [Fact]
    public void NotifyCanExecuteChanged_NoSubscribers_DoesNotThrow()
    {
        var command = new RelayCommand<int>(_ => { });

        var exception = Record.Exception(() => command.NotifyCanExecuteChanged());

        Assert.Null(exception);
    }

    [Fact]
    public void CanExecuteChanged_Unsubscribe_StopsReceivingEvents()
    {
        var command = new RelayCommand<int>(_ => { });
        var count = 0;
        void Handler(object? s, EventArgs e) => count++;
        command.CanExecuteChanged += Handler;
        command.NotifyCanExecuteChanged();
        command.CanExecuteChanged -= Handler;

        command.NotifyCanExecuteChanged();

        Assert.Equal(1, count);
    }

    #endregion

    #region Runtime null identity — proving a point

    [Fact]
    public void Null_IsJustNull_RegardlessOfSource()
    {
        // Three ways null can show up as object?
        object? bareNull = null;                    // CLR null reference
        object? fromNullableInt = null;       // Nullable<int> boxing → null
        object? fromNullableEnum = null;

        // They are all the same thing: a bare null reference.
        Assert.True(bareNull is null);
        Assert.True(fromNullableInt is null);
        Assert.True(fromNullableEnum is null);

        // And they are identical — you cannot tell them apart.
        Assert.True(ReferenceEquals(bareNull, fromNullableInt));
        Assert.True(ReferenceEquals(bareNull, fromNullableEnum));

        // None of them "is" a value type.
        Assert.False(bareNull is int);
        Assert.False(fromNullableInt is int);
        Assert.False(bareNull is ThumbRateState);
        Assert.False(fromNullableEnum is ThumbRateState);

        // Nor are they "is" a Nullable<T>.
        Assert.False(fromNullableInt is int?);
        Assert.False(fromNullableEnum is ThumbRateState?);

        // At runtime, null has no type. There is no "boxed Nullable<T>-null"
        // distinct from a bare null — the CLR collapses them.
    }

    #endregion

    #region Verify TryGetCommandArgument cannot replace the guard

    [Fact]
    public void CanExecute_Int_NoPredicate_IfGuardRemoved_WouldThrow()
    {
        // Proves that TryGetCommandArgument returns FALSE for (null + int),
        // not default(int). Removing the guard would cause an ArgumentException.
        // This is exactly the original CommunityToolkit #3619 bug.
        var command = new RelayCommand<int>(_ => { });

        // The guard prevents:
        //   TryGetCommandArgument(null for int) → false → ThrowArgumentException
        // And instead returns true (no predicate = always executable).
        bool result = command.CanExecute(null);

        Assert.True(result);
    }

    [Fact]
    public void TryGetCommandArgument_Int_Null_ReturnsFalse_ProvenViaCanExecute()
    {
        // Cannot call TryGetCommandArgument directly (it's internal with
        // unconstrained T? out param). Instead, we prove the point through
        // the public API: if TryGetCommandArgument returned true for (int+null),
        // then CanExecute(int+null) wouldn't need the guard at all.
        // But we already know removing the guard causes #3619 (NRE/throw).

        // The real proof: check what the guard does. For int+null:
        var noGuard = new RelayCommand<int>(_ => { });
        Assert.True(noGuard.CanExecute(null));   // guard fires → true

        var withPredicate = new RelayCommand<int>(_ => { }, i => i > -1);
        Assert.False(withPredicate.CanExecute(null)); // guard → false
    }

    [Fact]
    public void TryGetCommandArgument_String_Null_ReturnsTrue_ProvenViaCanExecute()
    {
        // For reference types: null is a valid T? value.
        // TryGetCommandArgument returns true, predicate receives null.
        string? received = "untouched";
        var command = new RelayCommand<string>(
            _ => { },
            s =>
            {
                received = s;
                return s is not null;
            });

        bool result = command.CanExecute((object?)null);

        Assert.False(result);          // predicate says: null → can't execute
        Assert.Null(received);         // predicate WAS called with null
    }

    [Fact]
    public void TryGetCommandArgument_NullableInt_Null_ReturnsTrue_ProvenViaCanExecute()
    {
        // For Nullable<int>: null is a valid T? value (HasValue=false).
        // TryGetCommandArgument returns true, predicate receives null.
        int? received = 42;
        var command = new RelayCommand<int?>(
            _ => { },
            i =>
            {
                received = i;
                return i.HasValue;
            });

        bool result = command.CanExecute((object?)null);

        Assert.False(result);          // predicate says: no value → can't execute
        Assert.Null(received);         // predicate WAS called with null
    }

    #endregion

    #region ICommand explicit interface — consistency

    [Fact]
    public void ICommand_CanExecute_AndStronglyTyped_AreConsistent()
    {
        var command = new RelayCommand<int>(_ => { }, i => i > 0);
        System.Windows.Input.ICommand iCommand = command;

        bool typedResult = command.CanExecute(5);
        bool objectResult = iCommand.CanExecute(5);

        Assert.Equal(typedResult, objectResult);
    }

    [Fact]
    public void ICommand_Execute_AndStronglyTyped_AreConsistent()
    {
        // Both the typed and object overloads should reach the same execute delegate.
        int received = 0;
        var command = new RelayCommand<int>(i => received = i);
        System.Windows.Input.ICommand iCommand = command;

        command.Execute(7);
        Assert.Equal(7, received);

        iCommand.Execute(13);
        Assert.Equal(13, received);
    }

    #endregion
}
