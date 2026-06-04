using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Nusantara.UI;

namespace Nusantara.UI.EditorTools
{
    /// <summary>
    /// Dockable swatch palette. Open via Tools > Nusantara > Palette.
    /// Select a UI element (Image / RawImage / Text / TextMeshProUGUI), click a swatch
    /// to apply its color — no hex typing, no alt-tabbing. Alt-click (or nothing selected)
    /// copies the hex to the clipboard instead. Colors come from NusantaraPalette (one
    /// source of truth), so this stays in sync with code automatically.
    /// </summary>
    public class NusantaraPaletteWindow : EditorWindow
    {
        Vector2 _scroll;
        bool _includeChildren;

        [MenuItem("Tools/Nusantara/Palette")]
        static void Open()
        {
            var w = GetWindow<NusantaraPaletteWindow>();
            w.titleContent = new GUIContent("Palette");
            w.minSize = new Vector2(240, 300);
            w.Show();
        }

        void OnSelectionChange() => Repaint();

        void OnGUI()
        {
            int targetCount = CountTargets(out string targetLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "Click a swatch to apply its color to the selected UI element(s).\n" +
                "Alt-click (or no selection) copies the hex instead.",
                MessageType.None);

            _includeChildren = EditorGUILayout.ToggleLeft("Include children", _includeChildren);

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(targetCount > 0
                    ? $"Target: {targetCount} × {targetLabel}"
                    : "Target: none — clicks copy hex", EditorStyles.miniLabel);
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            string group = null;
            foreach (var s in NusantaraPalette.Swatches)
            {
                if (s.Group != group)
                {
                    group = s.Group;
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField(group, EditorStyles.boldLabel);
                }
                DrawSwatch(s);
            }
            EditorGUILayout.EndScrollView();
        }

        void DrawSwatch(NusantaraPalette.Swatch s)
        {
            Rect r = GUILayoutUtility.GetRect(1, 26, GUILayout.ExpandWidth(true));

            Rect chip = new Rect(r.x + 2, r.y + 3, 34, r.height - 6);
            EditorGUI.DrawRect(chip, s.Color);
            // thin dark frame so light swatches stay visible
            Handles.color = new Color(0f, 0f, 0f, 0.35f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(chip.x, chip.y), new Vector3(chip.xMax, chip.y),
                new Vector3(chip.xMax, chip.yMax), new Vector3(chip.x, chip.yMax),
                new Vector3(chip.x, chip.y));

            Rect lab = new Rect(chip.xMax + 8, r.y, r.width - chip.width - 12, r.height);
            GUI.Label(lab, $"{s.Name}   #{s.HexCode}");

            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                HandleClick(s, Event.current.alt);
        }

        void HandleClick(NusantaraPalette.Swatch s, bool forceCopy)
        {
            var targets = CollectGraphics();
            if (forceCopy || targets.Count == 0)
            {
                EditorGUIUtility.systemCopyBuffer = "#" + s.HexCode;
                ShowNotification(new GUIContent($"Copied  #{s.HexCode}"));
                return;
            }

            Undo.RecordObjects(targets.ToArray(), "Apply Nusantara color");
            foreach (var g in targets)
            {
                g.color = s.Color;
                EditorUtility.SetDirty(g);
            }
            ShowNotification(new GUIContent($"Applied {s.Name} to {targets.Count}"));
        }

        List<Graphic> CollectGraphics()
        {
            var list = new List<Graphic>();
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                if (_includeChildren) list.AddRange(go.GetComponentsInChildren<Graphic>(true));
                else list.AddRange(go.GetComponents<Graphic>());
            }
            return list;
        }

        int CountTargets(out string label)
        {
            var g = CollectGraphics();
            label = g.Count > 0 ? g[0].GetType().Name : "";
            return g.Count;
        }
    }
}
