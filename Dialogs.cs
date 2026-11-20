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

namespace RecordRun;

static class Dialogs
{
    public static string Prompt(IWin32Window owner, string title, string label, string def)
    {
        using var f = new Form { Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(340, 100), MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false };
        var l = new Label { Text = label, Left = 12, Top = 12, Width = 316 };
        var t = new TextBox { Text = def ?? "", Left = 12, Top = 34, Width = 316 };
        var ok = new Button { Text = "OK", Left = 172, Top = 64, Width = 75, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Left = 253, Top = 64, Width = 75, DialogResult = DialogResult.Cancel };
        f.Controls.AddRange(new Control[] { l, t, ok, cancel });
        f.AcceptButton = ok; f.CancelButton = cancel;
        f.Shown += (_, _) => t.SelectAll();
        return f.ShowDialog(owner) == DialogResult.OK ? t.Text.Trim() : null;
    }
}

class LibraryForm : Form
{
    readonly ListBox lb = new() { Left = 12, Top = 12, Width = 296, Height = 210 };
    public string Selected { get; private set; }
    public string RenamedFrom, RenamedTo, DeletedName;

    public LibraryForm()
    {
        Text = "Saved recordings"; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 236); MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        var load = new Button { Text = "Load", Left = 320, Top = 12, Width = 88, Height = 30 };
        var ren = new Button { Text = "Rename", Left = 320, Top = 48, Width = 88, Height = 30 };
        var del = new Button { Text = "Delete", Left = 320, Top = 84, Width = 88, Height = 30 };
        var close = new Button { Text = "Close", Left = 320, Top = 192, Width = 88, Height = 30, DialogResult = DialogResult.Cancel };
        Controls.AddRange(new Control[] { lb, load, ren, del, close });
        CancelButton = close;
        Refill();

        load.Click += (_, _) => { if (lb.SelectedItem != null) { Selected = (string)lb.SelectedItem; DialogResult = DialogResult.OK; } };
        lb.DoubleClick += (_, _) => load.PerformClick();
        ren.Click += (_, _) =>
        {
            if (lb.SelectedItem is not string old) return;
            var n = Dialogs.Prompt(this, "Rename", "New name:", old);
            if (string.IsNullOrWhiteSpace(n)) return;
            n = Store.Clean(n);
            if (n == old) return;
            if (Store.Exists(n)) { MessageBox.Show(this, "A recording with that name already exists."); return; }
            try { Store.Rename(old, n); RenamedFrom = old; RenamedTo = n; Refill(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        };
        del.Click += (_, _) =>
        {
            if (lb.SelectedItem is not string n) return;
            if (MessageBox.Show(this, $"Delete \"{n}\"?", "Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { Store.Delete(n); DeletedName = n; Refill(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        };
    }

    void Refill() { lb.Items.Clear(); foreach (var n in Store.Names()) lb.Items.Add(n); }
}
