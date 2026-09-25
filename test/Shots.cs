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

    // The world where "You feel wary" was seen in the Coal Pits, so effect shots are real ones.
    const uint DemoSeed = 1184881785;

    static Snapshot Make(string place, string[] places, string[] flags, int orbs, string rawBiome = null, int deaths = 1000) {
      var s = new Snapshot {
        CurrentPlace = place, CurrentBiome = rawBiome ?? place, OrbCount = orbs,
        GameRunning = true, InRun = true, Moving = true, Seed = DemoSeed.ToString()
      };
      foreach (var p in places) s.Places.Add(p);
      foreach (var f in flags) s.Flags.Add(f);
      // Same computation the tracker does, so the effect line matches the real overlay.
      Modifier m;
      if (BiomeModifiers.For(DemoSeed, deaths, s.Flags).TryGetValue(s.CurrentBiome, out m)) s.EffectText = m.Text;
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
        // The bar is the status line now: dot, where you are, seed, then the buttons.
        using (var b = new SolidBrush(Palette.BgAlt)) g.FillRectangle(b, 0, 0, w, barH);
        int right = w;
        using (var f = new Font("Segoe UI", 8f))
        using (var b = new SolidBrush(Palette.Dim)) {
          g.DrawString("X", f, b, w - 20 * Scale, 8 * Scale);   right -= 28 * Scale;
          var lbl = hideDone ? "show all" : "hide done";
          var sz = g.MeasureString(lbl, f);
          g.DrawString(lbl, f, b, right - sz.Width - 6 * Scale, 8 * Scale);
          right -= (int)sz.Width + 14 * Scale;
          var ls = g.MeasureString("lock", f);
          g.DrawString("lock", f, b, right - ls.Width - 6 * Scale, 8 * Scale);
          right -= (int)ls.Width + 14 * Scale;
          if (!snap.GameRunning) {
            // The real bar shows a Launch button here while the game is closed.
            var lz = g.MeasureString("Launch Noita", f);
            var r = new Rectangle(right - (int)lz.Width - 16 * Scale, 5 * Scale, (int)lz.Width + 12 * Scale, barH - 10 * Scale);
            using (var bb = new SolidBrush(Color.FromArgb(46, 52, 62))) g.FillRectangle(bb, r);
            using (var tb = new SolidBrush(Palette.Text)) g.DrawString("Launch Noita", f, tb, r.X + 6 * Scale, r.Y + 3 * Scale);
            right = r.X - 6 * Scale;
          }
        }
        int d = 7 * Scale, lpad = 9 * Scale, tx = lpad + 14 * Scale;
        using (var b = new SolidBrush(snap.GameRunning ? Palette.Live : Palette.Dimmer))
          g.FillEllipse(b, lpad, (barH - d) / 2, d, d);
        using (var fb = new Font("Segoe UI", 9f, FontStyle.Bold))
        using (var fs = new Font("Segoe UI", 8.25f, FontStyle.Regular)) {
          float lineH = g.MeasureString("Xg", fb).Height;
          float statusW = g.MeasureString(status, fb).Width;
          if (snap.Seed.Length > 0 && snap.InRun) {
            var seed = "seed " + snap.Seed;
            var sz = g.MeasureString(seed, fs);
            if (right - tx - statusW > sz.Width + 12 * Scale) {
              using (var b = new SolidBrush(Palette.Dimmer))
                g.DrawString(seed, fs, b, right - sz.Width - 6 * Scale,
                             (barH - g.MeasureString("Xg", fs).Height) / 2);
              right -= (int)sz.Width + 10 * Scale;
            }
          }
          using (var b = new SolidBrush(snap.GameRunning ? Palette.Text : Palette.Dim))
            g.DrawString(status, fb, b,
              new RectangleF(tx, (barH - lineH) / 2, Math.Max(10, right - tx), lineH * 1.2f),
              new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter });
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
      var fresh = Make("coalmine", new[] { "surface", "coalmine" }, new string[0], 0, null, 0);
      Shot("01-new-idle.png",     "In the Mines", fresh, true,  true);
      Shot("02-new-expanded.png", "In the Mines", fresh, false, true);

      // 3: partway in, Detours has unlocked.
      var mid = Make("excavationsite",
        new[] { "surface", "coalmine", "holymountain", "excavationsite" }, new string[0], 0, null, 2);
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

      // 8: a rolled biome effect, predicted from the seed. This is the world and biome where
      //    the game really did say "You feel wary".
      var wary = Make("excavationsite", VeteranPlaces, VeteranFlags, 2, "excavationsite");
      Shot("08-biome-effect.png", "In the Coal Pits", wary, true, true);
    }
  }
}
