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

using System.Diagnostics;
using System.Runtime.InteropServices;
namespace RecordRun;

class MainForm : Form
{
    enum State { Idle, Recording, Playing }
    State state = State.Idle;

    Recording current;           // loaded or just-recorded
    string currentName;
    readonly List<Evt> buf = new();
    readonly Stopwatch recSw = new();
    readonly Player player = new();

    IntPtr kbHook, mouseHook;
    readonly Native.HookProc kbProc, mouseProc;
    bool f12Swallow;

    Button btnRec, btnStopRec, btnPlay, btnPause, btnStopPlay, btnSave, btnLoad;
    NumericUpDown nudTimes, nudDelay;
    CheckBox chkInf;
    Label lblName, lblDur, lblStatus;
    readonly System.Windows.Forms.Timer ui = new() { Interval = 100 };

    public MainForm()
    {
        kbProc = KbProc; mouseProc = MouseProc;
        Text = "RecordRun"; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(420, 374);

        Controls.Add(Hdr("Record", 12, 10));
        btnRec = B("● Record", 12, 32, 196, 34, (_, _) => StartRecording());
        btnStopRec = B("■ Stop recording", 212, 32, 196, 34, (_, _) => StopRecording(true));

        Controls.Add(Hdr("Playback", 12, 78));
        btnPlay = B("▶ Play", 12, 100, 130, 34, (_, _) => StartPlayback());
        btnPause = B("⏸ Pause", 146, 100, 130, 34, (_, _) => { if (player.Paused) player.Resume(); else player.Pause(); UpdateUi(); });
        btnStopPlay = B("■ Stop", 280, 100, 128, 34, (_, _) => player.Stop());

        Controls.Add(Hdr("Repeat", 12, 146));
        nudTimes = new NumericUpDown { Left = 12, Top = 170, Width = 64, Minimum = 1, Maximum = 99999, Value = 1 };
        chkInf = new CheckBox { Text = "Infinite loop", Left = 90, Top = 171, Width = 110 };
        var ld = new Label { Text = "Delay (s):", Left = 212, Top = 174, Width = 62 };
        nudDelay = new NumericUpDown { Left = 278, Top = 170, Width = 70, Minimum = 0, Maximum = 86400, DecimalPlaces = 1, Increment = 0.5M };
        Controls.AddRange(new Control[] { nudTimes, chkInf, ld, nudDelay });
        chkInf.CheckedChanged += (_, _) => UpdateUi();

        btnSave = B("Save…", 12, 212, 196, 32, (_, _) => Save());
        btnLoad = B("Load…", 212, 212, 196, 32, (_, _) => LoadDialog());

        lblName = new Label { Left = 12, Top = 258, Width = 396, Text = "Recording: (none)" };
        lblDur = new Label { Left = 12, Top = 280, Width = 396, Text = "" };
        lblStatus = new Label { Left = 12, Top = 302, Width = 396, Text = "Idle" };
        var hint = new Label { Left = 12, Top = 326, Width = 396, Text = "Emergency stop: Ctrl+Alt+F12", ForeColor = Color.DimGray };
        var copy = new Label { Left = 12, Top = 348, Width = 396, Text = "RecordRun © 2026 FaveryX", ForeColor = Color.DimGray };
        Controls.AddRange(new Control[] { lblName, lblDur, lblStatus, hint, copy });

        player.Finished += () => { if (!IsDisposed) BeginInvoke(new Action(() => { state = State.Idle; UpdateUi(); })); };
        ui.Tick += (_, _) => UpdateUi(); ui.Start();

        Load += (_, _) =>
        {
            kbHook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, kbProc, Native.GetModuleHandle(null), 0);
            if (kbHook == IntPtr.Zero) MessageBox.Show(this, "Could not install the keyboard hook (emergency hotkey and key recording unavailable).");
            UpdateUi();
        };
        FormClosing += (_, _) => { player.Stop(); Unhook(ref mouseHook); Unhook(ref kbHook); };
    }

    static Label Hdr(string t, int x, int y) => new() { Text = t, Left = x, Top = y, Width = 200, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
    Button B(string t, int x, int y, int w, int h, EventHandler c)
    {
        var b = new Button { Text = t, Left = x, Top = y, Width = w, Height = h };
        b.Click += c; Controls.Add(b); return b;
    }
    static void Unhook(ref IntPtr h) { if (h != IntPtr.Zero) { Native.UnhookWindowsHookEx(h); h = IntPtr.Zero; } }

    // ---------- Hooks ----------
    IntPtr KbProc(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0)
        {
            var k = Marshal.PtrToStructure<Native.KBLL>(l);
            if (k.extra != Native.Tag)
            {
                int msg = w.ToInt32();
                bool down = msg == Native.WM_KEYDOWN || msg == Native.WM_SYSKEYDOWN;
                if (k.vk == 0x7B) // F12
                {
                    bool ctrlAlt = (Native.GetAsyncKeyState(0x11) & 0x8000) != 0 && (Native.GetAsyncKeyState(0x12) & 0x8000) != 0;
                    if (down && ctrlAlt) { f12Swallow = true; BeginInvoke(new Action(EmergencyStop)); return (IntPtr)1; }
                    if (!down && f12Swallow) { f12Swallow = false; return (IntPtr)1; }
                }
                if (state == State.Recording)
                    buf.Add(new Evt { T = recSw.Elapsed.TotalMilliseconds, K = down ? Kind.KeyDown : Kind.KeyUp,
                        A = (int)k.vk, Scan = (int)k.scan, Ext = (k.flags & 1) != 0 });
            }
        }
        return Native.CallNextHookEx(kbHook, code, w, l);
    }

    IntPtr MouseProc(int code, IntPtr w, IntPtr l)
    {
        if (code >= 0 && state == State.Recording)
        {
            var m = Marshal.PtrToStructure<Native.MSLL>(l);
            if (m.extra != Native.Tag)
            {
                var e = new Evt { T = recSw.Elapsed.TotalMilliseconds, X = m.pt.X, Y = m.pt.Y };
                bool ok = true;
                switch (w.ToInt32())
                {
                    case Native.WM_MOUSEMOVE: e.K = Kind.Move; break;
                    case Native.WM_LBUTTONDOWN: e.K = Kind.ButtonDown; e.A = 0; break;
                    case Native.WM_LBUTTONUP: e.K = Kind.ButtonUp; e.A = 0; break;
                    case Native.WM_RBUTTONDOWN: e.K = Kind.ButtonDown; e.A = 1; break;
                    case Native.WM_RBUTTONUP: e.K = Kind.ButtonUp; e.A = 1; break;
                    case Native.WM_MBUTTONDOWN: e.K = Kind.ButtonDown; e.A = 2; break;
                    case Native.WM_MBUTTONUP: e.K = Kind.ButtonUp; e.A = 2; break;
                    case Native.WM_MOUSEWHEEL: e.K = Kind.Wheel; e.A = (short)(m.data >> 16); break;
                    case Native.WM_MOUSEHWHEEL: e.K = Kind.HWheel; e.A = (short)(m.data >> 16); break;
                    default: ok = false; break;
                }
                if (ok) buf.Add(e);
            }
        }
        return Native.CallNextHookEx(mouseHook, code, w, l);
    }

    // ---------- Record ----------
    void StartRecording()
    {
        if (state != State.Idle) return;
        buf.Clear();
        Native.GetCursorPos(out var p);
        recSw.Restart();
        buf.Add(new Evt { T = 0, K = Kind.Move, X = p.X, Y = p.Y });
        mouseHook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, mouseProc, Native.GetModuleHandle(null), 0);
        if (mouseHook == IntPtr.Zero) { MessageBox.Show(this, "Could not install the mouse hook."); return; }
        state = State.Recording; UpdateUi();
    }

    void StopRecording(bool viaButton)
    {
        if (state != State.Recording) return;
        state = State.Idle;
        Unhook(ref mouseHook);

        if (viaButton)
        {
            // Drop the click on our own Stop button (and the moves leading to it).
            var b = Bounds;
            int i = buf.FindLastIndex(e => e.K == Kind.ButtonDown && b.Contains(e.X, e.Y));
            if (i > 0) buf.RemoveRange(i, buf.Count - i);
            while (buf.Count > 1 && buf[^1].K == Kind.Move) buf.RemoveAt(buf.Count - 1);
        }
        // Drop trailing Ctrl/Alt presses left over from the emergency hotkey.
        while (buf.Count > 1 && buf[^1].K is Kind.KeyDown or Kind.KeyUp && buf[^1].A is 0x11 or 0x12 or 0xA2 or 0xA3 or 0xA4 or 0xA5)
            buf.RemoveAt(buf.Count - 1);

        current = new Recording { Events = buf.ToList(), DurationMs = buf.Count > 0 ? buf[^1].T : 0 };
        currentName = null;
        UpdateUi();
    }

    // ---------- Playback ----------
    void StartPlayback()
    {
        if (state != State.Idle) return;
        if (current == null || current.Events.Count == 0) { MessageBox.Show(this, "Nothing to play. Record or load something first."); return; }
        state = State.Playing;
        player.Start(current, (int)nudTimes.Value, chkInf.Checked, (double)nudDelay.Value);
        UpdateUi();
    }

    void EmergencyStop()
    {
        player.Stop();
        StopRecording(false);
        UpdateUi();
    }

    // ---------- Save / Load ----------
    void Save()
    {
        if (state != State.Idle || current == null || current.Events.Count == 0) { MessageBox.Show(this, "No recording to save."); return; }
        var n = Dialogs.Prompt(this, "Save recording", "Name:", currentName ?? "Recording " + DateTime.Now.ToString("yyyy-MM-dd HH-mm"));
        if (string.IsNullOrWhiteSpace(n)) return;
        n = Store.Clean(n);
        if (n.Length == 0) return;
        if (Store.Exists(n) && n != currentName &&
            MessageBox.Show(this, $"\"{n}\" exists. Overwrite?", "Save", MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        try { Store.Save(n, current); currentName = n; UpdateUi(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }

    void LoadDialog()
    {
        if (state != State.Idle) return;
        using var f = new LibraryForm();
        var res = f.ShowDialog(this);
        if (f.RenamedFrom != null && f.RenamedFrom == currentName) currentName = f.RenamedTo;
        if (f.DeletedName != null && f.DeletedName == currentName) currentName = null;
        if (res == DialogResult.OK && f.Selected != null)
        {
            try { current = Store.Load(f.Selected); currentName = f.Selected; }
            catch (Exception ex) { MessageBox.Show(this, "Could not load: " + ex.Message); }
        }
        UpdateUi();
    }

    // ---------- UI state ----------
    static string Fmt(double ms) => TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss\.f");

    void UpdateUi()
    {
        bool idle = state == State.Idle, rec = state == State.Recording, play = state == State.Playing;
        btnRec.Enabled = idle; btnStopRec.Enabled = rec;
        btnPlay.Enabled = idle && current != null; btnPause.Enabled = play; btnStopPlay.Enabled = play;
        btnPause.Text = player.Paused ? "▶ Resume" : "⏸ Pause";
        btnSave.Enabled = idle && current != null; btnLoad.Enabled = idle;
        nudTimes.Enabled = idle && !chkInf.Checked; chkInf.Enabled = idle; nudDelay.Enabled = idle;

        lblName.Text = "Recording: " + (current == null ? "(none)" : currentName ?? "(unsaved)");
        if (rec)
        {
            lblDur.Text = $"Duration: {Fmt(recSw.Elapsed.TotalMilliseconds)}  ·  {buf.Count} events";
            lblStatus.Text = "Status: Recording…";
        }
        else
        {
            lblDur.Text = current == null ? "" : $"Duration: {Fmt(current.DurationMs)}  ·  {current.Events.Count} events";
            if (play)
            {
                string run = player.Infinite ? $"{player.Run}/∞" : $"{player.Run}/{player.Total}";
                string what = player.InDelay ? "Waiting between repeats" : "Playing";
                lblStatus.Text = $"Status: {(player.Paused ? "Paused" : what)} — run {run}  ·  {Fmt(player.Now())}";
            }
            else lblStatus.Text = "Status: Idle";
        }
    }
}
