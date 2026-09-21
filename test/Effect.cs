using System;

namespace NoitaOverlay {
  /// <summary>Prints what the live tracker sees, and whether an effect is derivable there.</summary>
  internal static class EffectCheck {
    static void Main() {
      var t = new Tracker();
      var s = t.Poll();
      Console.WriteLine("gameRunning : " + s.GameRunning + "   inRun: " + s.InRun + "   moving: " + s.Moving);
      Console.WriteLine("chunk       : " + s.WorldX + "," + s.WorldY);
      Console.WriteLine("raw biome   : '" + s.CurrentBiome + "'");
      Console.WriteLine("canon place : '" + s.CurrentPlace + "'");
      var eff = Model.Effect(s.CurrentBiome);
      Console.WriteLine("effect      : " + (eff ?? "(none derivable for this biome)"));
      Console.WriteLine();
      Console.WriteLine("Biomes where an effect CAN be shown:");
      foreach (var b in new[] { "winter","winter_caves","mountain_top","mountain_floating_island",
                                "desert","lavalake","lavalake_pit","pyramid_entrance","pyramid_left",
                                "pyramid_top","pyramid_right","watercave" })
        Console.WriteLine("   " + b.PadRight(26) + Model.Effect(b));
    }
  }
}
