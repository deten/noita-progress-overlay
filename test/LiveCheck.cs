using System;
using System.Linq;

namespace NoitaOverlay {
  /// <summary>Runs the real tracker against the real save and prints what it resolves.</summary>
  internal static class LiveCheck {
    static void Main() {
      var t = new Tracker();
      var s = t.Poll();
      Console.WriteLine("error        : " + (t.Error ?? "(none)"));
      Console.WriteLine("running      : " + s.GameRunning + "   inRun: " + s.InRun);
      Console.WriteLine("seed         : " + (s.Seed.Length > 0 ? s.Seed : "(unknown)"));
      Console.WriteLine("deaths       : " + s.Deaths);
      Console.WriteLine("biome        : " + s.CurrentBiome + "  -> " + s.CurrentPlace);
      Console.WriteLine("effect here  : " + (s.EffectText ?? "(none)") + (s.EffectCorrected ? "  [corrected]" : ""));
      Console.WriteLine("run flags    : " + (s.RunFlags.Count == 0 ? "(none yet)" : string.Join(", ", s.RunFlags.OrderBy(x => x))));
      Console.WriteLine("places known : " + s.Places.Count);
      Console.WriteLine();
      Console.WriteLine("this world's rolled effects:");
      foreach (var b in new[] { "coalmine","coalmine_alt","excavationsite","fungicave","snowcave",
                                "snowcastle","rainforest","vault","crypt","lake_statue" }) {
        var m = t.Predicted(b);
        Console.WriteLine("   " + b.PadRight(16) + (m == null ? "-" : m.Text));
      }
    }
  }
}
