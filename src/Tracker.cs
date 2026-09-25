using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
    public string EffectText;                 // this biome's effect in this world, or null for none
    public bool   EffectCorrected;            // came from your correction rather than the prediction
    public int    WorldX, WorldY;
    public string Seed = "";                  // the world on disk right now, "" while unknown
    public int    Deaths;
    public int    OrbCount;
    public HashSet<string> Flags    = new HashSet<string>();
    public HashSet<string> RunFlags = new HashSet<string>();  // lifetime union of per-run flags
    public HashSet<string> Places   = new HashSet<string>();  // lifetime, canonical
    public HashSet<string> Manual   = new HashSet<string>();  // leads the player ticked off
    public string NewlyEntered;               // set for one tick when a new place is first reached
  }

  /// <summary>
  /// Reads Noita's save directory. Never writes to it, never touches the process --
  /// only files the game already produces on its own.
  /// </summary>
  internal sealed class Tracker {

    static readonly Regex ChunkRe = new Regex(@"^world_(-?\d+)_(-?\d+)\.png_petri$", RegexOptions.Compiled);

    /// <summary>Run flags worth remembering across runs, i.e. the ones some goal checks.</summary>
    static readonly HashSet<string> WantedRunFlags = new HashSet<string> {
      "kantele_secret_00", "kantele_secret_01", "kantele_secret_02", "alchemy_kantele",
      "ocarina_secret_00", "ocarina_secret_01", "ocarina_secret_02", "alchemy_ocarina",
      "musicmachine1", "musicmachine2", "musicmachine3", "musicmachine4",
      "statue_hands_destroyed_1", "statue_hands_destroyed_2", "statue_hands_destroyed_3",
      "exploding_gold", "fishing_hut_a", "fishing_hut_b", "greed_curse", "greed_curse_gone",
    };

    readonly string _root;                    // ...\Nolla_Games_Noita
    readonly string _dir;                     // %APPDATA%\NoitaOverlay
    readonly HashSet<string> _history  = new HashSet<string>();
    readonly HashSet<string> _manual   = new HashSet<string>();
    readonly HashSet<string> _runFlags = new HashSet<string>();
    readonly Dictionary<string, string> _corrections = new Dictionary<string, string>();
    string _lastPlace = "";

    // change detection, so nothing is re-parsed unless the game rewrote it
    readonly Dictionary<string, DateTime> _seen = new Dictionary<string, DateTime>();
    int _deaths;
    DateTime _latestSessionTime = DateTime.MinValue;
    bool _latestSessionDead;
    string _modSeed = null; int _modDeaths = -1; int _modFlagCount = -1;
    Dictionary<string, Modifier> _modifiers = new Dictionary<string, Modifier>();

    public string SaveDir { get; private set; }
    public string Error   { get; private set; }

    string P(string name) { return Path.Combine(_dir, name); }

    public Tracker() {
      _root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        @"AppData\LocalLow\Nolla_Games_Noita");
      _dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoitaOverlay");
      Directory.CreateDirectory(_dir);

      if (File.Exists(P("history.txt"))) ReadSet(P("history.txt"), _history);
      else { ImportPastRuns(); SaveSet(P("history.txt"), _history); }
      ReadSet(P("leads.txt"), _manual);
      ReadSet(P("runflags.txt"), _runFlags);
      LoadCorrections();
    }

    static void ReadSet(string path, HashSet<string> into) {
      try {
        if (!File.Exists(path)) return;
        foreach (var l in File.ReadAllLines(path)) { var t = l.Trim(); if (t.Length > 0) into.Add(t); }
      } catch { }
    }

    static void SaveSet(string path, HashSet<string> set) {
      try { File.WriteAllLines(path, set.OrderBy(x => x).ToArray()); } catch { }
    }

    /// <summary>True the first time a file is seen, and again each time the game rewrites it.</summary>
    bool Changed(string path) {
      try {
        if (!File.Exists(path)) return false;
        var t = File.GetLastWriteTimeUtc(path);
        DateTime prev;
        if (_seen.TryGetValue(path, out prev) && prev == t) return false;
        _seen[path] = t;
        return true;
      } catch { return false; }
    }

    // ---- leads -------------------------------------------------------------

    /// <summary>Tick a lead off, or un-tick it. Nothing in the game records these.</summary>
    public void ToggleManual(string id) {
      if (_manual.Contains(id)) _manual.Remove(id); else _manual.Add(id);
      SaveSet(P("leads.txt"), _manual);
    }

    // ---- biome effect corrections ------------------------------------------
    // Effects are computed from the seed now. If the game ever shows you something different,
    // a correction overrides the prediction for that world and biome. effects.txt keeps the
    // format it had when these were typed in by hand, so old notes carry over.

    static string Key(string seed, string biome) { return seed + "|" + biome; }

    void LoadCorrections() {
      try {
        if (!File.Exists(P("effects.txt"))) return;
        foreach (var l in File.ReadAllLines(P("effects.txt"))) {
          var p = l.Split('|');
          if (p.Length == 3) _corrections[Key(p[0].Trim(), p[1].Trim())] = p[2].Trim();
        }
      } catch { }
    }

    public void CorrectEffect(string seed, string biome, string modifierId) {
      if (string.IsNullOrEmpty(seed) || string.IsNullOrEmpty(biome)) return;
      if (modifierId == null) _corrections.Remove(Key(seed, biome));
      else _corrections[Key(seed, biome)] = modifierId;
      try {
        var lines = _corrections.Select(kv => {
          var p = kv.Key.Split('|');
          return p[0] + " | " + p[1] + " | " + kv.Value;
        }).OrderBy(x => x).ToArray();
        File.WriteAllLines(P("effects.txt"), lines);
      } catch { }
    }

    public string Correction(string seed, string biome) {
      string v;
      return _corrections.TryGetValue(Key(seed, biome), out v) ? v : null;
    }

    /// <summary>What the seed says this biome's effect is, ignoring corrections.</summary>
    public Modifier Predicted(string biome) {
      Modifier m;
      return biome != null && _modifiers.TryGetValue(biome, out m) ? m : null;
    }

    // ---- history -----------------------------------------------------------

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
              if (key != null) AddBiome(key.Value);
            }
          } catch { }
        }
      }
    }

    bool AddBiome(string raw) {
      var canon = Model.Canon(raw.Trim().Replace("$biome_", ""));
      return canon.Length > 0 && _history.Add(canon);
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

    // ---- reading the compressed saves --------------------------------------

    /// <summary>
    /// Strings in Noita's binary serialisation are a big-endian u32 length then the bytes.
    /// We only ever test membership against names we know, so a loose scan is enough.
    /// </summary>
    static IEnumerable<string> BinaryStrings(byte[] b) {
      for (int i = 0; i + 4 <= b.Length; i++) {
        int len = b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3];
        if (len < 3 || len > 400 || i + 4 + len > b.Length) continue;
        bool ok = true;
        for (int k = 0; k < len; k++) { byte c = b[i + 4 + k]; if (c < 32 || c > 126) { ok = false; break; } }
        if (!ok) continue;
        yield return Encoding.ASCII.GetString(b, i + 4, len);
        i += 3 + len;
      }
    }

    /// <summary>
    /// The world seed, from .autosave (rewritten every few minutes mid-run) or .stream_info
    /// (written on quit). Both decompress to a stream whose second big-endian u32 is the seed.
    /// </summary>
    static string SeedFrom(string path) {
      var b = FastLz.ReadNoitaFile(path);
      if (b == null || b.Length < 8 || b[0] != 0 || b[1] != 0 || b[2] != 0) return null;
      uint seed = (uint)(b[4] << 24 | b[5] << 16 | b[6] << 8 | b[7]);
      return seed == 0 ? null : seed.ToString(CultureInfo.InvariantCulture);
    }

    string ResolveSeed(string worldDir, bool running) {
      string auto = Path.Combine(worldDir, ".autosave"), info = Path.Combine(worldDir, ".stream_info");
      string pick = null; DateTime pickT = DateTime.MinValue;
      foreach (var f in new[] { auto, info }) {
        if (!File.Exists(f)) continue;
        var t = File.GetLastWriteTimeUtc(f);
        if (t > pickT) { pick = f; pickT = t; }
      }
      if (pick == null) return "";
      // A world that ended in death cannot be continued. If the newest session died and was
      // written at or after this file, the file belongs to that dead world; anything running
      // now is a new world whose seed will not be on disk until its first autosave.
      if (running && _latestSessionDead && _latestSessionTime >= pickT.AddMinutes(-2)) return "";
      // Only decompress when the game has actually rewritten the file.
      if (pick != _seedPath || pickT != _seedTime) {
        _seedValue = SeedFrom(pick) ?? "";
        _seedPath = pick; _seedTime = pickT;
      }
      return _seedValue;
    }

    string _seedPath, _seedValue = "";
    DateTime _seedTime;

    void ReadSessions(string sessDir) {
      if (!Directory.Exists(sessDir)) return;
      var files = new DirectoryInfo(sessDir).GetFiles("*_stats.xml");
      var newest = files.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
      if (newest == null || newest.LastWriteTimeUtc == _latestSessionTime) return;

      int deaths = 0; bool newestDead = false;
      foreach (var f in files) {
        try {
          var doc = new XmlDocument(); doc.Load(f.FullName);
          var st = doc.SelectSingleNode("//stats") as XmlElement;
          bool dead = st != null && st.GetAttribute("dead") == "1";
          if (dead) deaths++;
          if (f.FullName == newest.FullName) newestDead = dead;
          foreach (XmlNode e in doc.SelectNodes("//biomes_visited/E")) {
            var k = e.Attributes["key"];
            if (k != null) AddBiome(k.Value);
          }
        } catch { }
      }
      _deaths = deaths;
      _latestSessionDead = newestDead;
      _latestSessionTime = newest.LastWriteTimeUtc;
      SaveSet(P("history.txt"), _history);
    }

    /// <summary>Per-run flags from the world state: live from the autosave, or from the quit save.</summary>
    void HarvestWorldState(string save) {
      bool changed = false;
      string auto = Path.Combine(save, @"world\.autosave_world_state");
      if (Changed(auto)) {
        var b = FastLz.ReadNoitaFile(auto);
        if (b != null) {
          string last = null;
          foreach (var str in BinaryStrings(b)) {
            if (WantedRunFlags.Contains(str) && _runFlags.Add(str)) changed = true;
            if (last == "visited_biomes")                           // the game's own list for this run
              foreach (var part in str.Split(',')) if (part.StartsWith("$biome_") && AddBiome(part)) changed = true;
            last = str;
          }
        }
      }
      string xml = Path.Combine(save, "world_state.xml");
      if (Changed(xml)) {
        try {
          var text = File.ReadAllText(xml);
          foreach (var f in WantedRunFlags) if (text.Contains(f) && _runFlags.Add(f)) changed = true;
        } catch { }
      }
      if (changed) { SaveSet(P("runflags.txt"), _runFlags); SaveSet(P("history.txt"), _history); }
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

        // Save activity says a run is underway. At the main menu Noita writes nothing, which is
        // why "is it open" comes from the process instead.
        var autos = Path.Combine(worldDir, ".autosave");
        if (File.Exists(autos) && (now - File.GetLastWriteTimeUtc(autos)).TotalMinutes < 5)
          s.InRun = true;

        // Newest chunk file is roughly where the player is.
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
            s.CurrentBiome = BiomeMap.At(s.WorldX + 256, s.WorldY + 256);   // chunk centre, not corner
            s.CurrentPlace = Model.Canon(s.CurrentBiome);
          }
          s.Moving = (now - newest.LastWriteTimeUtc).TotalSeconds < 20;
          if (s.Moving) s.InRun = true;
        }

        // Lifetime flags, one file each, written the moment they are earned.
        string flagDir = Path.Combine(save, @"persistent\flags");
        if (Directory.Exists(flagDir))
          foreach (var f in Directory.GetFiles(flagDir)) s.Flags.Add(Path.GetFileName(f));

        // Orbs collected, lifetime (one numbered file per orb).
        string orbDir = Path.Combine(save, @"persistent\orbs_new");
        if (Directory.Exists(orbDir))
          s.OrbCount = Directory.GetFiles(orbDir).Count(f => { int _n; return int.TryParse(Path.GetFileName(f), out _n); });

        ReadSessions(Path.Combine(save, @"stats\sessions"));
        HarvestWorldState(save);
        s.Deaths = _deaths;
        s.Seed = ResolveSeed(worldDir, s.GameRunning);

        // Live discovery: standing somewhere new counts immediately.
        if (s.CurrentPlace.Length > 0 && s.CurrentPlace != _lastPlace) {
          _lastPlace = s.CurrentPlace;
          if (_history.Add(s.CurrentPlace)) {
            s.NewlyEntered = s.CurrentPlace;
            SaveSet(P("history.txt"), _history);
          }
        }

        s.Places   = new HashSet<string>(_history);
        s.Manual   = new HashSet<string>(_manual);
        s.RunFlags = new HashSet<string>(_runFlags);

        // Biome effect. Recompute the world's table only when something it depends on changes.
        uint seedNum;
        bool haveSeed = uint.TryParse(s.Seed, NumberStyles.None, CultureInfo.InvariantCulture, out seedNum);
        if (haveSeed && (s.Seed != _modSeed || _deaths != _modDeaths || s.Flags.Count != _modFlagCount)) {
          _modifiers = BiomeModifiers.For(seedNum, _deaths, s.Flags);
          _modSeed = s.Seed; _modDeaths = _deaths; _modFlagCount = s.Flags.Count;
        }
        if (s.CurrentBiome.Length > 0) {
          var corr = haveSeed ? Correction(s.Seed, s.CurrentBiome) : null;
          if (corr != null) {
            s.EffectCorrected = true;
            s.EffectText = corr == "NONE" ? null : Model.ModifierText(corr);
          } else if (haveSeed) {
            var pm = Predicted(s.CurrentBiome);
            s.EffectText = pm == null ? null : pm.Text;
          } else if (BiomeModifiers.IsSeedIndependent(s.CurrentBiome)) {
            // No seed yet (a brand new world before its first autosave). These never depend on it.
            var any = BiomeModifiers.For(1, 1000, s.Flags);
            Modifier fm;
            s.EffectText = any.TryGetValue(s.CurrentBiome, out fm) ? fm.Text : null;
          }
        }
      } catch (Exception ex) {
        Error = ex.Message;
      }
      return s;
    }

    // ---- goal checks -------------------------------------------------------

    /// <summary>"any:a,b" or "all:a,b" against the flags seen across runs.</summary>
    static bool RunFlagMet(string spec, HashSet<string> seen) {
      bool all = spec.StartsWith("all:");
      var names = spec.Substring(spec.IndexOf(':') + 1).Split(',');
      return all ? names.All(seen.Contains) : names.Any(seen.Contains);
    }

    static bool Has(Source src, string key, int n, Snapshot s) {
      switch (src) {
        case Source.Place:    return s.Places.Contains(key);
        case Source.Flag:     return s.Flags.Contains(key);
        case Source.OrbCount: return s.OrbCount >= n;
        case Source.Manual:   return s.Manual.Contains(key);
        case Source.RunFlag:  return RunFlagMet(key, s.RunFlags);
      }
      return false;
    }

    /// <summary>Has the goal's own condition been met -- for a place, merely reaching it.</summary>
    public static bool Reached(Goal g, Snapshot s) {
      // Run flags can also be ticked by hand, for things done before the overlay was watching.
      if (g.Source == Source.RunFlag && s.Manual.Contains(g.Id)) return true;
      return Has(g.Source, g.Key, g.N, s);
    }

    public static bool DeedDone(Deed d, Snapshot s) { return Has(d.Source, d.Key, 0, s); }

    /// <summary>Fully finished: reached, and every deed there accomplished.</summary>
    public static bool Done(Goal g, Snapshot s) {
      if (!Reached(g, s)) return false;
      foreach (var d in g.Deeds) if (!DeedDone(d, s)) return false;
      return true;
    }
  }
}
