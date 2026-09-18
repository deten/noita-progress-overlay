Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;using System.Collections.Generic;
public class Enu {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc p, IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public static List<string> Find(uint target) {
    var outp = new List<string>();
    EnumWindows((h,l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid != target) return true;
      var t = new StringBuilder(256); GetWindowText(h,t,256);
      var c = new StringBuilder(256); GetClassName(h,c,256);
      RECT r; GetWindowRect(h, out r);
      outp.Add(h + " vis=" + IsWindowVisible(h) + " cls=" + c + " title='" + t + "' rect=" + r.Left + "," + r.Top + " " + (r.Right-r.Left) + "x" + (r.Bottom-r.Top));
      return true;
    }, IntPtr.Zero);
    return outp;
  }
}
"@
$p = Get-Process NoitaOverlay -ErrorAction Stop
Write-Host "pid=$($p.Id)"
[Enu]::Find([uint32]$p.Id) | ForEach-Object { Write-Host "  $_" }
