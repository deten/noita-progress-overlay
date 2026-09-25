using System;

namespace NoitaOverlay {

  /// <summary>
  /// Noita's procedural RNG. A Bob Jenkins style mix of the world seed and a coordinate pair
  /// picks a seed, which then drives a Park-Miller (MINSTD) LCG.
  /// Reimplemented from the algorithm described on the Noita wiki and the MIT licensed
  /// references in pudy248/noitaWandAtlas (credited to kaliuresis) and TwoAbove/noita-tools.
  /// Checked against the published vectors in test/PrngVectors.cs.
  /// </summary>
  internal sealed class NollaPrng {
    public uint WorldSeed;
    public int Seed;

    public NollaPrng(uint worldSeed) { WorldSeed = worldSeed; Seed = unchecked((int)worldSeed); }

    // Truncating double -> 32 bit conversion, as the game's compiled code does it.
    static ulong SeedHelper(double r) {
      ulong e = (ulong)BitConverter.DoubleToInt64Bits(r) & 0x7fffffffffffffffUL;
      long c = r < 0 ? -1L : 1L;
      ulong f = (e & 0xfffffffffffffUL) | 0x0010000000000000UL;
      ulong g = 0x433UL - (e >> 0x34);
      ulong h = g >= 64 ? 0UL : f >> (int)g;
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

      if (102400.0 <= Math.Abs(yy) || Math.Abs(xx) <= 1.0) {
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

    void Step() {
      unchecked {
        int v = Seed * 0x41a7 + (Seed / 0x1f31d) * -0x7fffffff;
        if (v <= 0) v += 0x7fffffff;
        Seed = v;
      }
    }

    public float  Next()  { Step(); return (float)Seed / 0x7fffffff; }
    public double NextD() { Step(); return (double)Seed / 0x7fffffff; }

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
      Y += 1;                                    // random_next increments y after each call
      return r;
    }
  }
}
