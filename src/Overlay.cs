using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NoitaOverlay {

  internal static class Palette {
    public static readonly Color Bg     = Color.FromArgb(18, 20, 24);
    public static readonly Color BgAlt  = Color.FromArgb(26, 29, 35);
    public static readonly Color Text   = Color.FromArgb(233, 235, 239);
    public static readonly Color Dim    = Color.FromArgb(128, 134, 146);
    public static readonly Color Dimmer = Color.FromArgb(88, 93, 103);
    public static readonly Color Live   = Color.FromArgb(110, 198, 122);
  }

  /// <summary>
  /// A panel that can opt out of the mouse entirely. WM_NCHITTEST is answered per window,
  /// so the form saying HTTRANSPARENT does nothing for clicks that land on a child control:
  /// each child has to say it too, or the click stops there.
  /// </summary>
  internal class ClickThrough : Panel {
    public bool Locked;
    protected override void WndProc(ref Message m) {
      const int WM_NCHITTEST = 0x0084, HTTRANSPARENT = -1;
      if (Locked && m.Msg == WM_NCHITTEST) { m.Result = (IntPtr)HTTRANSPARENT; return; }
      base.WndProc(ref m);
    }
  }

  /// <summary>Draws the tiered board. All layout happens in one pass so hit-testing stays in sync.</summary>
  internal sealed class Board : ClickThrough {
    public Snapshot Snap = new Snapshot();
    public bool HideDone;
    public string Toast; public DateTime ToastUntil;

    public bool Compact;                                  // idle: just the next task + pins
    public bool ShowExtras;                               // opt-in wiki-ish checkbox tiers
    public HashSet<string> Pinned = new HashSet<string>();
    public Suggestion Next, Side;
    public Action PinsChanged;
    public Action<string> ManualToggled;
    /// <summary>Height the content actually needs, so the form can size itself to it.</summary>
    public int ContentHeight { get; private set; }

    /// <summary>Force a scale factor instead of reading screen DPI. Used for rendering docs images.</summary>
    public float ScaleOverride;

    /// <summary>Paint straight into a bitmap, no window needed. Used to generate screenshots.</summary>
    public void RenderTo(Bitmap bmp) {
      using (var g = Graphics.FromImage(bmp))
        OnPaint(new PaintEventArgs(g, new Rectangle(0, 0, bmp.Width, bmp.Height)));
    }

    readonly HashSet<int> _collapsed = new HashSet<int>();
    readonly List<Tuple<Rectangle, int>> _hits = new List<Tuple<Rectangle, int>>();
    readonly List<Tuple<Rectangle, string>> _goalHits = new List<Tuple<Rectangle, string>>();
    int _contentH;

    readonly Font _fTier   = new Font("Segoe UI", 9.5f, FontStyle.Bold);
    readonly Font _fTitle  = new Font("Segoe UI", 9.5f, FontStyle.Regular);
    readonly Font _fDeed   = new Font("Segoe UI", 8.75f, FontStyle.Regular);
    readonly Font _fHint   = new Font("Segoe UI", 8.25f, FontStyle.Italic);
    readonly Font _fSmall  = new Font("Segoe UI", 8.25f, FontStyle.Regular);
    readonly Font _fStatus = new Font("Segoe UI", 9.5f, FontStyle.Bold);

    public Board() {
      DoubleBuffered = true;
      AutoScroll = true;
      BackColor = Palette.Bg;
      ResizeRedraw = true;
    }

    protected override void OnMouseClick(MouseEventArgs e) {
      base.OnMouseClick(e);
      var p = new Point(e.X - AutoScrollPosition.X, e.Y - AutoScrollPosition.Y);
      foreach (var h in _hits) {
        if (h.Item1.Contains(p)) {
          if (_collapsed.Contains(h.Item2)) _collapsed.Remove(h.Item2); else _collapsed.Add(h.Item2);
          Invalidate();
          return;
        }
      }
      foreach (var h in _goalHits) {
        if (!h.Item1.Contains(p)) continue;
        var goal = Model.Goals.FirstOrDefault(x => x.Id == h.Item2);
        // Leads have nothing to detect, so clicking one ticks it off instead of pinning it.
        // Run-flag goals can be ticked by hand too, for things done before the overlay watched.
        if (goal != null && (goal.Source == Source.Manual || goal.Source == Source.RunFlag)) {
          if (ManualToggled != null) ManualToggled(goal.Source == Source.Manual ? goal.Key : goal.Id);
        } else {
          if (Pinned.Contains(h.Item2)) Pinned.Remove(h.Item2); else Pinned.Add(h.Item2);
          if (PinsChanged != null) PinsChanged();
        }
        Invalidate();
        return;
      }
    }

    static void DrawPin(Graphics g, float x, float y, float s, Color c) {
      using (var b = new SolidBrush(c)) {
        g.FillEllipse(b, x, y, s, s);
        g.FillRectangle(b, x + s * 0.42f, y + s * 0.8f, s * 0.16f, s * 0.7f);
      }
    }

    // Long hints used to run off the right edge. Clip them instead.
    static readonly StringFormat OneLine = new StringFormat(StringFormatFlags.NoWrap) {
      Trimming = StringTrimming.EllipsisCharacter
    };
    static readonly StringFormat Wrapped = new StringFormat {
      Trimming = StringTrimming.EllipsisCharacter
    };

    /// <summary>Line height in the Graphics' own units. Font.Height is 96-dpi and lies when
    /// we render at a higher resolution, which silently chopped text in half.</summary>
    static float LineH(Graphics g, Font f) { return g.MeasureString("Xg", f).Height; }

    static void Clip(Graphics g, string s, Font f, Color c, float x, float y, float maxW) {
      using (var b = new SolidBrush(c))
        g.DrawString(s, f, b, new RectangleF(x, y, Math.Max(8f, maxW), LineH(g, f) * 1.4f), OneLine);
    }

    static void Clip2(Graphics g, string s, Font f, Color c, float x, float y, float maxW) {
      using (var b = new SolidBrush(c))
        g.DrawString(s, f, b, new RectangleF(x, y, Math.Max(8f, maxW), LineH(g, f) * 2.1f), Wrapped);
    }

    static Color TierColour(int tier) {
      foreach (var t in Model.Tiers) if (t.N == tier) return Color.FromArgb(t.R, t.G, t.B);
      return Palette.Dim;
    }

    /// <summary>state: 0 = not started, 1 = reached but unfinished, 2 = complete.</summary>
    static void DrawMarker(Graphics g, float x, float y, Color tc, int state, float size) {
      if (state == 2) {
        using (var pn = new Pen(Color.FromArgb(115, tc.R, tc.G, tc.B), size * 0.2f)) {
          g.DrawLine(pn, x, y + size * 0.45f, x + size * 0.33f, y + size * 0.85f);
          g.DrawLine(pn, x + size * 0.33f, y + size * 0.85f, x + size, y);
        }
      } else {
        using (var pn = new Pen(tc, size * 0.16f)) g.DrawEllipse(pn, x, y, size, size);
        if (state == 1)
          using (var b = new SolidBrush(tc)) g.FillPie(b, x, y, size, size, 90, 180);
      }
    }

    protected override void OnPaint(PaintEventArgs e) {
      var g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;
      g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
      g.Clear(Palette.Bg);
      g.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);
      _hits.Clear();
      _goalHits.Clear();

      float sc = ScaleOverride > 0 ? ScaleOverride : g.DpiX / 96f;   // geometry scales; point fonts already do
      Func<float, int> S = v => (int)Math.Round(v * sc);

      int W = ClientSize.Width - (VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0);
      int pad = S(12), y = S(10), inner = W - pad * 2;

      // Status lives in the title bar. Directly under it, this biome's effect in this world,
      // worked out from the seed, so it stays up for as long as you are in the zone.
      if (Snap.GameRunning && Snap.EffectText != null) {
        Clip(g, Snap.EffectText, _fHint, Palette.Dim, pad + S(2), y, inner - S(4));
        y += S(19);
      }

      // ---- discovery toast ------------------------------------------------
      if (Toast != null && DateTime.UtcNow < ToastUntil) {
        var r = new Rectangle(pad, y, inner, S(34));
        using (var b = new SolidBrush(Color.FromArgb(40, 110, 198, 122))) g.FillRectangle(b, r);
        using (var pn = new Pen(Palette.Live)) g.DrawRectangle(pn, r);
        using (var b = new SolidBrush(Palette.Live)) g.DrawString(Toast, _fTitle, b, pad + S(10), y + S(8));
        y += S(44);
      }

      // ---- compact: the one next thing, plus anything you pinned ----------
      if (Compact) {
        if (Next != null) {
          var tcN = TierColour(Next.Tier);
          using (var b = new SolidBrush(Palette.Dimmer))
            g.DrawString(Next.Urgent ? "DO THIS" : "NEXT", _fSmall, b, pad + S(2), y);
          if (Next.Reason.Length > 0) {
            var rs = g.MeasureString(Next.Reason, _fHint);
            using (var b = new SolidBrush(Palette.Dimmer))
              g.DrawString(Next.Reason, _fHint, b, W - pad - rs.Width, y);
          }
          y += S(16);
          float tw = inner - S(14);
          var titleFont = Next.Urgent ? _fStatus : _fTitle;
          int titleH = (int)Math.Ceiling(LineH(g, titleFont) * 1.25f);
          // Two lines for the hint: these can be long, and truncating them loses the point.
          var hintH = (int)Math.Ceiling(g.MeasureString(Next.Hint, _fHint,
                        new SizeF(tw, LineH(g, _fHint) * 2.1f), Wrapped).Height) + S(4);
          using (var b = new SolidBrush(tcN))
            g.FillRectangle(b, pad, y + S(2), S(3), titleH + hintH - S(4));
          Clip(g, Next.Title, titleFont, Palette.Text, pad + S(11), y, tw);
          Clip2(g, Next.Hint, _fHint, Palette.Dim, pad + S(11), y + titleH, tw);
          y += titleH + hintH + S(6);
        }

        // The optional half: present, but plainly not being asked of you.
        if (Side != null) {
          using (var b = new SolidBrush(Palette.Dimmer))
            g.DrawString("or, no rush", _fHint, b, pad + S(2), y);
          y += S(15);
          Clip(g, Side.Title, _fDeed, Palette.Dim, pad + S(11), y, inner - S(14));
          y += S(19);
        }

        foreach (var id in Pinned) {
          var go = Model.Goals.FirstOrDefault(x => x.Id == id);
          if (go == null || (Next != null && go.Id == Next.GoalId)) continue;
          bool rch = Tracker.Reached(go, Snap), dn = Tracker.Done(go, Snap);
          var tc2 = TierColour(go.Tier);
          DrawPin(g, pad + S(3), y + S(3), S(7), tc2);
          using (var b = new SolidBrush(dn ? Palette.Dimmer : Palette.Text))
            g.DrawString(go.DisplayTitle(rch), _fDeed, b, pad + S(20), y);
          using (var b = new SolidBrush(Palette.Dim))
            g.DrawString(go.Hint, _fHint, b, pad + S(20), y + S(15));
          y += S(34);
        }
        y += S(6);
        ContentHeight = y;
        if (_contentH != y) { _contentH = y; AutoScrollMinSize = new Size(0, y); }
        return;
      }

      // ---- tiers ----------------------------------------------------------
      foreach (var t in Model.Tiers) {
        if (Model.IsExtra(t.N) && !ShowExtras) continue;
        var goals = Model.Goals.Where(x => x.Tier == t.N).ToArray();
        int done = goals.Count(x => Tracker.Done(x, Snap));
        var tc = Color.FromArgb(t.R, t.G, t.B);

        // Not earned yet: show that there is more, without saying what.
        if (!Model.TierUnlocked(t.N, Snap)) {
          using (var b = new SolidBrush(Color.FromArgb(70, t.R, t.G, t.B)))
            g.FillRectangle(b, pad, y + S(4), S(3), S(14));
          using (var b = new SolidBrush(Palette.Dimmer))
            g.DrawString(t.Name.ToUpperInvariant(), _fSmall, b, pad + S(11), y + S(2));
          using (var b = new SolidBrush(Color.FromArgb(70, 74, 84)))
            g.DrawString("later", _fHint, b, pad + S(11) + g.MeasureString(t.Name.ToUpperInvariant(), _fSmall).Width + S(8), y + S(3));
          y += S(24);
          continue;
        }

        bool collapsed = _collapsed.Contains(t.N);

        _hits.Add(Tuple.Create(new Rectangle(pad, y, inner, S(32)), t.N));
        using (var b = new SolidBrush(tc)) g.FillRectangle(b, pad, y + S(4), S(3), S(21));
        using (var b = new SolidBrush(tc))
          g.DrawString(t.Name.ToUpperInvariant(), _fTier, b, pad + S(11), y + S(1));
        using (var b = new SolidBrush(Palette.Dimmer)) {
          g.DrawString(t.Blurb, _fHint, b, pad + S(11), y + S(16));
          var cnt = done + "/" + goals.Length + (collapsed ? "  +" : "  -");
          var sz = g.MeasureString(cnt, _fSmall);
          g.DrawString(cnt, _fSmall, b, W - pad - sz.Width, y + S(6));
        }
        y += S(36);

        if (!collapsed) {
          foreach (var go in goals) {
            bool ok      = Tracker.Done(go, Snap);
            bool reached = Tracker.Reached(go, Snap);
            if (ok && HideDone) continue;
            bool here = go.Source == Source.Place && go.Key == Snap.CurrentPlace;

            // Before you have been: a nudge, no name. After: the name, plus a reminder of
            // where the place actually was, which a nudge is no longer any use for.
            string sub = reached ? (ok ? null : go.Where) : go.Hint;
            bool showHint = sub != null;
            int rowH = showHint ? S(34) : S(21);
            if (here)
              using (var b = new SolidBrush(Color.FromArgb(34, t.R, t.G, t.B)))
                g.FillRectangle(b, pad, y - S(2), inner, rowH);

            _goalHits.Add(Tuple.Create(new Rectangle(pad, y - S(2), inner, rowH), go.Id));
            DrawMarker(g, pad + S(4), y + S(4), tc, ok ? 2 : (reached ? 1 : 0), S(9));
            float gw = inner - S(26) - (Pinned.Contains(go.Id) ? S(14) : 0);
            Clip(g, go.DisplayTitle(reached), _fTitle, ok ? Palette.Dimmer : Palette.Text, pad + S(22), y - S(1), gw);
            if (Pinned.Contains(go.Id)) DrawPin(g, W - pad - S(13), y + S(3), S(7), tc);

            if (showHint) {
              string hint = sub;
              if (go.Source == Source.OrbCount)
                hint = Snap.OrbCount + " of " + go.N + " found. " + go.Hint;
              Clip(g, hint, _fHint, Palette.Dim, pad + S(22), y + S(16), inner - S(26));
            }
            y += rowH;

            // Deeds: what is actually left to do somewhere you have already stood.
            foreach (var d in go.Deeds) {
              bool dd = Tracker.DeedDone(d, Snap);
              if (dd && HideDone) continue;
              DrawMarker(g, pad + S(28), y + S(3), tc, dd ? 2 : 0, S(7));
              Clip(g, d.Title, _fDeed, dd ? Palette.Dimmer : Palette.Text, pad + S(42), y - S(2), inner - S(46));
              if (!dd) {
                Clip(g, d.Hint, _fHint, Palette.Dim, pad + S(42), y + S(13), inner - S(46));
                y += S(31);
              } else y += S(18);
            }
          }
          y += S(8);
        }
      }

      y += S(10);
      ContentHeight = y;
      if (_contentH != y) { _contentH = y; AutoScrollMinSize = new Size(0, y); }
    }
  }

  internal sealed class OverlayForm : Form {
    // Setting ShowInTaskbar = false makes WinForms recreate the window handle, which silently
    // drops WS_EX_TOPMOST -- so the TopMost = true set in the constructor is thrown away.
    // Re-apply it, but only when it is actually missing: unconditionally re-asserting z-order
    // every tick is what appeared to upset Noita during a focus switch.
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    const int GWL_EXSTYLE = -20, WS_EX_TOPMOST = 0x8;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

    // Apply the overlay styles at window-creation time. Setting WS_EX_TOPMOST after the fact
    // via SetWindowPos is silently refused while Noita holds the foreground, but the style
    // may survive if it is present in the CreateWindowEx call itself.
    // NOACTIVATE also stops the overlay stealing focus from the game when it appears.
    protected override CreateParams CreateParams {
      get {
        const int WS_EX_TOPMOST_ = 0x00000008, WS_EX_TOOLWINDOW = 0x00000080, WS_EX_NOACTIVATE = 0x08000000;
        var cp = base.CreateParams;
        cp.ExStyle |= WS_EX_TOPMOST_ | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
        return cp;
      }
    }

    void EnsureTopMost() {
      if (!IsHandleCreated) return;
      if ((GetWindowLong(Handle, GWL_EXSTYLE) & WS_EX_TOPMOST) != 0) return;   // already correct
      SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    protected override void OnShown(EventArgs e) {
      base.OnShown(e);
      EnsureTopMost();
    }

    // A borderless form has no resize grip, so claim the right/bottom edges by hand.
    // When locked, every point except the lock button reports HTTRANSPARENT, which makes
    // Windows deliver the click to whatever is underneath. Doing it per-point rather than
    // with WS_EX_TRANSPARENT is what keeps the unlock button reachable.
    protected override void WndProc(ref Message m) {
      const int WM_NCHITTEST = 0x0084, HTTRANSPARENT = -1, HTRIGHT = 11, HTBOTTOM = 15, HTBOTTOMRIGHT = 17;
      if (m.Msg == WM_NCHITTEST) {
        base.WndProc(ref m);
        int lp = unchecked((int)m.LParam.ToInt64());
        var p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));

        if (_locked) {
          if (_lockBtn != null && LockButtonBounds().Contains(p)) return;   // keep this one live
          m.Result = (IntPtr)HTTRANSPARENT;
          return;
        }

        int grip = 8;
        bool r = p.X >= ClientSize.Width - grip, b = p.Y >= ClientSize.Height - grip;
        if (r && b)      m.Result = (IntPtr)HTBOTTOMRIGHT;
        else if (r)      m.Result = (IntPtr)HTRIGHT;
        else if (b)      m.Result = (IntPtr)HTBOTTOM;
        return;
      }
      base.WndProc(ref m);
    }

    /// <summary>Lock button position in form client coordinates.</summary>
    Rectangle LockButtonBounds() {
      return RectangleToClient(_lockBtn.RectangleToScreen(_lockBtn.ClientRectangle));
    }

    bool _locked;
    Button _lockBtn, _launchBtn, _toggleBtn, _closeBtn;
    ClickThrough _bar;
    string _status = "", _seed = "";
    bool _live;

    void SetLocked(bool on) {
      _locked = on;
      if (_lockBtn != null) {
        _lockBtn.Text = on ? "locked" : "lock";
        _lockBtn.ForeColor = on ? Palette.Live : Palette.Dim;
      }
      // Every child window has to decline the mouse for itself.
      _board.Locked = on;
      if (_bar != null) _bar.Locked = on;
      // Nothing but unlocking is available while locked, so the other buttons go away.
      foreach (var b in new[] { _toggleBtn, _closeBtn, _launchBtn })
        if (b != null) b.Visible = on ? false : (b != _launchBtn || !_live);
      // Collapse back to the compact view and stay there.
      if (on) {
        _overTicks = 0;
        _board.Compact = true;
        _board.Invalidate();
      }
      if (_bar != null) _bar.Invalidate();
      _cfg.Locked = on;
    }

    readonly Tracker _tracker = new Tracker();
    readonly Board _board = new Board();
    readonly Timer _timer = new Timer();
    readonly Timer _fade = new Timer();
    Point _drag; bool _dragging;

    // Faint while you play, solid when you actually look at it. Noita is mouse-aimed, so the
    // cursor sweeps over this constantly -- require a short dwell before brightening, or it
    // would flicker every time you aimed at the top-right of the screen.
    readonly Settings _cfg = new Settings();
    double IdleOpacity { get { return _cfg.IdleOpacity; } }
    double HoverOpacity { get { return _cfg.HoverOpacity; } }
    int DwellTicks { get { return Math.Max(1, _cfg.DwellMs / 40); } }
    int _overTicks;

    int _expandedH;                    // remembered full height (user-resizable)
    int _barH;

    void FadeStep() {
      // Locked means locked: no expanding, no brightening, just the compact strip.
      bool over = !_locked && (Bounds.Contains(Cursor.Position) || _dragging);
      _overTicks = over ? _overTicks + 1 : 0;
      bool open = !_locked && (_overTicks >= DwellTicks || _dragging);

      if (_board.Compact == open) {     // state flipped -- switch the view
        _board.Compact = !open;
        _board.Invalidate();
      }

      double target = open ? HoverOpacity : IdleOpacity;
      double d = target - Opacity;
      if (Math.Abs(d) < 0.015) { if (Opacity != target) Opacity = target; }
      else Opacity += d * 0.30;        // ease toward the target rather than snapping

      // Grow and shrink so the idle overlay covers as little screen as possible.
      int want = open ? _expandedH : Math.Min(_board.ContentHeight + _barH + 4, _expandedH);
      if (want < _barH + 40) want = _barH + 40;
      int dh = want - Height;
      if (Math.Abs(dh) <= 2) { if (Height != want) Height = want; }
      else Height += (int)Math.Round(dh * 0.35);
    }

    /// <summary>Right-click menu so transparency is adjustable without editing the ini.</summary>
    void BuildMenu() {
      var menu = new ContextMenuStrip { ShowImageMargin = false };

      var idle = new ToolStripMenuItem("Idle transparency");
      foreach (var pct in new[] { 10, 15, 20, 28, 40, 55, 75, 100 }) {
        int p = pct;
        var mi = new ToolStripMenuItem(p + "%", null, (s, e) => { _cfg.IdleOpacity = p / 100.0; SyncMenu(menu); });
        mi.Tag = "idle:" + p;
        idle.DropDownItems.Add(mi);
      }
      var hover = new ToolStripMenuItem("Hover transparency");
      foreach (var pct in new[] { 60, 75, 85, 97, 100 }) {
        int p = pct;
        var mi = new ToolStripMenuItem(p + "%", null, (s, e) => { _cfg.HoverOpacity = p / 100.0; SyncMenu(menu); });
        mi.Tag = "hover:" + p;
        hover.DropDownItems.Add(mi);
      }
      var dwell = new ToolStripMenuItem("Expand delay");
      foreach (var ms in new[] { 0, 100, 200, 400, 800 }) {
        int d = ms;
        var mi = new ToolStripMenuItem(d == 0 ? "instant" : d + " ms", null, (s, e) => { _cfg.DwellMs = d; SyncMenu(menu); });
        mi.Tag = "dwell:" + d;
        dwell.DropDownItems.Add(mi);
      }

      menu.Items.Add(idle);
      menu.Items.Add(hover);
      menu.Items.Add(dwell);
      // Effects are predicted from the seed. If the game ever shows something different, this
      // overrides the prediction for that world and biome, and effects.txt records the miss.
      var effect = new ToolStripMenuItem("Correct the effect here");
      foreach (var m in Modifiers.All) {
        if (!m.InWeightedTable || m.Probability <= 0) continue;     // only ones a roll can give
        string id = m.Id;
        effect.DropDownItems.Add(new ToolStripMenuItem(m.Text, null, (s, e) => {
          var sn = _board.Snap;
          _tracker.CorrectEffect(sn.Seed, sn.CurrentBiome, id);
          Refresh_();
        }) { Tag = id });
      }
      effect.DropDownItems.Add(new ToolStripSeparator());
      effect.DropDownItems.Add(new ToolStripMenuItem("There is no effect here", null, (s, e) => {
        var sn = _board.Snap;
        _tracker.CorrectEffect(sn.Seed, sn.CurrentBiome, "NONE");
        Refresh_();
      }) { Tag = "NONE" });
      effect.DropDownItems.Add(new ToolStripMenuItem("Use the prediction", null, (s, e) => {
        var sn = _board.Snap;
        _tracker.CorrectEffect(sn.Seed, sn.CurrentBiome, null);
        Refresh_();
      }));
      menu.Opening += (s, e) => {
        var sn = _board.Snap;
        bool ok = sn.GameRunning && sn.Seed.Length > 0 && sn.CurrentBiome.Length > 0;
        effect.Enabled = ok;
        effect.Text = ok ? "Correct the effect here (" + Model.PlaceName(sn.CurrentPlace) + ")"
                         : "Correct the effect here (needs a run in progress)";
        // Tick whatever is currently in force, so it is clear what you would be changing.
        string current = ok ? (_tracker.Correction(sn.Seed, sn.CurrentBiome)
                               ?? (_tracker.Predicted(sn.CurrentBiome) != null ? _tracker.Predicted(sn.CurrentBiome).Id : "NONE"))
                            : null;
        foreach (ToolStripItem it in effect.DropDownItems) {
          var mi = it as ToolStripMenuItem;
          if (mi != null && mi.Tag != null) mi.Checked = (string)mi.Tag == current;
        }
      };
      menu.Items.Add(effect);

      menu.Items.Add(new ToolStripSeparator());
      var extras = new ToolStripMenuItem("Add other items");
      extras.ToolTipText = "Extra checkbox lists. Nothing detects these, you tick them yourself.";
      extras.Checked = _cfg.ShowExtras;
      extras.Click += (s, e) => {
        _cfg.ShowExtras = !_cfg.ShowExtras;
        extras.Checked = _board.ShowExtras = _cfg.ShowExtras;
        _board.Invalidate();
      };
      menu.Items.Add(extras);
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(new ToolStripMenuItem("Clear pinned", null, (s, e) => {
        _board.Pinned.Clear(); SavePins(); _board.Invalidate();
      }));
      menu.Items.Add(new ToolStripMenuItem("Reset position", null, (s, e) => {
        var wa = Screen.PrimaryScreen.WorkingArea;
        Location = new Point(wa.Right - Width - 20, wa.Top + 40);
        _cfg.X = Location.X; _cfg.Y = Location.Y;
      }));
      menu.Items.Add(new ToolStripSeparator());
      menu.Items.Add(new ToolStripMenuItem("Close overlay", null, (s, e) => Close()));
      menu.Opening += (s, e) => SyncMenu(menu);

      ContextMenuStrip = menu;
      _board.ContextMenuStrip = menu;
      SyncMenu(menu);
    }

    /// <summary>Tick whichever preset matches the current setting.</summary>
    void SyncMenu(ContextMenuStrip menu) {
      foreach (ToolStripItem top in menu.Items) {
        var parent = top as ToolStripMenuItem;
        if (parent == null) continue;
        foreach (ToolStripItem child in parent.DropDownItems) {
          var mi = child as ToolStripMenuItem;
          if (mi == null || mi.Tag == null) continue;
          // Only the preset items use "name:value" tags. Other menus tag their items too
          // (the effect list uses modifier ids), so leave anything that is not ours alone.
          var parts = mi.Tag.ToString().Split(':');
          int v;
          if (parts.Length != 2 || !int.TryParse(parts[1], out v)) continue;
          if (parts[0] == "idle")  mi.Checked = Math.Abs(_cfg.IdleOpacity  * 100 - v) < 0.5;
          if (parts[0] == "hover") mi.Checked = Math.Abs(_cfg.HoverOpacity * 100 - v) < 0.5;
          if (parts[0] == "dwell") mi.Checked = _cfg.DwellMs == v;
        }
      }
    }

    /// <summary>
    /// Start Noita. Prefer Steam's protocol handler so the game launches exactly as it would
    /// from the library (Steam overlay, cloud saves, achievements all wired up); fall back to
    /// running noita.exe directly if Steam is not available.
    /// </summary>
    void LaunchNoita() {
      const string AppId = "881100";
      try {
        System.Diagnostics.Process.Start("steam://rungameid/" + AppId);
        return;
      } catch { }
      foreach (var exe in FindNoitaExe()) {
        try {
          System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
            FileName = exe, WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true
          });
          return;
        } catch { }
      }
      MessageBox.Show("Could not start Noita. Launch it from Steam and the overlay will pick it up.",
                      "Noita Overlay", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    /// <summary>Likely noita.exe locations, across common Steam library drives.</summary>
    static IEnumerable<string> FindNoitaExe() {
      const string tail = @"steamapps\common\Noita\noita.exe";
      var roots = new List<string>();
      try {
        var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        if (k != null) {
          var sp = k.GetValue("SteamPath") as string;
          if (!string.IsNullOrEmpty(sp)) roots.Add(sp.Replace('/', '\\'));
        }
      } catch { }
      roots.Add(@"C:\Program Files (x86)\Steam");
      roots.Add(@"C:\Program Files\Steam");
      foreach (var d in new[] { "D", "E", "F", "G" }) roots.Add(d + @":\SteamLibrary");
      foreach (var r in roots) {
        string p1 = Path.Combine(r, tail);
        if (File.Exists(p1)) yield return p1;
      }
    }

    void LoadPins() {
      try {
        if (File.Exists(_pinPath))
          foreach (var l in File.ReadAllLines(_pinPath)) {
            var t = l.Trim();
            if (t.Length > 0) _board.Pinned.Add(t);
          }
      } catch { }
    }

    void SavePins() {
      try { File.WriteAllLines(_pinPath, _board.Pinned.ToArray()); } catch { }
    }

    static readonly string _pinPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"NoitaOverlay\pinned.txt");

    public OverlayForm() {
      Text = "Noita Progress";
      FormBorderStyle = FormBorderStyle.None;
      StartPosition = FormStartPosition.Manual;
      AutoScaleMode = AutoScaleMode.Dpi;
      BackColor = Palette.Bg;
      TopMost = true;
      ShowInTaskbar = false;
      Opacity = IdleOpacity;
      KeyPreview = true;

      _cfg.Load();
      Opacity = _cfg.IdleOpacity;

      float sc;
      using (var g = CreateGraphics()) sc = g.DpiX / 96f;
      int w = _cfg.Width  > 100 ? _cfg.Width  : (int)(360 * sc);
      int h = _cfg.Height > 100 ? _cfg.Height : (int)(470 * sc);
      int barH = (int)(28 * sc);
      Size = new Size(w, h);
      var wa = Screen.PrimaryScreen.WorkingArea;
      var loc = new Point(wa.Right - w - 20, wa.Top + 40);
      if (_cfg.X != int.MinValue && _cfg.Y != int.MinValue) {
        var saved = new Point(_cfg.X, _cfg.Y);
        // only restore if it still lands on a screen that exists
        foreach (var scr in Screen.AllScreens)
          if (scr.WorkingArea.Contains(new Rectangle(saved, new Size(40, 40)))) { loc = saved; break; }
      }
      Location = loc;

      var bar = new ClickThrough { Dock = DockStyle.Top, Height = barH, BackColor = Palette.BgAlt };
      _bar = bar;
      var close  = MakeBtn("X", (int)(28 * sc), (s, e) => Close());  _closeBtn = close;
      _board.HideDone = true;                       // default: only show what is left
      var toggle = MakeBtn("show all", (int)(66 * sc), null);  _toggleBtn = toggle;
      toggle.Click += (s, e) => {
        _board.HideDone = !_board.HideDone;
        _cfg.HideDone = _board.HideDone;
        toggle.Text = _board.HideDone ? "show all" : "hide done";
        _board.Invalidate();
      };
      // The bar is the status line: dot, where you are, seed. No separate title row.
      _launchBtn = MakeBtn("Launch Noita", (int)(88 * sc), (s, e) => LaunchNoita());
      _launchBtn.Visible = false;
      _launchBtn.ForeColor = Palette.Text;
      _launchBtn.Dock = DockStyle.Right;

      var fBar  = new Font("Segoe UI", 9f, FontStyle.Bold);
      var fSeed = new Font("Segoe UI", 8.25f, FontStyle.Regular);
      bar.Paint += (s, e) => {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        int right = bar.ClientSize.Width;
        foreach (Control c in bar.Controls) if (c.Visible && c.Dock == DockStyle.Right) right -= c.Width;
        int d = (int)(7 * sc), pad = (int)(9 * sc);
        using (var b = new SolidBrush(_live ? Palette.Live : Palette.Dimmer))
          g.FillEllipse(b, pad, (bar.Height - d) / 2, d, d);

        var tx = pad + (int)(14 * sc);
        float lineH = g.MeasureString("Xg", fBar).Height;     // real height, not the 96-dpi one
        float statusW = g.MeasureString(_status, fBar).Width;

        // Status gets the space first. The seed only appears if it genuinely fits.
        if (_seed.Length > 0) {
          var sz = g.MeasureString(_seed, fSeed);
          if (right - tx - statusW > sz.Width + (int)(12 * sc)) {
            using (var b = new SolidBrush(Palette.Dimmer))
              g.DrawString(_seed, fSeed, b, right - sz.Width - (int)(6 * sc),
                           (bar.Height - g.MeasureString("Xg", fSeed).Height) / 2);
            right -= (int)sz.Width + (int)(10 * sc);
          }
        }
        using (var b = new SolidBrush(_live ? Palette.Text : Palette.Dim))
          g.DrawString(_status, fBar, b,
            new RectangleF(tx, (bar.Height - lineH) / 2, Math.Max(10, right - tx), lineH * 1.2f),
            new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter });
      };
      // Clicking the bar forces an immediate re-check rather than waiting for the tick.
      bar.MouseClick += (s, e) => { if (!_dragging) Refresh_(); };
      _lockBtn = MakeBtn("lock", (int)(48 * sc), null);
      _lockBtn.Click += (s, e) => SetLocked(!_locked);
      close.Dock = DockStyle.Right; toggle.Dock = DockStyle.Right; _lockBtn.Dock = DockStyle.Right;
      bar.Controls.Add(_launchBtn); bar.Controls.Add(_lockBtn);
      bar.Controls.Add(toggle); bar.Controls.Add(close);

      foreach (Control c in new Control[] { bar }) {
        c.MouseDown += (s, e) => { _dragging = true; _drag = e.Location; };
        c.MouseUp   += (s, e) => _dragging = false;
        c.MouseMove += (s, e) => {
          if (_dragging) Location = new Point(Location.X + e.X - _drag.X, Location.Y + e.Y - _drag.Y);
        };
      }

      _board.Dock = DockStyle.Fill;
      Controls.Add(_board);
      Controls.Add(bar);

      KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

      _expandedH = h;
      _barH = barH;
      _board.Compact = true;
      _board.HideDone = _cfg.HideDone;
      _board.ShowExtras = _cfg.ShowExtras;
      SetLocked(_cfg.Locked);
      toggle.Text = _board.HideDone ? "show all" : "hide done";
      _board.PinsChanged = SavePins;
      
      _board.ManualToggled = id => { _tracker.ToggleManual(id); Refresh_(); };
      LoadPins();
      BuildMenu();
      // Track manual resizes so the expanded height is whatever the user last chose.
      ResizeEnd += (s, e) => {
        if (!_board.Compact) { _expandedH = Height; _cfg.Height = Height; }
        _cfg.Width = Width;
      };
      Move += (s, e) => { if (!_dragging) return; _cfg.X = Location.X; _cfg.Y = Location.Y; };

      _timer.Interval = 1500;
      _timer.Tick += (s, e) => Refresh_();
      _timer.Start();

      _fade.Interval = 40;
      _fade.Tick += (s, e) => FadeStep();
      _fade.Start();

      Refresh_();
    }

    Button MakeBtn(string text, int width, EventHandler onClick) {
      var b = new Button {
        Text = text, Width = width, FlatStyle = FlatStyle.Flat,
        ForeColor = Palette.Dim, BackColor = Palette.BgAlt,
        Font = new Font("Segoe UI", 8f), TabStop = false
      };
      b.FlatAppearance.BorderSize = 0;
      if (onClick != null) b.Click += onClick;
      return b;
    }

    void Refresh_() {
      EnsureTopMost();          // no-op unless the style has actually been lost
      var s = _tracker.Poll();
      _board.Snap = s;
      _board.Next = Model.Recommend(s);
      _board.Side = Model.Explore(s, _board.Next == null ? null : _board.Next.GoalId);

      if (_tracker.Error != null)          _status = _tracker.Error;
      else if (!s.GameRunning)             _status = "Noita is not running";
      else if (!s.InRun)                   _status = "Noita is open, no run yet";
      else if (s.CurrentPlace.Length == 0) _status = "In a run";
      else if (s.Moving)                   _status = "In " + Model.PlaceName(s.CurrentPlace);
      else                                 _status = "Paused in " + Model.PlaceName(s.CurrentPlace);

      _live = s.GameRunning;
      _seed = (s.Seed.Length > 0 && s.InRun) ? "seed " + s.Seed : "";
      // Nothing but the lock button is operable while locked, so do not resurrect this one.
      bool wantLaunch = !s.GameRunning && !_locked;
      if (_launchBtn != null && _launchBtn.Visible != wantLaunch) _launchBtn.Visible = wantLaunch;
      if (_bar != null) _bar.Invalidate();

      if (s.NewlyEntered != null) {
        _board.Toast = "First time: " + Model.PlaceName(s.NewlyEntered);
        _board.ToastUntil = DateTime.UtcNow.AddSeconds(12);
      }
      _board.Invalidate();
    }

    [STAThread]
    static void Main() {
      // If anything ever goes wrong, leave a stack trace behind instead of vanishing silently.
      Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
      Application.ThreadException += (s, e) => LogCrash(e.Exception);
      AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash(e.ExceptionObject as Exception);

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);
      Application.Run(new OverlayForm());
    }

    static void LogCrash(Exception ex) {
      try {
        var path = Path.Combine(Settings.Dir, "crash.log");
        File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                                 (ex == null ? "(no exception object)" : ex.ToString()) +
                                 Environment.NewLine + Environment.NewLine);
      } catch { }
    }
  }
}
