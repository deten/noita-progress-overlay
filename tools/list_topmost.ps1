Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;using System.Collections.Generic;
public class TM {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc p, IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int i);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public static List<string> All() {
    var res = new List<string>();
    EnumWindows((h,l) => {
      if (!IsWindowVisible(h)) return true;
      int ex = GetWindowLong(h,-20);
      if ((ex & 0x8) == 0) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.Right-r.Left < 40) return true;
      var t = new StringBuilder(128); GetWindowText(h,t,128);
      res.Add(string.Format("'{0}'  {1}x{2}  ex=0x{3:X8}", t, r.Right-r.Left, r.Bottom-r.Top, ex));
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
"@
$all = [TM]::All()
"windows currently holding WS_EX_TOPMOST: $($all.Count)"
$all | ForEach-Object { "  $_" }
