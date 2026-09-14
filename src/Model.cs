using System;
using System.Collections.Generic;

namespace NoitaOverlay {

  /// <summary>
  /// How a goal decides it is complete. Manual exists because plenty of Noita has no
  /// persistent flag behind it: the whole sun chain writes nothing until its final step,
  /// so a player three steps in would otherwise see no sign of it at all.
  /// </summary>
  internal enum Source { Place, Flag, OrbCount, Manual }

  /// <summary>
  /// Something you can actually accomplish at a place, as opposed to merely standing in it.
  /// Every key here is a flag the game itself writes via AddFlagPersistent.
  /// </summary>
  internal sealed class Deed {
    public string Title, Hint, Key;
    public Source Source;
    public Deed(string title, string hint, string key, Source src = Source.Flag) {
      Title = title; Hint = hint; Key = key; Source = src;
    }
  }

  internal sealed class Goal {
    public string Id, Title, Hint, Key;
    public string Veil;                 // shown instead of Title until you have been there
    public int Tier;
    public Source Source;
    public int N;                       // for OrbCount
    public Deed[] Deeds = new Deed[0];
    public Goal(int tier, string id, string title, string hint, Source src, string key, int n = 0) {
      Tier = tier; Id = id; Title = title; Hint = hint; Source = src; Key = key; N = n;
    }
    /// <summary>Where it is. Only revealed once you have been, as a reminder rather than a spoiler.</summary>
    public string Where;

    public Goal With(params Deed[] deeds) { Deeds = deeds; return this; }
    /// <summary>Hide the place's real name until it has been reached.</summary>
    public Goal Veiled(string veil) { Veil = veil; return this; }
    /// <summary>Set the location reminder shown after your first visit.</summary>
    public Goal At(string where) { Where = where; return this; }
    public string DisplayTitle(bool reached) {
      return (!reached && Veil != null) ? Veil : Title;
    }
  }

  internal sealed class TierDef {
    public int N; public string Name, Blurb; public int R, G, B;
    public TierDef(int n, string name, string blurb, int r, int g, int b) {
      N = n; Name = name; Blurb = blurb; R = r; G = g; B = b;
    }
  }

  /// <summary>One thing worth doing next -- either a whole goal, or a single deed within one.</summary>
  internal sealed class Suggestion {
    public string GoalId, Title, Hint, Reason;
    public int Tier;
    /// <summary>True while the main descent is unfinished: that IS the task, everything else can wait.</summary>
    public bool Urgent;
  }

  internal static class Model {

    /// <summary>Has the player finished the main line (down, boss, ending)?</summary>
    public static bool BeatenTheGame(Snapshot s) {
      foreach (var g in Goals) if (g.Tier == 1 && !Tracker.Done(g, s)) return false;
      return true;
    }

    /// <summary>
    /// Tiers reveal themselves as you earn them. A first-time player should be looking at one
    /// instruction, not at a catalogue of places they have no idea how to reach -- and naming
    /// the Meat Realm to someone who has not yet cleared the Mines spoils it for no gain.
    ///   1 Foundations  always
    ///   2 Detours      once you are actually descending (reached the Coal Pits)
    ///   3-5            once you have finished a run
    /// </summary>
    public static bool TierUnlocked(int tier, Snapshot s) {
      if (tier <= 1) return true;
      if (tier == 2) return s.Places.Contains("excavationsite") || BeatenTheGame(s);
      return BeatenTheGame(s);
    }

    /// <summary>Leads are prompts, not tasks, so they never become the recommendation.</summary>
    static bool IsLead(Goal g) { return g.Tier == 6 || g.Source == Source.Manual; }

    /// <summary>
    /// Picks the one thing to put in front of the player. Preference order:
    ///   0. while the main descent is unfinished, that is the whole answer -- do not
    ///      bury a new player under side content they have no context for yet
    ///   1. an unfinished deed where you are standing right now
    ///   2. an unfinished goal for the place you are standing in
    ///   3. an unfinished deed at a place you have already been (you know the way)
    ///   4. otherwise the shallowest unfinished tier, in authored order
    /// </summary>
    public static Suggestion Recommend(Snapshot s) {
      // 0 -- the main line. One instruction, no menu.
      foreach (var g in Goals) {
        if (g.Tier != 1 || Tracker.Done(g, s)) continue;
        bool r = Tracker.Reached(g, s);
        return new Suggestion {
          GoalId = g.Id, Tier = 1, Urgent = true,
          Title  = "Head down",
          Hint   = g.DisplayTitle(r) + ". " + g.Hint,
          Reason = "the way on"
        };
      }

      // 1 & 2 -- something right under your feet.
      if (!string.IsNullOrEmpty(s.CurrentPlace)) {
        foreach (var g in Goals) {
          if (g.Source != Source.Place || g.Key != s.CurrentPlace) continue;
          foreach (var d in g.Deeds)
            if (!Tracker.DeedDone(d, s))
              return new Suggestion { GoalId = g.Id, Tier = g.Tier, Title = g.Title + " - " + d.Title,
                                      Hint = d.Hint, Reason = "you are here now" };
          if (!Tracker.Reached(g, s))
            return new Suggestion { GoalId = g.Id, Tier = g.Tier, Title = g.DisplayTitle(false),
                                    Hint = g.Hint, Reason = "you are here now" };
        }
      }

      // 3 -- unfinished business somewhere you already know how to reach.
      for (int tier = 1; tier <= 5; tier++) {
        if (!TierUnlocked(tier, s)) continue;
        foreach (var g in Goals) {
          if (g.Tier != tier || IsLead(g) || !Tracker.Reached(g, s)) continue;
          foreach (var d in g.Deeds)
            if (!Tracker.DeedDone(d, s))
              return new Suggestion { GoalId = g.Id, Tier = g.Tier, Title = g.Title + " - " + d.Title,
                                      // you have been, so remind where rather than re-tease what
                                      Hint = g.Where ?? d.Hint, Reason = "you have been here before" };
        }
      }

      // 4 -- the shallowest thing still outstanding.
      for (int tier = 1; tier <= 5; tier++) {
        if (!TierUnlocked(tier, s)) continue;
        foreach (var g in Goals) {
          if (g.Tier != tier || IsLead(g) || Tracker.Done(g, s)) continue;
          bool reached = Tracker.Reached(g, s);
          return new Suggestion { GoalId = g.Id, Tier = g.Tier, Title = g.DisplayTitle(reached),
                                  Hint = g.Hint, Reason = TierName(tier) };
        }
      }

      return new Suggestion { GoalId = "", Tier = 5, Title = "Everything on the board is done.",
                              Hint = "Genuinely. Go find something it does not track.", Reason = "" };
    }

    /// <summary>
    /// A standing invitation to wander, shown small and dim underneath the real task.
    /// Deliberately never urgent: it exists so a player knows the world is wider,
    /// not so they feel behind on it.
    /// </summary>
    public static Suggestion Explore(Snapshot s, string excludeGoalId) {
      // Somewhere you have never been, shallowest first, never the main line.
      for (int tier = 2; tier <= 4; tier++) {
        if (!TierUnlocked(tier, s)) continue;
        foreach (var g in Goals) {
          if (g.Tier != tier || g.Source != Source.Place) continue;
          if (g.Id == excludeGoalId || Tracker.Reached(g, s)) continue;
          return new Suggestion {
            GoalId = g.Id, Tier = g.Tier, Urgent = false,
            Title = g.DisplayTitle(false), Hint = g.Hint, Reason = "whenever you like"
          };
        }
      }
      return null;
    }

    static string TierName(int t) {
      foreach (var x in Tiers) if (x.N == t) return x.Blurb;
      return "";
    }

    public static readonly TierDef[] Tiers = {
      new TierDef(1, "Foundations",  "the run you already know",        0x6E, 0xC6, 0x7A),
      new TierDef(2, "Detours",      "just off the way down",           0x5B, 0xB8, 0xD4),
      new TierDef(3, "Off the Path", "the world is wider than the pit", 0xE0, 0xB0, 0x4C),
      new TierDef(4, "Far Reaches",  "few ever stand here",             0xE3, 0x7F, 0x3C),
      new TierDef(5, "Mastery",      "the game behind the game",        0xD0, 0x5A, 0x7A),
      new TierDef(6, "Leads",        "things to notice, tick them off yourself", 0x9A, 0x8C, 0xC4),
    };

    /// <summary>Collapses Noita's 128 internal biome ids onto the handful of places a player thinks in.</summary>
    public static string Canon(string biome) {
      if (string.IsNullOrEmpty(biome)) return "";
      if (biome.StartsWith("temple_altar") || biome.StartsWith("temple_wall")) return "holymountain";
      if (biome.StartsWith("orbrooms/"))      return "orbroom";
      // "solid_wall_tower" (no suffix) is generic structure spanning the whole map -- not a place.
      // Only the numbered levels are the actual Tower.
      if (biome == "tower/solid_wall_tower")  return "";
      if (biome.StartsWith("tower/"))         return "tower";
      if (biome.StartsWith("pyramid"))        return "pyramid";
      if (biome.StartsWith("rainforest"))     return "rainforest";
      if (biome.StartsWith("lavalake"))       return "lavalake";
      if (biome.StartsWith("lava"))           return "lava";
      if (biome.StartsWith("snowcastle"))     return "snowcastle";
      if (biome.StartsWith("snowcave"))       return "snowcave";
      if (biome.StartsWith("excavationsite")) return "excavationsite";
      if (biome.StartsWith("wizardcave"))     return "wizardcave";
      if (biome.StartsWith("boss_arena"))     return "boss_arena";
      if (biome.StartsWith("essenceroom"))    return "essenceroom";
      if (biome.StartsWith("mountain_") || biome == "hills" || biome == "hills2") return "surface";
      if (biome == "meatroom")                return "meat";
      if (biome == "roboroom" || biome == "robot_egg") return "robobase";
      if (biome == "lake_statue" || biome == "lake_deep") return "lake";
      if (biome.StartsWith("data/")) return "";
      // Structural / filler cells that are not somewhere a player would say they had "been".
      switch (biome) {
        case "solid_wall": case "empty": case "roadblock": case "scale": case "bridge":
        case "sky_light_injector": case "solid_wall_temple": case "water": case "end_wall":
          return "";
      }
      return biome;
    }

    /// <summary>Human-facing names, taken from the game's own translation strings where one exists.</summary>
    public static readonly Dictionary<string, string> PlaceNames = new Dictionary<string, string> {
      { "surface",      "the surface" },            { "coalmine",     "the Mines" },
      { "coalmine_alt", "the Collapsed Mines" },    { "excavationsite", "the Coal Pits" },
      { "fungicave",    "the Fungal Caverns" },     { "snowcave",     "the Snowy Depths" },
      { "snowcastle",   "Hiisi Base" },             { "rainforest",   "the Underground Jungle" },
      { "vault",        "the Vault" },              { "crypt",        "the Temple of the Art" },
      { "boss_arena",   "the Laboratory" },         { "boss_victoryroom", "the Work" },
      { "holymountain", "a Holy Mountain" },        { "wandcave",     "the magical temple" },
      { "liquidcave",   "the ancient laboratory" }, { "dragoncave",   "the Dragoncave" },
      { "orbroom",      "an Orb chamber" },         { "desert",       "the Desert" },
      { "pyramid",      "the Pyramid" },            { "winter",       "the snowy wasteland" },
      { "winter_caves", "the frozen caves" },       { "lake",         "the Lake" },
      { "fungiforest",  "the Fungal Forest" },      { "tower",        "the Tower" },
      { "clouds",       "the Cloudscape" },         { "the_sky",      "the sky" },
      { "meat",         "the Meat Realm" },         { "robobase",     "the Power Plant" },
      { "wizardcave",   "the Wizards Den" },        { "secret_lab",   "the abandoned alchemy lab" },
      { "the_end",      "the End" },                { "vault_frozen", "the Frozen Vault" },
      { "sandcave",     "the Sandcave" },           { "lavalake",     "the Lava Lake" },
      { "lava",         "the lava" },               { "gold",         "a vein of gold" },
      { "essenceroom",  "an Essence chamber" },     { "watercave",    "a water cave" },
      { "funroom",      "the stone mushroom" },
      { "ghost_secret", "a hidden chamber" },       { "mestari_secret", "a hidden chamber" },
      { "solid_wall_hidden_cavern", "a hidden cavern" },
    };

    public static string PlaceName(string canon) {
      string v;
      if (PlaceNames.TryGetValue(canon, out v)) return v;
      return canon.Replace('_', ' ');
    }

    /// <summary>
    /// The board. Hints name a destination or a shape, never a method or a payoff --
    /// enough to make you go look, not enough to spoil finding out.
    /// Deeds are things the game records you having DONE somewhere, so that walking
    /// through a biome no longer counts as having finished with it.
    /// </summary>
    public static readonly Goal[] Goals = {
      // ---- 1. Foundations -------------------------------------------------
      new Goal(1, "t1_down",   "Go down through the Holy Mountains", "Deeper each time. See how far they go.",             Source.Place, "snowcastle"),
      new Goal(1, "t1_deep",   "Reach the bottom of the world",      "The pit does end. Something is down there.",         Source.Place, "boss_arena"),
      new Goal(1, "t1_boss",   "Face what waits at the bottom",      "It has been waiting the whole time.",                Source.Flag,  "boss_centipede"),
      new Goal(1, "t1_ending", "Finish the run",                     "Take what you won where it needs to go.",            Source.Flag,  "progress_ending0"),

      // ---- 2. Detours -----------------------------------------------------
      new Goal(2, "t2_alt",     "The Collapsed Mines",          "The Mines are not solid all the way across.",      Source.Place, "coalmine_alt")
        .Veiled("A way through the rock beside the Mines").At("West side of the Mines, through the rock."),
      new Goal(2, "t2_fungi",   "The Fungal Caverns",           "Something grows in the dark beside the Coal Pits.", Source.Place, "fungicave")
        .Veiled("Somewhere things grow in the dark").At("Off the Coal Pits, to either side."),
      new Goal(2, "t2_wand",    "The magical temple",           "Not every temple is a Holy Mountain.",              Source.Place, "wandcave")
        .Veiled("A temple that is not a Holy Mountain").At("Scattered deep, off the main path."),
      new Goal(2, "t2_vault",   "The Vault",                    "There is power running through it.",                Source.Place, "vault")
        .Veiled("Somewhere power is kept").At("On the way down, below the Jungle."),
      new Goal(2, "t2_crypt",   "The Temple of the Art",        "Deeper than most runs ever go.",                    Source.Place, "crypt")
        .Veiled("A temple deeper than most runs reach").At("On the way down, below the Vault."),
      new Goal(2, "t2_jungle",  "The Underground Jungle",       "Green, loud, and far too alive.",                   Source.Place, "rainforest")
        .Veiled("Somewhere green, and far too alive").At("On the way down, below Hiisi Base."),
      new Goal(2, "t2_liquid",  "The ancient laboratory",       "Someone worked down here once.",                    Source.Place, "liquidcave")
        .Veiled("Somewhere someone used to work").At("West of the Mines, at Mines depth."),
      new Goal(2, "t2_dragon",  "The Dragoncave",               "Something makes its home off to one side.",                Source.Place, "dragoncave")
        .Veiled("A lair, with something in it").At("East, at Underground Jungle depth.")
        .With(new Deed("and kill what lives there", "Still alive, last anyone checked.", "miniboss_dragon")),
      new Goal(2, "t2_orb",     "Take an Orb from its chamber", "They sit sealed in rooms of their own.",            Source.Place, "orbroom")
        .Veiled("Open a sealed chamber").At("Sealed rooms, scattered east and west."),
      new Goal(2, "t2_frozen",  "The Frozen Vault",             "The Vault has a colder cousin, far to one side.",   Source.Place, "vault_frozen")
        .Veiled("Go far west, then down to Coal Pits depth").At("Far west, Mines to Coal Pits depth."),
      new Goal(2, "t2_sand",    "The Sandcave",                 "There is something under the desert.",         Source.Place, "sandcave")
        .Veiled("Go far east, then dig down under the sand").At("Under the eastern desert."),
      new Goal(2, "t2_lavalake","The Lava Lake",                "Heat, and a lot of it, off to one side.",           Source.Place, "lavalake")
        .Veiled("Just east of the Mines, a little deeper").At("East of the Mines, Coal Pits depth."),

      // ---- 3. Off the Path ------------------------------------------------
      new Goal(3, "t3_desert",  "The Desert",          "The world does not end at the edge of the Mines. Go east.", Source.Place, "desert")
        .Veiled("Explore east into the desert").At("East along the surface."),
      new Goal(3, "t3_winter",  "The snowy wasteland", "Go west instead, and keep going.",                          Source.Place, "winter")
        .Veiled("Explore west into the snow").At("West along the surface."),
      new Goal(3, "t3_pyramid", "The Pyramid",         "Something was built out in the sand long before you.",       Source.Place, "pyramid")
        .Veiled("Far east on the surface, past the desert").At("Far east on the surface, in the desert.")
        .With(new Deed("and beat what guards it", "It does not want you inside.", "miniboss_limbs")),
      new Goal(3, "t3_lake",    "The Lake",            "Open water, out to the west.",                 Source.Place, "lake")
        .Veiled("Far west on the surface, past the snow").At("Far west, past the snowy wasteland.")
        .With(new Deed("and face what stands in it", "Someone is out there in the water.", "miniboss_islandspirit")),
      new Goal(3, "t3_forest",  "The Fungal Forest",   "The caverns were not the whole of it.",                      Source.Place, "fungiforest")
        .Veiled("Very far east, then underground").At("Very far east, underground."),
      new Goal(3, "t3_tower",   "The Tower",           "Something out east was built, not dug.",                     Source.Place, "tower")
        .Veiled("Far east, then down below the sand").At("Far east, below the desert.")
        .With(new Deed("and learn what it keeps", "A tower is usually built around something.", "secret_tower")),
      new Goal(3, "t3_sky",     "Above the clouds",    "Down is not the only direction.",                            Source.Place, "the_sky")
        .Veiled("Straight up - the very top of the world").At("Straight up, above everything.")
        .With(new Deed("and meet what flies there", "You are not alone up there.", "miniboss_sky")),
      new Goal(3, "t3_clouds",  "The Cloudscape",      "There is ground up there too, of a sort.",                   Source.Place, "clouds")
        .Veiled("High above the surface").At("High above the surface."),
      new Goal(3, "t3_mushroom","The stone mushroom",  "Big enough to spot if you go looking.",                      Source.Place, "funroom")
        .Veiled("Something out east was grown, not dug").At("Far east, at Snowy Depths depth."),

      // ---- 4. Far Reaches -------------------------------------------------
      new Goal(4, "t4_meat",   "The Meat Realm",  "Somewhere the world stops being rock.",  Source.Place, "meat")
        .Veiled("Far east, deeper than Hiisi Base").At("Far east, deeper than Hiisi Base.")
        .With(new Deed("and kill what it is made of", "The place itself is alive.", "miniboss_meat")),
      new Goal(4, "t4_robo",   "The Power Plant", "Something down there is still running.", Source.Place, "robobase")
        .Veiled("Very deep - below the Jungle").At("Very deep, below the Jungle.")
        .With(new Deed("and shut down its keeper", "Something is maintaining it.", "miniboss_robot")),
      new Goal(4, "t4_wizard", "The Wizards Den", "You are not the first to come down here.", Source.Place, "wizardcave")
        .Veiled("Deep, and east of the way down").At("Deep, east of the way down.")
        .With(new Deed("and out-duel the master", "One of them is still practising.", "miniboss_wizard")),
      new Goal(4, "t4_lab",    "The abandoned alchemy lab", "Someone solved it before you, and left.", Source.Place, "secret_lab")
        .Veiled("Just west of the start, at Mines depth").At("West of the start, at Mines depth.")
        .With(new Deed("and settle with its owner", "They did not leave. Not entirely.", "miniboss_alchemist")),
      new Goal(4, "t4_end",    "The End",         "Past the bottom there is still further.", Source.Place, "the_end")
        .Veiled("Below the Laboratory").At("Below the Laboratory."),
      new Goal(4, "t4_orbs",   "Gather every Orb", "There are eleven. They are not all on the way down.", Source.OrbCount, "", 11),
      new Goal(4, "t4_ess",    "Gather the four essences", "Four rooms hold four of them, scattered wide.", Source.Flag, "secret_allessences")
        .With(new Deed("the fire essence",  "Held out in open water.",        "essence_fire"),
              new Deed("the air essence",   "Held high up.",                  "essence_air"),
              new Deed("the water essence", "Held somewhere very hot.",       "essence_water"),
              new Deed("the earth essence", "Held in a chamber near the top.","essence_laser")),
      new Goal(4, "t4_hut",    "Find the hut",   "Two of them, actually, at the far edges.", Source.Flag, "progress_hut_a"),

      // ---- 5. Mastery -----------------------------------------------------
      new Goal(5, "t5_ending1",  "A different ending",       "The ending you know is the first of several.", Source.Flag, "progress_ending1"),
      new Goal(5, "t5_ending2",  "And another",              "Keep going past that one.",                    Source.Flag, "progress_ending2"),
      new Goal(5, "t5_sun",      "Reach the sun",            "It is a place, not a light.",                  Source.Flag, "progress_sun"),
      new Goal(5, "t5_darksun",  "Reach the dark sun",       "And it has an opposite.",                      Source.Flag, "progress_darksun"),
      new Goal(5, "t5_sunkill",  "Put out a sun",            "Yes, really.",                                 Source.Flag, "progress_sunkill"),
      new Goal(5, "t5_amulet",   "Wear what was buried",     "The sand keeps more than one thing.",          Source.Flag, "secret_amulet"),
      new Goal(5, "t5_hourglass","Find the hourglass",       "Somewhere inside Hiisi Base.",  Source.Flag, "secret_hourglass"),
      new Goal(5, "t5_eye",      "Find the buried eye",      "Buried in the Snowy Depths.",                  Source.Flag, "secret_buried_eye"),
      new Goal(5, "t5_medit",    "Sit with the cube",        "Somewhere out in the sand.",     Source.Flag, "secret_meditation"),
      new Goal(5, "t5_tablet",   "Use an altar tablet",      "There are tablets on the altars.",Source.Flag, "misc_altar_tablet"),
      new Goal(5, "t5_nohit",    "Finish without being hit", "Not once.",                                    Source.Flag, "progress_nohit"),
      new Goal(5, "t5_pacifist", "Finish without killing",   "Nothing at all.",                              Source.Flag, "progress_pacifist"),
      new Goal(5, "t5_nogold",   "Finish without gold",      "Leave every coin where it lies.",              Source.Flag, "progress_nogold"),
      new Goal(5, "t5_minit",    "Finish fast",              "Much faster than you think is possible.",      Source.Flag, "progress_minit"),
      new Goal(5, "t5_ngplus",   "Begin again, changed",     "The run remembers.",                           Source.Flag, "progress_ngplus"),
      new Goal(5, "t5_nightmare","Survive Nightmare mode",   "The game offers this one openly.",             Source.Flag, "progress_nightmare"),

      // ---- 6. Leads -------------------------------------------------------
      // The game records none of these, so there is nothing to detect. They are
      // pointers at chains that start small and go a long way. Tick them yourself.
      new Goal(6, "l_mushroom","Standing at the mushroom does nothing",
        "Nothing to you as you are, anyway. Arrive differently.",       Source.Manual, "l_mushroom"),
      new Goal(6, "l_stones",  "Some rocks are not scenery",
        "A few have names. Carrying one somewhere may matter.",         Source.Manual, "l_stones"),
      new Goal(6, "l_seed",    "Something small can be planted",
        "It does nothing where you found it.",                          Source.Manual, "l_seed"),
      new Goal(6, "l_moon",    "The moon is not out of reach",
        "And it is not the only one.",                                  Source.Manual, "l_moon"),
      new Goal(6, "l_carry",   "Things can be carried further than seems sensible",
        "The long chains start with not leaving something behind.", Source.Manual, "l_carry"),
      new Goal(6, "l_tablets", "The tablets can be read",
        "There are more of them than you have found.",                  Source.Manual, "l_tablets"),
      new Goal(6, "l_music",   "Sound is used for more than atmosphere",
        "Some things are listening.",                                   Source.Manual, "l_music"),
      new Goal(6, "l_eyes",    "The symbols repeat",
        "The same marks turn up in unrelated places.", Source.Manual, "l_eyes"),
    };
  }
}
