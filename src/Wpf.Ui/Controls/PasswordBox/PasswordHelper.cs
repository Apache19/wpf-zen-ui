// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

#pragma warning disable SA1601

public partial class PasswordBox
{
    /// <summary>
    /// Helper class for managing password operations in <see cref="PasswordBox"/>.
    /// </summary>
    private class PasswordHelper
    {
        private readonly PasswordBox _passwordBox;

        private string _currentText;
        private string _newPassword;
        private string _currentPassword;

        /// <summary>
        /// Initializes a new instance of the <see cref="PasswordHelper"/> class.
        /// </summary>
        /// <param name="passwordBox">The parent <see cref="PasswordBox"/> control.</param>
        public PasswordHelper(PasswordBox passwordBox)
        {
            _passwordBox = passwordBox;
            _currentText = string.Empty;
            _newPassword = string.Empty;
            _currentPassword = string.Empty;
        }

        /// <summary>
        /// Calculates and returns the new password value based on current input.
        /// </summary>
        /// <param name="textChanges">The text changes.</param>
        /// <returns>The updated password string.</returns>
        /// <remarks>
        /// Handles three scenarios:
        /// 1. When text is being deleted
        /// 2. When password is revealed (plain text mode)
        /// 3. When password is hidden (masked character mode)
        /// </remarks>
        public string GetNewPassword(ICollection<TextChange> textChanges)
        {
            _currentPassword = _passwordBox.Password;
            _newPassword = _currentPassword;
            _currentText = _passwordBox.Text;

            if (_passwordBox.IsPasswordRevealed)
            {
                _newPassword = _currentText;
                return _newPassword;
            }

            return HandleHiddenModeChanges(textChanges);
        }

        /// <summary>
        /// Handles password changes when in hidden (masked) mode.
        /// </summary>
        /// <param name="textChanges">The text changes.</param>
        /// <returns>The updated password string.</returns>
        private string HandleHiddenModeChanges(ICollection<TextChange> textChanges)
        {
            string password = _currentPassword ?? string.Empty;

            foreach (TextChange textChange in textChanges)
            {
                int offset = Clamp(textChange.Offset, 0, password.Length);
                int removedLength = Clamp(textChange.RemovedLength, 0, password.Length - offset);

                if (removedLength > 0)
                {
                    password = password.Remove(offset, removedLength);
                }

                if (textChange.AddedLength > 0)
                {
                    string insertedText = GetInsertedText(textChange.Offset, textChange.AddedLength);

                    if (insertedText.Length > 0)
                    {
                        int insertOffset = Clamp(textChange.Offset, 0, password.Length);

                        password = password.Insert(insertOffset, insertedText);
                    }
                }
            }

            return password;
        }

        private string GetInsertedText(int offset, int length)
        {
            if (length <= 0 || string.IsNullOrEmpty(_currentText))
            {
                return string.Empty;
            }

            if (offset < 0)
            {
                length += offset;
                offset = 0;
            }

            if (length <= 0 || offset >= _currentText.Length)
            {
                return string.Empty;
            }

            return _currentText.Substring(offset, Math.Min(length, _currentText.Length - offset));
        }
    }
}
