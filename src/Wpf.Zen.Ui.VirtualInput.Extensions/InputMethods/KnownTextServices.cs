// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;

/// <summary>
/// Shares TSF service identities between adapters and candidate policy.
/// Names identify specific vendors and services, rather than general input schemes or languages.
/// Each consumer defines its own supported capabilities for these identities.
/// </summary>
internal static class KnownTextServices
{
    /// <summary>
    /// Identifies the Microsoft Pinyin text service.
    /// </summary>
    internal static readonly Guid MicrosoftPinyin = new("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E");

    /// <summary>
    /// Identifies the Microsoft Bopomofo text service.
    /// </summary>
    internal static readonly Guid MicrosoftBopomofo = new("B115690A-EA02-48D5-A231-E3578D2FDF80");

    /// <summary>
    /// Identifies the IFlytek text service.
    /// </summary>
    internal static readonly Guid IFlytek = new("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643");

    /// <summary>
    /// Identifies the WeChat text service.
    /// </summary>
    internal static readonly Guid WeChat = new("86598FB9-66A2-463E-B9C2-AEB906D477AD");

    /// <summary>
    /// Identifies the Microsoft Japanese text service.
    /// </summary>
    internal static readonly Guid MicrosoftJapanese = new("03B5835F-F03C-411B-9CE2-AA23E1171E36");

    /// <summary>
    /// Identifies the Microsoft Cangjie text service.
    /// </summary>
    internal static readonly Guid MicrosoftCangjie = new("531FDEBF-9B4C-4A43-A2AA-960E8FCDC732");

    /// <summary>
    /// Identifies the Microsoft Korean text service.
    /// </summary>
    internal static readonly Guid MicrosoftKorean = new("A028AE76-01B1-46C2-99C4-ACD9858AE02F");

    /// <summary>
    /// Identifies the Microsoft Old Hangul text service.
    /// </summary>
    internal static readonly Guid MicrosoftOldHangul = new("A1E2B86B-924A-4D43-80F6-8A820DF7190F");

    /// <summary>
    /// Identifies the WindowsTable text service.
    /// </summary>
    internal static readonly Guid WindowsTable = new("E429B25A-E5D3-4D1F-9BE3-0C608477E3A1");
}
