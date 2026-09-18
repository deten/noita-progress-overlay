using System;
using System.Linq;

namespace NoitaOverlay {
  /// <summary>Console harness: prints the board exactly as the overlay would compute it.</summary>
  internal static class Probe {
    static void Main() {
      var t = new Tracker();
      var s = t.Poll();
      Console.WriteLine("saveDir      : " + (t.SaveDir ?? "(none)"));
      Console.WriteLine("error        : " + (t.Error ?? "(none)"));
      Console.WriteLine("gameRunning  : " + s.GameRunning + "   moving: " + s.Moving);
      Console.WriteLine("chunk        : " + s.WorldX + "," + s.WorldY);
      Console.WriteLine("currentPlace : " + s.CurrentPlace + "  => " + Model.PlaceName(s.CurrentPlace));
      Console.WriteLine("seed         : " + s.Seed + "   playtime: " + s.Playtime);
      Console.WriteLine("orbs         : " + s.OrbCount);
      Console.WriteLine("flags        : " + s.Flags.Count);
      Console.WriteLine("placesKnown  : " + s.Places.Count);
      Console.WriteLine();
      foreach (var tier in Model.Tiers) {
        var gs = Model.Goals.Where(x => x.Tier == tier.N).ToArray();
        int done = gs.Count(x => Tracker.Done(x, s));
        Console.WriteLine("== " + tier.Name.ToUpperInvariant() + "  " + done + "/" + gs.Length + " ==");
        foreach (var g in gs) {
          bool reached = Tracker.Reached(g, s);
          string mark = Tracker.Done(g, s) ? "x" : (reached ? "~" : " ");
          Console.WriteLine("   [" + mark + "] " + g.DisplayTitle(reached));
          foreach (var d in g.Deeds)
            Console.WriteLine("        [" + (Tracker.DeedDone(d, s) ? "x" : " ") + "] " + d.Title);
        }
        Console.WriteLine();
      }
      Console.WriteLine("-- places in history --");
      Console.WriteLine(string.Join(", ", s.Places.OrderBy(x => x).ToArray()));
    }
  }
}
