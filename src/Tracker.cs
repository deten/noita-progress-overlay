using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;

namespace NoitaOverlay {

  /// <summary>A snapshot of everything the overlay knows right now.</summary>
  internal sealed class Snapshot {
    public bool GameRunning;                  // the process is up (menu counts)
    public bool InRun;                        // the save is being written to => actually in a run
    public bool Moving;                       // chunks written recently => actively playing
    public string CurrentPlace = "";          // canonical place key, "" if unknown
    public string CurrentBiome = "";          // raw biome id, before collapsing; effects need the exact one
    public string EffectText;                 // the biome's effect, known or noted
    public bool   EffectCanBeNoted;           // rolled here, and not noted yet
    public int    WorldX, WorldY;
    public string Seed = "";
    public string Playtime = "";
    public int    OrbCount;
    public HashSet<string> Flags  = new HashSet<string>();
    public HashSet<string> Places = new HashSet<string>();   // lifetime, canonical
    public HashSet<string> Manual = new HashSet<string>();   // leads the player ticked off
    public string NewlyEntered;               // set for one tick when a new place is first reached
  }

  /// <summary>
  /// Reads Noita's save directory. Never writes to it, never touches the process --
  /// only files the game already produces on its own.
  /// </summary>
  internal sealed class Tracker {

    static readonly Regex ChunkRe = new Regex(@"^world_(-?\d+)_(-?\d+)\.png_petri$", RegexOptions.Compiled);

    readonly string _root;                    // ...\Nolla_Games_Noita
    readonly string _historyPath;
    readonly string _manualPath;
    readonly HashSet<string> _history = new HashSet<string>();
    readonly HashSet<string> _manual = new HashSet<string>();
    string _lastPlace = "";

    public string SaveDir { get; private set; }
    public string Error   { get; private set; }

    public Tracker() {
      _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        @"AppData\LocalLow\Nolla_Games_Noita");
      string appdata = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      string dir = Path.Combine(appdata, "NoitaOverlay");
      Directory.CreateDirectory(dir);
      _historyPath = Path.Combine(dir, "history.txt");
      _manualPath  = Path.Combine(dir, "leads.txt");
      LoadHistory();
      LoadEffects();
      try {
        if (File.Exists(_manualPath))
          foreach (var l in File.ReadAllLines(_manualPath)) {
            var t = l.Trim();
            if (t.Length > 0) _manual.Add(t);
          }
      } catch { }
    }

    // ---- biome effects -----------------------------------------------------
    // The game rolls most of these per world and writes them nowhere, so once you have
    // seen the message we keep it, keyed by seed and biome. Also a record of
    // (seed, biome, modifier) that can be tested against later if the RNG is cracked.

    readonly Dictionary<string, string> _effects = new Dictionary<string, string>();
    string _effectsPath;

    static string EffectKey(string seed, string biome) { return seed + "|" + biome; }

    void LoadEffects() {
      _effectsPath = Path.Combine(Path.GetDirectoryName(_historyPath), "effects.txt");
      try {
        if (!File.Exists(_effectsPath)) return;
        foreach (var l in File.ReadAllLines(_effectsPath)) {
          var p = l.Split('|');
          if (p.Length == 3) _effects[EffectKey(p[0].Trim(), p[1].Trim())] = p[2].Trim();
        }
      } catch { }
    }

    /// <summary>Record what the game told you on entering this biome, for this world.</summary>
    public void NoteEffect(string seed, string biome, string modifierId) {
      if (string.IsNullOrEmpty(seed) || string.IsNullOrEmpty(biome)) return;
      if (modifierId == null) _effects.Remove(EffectKey(seed, biome));
      else _effects[EffectKey(seed, biome)] = modifierId;
      try {
        var lines = new List<string>();
        foreach (var kv in _effects) {
          var p = kv.Key.Split('|');
          lines.Add(p[0] + " | " + p[1] + " | " + kv.Value);
        }
        lines.Sort();
        File.WriteAllLines(_effectsPath, lines.ToArray());
      } catch { }
    }

    public string NotedEffect(string seed, string biome) {
      string v;
      return _effects.TryGetValue(EffectKey(seed, biome), out v) ? v : null;
    }

    /// <summary>Tick a lead off, or un-tick it. Nothing in the game records these.</summary>
    public void ToggleManual(string id) {
      if (_manual.Contains(id)) _manual.Remove(id); else _manual.Add(id);
      try { File.WriteAllLines(_manualPath, _manual.ToArray()); } catch { }
    }

    // ---- history -----------------------------------------------------------

    void LoadHistory() {
      if (File.Exists(_historyPath)) {
        foreach (var l in File.ReadAllLines(_historyPath)) {
          var s = l.Trim();
          if (s.Length > 0) _history.Add(s);
        }
      } else {
        ImportPastRuns();     // first launch: give credit for everything already played
        SaveHistory();
      }
    }

    void SaveHistory() {
      try { File.WriteAllLines(_historyPath, _history.OrderBy(x => x).ToArray()); } catch { }
    }

    /// <summary>Seeds history from every past session's stats file across all save slots.</summary>
    void ImportPastRuns() {
      if (!Directory.Exists(_root)) return;
      foreach (var save in Directory.GetDirectories(_root, "save*")) {
        string sess = Path.Combine(save, @"stats\sessions");
        if (!Directory.Exists(sess)) continue;
        foreach (var f in Directory.GetFiles(sess, "*_stats.xml")) {
          try {
            var doc = new XmlDocument();
            doc.Load(f);
            foreach (XmlNode e in doc.SelectNodes("//biomes_visited/E")) {
              var key = e.Attributes["key"];
              if (key == null) continue;
              var canon = Model.Canon(key.Value.Replace("$biome_", ""));
              if (canon.Length > 0) _history.Add(canon);
            }
          } catch { }
        }
      }
    }

    /// <summary>
    /// Is Noita up? Purely a process-name lookup -- nothing is opened, read or attached to,
    /// so this cannot affect the running game.
    /// </summary>
    static bool IsNoitaRunning() {
      foreach (var name in new[] { "noita", "noita_dev" }) {
        try {
          if (System.Diagnostics.Process.GetProcessesByName(name).Length > 0) return true;
        } catch { }
      }
      return false;
    }

    // ---- save slot selection ----------------------------------------------

    /// <summary>The save slot the game most recently wrote to.</summary>
    string ActiveSave() {
      if (!Directory.Exists(_root)) return null;
      string best = null; DateTime bestT = DateTime.MinValue;
      foreach (var d in Directory.GetDirectories(_root, "save0*")) {
        var w = Path.Combine(d, "world");
        if (!Directory.Exists(w)) continue;
        try {
          var t = new DirectoryInfo(w).LastWriteTimeUtc;
          if (t > bestT) { bestT = t; best = d; }
        } catch { }
      }
      return best;
    }

    // ---- the poll ----------------------------------------------------------

    public Snapshot Poll() {
      var s = new Snapshot();
      try {
        // Independent of the save files, so it still reports correctly on a fresh install.
        s.GameRunning = IsNoitaRunning();

        string save = ActiveSave();
        SaveDir = save;
        if (save == null) { Error = "Noita save folder not found."; return s; }
        Error = null;

        string worldDir = Path.Combine(save, "world");
        var now = DateTime.UtcNow;

        // Save activity tells us something different from "is it open" -- that a run is underway.
        // At the main menu Noita writes nothing at all, which is why process state is checked
        // separately above rather than inferred from the files.
        var autos = Path.Combine(worldDir, ".autosave");
        if (File.Exists(autos) && (now - File.GetLastWriteTimeUtc(autos)).TotalMinutes < 5)
          s.InRun = true;

        // Newest chunk file == roughly where the player is.
        FileInfo newest = null;
        try {
          foreach (var fi in new DirectoryInfo(worldDir).GetFiles("world_*.png_petri"))
            if (newest == null || fi.LastWriteTimeUtc > newest.LastWriteTimeUtc) newest = fi;
        } catch { }

        if (newest != null) {
          var m = ChunkRe.Match(newest.Name);
          if (m.Success) {
            s.WorldX = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            s.WorldY = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            // Sample the chunk centre, not its corner.
            s.CurrentBiome = BiomeMap.At(s.WorldX + 256, s.WorldY + 256);
            s.CurrentPlace = Model.Canon(s.CurrentBiome);
          }
          var age = (now - newest.LastWriteTimeUtc).TotalSeconds;
          s.Moving = age < 20;
          if (s.Moving) s.InRun = true;
        }

        // Lifetime flags.
        string flagDir = Path.Combine(save, @"persistent\flags");
        if (Directory.Exists(flagDir))
          foreach (var f in Directory.GetFiles(flagDir))
            s.Flags.Add(Path.GetFileName(f));

        // Orbs collected, lifetime (one numbered file per orb).
        string orbDir = Path.Combine(save, @"persistent\orbs_new");
        if (Directory.Exists(orbDir))
          s.OrbCount = Directory.GetFiles(orbDir).Count(f => {
            int _n; return int.TryParse(Path.GetFileName(f), out _n);
          });

        // Latest session summary (written on quit, so treated as best-effort).
        string sess = Path.Combine(save, @"stats\sessions");
        if (Directory.Exists(sess)) {
          FileInfo latest = null;
          foreach (var fi in new DirectoryInfo(sess).GetFiles("*_stats.xml"))
            if (latest == null || fi.LastWriteTimeUtc > latest.LastWriteTimeUtc) latest = fi;
          if (latest != null) {
            try {
              var doc = new XmlDocument(); doc.Load(latest.FullName);
              var st = doc.SelectSingleNode("//stats") as XmlElement;
              if (st != null) {
                s.Seed     = st.GetAttribute("world_seed");
                s.Playtime = st.GetAttribute("playtime_str");
              }
              foreach (XmlNode e in doc.SelectNodes("//biomes_visited/E")) {
                var k = e.Attributes["key"];
                if (k == null) continue;
                var c = Model.Canon(k.Value.Replace("$biome_", ""));
                if (c.Length > 0) _history.Add(c);
              }
            } catch { }
          }
        }

        // Live discovery: standing somewhere new counts immediately.
        if (s.CurrentPlace.Length > 0 && s.CurrentPlace != _lastPlace) {
          _lastPlace = s.CurrentPlace;
          if (!_history.Contains(s.CurrentPlace)) {
            _history.Add(s.CurrentPlace);
            s.NewlyEntered = s.CurrentPlace;
            SaveHistory();
          }
        }

        s.Places = new HashSet<string>(_history);
        s.Manual = new HashSet<string>(_manual);

        // Hardcoded effects are certain. Otherwise use whatever you noted for this world.
        s.EffectText = Model.Effect(s.CurrentBiome);
        if (s.EffectText == null && s.CurrentBiome.Length > 0) {
          var noted = NotedEffect(s.Seed, s.CurrentBiome);
          if (noted != null) s.EffectText = Model.ModifierText(noted);
          else s.EffectCanBeNoted = Model.EffectIsRolled(s.CurrentBiome) && s.Seed.Length > 0;
        }
      } catch (Exception ex) {
        Error = ex.Message;
      }
      return s;
    }

    static bool Has(Source src, string key, int n, Snapshot s) {
      switch (src) {
        case Source.Place:    return s.Places.Contains(key);
        case Source.Flag:     return s.Flags.Contains(key);
        case Source.OrbCount: return s.OrbCount >= n;
        case Source.Manual:   return s.Manual.Contains(key);
      }
      return false;
    }

    /// <summary>Has the goal's own condition been met -- for a place, merely reaching it.</summary>
    public static bool Reached(Goal g, Snapshot s) { return Has(g.Source, g.Key, g.N, s); }

    public static bool DeedDone(Deed d, Snapshot s) { return Has(d.Source, d.Key, 0, s); }

    /// <summary>Fully finished: reached, and every deed there accomplished.</summary>
    public static bool Done(Goal g, Snapshot s) {
      if (!Reached(g, s)) return false;
      foreach (var d in g.Deeds) if (!DeedDone(d, s)) return false;
      return true;
    }
  }
}
