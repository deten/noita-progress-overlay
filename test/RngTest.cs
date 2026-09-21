using System;

namespace NoitaOverlay {

  /// <summary>
  /// Noita's procedural RNG. A Bob Jenkins style position hash mixes the world seed with a
  /// coordinate pair to pick a seed, which then drives a Park-Miller (MINSTD) LCG.
  /// Reimplemented from the algorithm described by the Noita wiki and the MIT licensed
  /// reference in pudy248/noitaWandAtlas, itself credited to kaliuresis.
  /// </summary>
  internal sealed class NollaPrng {
    public uint WorldSeed;
    public int Seed;

    public NollaPrng(uint worldSeed) { WorldSeed = worldSeed; Seed = unchecked((int)worldSeed); }

    static ulong SeedHelper(double r) {
      ulong e = (ulong)BitConverter.DoubleToInt64Bits(r) & 0x7fffffffffffffffUL;
      long c = r < 0 ? -1L : 1L;
      ulong f = (e & 0xfffffffffffffUL) | 0x0010000000000000UL;
      ulong g = 0x433UL - (e >> 0x34);
      ulong h = g >= 64 ? 0UL : f >> (int)g;          // x86 shifts mask the count; guard anyway
      uint j = unchecked(~(uint)((0x433UL < (((e >> 0x20) & 0xffffffffUL) >> 0x14)) ? 1 : 0) + 1);
      ulong a = ((ulong)j << 0x20) | j;
      long b = unchecked((long)((~a & h) | ((f << 0xd) & a)) * c);
      return (ulong)b & 0xffffffffUL;
    }

    static uint Mix(uint a, uint b, uint ws) {
      unchecked {
        uint v1, v2, v3;
        v2 = ((a - b) - ws) ^ (ws >> 0xd);
        v1 = ((b - v2) - ws) ^ (v2 << 8);
        v3 = ((ws - v2) - v1) ^ (v1 >> 0xd);
        v2 = ((v2 - v1) - v3) ^ (v3 >> 0xc);
        v1 = ((v1 - v2) - v3) ^ (v2 << 0x10);
        v3 = ((v3 - v2) - v1) ^ (v1 >> 5);
        v2 = ((v2 - v1) - v3) ^ (v3 >> 3);
        v1 = ((v1 - v2) - v3) ^ (v2 << 10);
        return ((v3 - v2) - v1) ^ (v1 >> 0xf);
      }
    }

    public void SetRandomSeed(double x, double y) {
      uint ws = WorldSeed;
      uint a = ws ^ 0x93262e6f;
      uint b = a & 0xfff;
      uint c = (a >> 0xc) & 0xfff;

      double xx = x + b;
      double yy = y + c;

      double r = xx * 134217727.0;
      ulong e = SeedHelper(r);

      double absX = Math.Abs(xx), absY = Math.Abs(yy);
      if (102400.0 <= absY || absX <= 1.0) {
        r = yy * 134217727.0;
      } else {
        double t = yy * 3483.328;
        t += (double)e;
        r = yy * t;
      }

      ulong f = SeedHelper(r);
      uint g = Mix((uint)e, (uint)f, ws);

      double s = g;
      s /= 4294967295.0;
      s *= 2147483639.0;
      s += 1.0;
      Seed = (int)s;

      Next();
      uint h = ws & 3;
      while (h > 0) { Next(); h--; }
    }

    public float Next() {
      unchecked {
        int v = Seed * 0x41a7 + (Seed / 0x1f31d) * -0x7fffffff;
        if (v < 0) v += 0x7fffffff;
        Seed = v;
        return (float)Seed / 0x7fffffff;
      }
    }

    public float ProceduralRandomf(double x, double y, float a, float b) {
      SetRandomSeed(x, y);
      return a + (b - a) * Next();
    }
  }

  /// <summary>Mirrors random_create / random_next from the game's utilities.lua.</summary>
  internal sealed class LuaRnd {
    public double X, Y;
    readonly NollaPrng _p;
    public LuaRnd(NollaPrng p, double x, double y) { _p = p; X = x; Y = y; }
    public float NextF(float min, float max) {
      float r = _p.ProceduralRandomf(X, Y, min, max);
      Y += 1;                                    // random_next increments y each call
      return r;
    }
  }

  internal static class RngTest {

    // biome_modifiers, in declaration order, with the weights used by the weighted pick.
    static readonly string[] ModIds = {
      "MOIST","FOG_OF_WAR_REAPPEARS","HIGH_GRAVITY","LOW_GRAVITY","CONDUCTIVE","FREEZING","HOT",
      "GOLD_VEIN","GOLD_VEIN_SUPER","PLANT_INFESTED","FURNISHED","BOOBY_TRAPPED","PERFORATED",
      "SPOOKY","GRAVITY_FIELDS","FUNGAL","FLOODED","GAS_FLOODED","SHIELDED","PROTECTION_FIELDS",
      "OMINOUS","INVISIBILITY","WORMY"
    };
    static readonly double[] ModProb = {
      0.7, 1, 0.5, 0.5, 0.2, 0.0, 0.6, 0.01, 0.00025, 1.0, 0.5, 0.75, 0.75,
      0.5, 0.3, 0.5, 0.75, 0.5, 0.1, 0.2, 0.2, 0.1, 0.05
    };

    // The nine biomes that get a roll, in the order the script walks them.
    static readonly string[] RollBiomes = {
      "coalmine","coalmine_alt","excavationsite","fungicave","snowcave",
      "snowcastle","rainforest","vault","crypt"
    };

    static string PickWeighted(LuaRnd rnd) {
      double sum = 0;
      foreach (var p in ModProb) sum += p;
      double val = rnd.NextF(0.0f, (float)sum);
      double acc = 0;
      for (int i = 0; i < ModIds.Length; i++) {
        double lo = acc, hi = acc + ModProb[i];
        if (val >= lo && val <= hi) return ModIds[i];
        acc = hi;
      }
      return ModIds[0];
    }

    static void Run(uint seed, int deaths) {
      var prng = new NollaPrng(seed);
      var rnd = new LuaRnd(prng, 347893, 90734);
      Console.WriteLine("seed " + seed + ", death_count " + deaths);
      foreach (var b in RollBiomes) {
        // has_modifiers: coalmine is skipped entirely below 8 deaths, consuming no roll
        if (b == "coalmine" && deaths < 8) { Console.WriteLine("   " + b.PadRight(16) + "(skipped, no roll)"); continue; }
        double chance = b == "coalmine" ? 0.2 : (b == "excavationsite" ? 0.15 : 0.1);
        float roll = rnd.NextF(0.0f, 1.0f);
        if (roll <= chance) {
          string m = PickWeighted(rnd);
          Console.WriteLine("   " + b.PadRight(16) + "roll=" + roll.ToString("F4") + "  -> " + m);
        } else {
          Console.WriteLine("   " + b.PadRight(16) + "roll=" + roll.ToString("F4") + "  -> none");
        }
      }
      Console.WriteLine();
    }

    /// <summary>
    /// Is the PRNG wrong, or just my call sequence? Sweep the plausible variants and see if
    /// ANY of them makes coalmine_alt come out BOOBY_TRAPPED. If none do, the PRNG itself is
    /// the problem rather than an off-by-N in how many rolls I think get consumed.
    /// </summary>
    static void Diagnose(uint seed) {
      Console.WriteLine("=== sweep: looking for anything that yields coalmine_alt = BOOBY_TRAPPED ===");
      int hits = 0;
      foreach (bool swap in new[] { false, true }) {
        for (int dy = -40; dy <= 400; dy++) {
          foreach (int deaths in new[] { 50, 0 }) {
            var prng = new NollaPrng(seed);
            double bx = swap ? 90734 : 347893, by = swap ? 347893 : 90734;
            var rnd = new LuaRnd(prng, bx, by + dy);
            string got = null;
            foreach (var b in RollBiomes) {
              if (b == "coalmine" && deaths < 8) continue;
              double chance = b == "coalmine" ? 0.2 : (b == "excavationsite" ? 0.15 : 0.1);
              float roll = rnd.NextF(0.0f, 1.0f);
              string m = roll <= chance ? PickWeighted(rnd) : null;
              if (b == "coalmine_alt") { got = m; break; }
            }
            if (got == "BOOBY_TRAPPED") {
              Console.WriteLine("   HIT  swap=" + swap + " dy=" + dy + " deaths=" + deaths);
              hits++;
            }
          }
        }
      }
      if (hits == 0)
        Console.WriteLine("   no variant works -> the PRNG itself does not match the game");
      else
        Console.WriteLine("   " + hits + " variant(s) matched -> sequence offset, not the PRNG");
    }

    static void Main() {
      Console.WriteLine("TARGET: seed 309501701, coalmine_alt should be BOOBY_TRAPPED");
      Console.WriteLine();
      Run(309501701, 50);
      Run(309501701, 0);
      Diagnose(309501701);
    }
  }
}
