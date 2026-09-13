using System;

namespace NoitaOverlay {
  /// <summary>Checks what the overlay would recommend at various stages of a player's progress.</summary>
  internal static class Simulate {

    static Snapshot Make(string place, string[] places, string[] flags, int orbs) {
      var s = new Snapshot { CurrentPlace = place, OrbCount = orbs };
      foreach (var p in places) s.Places.Add(p);
      foreach (var f in flags) s.Flags.Add(f);
      return s;
    }

    static void Show(string label, Snapshot s) {
      var n = Model.Recommend(s);
      var e = Model.Explore(s, n == null ? null : n.GoalId);
      Console.WriteLine("== " + label + " ==");
      Console.WriteLine("   " + (n.Urgent ? "DO THIS" : "NEXT") + "  (" + n.Reason + ")");
      Console.WriteLine("     " + n.Title);
      Console.WriteLine("     " + n.Hint);
      if (e != null) {
        Console.WriteLine("   or, no rush");
        Console.WriteLine("     " + e.Title);
      } else Console.WriteLine("   (no explore suggestion)");
      Console.WriteLine();
    }

    static void Main() {
      Show("brand new player, first run, standing on the surface",
        Make("surface", new[] { "surface" }, new string[0], 0));

      Show("a few runs in, reached the Coal Pits",
        Make("excavationsite", new[] { "surface", "coalmine", "excavationsite", "holymountain" }, new string[0], 0));

      Show("deep, but has never beaten the boss",
        Make("snowcastle", new[] { "surface", "coalmine", "excavationsite", "snowcave", "snowcastle", "holymountain" },
             new string[0], 0));

      Show("killed the boss but never finished the run",
        Make("boss_arena", new[] { "surface", "coalmine", "excavationsite", "snowcave", "snowcastle", "boss_arena", "holymountain" },
             new[] { "boss_centipede" }, 0));

      Show("beaten the game (the real save), idle at the surface",
        Make("surface",
             new[] { "surface","coalmine","coalmine_alt","excavationsite","fungicave","snowcave","snowcastle",
                     "rainforest","vault","crypt","boss_arena","boss_victoryroom","holymountain","wandcave",
                     "liquidcave","dragoncave","orbroom","winter","watercave" },
             new[] { "boss_centipede", "progress_ending0", "essence_laser" }, 2));

      Show("beaten the game, and standing in the Dragoncave",
        Make("dragoncave",
             new[] { "surface","coalmine","coalmine_alt","excavationsite","fungicave","snowcave","snowcastle",
                     "rainforest","vault","crypt","boss_arena","boss_victoryroom","holymountain","wandcave",
                     "liquidcave","dragoncave","orbroom","winter","watercave" },
             new[] { "boss_centipede", "progress_ending0", "essence_laser" }, 2));
    }
  }
}
