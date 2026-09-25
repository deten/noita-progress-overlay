# Asks Windows which window would receive the mouse at points spread across the overlay.
# WindowFromPoint is what mouse routing actually uses, so it answers the real question:
# when locked, the mouse at those points should reach whatever is underneath, not us.
# (An earlier version of this script sent WM_NCHITTEST to the overlay's own windows. That
# only proves the overlay answers HTTRANSPARENT, which Windows honours for same-thread
# windows only, so it passed while Noita still got no mouse input.)
param([switch]$Toggle)   # also press the unlock hotkey and check the lock flips

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class Probe {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }

  public static IntPtr Main_; public static RECT Rect;
  public static bool Find(uint pid) {
    Main_ = IntPtr.Zero;
    EnumWindows((h, l) => {
      uint p; GetWindowThreadProcessId(h, out p);
      if (p != pid || !IsWindowVisible(h)) return true;
      RECT r; GetWindowRect(h, out r);
      if (r.R - r.L < 100) return true;
      Main_ = h; Rect = r; return false;
    }, IntPtr.Zero);
    return Main_ != IntPtr.Zero;
  }
  public static uint OwnerAt(int x, int y) {
    var pt = new POINT { X = x, Y = y };
    uint pid; GetWindowThreadProcessId(WindowFromPoint(pt), out pid);
    return pid;
  }
  public static void PressHotkey() {        // Ctrl+Shift+L
    keybd_event(0x11, 0, 0, UIntPtr.Zero); keybd_event(0x10, 0, 0, UIntPtr.Zero);
    keybd_event(0x4C, 0, 0, UIntPtr.Zero); keybd_event(0x4C, 0, 2, UIntPtr.Zero);
    keybd_event(0x10, 0, 2, UIntPtr.Zero); keybd_event(0x11, 0, 2, UIntPtr.Zero);
  }
}
"@
[void][Probe]::SetProcessDPIAware()

function Check([string]$label) {
  $o = Get-Process NoitaOverlay -ErrorAction Stop
  if (-not [Probe]::Find([uint32]$o.Id)) { throw "overlay window not found" }
  $r = [Probe]::Rect
  $ex = [Probe]::GetWindowLong([Probe]::Main_, -20)
  $ours = 0; $total = 0; $below = @{}
  foreach ($fx in 0.08, 0.3, 0.5, 0.7, 0.92) {
    foreach ($fy in 0.1, 0.35, 0.6, 0.85) {
      $x = [int]($r.L + ($r.R - $r.L) * $fx); $y = [int]($r.T + ($r.B - $r.T) * $fy)
      $owner = [Probe]::OwnerAt($x, $y); $total++
      if ($owner -eq $o.Id) { $ours++ }
      else { $n = try { (Get-Process -Id $owner -ErrorAction Stop).ProcessName } catch { "pid $owner" }; $below[$n] = 1 }
    }
  }
  "{0,-9} WS_EX_TRANSPARENT={1}  WS_EX_LAYERED={2}   mouse reaches overlay at {3}/{4} points{5}" -f `
    $label, (($ex -band 0x20) -ne 0), (($ex -band 0x80000) -ne 0), $ours, $total,
    $(if ($below.Count) { "; otherwise " + ($below.Keys -join ', ') } else { "" })
}

Check "now:"
if ($Toggle) {
  [Probe]::PressHotkey(); Start-Sleep -Milliseconds 700
  Check "after key:"
  [Probe]::PressHotkey(); Start-Sleep -Milliseconds 700
  Check "after key:"
}
