using System;
using System.Linq;

namespace NoitaOverlay {
  /// <summary>
  /// Renders the board as text, exactly as the overlay would draw it, from the real save.
  /// Lets us see what a given situation looks like without having to play to it.
  /// </summary>
  internal static class Preview {

    static void Compact(Snapshot s, string status) {
      var n = Model.Recommend(s);
      var e = Model.Explore(s, n == null ? null : n.GoalId);
      Console.WriteLine("+----------------------------------------------------------+");
      Console.WriteLine("| NOITA  -  what is left                    show all     X  |");
      Console.WriteLine("|                                                          |");
      Console.WriteLine("|  * " + status.PadRight(53) + " |");
      Console.WriteLine("|                                                          |");
      Console.WriteLine("|  " + (n.Urgent ? "DO THIS" : "NEXT").PadRight(28) + n.Reason.PadLeft(27) + "  |");
      Console.WriteLine("|  | " + n.Title.PadRight(53) + " |");
      Console.WriteLine("|  | " + n.Hint.PadRight(53) + " |");
      if (e != null) {
        Console.WriteLine("|                                                          |");
        Console.WriteLine("|  or, no rush                                             |");
        Console.WriteLine("|    " + e.Title.PadRight(53) + " |");
      }
      Console.WriteLine("+----------------------------------------------------------+");
    }

    static void Expanded(Snapshot s, bool hideDone) {
      foreach (var t in Model.Tiers) {
        var gs = Model.Goals.Where(x => x.Tier == t.N).ToArray();
        int done = gs.Count(x => Tracker.Done(x, s));
        Console.WriteLine();
        if (!Model.TierUnlocked(t.N, s)) {
          Console.WriteLine("  " + t.Name.ToUpperInvariant() + "   later");
          continue;
        }
        Console.WriteLine("  " + t.Name.ToUpperInvariant().PadRight(46) + (done + "/" + gs.Length).PadLeft(6));
        Console.WriteLine("  " + t.Blurb);
        foreach (var g in gs) {
          bool ok = Tracker.Done(g, s), reached = Tracker.Reached(g, s);
          if (ok && hideDone) continue;
          string mark = ok ? "[x]" : (reached ? "[~]" : "[ ]");
          Console.WriteLine("   " + mark + " " + g.DisplayTitle(reached));
          string sub = reached ? (ok ? null : g.Where) : g.Hint;
          if (g.Source == Source.OrbCount) sub = s.OrbCount + " of " + g.N + " found. " + g.Hint;
          if (sub != null) Console.WriteLine("        " + sub);
          foreach (var d in g.Deeds) {
            bool dd = Tracker.DeedDone(d, s);
            if (dd && hideDone) continue;
            Console.WriteLine("        " + (dd ? "[x]" : "[ ]") + " " + d.Title);
            if (!dd) Console.WriteLine("            " + d.Hint);
          }
        }
      }
    }

    static Snapshot Blank(string place) {
      var s = new Snapshot { CurrentPlace = place, GameRunning = true, InRun = true, Moving = true };
      if (place.Length > 0) s.Places.Add(place);
      return s;
    }

    static void Main(string[] args) {
      Console.WriteLine("################ BRAND NEW PLAYER, FIRST EVER RUN ################");
      var fresh = Blank("coalmine");
      fresh.Places.Add("surface");
      Compact(fresh, "In the Mines");
      Console.WriteLine();
      Console.WriteLine("--- hovered ---");
      Expanded(fresh, true);

      Console.WriteLine();
      Console.WriteLine();
      Console.WriteLine("################ SAME PLAYER, NOW AT THE COAL PITS ################");
      var mid = Blank("excavationsite");
      mid.Places.Add("surface"); mid.Places.Add("coalmine"); mid.Places.Add("holymountain");
      Compact(mid, "In the Coal Pits");
      Console.WriteLine();
      Console.WriteLine("--- hovered ---");
      Expanded(mid, true);
    }
  }
}
