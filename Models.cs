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

using System.Text.Json;
namespace RecordRun;

enum Kind { Move, ButtonDown, ButtonUp, Wheel, HWheel, KeyDown, KeyUp }

class Evt
{
    public double T { get; set; }      // ms since recording start
    public Kind K { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int A { get; set; }         // button (0=L,1=R,2=M), wheel delta, or virtual-key code
    public int Scan { get; set; }
    public bool Ext { get; set; }
}

class Recording
{
    public double DurationMs { get; set; }
    public DateTime Created { get; set; } = DateTime.Now;
    public List<Evt> Events { get; set; } = new();
}

static class Store
{
    public static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RecordRun", "Recordings");

    static string P(string name) => Path.Combine(Dir, name + ".json");

    public static string Clean(string n)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return n.Trim();
    }

    public static List<string> Names() =>
        !Directory.Exists(Dir) ? new() :
        Directory.GetFiles(Dir, "*.json").Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();

    public static bool Exists(string name) => File.Exists(P(name));
    public static void Save(string name, Recording r) { Directory.CreateDirectory(Dir); File.WriteAllText(P(name), JsonSerializer.Serialize(r)); }
    public static Recording Load(string name) => JsonSerializer.Deserialize<Recording>(File.ReadAllText(P(name)));
    public static void Delete(string name) => File.Delete(P(name));
    public static void Rename(string from, string to) => File.Move(P(from), P(to));
}
