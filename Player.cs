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
namespace RecordRun;

class Player
{
    readonly ManualResetEventSlim gate = new(true);
    CancellationTokenSource cts;
    long clockStart, pausedTicks, pauseAt;
    readonly Dictionary<int, Evt> keysDown = new();
    readonly HashSet<int> buttonsDown = new();

    public volatile bool Running, InDelay;
    public volatile int Run;
    public int Total;
    public bool Infinite;
    public event Action Finished;

    public bool Paused => Running && !gate.IsSet;

    void StartClock() { clockStart = Stopwatch.GetTimestamp(); pausedTicks = 0; }
    public double Now()
    {
        long end = Paused ? pauseAt : Stopwatch.GetTimestamp();
        return (end - clockStart - pausedTicks) * 1000.0 / Stopwatch.Frequency;
    }

    public void Start(Recording r, int times, bool infinite, double delaySec)
    {
        Total = times; Infinite = infinite; Running = true; gate.Set();
        cts = new CancellationTokenSource();
        var ct = cts.Token;
        new Thread(() => Loop(r, delaySec, ct)) { IsBackground = true, Priority = ThreadPriority.AboveNormal }.Start();
    }

    public void Pause() { if (!Running || Paused) return; pauseAt = Stopwatch.GetTimestamp(); gate.Reset(); }
    public void Resume() { if (!Paused) return; pausedTicks += Stopwatch.GetTimestamp() - pauseAt; gate.Set(); }
    public void Stop() { cts?.Cancel(); gate.Set(); }

    void Loop(Recording r, double delaySec, CancellationToken ct)
    {
        Native.timeBeginPeriod(1);
        try
        {
            for (Run = 1; Infinite || Run <= Total; Run++)
            {
                InDelay = false; StartClock();
                foreach (var e in r.Events) { WaitUntil(e.T, ct); Send(e); }
                WaitUntil(Math.Max(r.DurationMs, 1), ct);
                ReleaseAll();
                if (Infinite || Run < Total) { InDelay = true; StartClock(); WaitUntil(Math.Max(delaySec * 1000, 0), ct); }
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            ReleaseAll();
            Native.timeEndPeriod(1);
            Running = false; InDelay = false;
            Finished?.Invoke();
        }
    }

    // Sleeps coarsely, then spins for the last ~2 ms for accurate timing. Honors pause and cancel.
    void WaitUntil(double targetMs, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (!gate.IsSet) { gate.Wait(ct); continue; }
            double rem = targetMs - Now();
            if (rem <= 0) return;
            if (rem > 2) Thread.Sleep(1); else Thread.SpinWait(100);
        }
    }

    void Send(Evt e)
    {
        switch (e.K)
        {
            case Kind.Move: Native.MoveTo(e.X, e.Y); break;
            case Kind.ButtonDown: buttonsDown.Add(e.A); Native.ButtonDown(e.A, e.X, e.Y); break;
            case Kind.ButtonUp: buttonsDown.Remove(e.A); Native.ButtonUp(e.A, e.X, e.Y); break;
            case Kind.Wheel: Native.Wheel(e.A); break;
            case Kind.HWheel: Native.HWheel(e.A); break;
            case Kind.KeyDown: keysDown[e.A] = e; Native.Key(e.A, e.Scan, e.Ext, false); break;
            case Kind.KeyUp: keysDown.Remove(e.A); Native.Key(e.A, e.Scan, e.Ext, true); break;
        }
    }

    // Never leave a key or mouse button stuck down after a stop / emergency stop.
    void ReleaseAll()
    {
        foreach (var k in keysDown.Values) Native.Key(k.A, k.Scan, k.Ext, true);
        foreach (var b in buttonsDown) Native.ButtonUpHere(b);
        keysDown.Clear(); buttonsDown.Clear();
    }
}
