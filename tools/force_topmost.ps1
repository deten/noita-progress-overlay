Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public class FT {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h,int i);
  [DllImport("user32.dll",SetLastError=true)] public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public static IntPtr Find(uint pid) {
    IntPtr found = IntPtr.Zero;
    EnumWindows((h,l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p != pid || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.Right-r.Left < 100) return true;
      found = h; return false;
    }, IntPtr.Zero);
    return found;
  }
}
"@
$o = Get-Process NoitaOverlay -ErrorAction Stop
$h = [FT]::Find([uint32]$o.Id)
"hwnd = $h"
"exStyle before = 0x{0:X8}  TOPMOST={1}" -f [FT]::GetWindowLong($h,-20), (([FT]::GetWindowLong($h,-20) -band 0x8) -ne 0)
$ok = [FT]::SetWindowPos($h, [IntPtr](-1), 0,0,0,0, 0x0013)   # NOMOVE|NOSIZE|NOACTIVATE
$err = [Runtime.InteropServices.Marshal]::GetLastWin32Error()
"SetWindowPos returned $ok (lastError=$err)"
Start-Sleep -Milliseconds 500
"exStyle after  = 0x{0:X8}  TOPMOST={1}" -f [FT]::GetWindowLong($h,-20), (([FT]::GetWindowLong($h,-20) -band 0x8) -ne 0)
