// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Wpf.Ui.Controls;

// 这是未公开的API
internal enum WindowCompositionAttrib
{
    WCA_UNDEFINED                     = 0,
    WCA_NCRENDERING_ENABLED           = 1,
    WCA_NCRENDERING_POLICY            = 2,
    WCA_TRANSITIONS_FORCEDISABLED     = 3,
    WCA_ALLOW_NCPAINT                 = 4,
    WCA_CAPTION_BUTTON_BOUNDS         = 5,
    WCA_NONCLIENT_RTL_LAYOUT          = 6,
    WCA_FORCE_ICONIC_REPRESENTATION   = 7,
    WCA_EXTENDED_FRAME_BOUNDS         = 8,
    WCA_HAS_ICONIC_BITMAP             = 9,
    WCA_THEME_ATTRIBUTES              = 10,
    WCA_NCRENDERING_EXILED            = 11,
    WCA_NCADORNMENTINFO               = 12,
    WCA_EXCLUDED_FROM_LIVEPREVIEW     = 13,
    WCA_VIDEO_OVERLAY_ACTIVE          = 14,
    WCA_FORCE_ACTIVEWINDOW_APPEARANCE = 15,
    WCA_DISALLOW_PEEK                 = 16,
    WCA_CLOAK                         = 17,
    WCA_CLOAKED                       = 18,
    WCA_ACCENT_POLICY                 = 19,
    WCA_FREEZE_REPRESENTATION         = 20,
    WCA_EVER_UNCLOAKED                = 21,
    WCA_VISUAL_OWNER                  = 22,
    WCA_LAST                          = 23,
    WCA_EXCLUDED_FROM_DDA             = 24, // 这是公开的
}

// 		### **1. WCA_UNDEFINED (0)**
// 		- **未定义**，保留值。
// 		
// 		---
// 		
// 		### **2. WCA_NCRENDERING_ENABLED (1)**
// 		- **控制非客户区的渲染状态**。
// 		- 启用或禁用窗口非客户区（标题栏、边框）的渲染。
// 		
// 		---
// 		
// 		### **3. WCA_NCRENDERING_POLICY (2)**
// 		- **设置非客户区的渲染策略**。
// 		- 控制非客户区的渲染方式（如是否启用透明效果）。
// 		
// 		---
// 		
// 		### **4. WCA_TRANSITIONS_FORCEDISABLED (3)**
// 		- **强制禁用窗口过渡动画**。
// 		- 禁用窗口打开、关闭、最小化等过渡动画。
// 		
// 		---
// 		
// 		### **5. WCA_ALLOW_NCPAINT (4)**
// 		- **允许非客户区自定义绘制**。
// 		- 允许应用程序在非客户区进行自定义绘制。
// 		
// 		---
// 		
// 		### **6. WCA_NONCLIENT_RTL_LAYOUT (5)**
// 		- **设置非客户区的布局方向（从右到左）**。
// 		- 用于支持从右到左（RTL）语言的窗口布局。
// 		
// 		---
// 		
// 		### **7. WCA_FORCE_ICONIC_REPRESENTATION (6)**
// 		- **强制窗口以图标形式显示**。
// 		- 通常用于任务栏预览或 Alt+Tab 切换窗口时的显示效果。
// 		
// 		---
// 		
// 		### **8. WCA_EXTENDED_FRAME_BOUNDS (7)**
// 		- **获取窗口扩展边框的边界**。
// 		- 用于获取窗口的实际边框范围（包括阴影等效果）。
// 		
// 		---
// 		
// 		### **9. WCA_HAS_ICONIC_BITMAP (8)**
// 		- **窗口是否具有图标化位图**。
// 		- 用于判断窗口是否有用于任务栏预览的图标化位图。
// 		
// 		---
// 		
// 		### **10. WCA_THEME_ATTRIBUTES (9)**
// 		- **设置窗口的主题属性**。
// 		- 控制窗口的主题样式（如颜色、字体等）。
// 		
// 		---
// 		
// 		### **11. WCA_NCRENDERING_EXILED (10)**
// 		- **将非客户区渲染排除在外**。
// 		- 用于某些特殊场景下禁用非客户区的渲染。
// 		
// 		---
// 		
// 		### **12. WCA_NCADORNMENTINFO (11)**
// 		- **获取非客户区的装饰信息**。
// 		- 用于获取窗口边框、标题栏等装饰信息。
// 		
// 		---
// 		
// 		### **13. WCA_EXCLUDED_FROM_LIVEPREVIEW (12)**
// 		- **将窗口从实时预览中排除**。
// 		- 禁用窗口在任务栏预览或 Alt+Tab 中的实时预览效果。
// 		
// 		---
// 		
// 		### **14. WCA_VIDEO_OVERLAY_ACTIVE (13)**
// 		- **视频叠加层是否激活**。
// 		- 用于判断窗口是否有视频叠加层（如全屏视频播放）。
// 		
// 		---
// 		
// 		### **15. WCA_FORCE_ACTIVEWINDOW_APPEARANCE (14)**
// 		- **强制窗口显示为活动状态**。
// 		- 即使窗口未激活，也显示为活动状态的外观。
// 		
// 		---
// 		
// 		### **16. WCA_DISALLOW_PEEK (15)**
// 		- **禁用窗口的 Peek 预览**。
// 		- 禁用鼠标悬停在任务栏时窗口的预览效果。
// 		
// 		---
// 		
// 		### **17. WCA_CLOAK (16)**
// 		- **隐藏窗口内容**。
// 		- 用于临时隐藏窗口内容（如切换虚拟桌面时）。
// 		
// 		---
// 		
// 		### **18. WCA_CLOAKED (17)**
// 		- **判断窗口是否被隐藏**。
// 		- 用于检查窗口是否处于隐藏状态。
// 		
// 		---
// 		
// 		### **19. WCA_ACCENT_POLICY (18)**
// 		- **设置窗口的强调色策略**。
// 		- **最常用的枚举项**，用于控制窗口的背景模糊、透明等效果（如 Windows 10/11 的亚克力效果）。
// 		
// 		---
// 		
// 		### **20. WCA_FREEZE_REPRESENTATION (19)**
// 		- **冻结窗口的表示**。
// 		- 用于在某些场景下冻结窗口的视觉表示（如截图时）。
// 		
// 		---
// 		
// 		### **21. WCA_EVER_UNCLOAKED (20)**
// 		- **窗口是否从未被隐藏过**。
// 		- 用于检查窗口是否一直处于可见状态。
// 		
// 		---
// 		
// 		### **22. WCA_VISUAL_OWNER (21)**
// 		- **设置窗口的视觉所有者**。
// 		- 用于指定窗口的视觉所有者（如子窗口的父窗口）。
// 		
// 		---
// 		
// 		### **23. WCA_LAST (22)**
// 		- **枚举的最后一个值**
// gptc 


/* https://learn.microsoft.com/en-us/windows/win32/dwm/windowcompositionattribdata
typedef struct _WINDOWCOMPOSITIONATTRIBDATA
{
    WINDOWCOMPOSITIONATTRIB Attrib;
    PVOID                   pvData;
    SIZE_T                  cbData;
}
WINDOWCOMPOSITIONATTRIBDATA;

typedef enum _ACCENT_STATE
{
    ACCENT_DISABLED                   = 0,
    ACCENT_ENABLE_GRADIENT            = 1,
    ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
    ACCENT_ENABLE_BLURBEHIND          = 3,
    ACCENT_ENABLE_ACRYLICBLURBEHIND   = 4,
    ACCENT_INVALID_STATE              = 5
}
ACCENT_STATE;

typedef struct _ACCENT_POLICY
{
    ACCENT_STATE AccentState;
    DWORD        AccentFlags;
    DWORD        GradientColor;
    DWORD        AnimationId;
}
ACCENT_POLICY;
*/
