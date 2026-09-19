# Asks Windows which window would receive a click at a point. WindowFromPoint honours
# HTTRANSPARENT, so if the overlay is properly locked it should NOT be the answer.
Add-Type @"
using System;using System.Runtime.InteropServices;using System.Text;
public class CT {
  [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")] public static extern IntPtr WindowFromPoint(POINT p);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll",CharSet=CharSet.Auto)] public static extern int GetClassName(IntPtr h,StringBuilder s,int n);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; public POINT(int x,int y){X=x;Y=y;} }
  public static string Who(int x, int y) {
    IntPtr h = WindowFromPoint(new POINT(x,y));
    uint pid; GetWindowThreadProcessId(h, out pid);
    var c = new StringBuilder(128); GetClassName(h,c,128);
    string name = "?";
    try { name = System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; } catch {}
    return name + "  (pid " + pid + ", class " + c + ")";
  }
}
"@
[void][CT]::SetProcessDPIAware()
$o = Get-Process NoitaOverlay -ErrorAction Stop
Add-Type -AssemblyName System.Windows.Forms
# locate the overlay window in physical pixels
Add-Type @"
using System;using System.Runtime.InteropServices;
public class W2 {
  public delegate bool P(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] public static extern bool EnumWindows(P p, IntPtr l);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rt,B; }
  public static R Found; public static bool Get(uint target) {
    bool ok=false; R f=new R();
    EnumWindows((h,l)=>{ uint pid; GetWindowThreadProcessId(h,out pid);
      if(pid!=target||!IsWindowVisible(h)) return true;
      R r; GetWindowRect(h,out r); if(r.Rt-r.L<100) return true;
      f=r; ok=true; return false; }, IntPtr.Zero);
    Found=f; return ok;
  }
}
"@
if (-not [W2]::Get([uint32]$o.Id)) { throw "overlay window not found" }
$r = [W2]::Found
$cx = [int](($r.L + $r.Rt) / 2)
$cy = [int]($r.T + ($r.B - $r.T) * 0.7)      # well into the board area
Write-Host "overlay rect $($r.L),$($r.T) to $($r.Rt),$($r.B)"
Write-Host ""
Write-Host "click at board centre ($cx,$cy) would go to:"
Write-Host "   $([CT]::Who($cx,$cy))"
$bx = [int]($r.Rt - 60); $by = [int]($r.T + 14)   # roughly the lock button
Write-Host "click at lock button area ($bx,$by) would go to:"
Write-Host "   $([CT]::Who($bx,$by))"
