using System;
using System.IO;

namespace NoitaOverlay {

  /// <summary>
  /// Noita writes several save files FastLZ-compressed behind an 8 byte header:
  /// [u32 compressed size][u32 decompressed size][FastLZ stream], little-endian.
  /// This is a straight port of fastlz_decompress (levels 1 and 2), public domain / MIT.
  /// </summary>
  internal static class FastLz {

    /// <summary>Read and decompress a Noita save file. Returns null if it is missing or malformed.</summary>
    public static byte[] ReadNoitaFile(string path) {
      try {
        if (!File.Exists(path)) return null;
        byte[] raw;
        // The game may be rewriting the file; share it rather than lock it.
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) {
          raw = new byte[fs.Length];
          int got = 0;
          while (got < raw.Length) { int n = fs.Read(raw, got, raw.Length - got); if (n <= 0) break; got += n; }
          if (got != raw.Length) return null;
        }
        if (raw.Length < 9) return null;
        int comp = BitConverter.ToInt32(raw, 0);
        int size = BitConverter.ToInt32(raw, 4);
        if (comp <= 0 || size <= 0 || comp > raw.Length - 8 || size > 64 * 1024 * 1024) return null;
        return Decompress(raw, 8, comp, size);
      } catch { return null; }
    }

    public static byte[] Decompress(byte[] input, int start, int length, int outSize) {
      var op = new byte[outSize];
      int level = (input[start] >> 5) + 1;
      return level == 1 ? Level1(input, start, length, op) : Level2(input, start, length, op);
    }

    static byte[] Level1(byte[] ip, int s, int len, byte[] op) {
      int i = s, end = s + len, o = 0;
      int ctrl = ip[i++] & 31;
      while (true) {
        if (ctrl >= 32) {
          int mlen = (ctrl >> 5) - 1;
          int ofs = (ctrl & 31) << 8;
          int r = o - ofs - 1;
          if (mlen == 6) mlen += ip[i++];
          r -= ip[i++];
          mlen += 3;
          if (r < 0 || o + mlen > op.Length) return null;
          for (int k = 0; k < mlen; k++) op[o++] = op[r++];      // may overlap: byte by byte
        } else {
          ctrl++;
          if (i + ctrl > end || o + ctrl > op.Length) return null;
          Buffer.BlockCopy(ip, i, op, o, ctrl);
          i += ctrl; o += ctrl;
        }
        if (i >= end) break;
        ctrl = ip[i++];
      }
      return o == op.Length ? op : null;
    }

    static byte[] Level2(byte[] ip, int s, int len, byte[] op) {
      const int MaxL2Distance = 8191;
      int i = s, end = s + len, o = 0;
      int ctrl = ip[i++] & 31;
      while (true) {
        if (ctrl >= 32) {
          int mlen = (ctrl >> 5) - 1;
          int ofs = (ctrl & 31) << 8;
          int r = o - ofs - 1;
          int code;
          if (mlen == 6) { do { code = ip[i++]; mlen += code; } while (code == 255); }
          code = ip[i++];
          r -= code;
          mlen += 3;
          if (code == 255 && ofs == (31 << 8)) {
            ofs = ip[i++] << 8;
            ofs += ip[i++];
            r = o - ofs - MaxL2Distance - 1;
          }
          if (r < 0 || o + mlen > op.Length) return null;
          for (int k = 0; k < mlen; k++) op[o++] = op[r++];
        } else {
          ctrl++;
          if (i + ctrl > end || o + ctrl > op.Length) return null;
          Buffer.BlockCopy(ip, i, op, o, ctrl);
          i += ctrl; o += ctrl;
        }
        if (i >= end) break;
        ctrl = ip[i++];
      }
      return o == op.Length ? op : null;
    }
  }
}
