using System;
using System.Text;

namespace NoitaOverlay {
  /// <summary>Decompresses Noita save files and shows what is inside.</summary>
  internal static class Dump {
    static void Main(string[] args) {
      foreach (var path in args) {
        Console.WriteLine("=== " + path);
        var b = FastLz.ReadNoitaFile(path);
        if (b == null) { Console.WriteLine("   (missing or failed to decompress)"); continue; }
        Console.WriteLine("   decompressed " + b.Length + " bytes");
        var hex = new StringBuilder();
        for (int i = 0; i < Math.Min(24, b.Length); i++) hex.Append(b[i].ToString("x2")).Append(' ');
        Console.WriteLine("   head: " + hex);
        if (b.Length >= 8) {
          uint be = (uint)(b[4] << 24 | b[5] << 16 | b[6] << 8 | b[7]);
          Console.WriteLine("   u32 BE @4 = " + be);
        }
        // printable runs of 6+ chars, so we can see the strings the format carries
        var sb = new StringBuilder(); int shown = 0;
        for (int i = 0; i <= b.Length && shown < 60; i++) {
          char c = i < b.Length ? (char)b[i] : '\0';
          if (c >= 32 && c < 127) sb.Append(c);
          else { if (sb.Length >= 6) { Console.WriteLine("      " + sb); shown++; } sb.Clear(); }
        }
      }
    }
  }
}
