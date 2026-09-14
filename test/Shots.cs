using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace NoitaOverlay {
  /// <summary>
  /// Renders the overlay to PNG files for the README. Draws straight to a bitmap at 2x,
  /// so the images are crisp and fully opaque regardless of the live transparency setting.
  /// </summary>
  internal static class Shots {

    const int Scale = 2;              // 2x for readable images
    const int LogicalWidth = 360;

    static Snapshot Make(string place, string[] places, string[] flags, int orbs) {
      var s = new Snapshot {
        CurrentPlace = place, OrbCount = orbs,
        GameRunning = true, InRun = true, Moving = true, Seed = "1713234715"
      };
      foreach (var p in places) s.Places.Add(p);
      foreach (var f in flags) s.Flags.Add(f);
      return s;
    }

    static readonly string[] VeteranPlaces = {
      "surface","coalmine","coalmine_alt","excavationsite","fungicave","snowcave","snowcastle",
      "rainforest","vault","crypt","boss_arena","boss_victoryroom","holymountain","wandcave",
      "liquidcave","dragoncave","orbroom","winter","watercave"
    };
    static readonly string[] VeteranFlags = { "boss_centipede", "progress_ending0", "essence_laser" };

    static void Shot(string file, string status, Snapshot snap, bool compact, bool hideDone, bool extras = false) {
      int w = LogicalWidth * Scale;
      int barH = 28 * Scale;

      var board = new Board {
        ScaleOverride = Scale,
        Snap = snap,
        Status = status,
        Compact = compact,
        HideDone = hideDone,
        ShowExtras = extras,
        Next = Model.Recommend(snap)
      };
      board.Side = Model.Explore(snap, board.Next == null ? null : board.Next.GoalId);

      // First pass to find out how tall the content is, then draw it for real.
      board.Size = new Size(w, 4000);
      using (var probe = new Bitmap(w, 4000)) { probe.SetResolution(96 * Scale, 96 * Scale); board.RenderTo(probe); }
      int h = Math.Max(board.ContentHeight + 6 * Scale, 60 * Scale);
      board.Size = new Size(w, h);

      var body = new Bitmap(w, h);
      body.SetResolution(96 * Scale, 96 * Scale);
      board.RenderTo(body);

      // Compose the title bar above the board so it looks like the real window.
      var outp = new Bitmap(w, h + barH);
      outp.SetResolution(96 * Scale, 96 * Scale);
      using (var g = Graphics.FromImage(outp)) {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        using (var b = new SolidBrush(Palette.BgAlt)) g.FillRectangle(b, 0, 0, w, barH);
        using (var f = new Font("Segoe UI", 8.25f, FontStyle.Bold))
        using (var b = new SolidBrush(Palette.Dim))
          g.DrawString("NOITA  ·  what is left", f, b, 10 * Scale, 7 * Scale);
        using (var f = new Font("Segoe UI", 8f))
        using (var b = new SolidBrush(Palette.Dim)) {
          var lbl = hideDone ? "show all" : "hide done";
          var sz = g.MeasureString(lbl, f);
          g.DrawString(lbl, f, b, w - sz.Width - 44 * Scale, 8 * Scale);
          g.DrawString("X", f, b, w - 22 * Scale, 8 * Scale);
        }
        g.DrawImage(body, 0, barH);
      }

      var dir = Path.Combine(Directory.GetCurrentDirectory(), "docs");
      Directory.CreateDirectory(dir);
      var path = Path.Combine(dir, file);
      outp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
      Console.WriteLine("wrote " + file + "  " + outp.Width + "x" + outp.Height);
      body.Dispose(); outp.Dispose(); board.Dispose();
    }

    [STAThread]
    static void Main() {
      Application.EnableVisualStyles();

      // 1 and 2: brand new player, first ever run.
      var fresh = Make("coalmine", new[] { "surface", "coalmine" }, new string[0], 0);
      Shot("01-new-idle.png",     "In the Mines", fresh, true,  true);
      Shot("02-new-expanded.png", "In the Mines", fresh, false, true);

      // 3: partway in, Detours has unlocked.
      var mid = Make("excavationsite",
        new[] { "surface", "coalmine", "holymountain", "excavationsite" }, new string[0], 0);
      Shot("03-detours-unlocked.png", "In the Coal Pits", mid, false, true);

      // 4 and 5: a player who has finished the game.
      var vet = Make("holymountain", VeteranPlaces, VeteranFlags, 2);
      Shot("04-veteran-idle.png",     "Paused in a Holy Mountain", vet, true,  true);
      Shot("05-veteran-expanded.png", "Paused in a Holy Mountain", vet, false, true);

      // 6: game closed, launcher button showing.
      var off = Make("", new string[0], new string[0], 0);
      off.GameRunning = false; off.InRun = false; off.Moving = false; off.Seed = "";
      Shot("06-not-running.png", "Noita is not running", off, true, true);

      // 7: the opt-in extra checkbox tiers, normally hidden.
      Shot("07-extras-on.png", "Paused in a Holy Mountain", vet, false, true, true);
    }
  }
}
