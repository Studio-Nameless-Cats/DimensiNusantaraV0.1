using UnityEditor;
using UnityEngine;
using Nusantara.UI;

namespace Nusantara.UI.EditorTools
{
    // A "see it all at once" preview of the role system. This doesn't touch your scene -
    // it just paints mock panels, buttons, text and bars using NusantaraPalette.Role.* so
    // you can eyeball whether the whole thing hangs together. Open via
    // Tools > Nusantara > Style Guide. Pairs with GUIDE_Color_Usage_Editor.md (the words)
    // and the Palette window (the click-to-apply swatches).
    public class NusantaraStyleGuideWindow : EditorWindow
    {
        Vector2 _scroll;

        [MenuItem("Tools/Nusantara/Style Guide")]
        static void Open()
        {
            var w = GetWindow<NusantaraStyleGuideWindow>();
            w.titleContent = new GUIContent("Style Guide");
            w.minSize = new Vector2(420, 480);
            w.Show();
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(6);

            EditorGUILayout.LabelField("Dimensi Nusantara — role preview", Title());
            EditorGUILayout.LabelField("Everything below is drawn from NusantaraPalette.Role.*", Sub());
            EditorGUILayout.Space(8);

            DrawPanels();
            DrawButtons();
            DrawText();
            DrawBars();
            DrawStates();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        // ── Sections ────────────────────────────────────────────────────────────

        void DrawPanels()
        {
            Section("Panels");

            // A Surface panel with a raised cell sitting inside it, plus some text.
            Rect outer = Block(96);
            Frame(outer, NusantaraPalette.Role.Surface);
            Label(new Rect(outer.x + 10, outer.y + 6, outer.width - 20, 18),
                  "Role.Surface — panel background", NusantaraPalette.Role.OnDark, FontStyle.Bold);

            Rect cell = new Rect(outer.x + 10, outer.y + 30, outer.width - 20, 26);
            Frame(cell, NusantaraPalette.Role.SurfaceRaised);
            Label(new Rect(cell.x + 8, cell.y + 4, cell.width - 16, 18),
                  "Role.SurfaceRaised — cell / chip / row", NusantaraPalette.Role.OnDark);

            Rect field = new Rect(outer.x + 10, outer.y + 62, outer.width - 20, 24);
            Frame(field, NusantaraPalette.Role.FieldBg);
            Label(new Rect(field.x + 8, field.y + 3, field.width - 16, 18),
                  "Role.FieldBg — text uses Role.OnField", NusantaraPalette.Role.OnField, FontStyle.Bold);
        }

        void DrawButtons()
        {
            Section("Buttons");

            Rect row = Block(40);
            float w = (row.width - 30) / 4f;

            Button(new Rect(row.x,                 row.y, w, row.height),
                   "Normal",  NusantaraPalette.Role.SurfaceRaised, NusantaraPalette.Role.OnDark);
            Button(new Rect(row.x + (w + 10),      row.y, w, row.height),
                   "Hover",   NusantaraPalette.Role.FieldBg,       NusantaraPalette.Role.OnField);
            Button(new Rect(row.x + 2 * (w + 10),  row.y, w, row.height),
                   "Active",  NusantaraPalette.Role.Accent,        NusantaraPalette.Role.OnDark);
            Button(new Rect(row.x + 3 * (w + 10),  row.y, w, row.height),
                   "Disabled", Fade(NusantaraPalette.Role.SurfaceRaised, 0.4f), NusantaraPalette.Role.Muted);

            Label(Block(16), "Normal -> SurfaceRaised, Hover -> FieldBg, Active/selected -> Accent",
                  NusantaraPalette.Role.Muted);
        }

        void DrawText()
        {
            Section("Text");

            // On-dark sample sits on a Surface strip.
            Rect dark = Block(54);
            Frame(dark, NusantaraPalette.Role.Surface);
            Label(new Rect(dark.x + 10, dark.y + 4,  dark.width - 20, 18),
                  "Role.OnDark — body text on panels", NusantaraPalette.Role.OnDark);
            Label(new Rect(dark.x + 10, dark.y + 22, dark.width - 20, 18),
                  "Role.Muted — sub-labels, 12 / 30 counters", NusantaraPalette.Role.Muted);
            Label(new Rect(dark.x + 10, dark.y + 38, dark.width - 20, 18),
                  "Role.Info — tips / links", NusantaraPalette.Role.Info);

            // On-field sample sits on a FieldBg strip.
            Rect field = Block(24);
            Frame(field, NusantaraPalette.Role.FieldBg);
            Label(new Rect(field.x + 10, field.y + 3, field.width - 20, 18),
                  "Role.OnField — text on the yellow field", NusantaraPalette.Role.OnField, FontStyle.Bold);
        }

        void DrawBars()
        {
            Section("Resource bars");

            // HP: smooth Success -> Warning -> Danger gradient, full width.
            Label(Block(15), "HP — gradient: Success -> Warning -> Danger", NusantaraPalette.Role.Muted);
            Rect hp = Block(18);
            Frame(hp, NusantaraPalette.Role.Surface);
            GradientBar(Inset(hp, 2),
                        NusantaraPalette.Role.Success,
                        NusantaraPalette.Role.Warning,
                        NusantaraPalette.Role.Danger);

            Bar("MP — Role.Magic", NusantaraPalette.Role.Magic, 0.7f);
            Bar("SP charging — Role.Warning", NusantaraPalette.Role.Warning, 0.5f);
            Bar("SP ready — Role.Accent", NusantaraPalette.Role.Accent, 1.0f);
            Bar("EXP — Role.Warning", NusantaraPalette.Role.Warning, 0.35f);
        }

        void DrawStates()
        {
            Section("States");

            DrawStateRow("Danger",  NusantaraPalette.Role.Danger,  "fatal / error / low HP");
            DrawStateRow("Warning", NusantaraPalette.Role.Warning, "careful, not fatal");
            DrawStateRow("Success", NusantaraPalette.Role.Success, "heal / reward / done");
            DrawStateRow("Info",    NusantaraPalette.Role.Info,    "tip / tutorial / link");
            DrawStateRow("Magic",   NusantaraPalette.Role.Magic,   "MP / SP / mystic");
        }

        void DrawStateRow(string name, Color c, string meaning)
        {
            Rect r = Block(24);
            Rect chip = new Rect(r.x, r.y, 90, r.height);
            Frame(chip, c);
            // Pick readable text on the chip (white for the dark roles, dark for the bright ones).
            Color on = Luminance(c) > 0.5f ? NusantaraPalette.Role.OnField : NusantaraPalette.Role.OnDark;
            Label(new Rect(chip.x + 6, chip.y + 3, chip.width - 10, 18), name, on, FontStyle.Bold);
            Label(new Rect(chip.xMax + 10, r.y + 3, r.width - chip.width - 14, 18), meaning, NusantaraPalette.Role.Muted);
        }

        // ── Little drawing helpers ──────────────────────────────────────────────

        void Bar(string caption, Color fill, float fraction)
        {
            Label(Block(15), caption, NusantaraPalette.Role.Muted);
            Rect track = Block(18);
            Frame(track, NusantaraPalette.Role.Surface);
            Rect inner = Inset(track, 2);
            EditorGUI.DrawRect(new Rect(inner.x, inner.y, inner.width * Mathf.Clamp01(fraction), inner.height), fill);
        }

        void Button(Rect r, string text, Color fill, Color textColor)
        {
            Frame(r, fill);
            Label(r, text, textColor, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        void GradientBar(Rect r, Color a, Color b, Color c)
        {
            int n = Mathf.Max(1, (int)r.width);
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                // First half a->b, second half b->c.
                Color col = t < 0.5f ? Color.Lerp(a, b, t * 2f) : Color.Lerp(b, c, (t - 0.5f) * 2f);
                EditorGUI.DrawRect(new Rect(r.x + i, r.y, 1, r.height), col);
            }
        }

        // A filled rect with a thin dark frame so light fills stay visible on the editor bg.
        void Frame(Rect r, Color fill)
        {
            EditorGUI.DrawRect(r, fill);
            Handles.color = new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(r.x, r.y), new Vector3(r.xMax, r.y),
                new Vector3(r.xMax, r.yMax), new Vector3(r.x, r.yMax), new Vector3(r.x, r.y));
        }

        void Label(Rect r, string text, Color color,
                   FontStyle style = FontStyle.Normal, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var s = new GUIStyle(EditorStyles.label)
            {
                fontStyle = style,
                alignment = anchor,
                richText  = false
            };
            s.normal.textColor = color;
            GUI.Label(r, text, s);
        }

        void Section(string title)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        // Reserve a full-width block of the given height and return its rect.
        Rect Block(float height) => GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));

        static Rect Inset(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width - by * 2, r.height - by * 2);
        static Color Fade(Color c, float a) => new Color(c.r, c.g, c.b, a);
        static float Luminance(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static GUIStyle Title()
        {
            var s = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            return s;
        }
        static GUIStyle Sub()
        {
            var s = new GUIStyle(EditorStyles.miniLabel);
            return s;
        }
    }
}
