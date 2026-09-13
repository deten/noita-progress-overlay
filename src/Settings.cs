using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace NoitaOverlay {

  /// <summary>
  /// Plain key=value settings in %APPDATA%\NoitaOverlay\settings.ini.
  /// Hand-editable; unknown keys are preserved so a future version can add more.
  /// </summary>
  internal sealed class Settings {
    public static readonly string Dir = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NoitaOverlay");
    static readonly string FilePath = Path.Combine(Dir, "settings.ini");

    readonly Dictionary<string, string> _kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public double IdleOpacity  { get { return Clamp(GetD("idle_opacity",  0.28), 0.05, 1.0); } set { Set("idle_opacity",  value); } }
    public double HoverOpacity { get { return Clamp(GetD("hover_opacity", 0.97), 0.10, 1.0); } set { Set("hover_opacity", value); } }
    public int    DwellMs      { get { return (int)Clamp(GetD("dwell_ms", 200), 0, 3000); }    set { Set("dwell_ms", value); } }
    public int    Width        { get { return (int)GetD("width",  0); }  set { Set("width",  value); } }
    public int    Height       { get { return (int)GetD("height", 0); }  set { Set("height", value); } }
    public int    X            { get { return (int)GetD("x", int.MinValue); } set { Set("x", value); } }
    public int    Y            { get { return (int)GetD("y", int.MinValue); } set { Set("y", value); } }
    public bool   HideDone     { get { return GetD("hide_done", 1) != 0; } set { Set("hide_done", value ? 1 : 0); } }

    static double Clamp(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

    double GetD(string k, double dflt) {
      string s; double v;
      if (_kv.TryGetValue(k, out s) && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
      return dflt;
    }

    void Set(string k, double v) {
      _kv[k] = v.ToString("0.####", CultureInfo.InvariantCulture);
      Save();
    }

    public void Load() {
      try {
        Directory.CreateDirectory(Dir);
        if (!File.Exists(FilePath)) { WriteTemplate(); return; }
        foreach (var raw in File.ReadAllLines(FilePath)) {
          var line = raw.Trim();
          if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(";")) continue;
          int eq = line.IndexOf('=');
          if (eq <= 0) continue;
          _kv[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
        }
      } catch { }
    }

    public void Save() {
      try {
        Directory.CreateDirectory(Dir);
        using (var w = new StreamWriter(FilePath, false)) {
          w.WriteLine("# Noita Overlay settings. Close the overlay before editing, or it will overwrite you.");
          w.WriteLine("# idle_opacity / hover_opacity: 0.05 - 1.0   dwell_ms: delay before it expands");
          foreach (var kv in _kv) w.WriteLine(kv.Key + " = " + kv.Value);
        }
      } catch { }
    }

    void WriteTemplate() {
      _kv["idle_opacity"]  = "0.28";
      _kv["hover_opacity"] = "0.97";
      _kv["dwell_ms"]      = "200";
      Save();
    }
  }
}
