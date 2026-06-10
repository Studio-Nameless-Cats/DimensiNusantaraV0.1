using UnityEngine;

namespace Nusantara.UI
{
    /// <summary>
    /// Single source of truth for the game's UI color palette (locked P4 warm scheme —
    /// see ART_DESIGN_PLAN.md). Use these instead of hardcoding hex anywhere in code.
    /// The editor swatch window (Tools > Nusantara > Palette) reads <see cref="Swatches"/>.
    /// </summary>
    public static class NusantaraPalette
    {
        // Core UI
        public static readonly Color Field        = Hex("F4B400"); // Kuning Nusantara — dominant field
        public static readonly Color FieldDeep     = Hex("C8930A"); // Emas Tua — drop-shadow blocks
        public static readonly Color Panel         = Hex("16110C"); // Hitam Arang — content panels
        public static readonly Color PanelRaised    = Hex("221A12"); // Arang Terang — cells / chips
        public static readonly Color Active         = Hex("E23A1E"); // Bara Merah — selected / HP / danger
        public static readonly Color Secondary      = Hex("F57A1F"); // Jingga — EXP / special / accents
        public static readonly Color TextLight       = Hex("EFE6D2"); // Gading — text on dark
        public static readonly Color TextMuted        = Hex("9C8A66"); // Pasir — sub-labels
        public static readonly Color TextOnYellow      = Hex("1A1410"); // Hitam — text on the field

        // Resource bars
        public static readonly Color Hp      = Hex("E23A1E");
        public static readonly Color Mp      = Hex("F4B400");
        public static readonly Color Special = Hex("F57A1F");
        public static readonly Color Exp     = Hex("F57A1F");

        // Elemental tags
        public static readonly Color ElemApi    = Hex("E23A1E");
        public static readonly Color ElemAir    = Hex("378ADD");
        public static readonly Color ElemTanah  = Hex("639922");
        public static readonly Color ElemAngin  = Hex("5DCAA5");
        public static readonly Color ElemMistik = Hex("F4B400");

        // Cool side + state colors.
        // The warm palette never had a cool anchor, so it had no easy way to say "calm",
        // "info" or "magic" without stealing a brand color. Nila is batik's indigo dye —
        // the cool half of the classic soga (brown/gold) + nila (indigo) pairing — so it's
        // both the color we were missing AND culturally the right one. The rest are clean
        // state colors kept separate from the brand reds/yellows on purpose.
        public static readonly Color Nila       = Hex("2B356E"); // indigo — cool anchor, magic
        public static readonly Color NilaTerang = Hex("5C6BC0"); // lighter indigo — info / links
        public static readonly Color Pirus      = Hex("1F9E86"); // teal — calm / water
        public static readonly Color Pandan     = Hex("3FA34D"); // green — success only
        public static readonly Color DangerDeep = Hex("A32D2D"); // deeper red, split out from Active

        // Semantic roles — what a color MEANS, not what it looks like.
        // UI code should reach for these instead of the raw colors above. That keeps
        // "selected" (Accent) and "about to die" (Danger) as separate ideas even though
        // both are reddish, and lets us remap any role in ONE place without chasing down
        // call sites. Region themes (see ART_DESIGN_PLAN.md) will later override these.
        public static class Role
        {
            public static readonly Color Accent        = Active;      // the thing that's selected / focused
            public static readonly Color Danger        = DangerDeep;  // HP low, death, real warnings
            public static readonly Color Magic         = Nila;        // SP, mystic, spirit stuff
            public static readonly Color Info          = NilaTerang;  // tutorials, tips, links
            public static readonly Color Success       = Pandan;      // heals, rewards, "done"
            public static readonly Color Warning       = Secondary;   // caution — orange, not red
            public static readonly Color Surface       = Panel;       // panel background
            public static readonly Color SurfaceRaised = PanelRaised; // cells, chips
            public static readonly Color FieldBg       = Field;       // the big yellow field
            public static readonly Color OnDark        = TextLight;   // text on panels
            public static readonly Color OnField       = TextOnYellow;// text on the field
            public static readonly Color Muted         = TextMuted;   // sub-labels
        }

        public struct Swatch
        {
            public readonly string Group;
            public readonly string Name;
            public readonly string HexCode;
            public readonly Color Color;
            public Swatch(string group, string name, string hex, Color color)
            {
                Group = group; Name = name; HexCode = hex; Color = color;
            }
        }

        /// <summary>Ordered list used by the editor swatch window.</summary>
        public static readonly Swatch[] Swatches =
        {
            new Swatch("Core", "Kuning (field)",   "F4B400", Field),
            new Swatch("Core", "Emas Tua (shadow)", "C8930A", FieldDeep),
            new Swatch("Core", "Hitam Arang (panel)", "16110C", Panel),
            new Swatch("Core", "Arang Terang (cell)", "221A12", PanelRaised),
            new Swatch("Core", "Bara Merah (active)", "E23A1E", Active),
            new Swatch("Core", "Jingga (accent)",   "F57A1F", Secondary),
            new Swatch("Core", "Gading (text)",     "EFE6D2", TextLight),
            new Swatch("Core", "Pasir (muted)",     "9C8A66", TextMuted),
            new Swatch("Core", "Hitam (on yellow)", "1A1410", TextOnYellow),

            new Swatch("Element", "Api",    "E23A1E", ElemApi),
            new Swatch("Element", "Air",    "378ADD", ElemAir),
            new Swatch("Element", "Tanah",  "639922", ElemTanah),
            new Swatch("Element", "Angin",  "5DCAA5", ElemAngin),
            new Swatch("Element", "Mistik", "F4B400", ElemMistik),

            new Swatch("Cool / State", "Nila (indigo)",   "2B356E", Nila),
            new Swatch("Cool / State", "Nila Terang",     "5C6BC0", NilaTerang),
            new Swatch("Cool / State", "Pirus (teal)",    "1F9E86", Pirus),
            new Swatch("Cool / State", "Pandan (green)",  "3FA34D", Pandan),
            new Swatch("Cool / State", "Danger (deep)",   "A32D2D", DangerDeep),

            // Apply by intent, not by look. These mirror the raw colors above.
            // Heads-up: we point these at the RAW fields (Active, DangerDeep, ...) on purpose,
            // NOT at Role.Accent etc. Role's values are just aliases of these same raw colors,
            // but referencing Role.* here creates a static-init order cycle (Swatches needs
            // Role, Role needs the outer class) that can snapshot the Role colors while they're
            // still default transparent. The window then drew empty swatches. Using the raw
            // fields, which are guaranteed set before Swatches runs, sidesteps the whole mess.
            new Swatch("Role", "Accent (selected)", "E23A1E", Active),
            new Swatch("Role", "Danger (HP/death)", "A32D2D", DangerDeep),
            new Swatch("Role", "Magic (SP/mystic)", "2B356E", Nila),
            new Swatch("Role", "Info (tips/links)",  "5C6BC0", NilaTerang),
            new Swatch("Role", "Success (reward)",   "3FA34D", Pandan),
            new Swatch("Role", "Warning (caution)",  "F57A1F", Secondary),

            // The surface + text roles. These reuse the Core colors, but listing them here
            // means the window shows the WHOLE role contract, not just the loud intent ones.
            new Swatch("Role", "Surface (panel bg)",  "16110C", Panel),
            new Swatch("Role", "Surface Raised (cell)", "221A12", PanelRaised),
            new Swatch("Role", "Field Bg (yellow)",   "F4B400", Field),
            new Swatch("Role", "On Dark (text)",      "EFE6D2", TextLight),
            new Swatch("Role", "On Field (text)",     "1A1410", TextOnYellow),
            new Swatch("Role", "Muted (sub-label)",   "9C8A66", TextMuted),
        };

        /// <summary>Parse "RRGGBB" or "RRGGBBAA" into a Color (opaque if no alpha).</summary>
        public static Color Hex(string h)
        {
            return ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        }
    }
}
