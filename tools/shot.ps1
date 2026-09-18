# Finds the overlay window by class prefix + title, raises it, and captures it.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public class Cap {
  public delegate bool Proc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(Proc p, IntPtr l);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetWindowText(IntPtr h,StringBuilder s,int n);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h,IntPtr a,int x,int y,int cx,int cy,uint f);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
  public static IntPtr Found; public static RECT FoundRect;
  public static bool Locate(uint target) {
    Found = IntPtr.Zero;
    EnumWindows((h,l) => {
      uint pid; GetWindowThreadProcessId(h, out pid);
      if (pid != target || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.Right-r.Left < 100) return true;
      Found = h; FoundRect = r; return false;
    }, IntPtr.Zero);
    return Found != IntPtr.Zero;
  }
}
"@
$p = Get-Process NoitaOverlay -ErrorAction Stop
if (-not [Cap]::Locate([uint32]$p.Id)) { throw "no visible overlay window" }
$r = [Cap]::FoundRect
$w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
Write-Host "capturing $($r.Left),$($r.Top) ${w}x$h"
# HWND_TOPMOST = -1, SWP_NOMOVE|SWP_NOSIZE = 0x0003
[void][Cap]::SetWindowPos([Cap]::Found, [IntPtr](-1), 0,0,0,0, 0x0003)
Start-Sleep -Milliseconds 600
$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, $bmp.Size)
$out = "$PSScriptRoot\..\dist\overlay.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Host "saved $out"
