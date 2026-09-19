# Asks every window of the overlay what it would do with a click at its own centre.
# NCHITTEST = -1 (HTTRANSPARENT) means the click passes through to whatever is underneath.
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public class LockProbe {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr p, P cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll", CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, Rt, B; }

  static long HitAtCentre(IntPtr h) {
    RECT r; GetWindowRect(h, out r);
    int cx = (r.L + r.Rt) / 2, cy = (r.T + r.B) / 2;
    return SendMessage(h, 0x0084, IntPtr.Zero, (IntPtr)((cy << 16) | (cx & 0xFFFF))).ToInt64();
  }

  public static List<string> Dump(uint pid) {
    var outp = new List<string>();
    EnumWindows((h, l) => {
      uint p2; GetWindowThreadProcessId(h, out p2);
      if (p2 != pid || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.Rt - r.L < 100) return true;
      outp.Add(string.Format("{0,-18} {1}", "FORM", HitAtCentre(h)));
      EnumChildWindows(h, (c, l2) => {
        var t = new StringBuilder(64); GetWindowText(c, t, 64);
        string name = t.Length > 0 ? t.ToString() : "(panel)";
        outp.Add(string.Format("{0,-18} {1}", name, HitAtCentre(c)));
        return true;
      }, IntPtr.Zero);
      return false;
    }, IntPtr.Zero);
    return outp;
  }
}
"@
$o = Get-Process NoitaOverlay -ErrorAction Stop
Write-Host ("{0,-18} {1}" -f "WINDOW", "NCHITTEST")
foreach ($line in [LockProbe]::Dump([uint32]$o.Id)) { Write-Host "  $line" }
Write-Host ""
Write-Host "-1 = HTTRANSPARENT, click passes through to the game"
Write-Host " 1 = HTCLIENT, the overlay takes the click"
