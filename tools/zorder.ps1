# Reports window styles and z-order for Noita and the overlay.
Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;using System.Collections.Generic;
public class Z {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc p, IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtr")] public static extern IntPtr GetWindowLongPtr(IntPtr h,int i);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public static List<string> Scan(uint[] pids) {
    var res = new List<string>(); int order = 0;
    IntPtr fg = GetForegroundWindow();
    EnumWindows((h,l) => {
      order++;
      uint pid; GetWindowThreadProcessId(h, out pid);
      bool want = false; foreach (var p in pids) if (p == pid) want = true;
      if (!want || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.Right-r.Left < 100) return true;
      var t = new StringBuilder(128); GetWindowText(h,t,128);
      long ex = GetWindowLongPtr(h, -20).ToInt64();
      res.Add(string.Format("z#{0,-4} pid={1,-6} '{2}' rect={3},{4} {5}x{6}  exStyle=0x{7:X8}  TOPMOST={8}  foreground={9}",
        order, pid, t, r.Left, r.Top, r.Right-r.Left, r.Bottom-r.Top, ex, (ex & 0x8) != 0, h == fg));
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
"@
$pids = @()
foreach ($n in @('noita','NoitaOverlay','NoitaOverlay_nodpi')) { $p = Get-Process $n -ErrorAction SilentlyContinue; if ($p) { $pids += [uint32]$p.Id } }
"scanning pids: $($pids -join ', ')  (lower z# = nearer the front)"
[Z]::Scan($pids) | ForEach-Object { "  $_" }
