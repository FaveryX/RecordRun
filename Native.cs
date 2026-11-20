// RecordRun - record and replay mouse and keyboard input
// Copyright (C) 2026 FaveryX
// SPDX-License-Identifier: GPL-3.0-or-later
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System.Runtime.InteropServices;
namespace RecordRun;

static class Native
{
    public const int WH_KEYBOARD_LL = 13, WH_MOUSE_LL = 14;
    public const int WM_MOUSEMOVE = 0x200, WM_LBUTTONDOWN = 0x201, WM_LBUTTONUP = 0x202,
        WM_RBUTTONDOWN = 0x204, WM_RBUTTONUP = 0x205, WM_MBUTTONDOWN = 0x207, WM_MBUTTONUP = 0x208,
        WM_MOUSEWHEEL = 0x20A, WM_MOUSEHWHEEL = 0x20E;
    public const int WM_KEYDOWN = 0x100, WM_KEYUP = 0x101, WM_SYSKEYDOWN = 0x104, WM_SYSKEYUP = 0x105;

    const uint MOVE = 1, LDOWN = 2, LUP = 4, RDOWN = 8, RUP = 0x10, MDOWN = 0x20, MUP = 0x40,
        WHEEL = 0x800, HWHEEL = 0x1000, VIRTUALDESK = 0x4000, ABSOLUTE = 0x8000;
    const uint KEYUP = 2, EXTENDED = 1;

    // Marks events we inject so our own hooks ignore them.
    public static readonly UIntPtr Tag = (UIntPtr)0x52454D53;

    public delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct MSLL { public POINT pt; public uint data, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] public struct KBLL { public uint vk, scan, flags, time; public UIntPtr extra; }

    [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx, dy; public uint data, flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion u; }

    [DllImport("user32.dll", SetLastError = true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc fn, IntPtr mod, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int code, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll")] public static extern IntPtr GetModuleHandle(string name);
    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int i);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
    [DllImport("winmm.dll")] public static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] public static extern uint timeEndPeriod(uint ms);

    static void Send(INPUT i) => SendInput(1, new[] { i }, Marshal.SizeOf<INPUT>());

    // Absolute move across the whole virtual desktop (all monitors), optionally combined with a button action.
    static void Mouse(uint flags, int x, int y, uint data, bool abs)
    {
        var mi = new MOUSEINPUT { flags = flags, data = data, extra = Tag };
        if (abs)
        {
            int vx = GetSystemMetrics(76), vy = GetSystemMetrics(77), vw = GetSystemMetrics(78), vh = GetSystemMetrics(79);
            mi.dx = (int)Math.Round((x - vx) * 65535.0 / Math.Max(1, vw - 1));
            mi.dy = (int)Math.Round((y - vy) * 65535.0 / Math.Max(1, vh - 1));
            mi.flags |= MOVE | ABSOLUTE | VIRTUALDESK;
        }
        Send(new INPUT { type = 0, u = new InputUnion { mi = mi } });
    }

    public static void MoveTo(int x, int y) => Mouse(0, x, y, 0, true);
    public static void ButtonDown(int b, int x, int y) => Mouse(b == 0 ? LDOWN : b == 1 ? RDOWN : MDOWN, x, y, 0, true);
    public static void ButtonUp(int b, int x, int y) => Mouse(b == 0 ? LUP : b == 1 ? RUP : MUP, x, y, 0, true);
    public static void ButtonUpHere(int b) => Mouse(b == 0 ? LUP : b == 1 ? RUP : MUP, 0, 0, 0, false);
    public static void Wheel(int delta) => Mouse(WHEEL, 0, 0, (uint)delta, false);
    public static void HWheel(int delta) => Mouse(HWHEEL, 0, 0, (uint)delta, false);

    public static void Key(int vk, int scan, bool ext, bool up)
    {
        var ki = new KEYBDINPUT { vk = (ushort)vk, scan = (ushort)scan, extra = Tag,
            flags = (up ? KEYUP : 0) | (ext ? EXTENDED : 0) };
        Send(new INPUT { type = 1, u = new InputUnion { ki = ki } });
    }
}
