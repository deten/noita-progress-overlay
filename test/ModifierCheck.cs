using System;
using System.Collections.Generic;
using System.Linq;

namespace NoitaOverlay {
  /// <summary>Validates BiomeModifiers.For against known worlds.</summary>
  internal static class ModifierCheck {
    static int fails = 0;

    static void Expect(string what, bool ok) {
      Console.WriteLine("  [" + (ok ? "PASS" : "FAIL") + "] " + what);
      if (!ok) fails++;
    }

    static void Show(uint seed, Dictionary<string, Modifier> m) {
      Console.WriteLine("seed " + seed + ":");
      foreach (var kv in m.OrderBy(k => k.Key))
        Console.WriteLine("    " + kv.Key.PadRight(26) + kv.Value.Id);
    }

    static void Main() {
      var noFlags = new HashSet<string>();

      // 1. noitool's BiomeModifier.spec.ts: seed 123 gives only the fixed assignments.
      //    (noitool's list leaves out the forced gloom and cosmetic freeze, so compare the rest.)
      var m123 = BiomeModifiers.For(123, 1000, noFlags);
      Show(123, m123);
      var want = new[] { "mountain_top","mountain_floating_island","winter","lavalake","desert",
                         "pyramid_entrance","pyramid_left","pyramid_top","pyramid_right","watercave" };
      var skip = new HashSet<string> { "wandcave","wizardcave","alchemist_secret","winter_caves" };
      var got = m123.Keys.Where(k => !skip.Contains(k)).OrderBy(k => k).ToArray();
      Expect("seed 123 matches noitool's published answer",
             got.SequenceEqual(want.OrderBy(k => k)));
      Console.WriteLine();

      // 2. Seen in game: "You feel wary" (BOOBY_TRAPPED) on entering the Coal Pits area, seed 1184881785.
      var m1 = BiomeModifiers.For(1184881785, 1000, noFlags);
      Show(1184881785, m1);
      Modifier ex;
      Expect("seed 1184881785 puts BOOBY_TRAPPED on the Coal Pits (observed in game)",
             m1.TryGetValue("excavationsite", out ex) && ex.Id == "BOOBY_TRAPPED");

      Console.WriteLine();

      // 3. Predicted before it was seen: "The air feels heavy" (HIGH_GRAVITY) on entering the
      //    Snowy Depths, seed 1584486744, 56 lifetime deaths at the time.
      var m2 = BiomeModifiers.For(1584486744, 56, noFlags);
      Modifier sc;
      Expect("seed 1584486744 puts HIGH_GRAVITY on the Snowy Depths (predicted, then seen in game)",
             m2.TryGetValue("snowcave", out sc) && sc.Id == "HIGH_GRAVITY");

      Console.WriteLine();
      Console.WriteLine(fails == 0 ? "ALL PASS" : fails + " FAILED");
      Environment.ExitCode = fails;
    }
  }
}
