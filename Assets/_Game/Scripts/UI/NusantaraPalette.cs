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
        };

        /// <summary>Parse "RRGGBB" or "RRGGBBAA" into a Color (opaque if no alpha).</summary>
        public static Color Hex(string h)
        {
            return ColorUtility.TryParseHtmlString("#" + h, out var c) ? c : Color.magenta;
        }
    }
}
