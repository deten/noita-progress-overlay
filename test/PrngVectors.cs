using System;

namespace NoitaOverlay {
  /// <summary>
  /// Checks NollaPrng against published test vectors from noitool's random.spec.ts
  /// (TwoAbove/noita-tools, MIT). SetRandomSeed(x, y), then four Random(a, b) calls.
  /// </summary>
  internal static class PrngVectors {
    static int Random(NollaPrng p, int a, int b) {
      return a + (int)((double)(b + 1 - a) * p.NextD());
    }

    static void Main() {
      var cases = new[] {
        new { seed = 1674055821u, x = 1475.5, y = 827.0, ans = new[] { 65, 40641, 57806, 3 } },
        new { seed = 1674055821u, x = 1385.5, y = 177.0, ans = new[] { 64, 57621, 39985, 12 } },
        new { seed = 1674055821u, x = 185.5,  y = 107.0, ans = new[] { 69, 86000, 94607, 9 } },
      };
      int pass = 0, total = 0;
      foreach (var c in cases) {
        var p = new NollaPrng(c.seed);
        p.SetRandomSeed(c.x, c.y);
        var got = new[] { Random(p, 0, 100), Random(p, 0, 100000), Random(p, 200, 100000), Random(p, 1, 12) };
        for (int i = 0; i < 4; i++) { total++; if (got[i] == c.ans[i]) pass++; }
        Console.WriteLine(string.Format("({0},{1})  want {2}  got {3}  {4}",
          c.x, c.y, string.Join(",", c.ans), string.Join(",", got),
          got[0] == c.ans[0] && got[1] == c.ans[1] && got[2] == c.ans[2] && got[3] == c.ans[3] ? "OK" : "MISMATCH"));
      }
      Console.WriteLine();
      Console.WriteLine(pass + "/" + total + " values match");
      Environment.ExitCode = pass == total ? 0 : 1;
    }
  }
}
