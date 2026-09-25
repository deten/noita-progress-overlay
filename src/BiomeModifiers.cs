using System;
using System.Collections.Generic;

namespace NoitaOverlay {

  /// <summary>
  /// Recomputes which biome gets which modifier in a given world, the same way
  /// get_modifier_mappings() in biome_modifiers.lua does it. The game rolls these at world
  /// generation and writes the result nowhere, but the roll is deterministic: the world seed
  /// plus a fixed coordinate pair, fed through Noita's procedural RNG (see Prng.cs).
  /// </summary>
  internal static class BiomeModifiers {

    // The groups that get a random roll, in the order the script walks them. The first name
    // in each group is the one the chance is rolled for.
    static readonly string[][] Rolled = {
      new[]{ "coalmine", "mountain_hall" },
      new[]{ "coalmine_alt" },
      new[]{ "excavationsite" },
      new[]{ "fungicave" },
      new[]{ "snowcave" },
      new[]{ "snowcastle" },
      new[]{ "rainforest", "rainforest_open" },
      new[]{ "vault" },
      new[]{ "crypt" },
    };

    const double ChancePerBiome = 0.1, ChanceCoalmine = 0.2, ChanceExcavation = 0.15;
    const double ChanceMoistFungicave = 0.5, ChanceMoistLake = 0.75;

    /// <summary>
    /// True where the effect never depends on the seed: outside the rolled groups, and not one
    /// of the two biomes that get an extra roll afterwards (fungicave, lake_statue).
    /// </summary>
    public static bool IsSeedIndependent(string biome) {
      if (biome == "fungicave" || biome == "lake_statue") return false;
      foreach (var g in Rolled) foreach (var b in g) if (b == biome) return false;
      return true;
    }

    static Modifier Get(string id) {
      foreach (var m in Modifiers.All) if (m.Id == id) return m;
      return null;
    }

    static bool AppliesTo(Modifier m, string biome, ICollection<string> flags) {
      if (m == null) return false;
      if (m.RequiresFlag != null && (flags == null || !flags.Contains(m.RequiresFlag))) return false;
      bool ok = true;
      if (m.NotIn != null) foreach (var s in m.NotIn) if (s == biome) { ok = false; break; }
      if (m.OnlyIn != null) { ok = false; foreach (var s in m.OnlyIn) if (s == biome) { ok = true; break; } }
      return ok;
    }

    static Modifier PickWeighted(LuaRnd rnd) {
      // Lua walks the table accumulating weight_min/weight_max, then draws once over the total.
      double sum = 0;
      foreach (var m in Modifiers.All) if (m.InWeightedTable) sum += m.Probability;
      double val = rnd.NextF(0.0f, (float)sum);
      double acc = 0;
      Modifier first = null;
      foreach (var m in Modifiers.All) {
        if (!m.InWeightedTable) continue;
        if (first == null) first = m;
        double lo = acc, hi = acc + m.Probability;
        if (val >= lo && val <= hi) return m;
        acc = hi;
      }
      return first;
    }

    /// <summary>
    /// Biome id -> modifier for the world with this seed.
    /// deaths: lifetime deaths when the world was made. Under 8, the Mines never roll, which
    /// also shifts every roll after them.
    /// flags: persistent flags, for the two modifiers that require one.
    /// </summary>
    public static Dictionary<string, Modifier> For(uint seed, int deaths, ICollection<string> flags) {
      var result = new Dictionary<string, Modifier>();
      var rnd = new LuaRnd(new NollaPrng(seed), 347893, 90734);

      foreach (var group in Rolled) {
        string head = group[0];
        Modifier picked = null;
        bool rolls = !(head == "coalmine" && deaths < 8);     // skipped before any roll is used
        if (rolls) {
          double chance = head == "coalmine" ? ChanceCoalmine
                        : head == "excavationsite" ? ChanceExcavation : ChancePerBiome;
          if (rnd.NextF(0.0f, 1.0f) <= chance) picked = PickWeighted(rnd);
        }
        foreach (var b in group) if (AppliesTo(picked, b, flags)) result[b] = picked;
      }

      Action<string, string> setIfNone = (biome, id) => { if (!result.ContainsKey(biome)) result[biome] = Get(id); };

      if (rnd.NextF(0.0f, 1.0f) < ChanceMoistFungicave) setIfNone("fungicave", "MOIST");

      var gloom = Get("FOG_OF_WAR_CLEAR_AT_PLAYER");
      result["wandcave"] = gloom;
      result["wizardcave"] = gloom;
      result["alchemist_secret"] = gloom;

      setIfNone("mountain_top", "FREEZING");
      setIfNone("mountain_floating_island", "FREEZING");
      setIfNone("winter", "FREEZING");
      result["winter_caves"] = Get("FREEZING_COSMETIC");

      setIfNone("lavalake", "HOT");
      setIfNone("desert", "HOT");
      setIfNone("pyramid_entrance", "HOT");
      setIfNone("pyramid_left", "HOT");
      setIfNone("pyramid_top", "HOT");
      setIfNone("pyramid_right", "HOT");

      setIfNone("watercave", "MOIST");

      if (rnd.NextF(0.0f, 1.0f) < ChanceMoistLake) setIfNone("lake_statue", "MOIST");

      return result;
    }
  }
}
