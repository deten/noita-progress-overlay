using System;
namespace NoitaOverlay {
  internal static class NoteCheck {
    static void Main() {
      var t = new Tracker();
      Console.WriteLine("noted for (309501701, coalmine_alt) : " +
        (t.NotedEffect("309501701", "coalmine_alt") ?? "(nothing)"));
      Console.WriteLine("  -> displays as: " +
        Model.ModifierText(t.NotedEffect("309501701", "coalmine_alt") ?? "?"));
      Console.WriteLine("noted for (309501701, snowcave)     : " +
        (t.NotedEffect("309501701", "snowcave") ?? "(nothing)"));
      Console.WriteLine();
      Console.WriteLine("is coalmine_alt a rolled biome? " + Model.EffectIsRolled("coalmine_alt"));
      Console.WriteLine("is desert a rolled biome?       " + Model.EffectIsRolled("desert"));
      Console.WriteLine("hardcoded effect for desert:    " + Model.Effect("desert"));
    }
  }
}
