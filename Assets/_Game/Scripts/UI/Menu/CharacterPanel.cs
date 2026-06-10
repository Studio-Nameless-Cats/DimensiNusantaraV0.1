using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Nusantara.UI.Motion;
using UnityEngine.InputSystem;

namespace Nusantara.UI
{
    /// <summary>
    /// Character tab. Left: a list of party members (click to inspect). Right: the
    /// selected member's portrait, stats, and skill loadout (normal + special).
    ///
    /// LOADOUT EDITING: if <see cref="skillsContainer"/> + <see cref="skillTogglePrefab"/>
    /// are wired, the NORMAL skills become interactive toggle rows — tap to add/remove
    /// from the member's equipped loadout (max <see cref="PartyMember.MaxEquippedSkills"/>).
    /// Special skills stay read-only (fixed per character). If those refs are left null
    /// the panel falls back to the original read-only text list in <see cref="skillsText"/>.
    ///
    /// Edits mutate the live PartyMember (which persists across scenes) and are written
    /// out the next time the player saves — no extra save call here.
    ///
    /// Refreshes automatically whenever the panel is shown (OnEnable).
    ///
    /// ── Unity setup ──────────────────────────────────────────────────────────
    ///   CharacterPanel (this component)
    ///     ├ MemberList (Vertical Layout Group)   → membersContainer
    ///     │    (MemberListButton prefab pooled here)
    ///     └ Detail
    ///          ├ Portrait (Image)                → portrait
    ///          ├ NameText (TMP)                  → nameText
    ///          ├ StatsText (TMP)                 → statsText
    ///          ├ SkillsText (TMP)                → skillsText      (special list / fallback)
    ///          ├ SkillsContainer (Layout, opt.)  → skillsContainer (loadout toggle rows)
    ///          │    (SkillToggleRow prefab pooled here)
    ///          ├ LoadoutCount (TMP, optional)    → loadoutCountText ("Skill: 3/4")
    ///          └ LoadoutHint (TMP, optional)     → loadoutHint
    /// </summary>
    public class CharacterPanel : MonoBehaviour
    {
        // Two ways to pick a character:
        //   1. Arrow switcher (new look) - wire prevButton/nextButton/switcherNameText.
        //      Click the arrows or press Q/E to cycle through the party, wraps around.
        //   2. Member list (legacy) - wire membersContainer + memberButtonPrefab.
        // Wire whichever the scene uses; the other half can stay null.

        [Header("Character switcher (arrows + Q/E)")]
        [Tooltip("Left arrow button - goes to the previous party member.")]
        [SerializeField] private Button prevButton;
        [Tooltip("Right arrow button - goes to the next party member.")]
        [SerializeField] private Button nextButton;
        [Tooltip("Big name label between the arrows (the 'BIMA' text).")]
        [SerializeField] private TextMeshProUGUI switcherNameText;
        [Tooltip("Optional motion profile - the name pulses when you switch. Null = no animation.")]
        [SerializeField] private MotionProfile switchMotion;

        [Header("Member list (legacy, leave null if using the switcher)")]
        [SerializeField] private Transform        membersContainer;
        [SerializeField] private MemberListButton memberButtonPrefab;

        [Header("Detail")]
        [SerializeField] private Image           portrait;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statsText;
        [Tooltip("Optional 'Lv 5' label. Leave null to fold the level into the stats text only.")]
        [SerializeField] private TextMeshProUGUI levelText;
        [Tooltip("Optional EXP bar (0..1 fill). Leave null to hide.")]
        [SerializeField] private Slider          expSlider;
        [Tooltip("Optional EXP value label, e.g. '120 / 290'. Leave null to hide.")]
        [SerializeField] private TextMeshProUGUI expText;
        [SerializeField] private TextMeshProUGUI skillsText;
        [SerializeField] private TextMeshProUGUI emptyText;   // "Belum ada anggota party"
        [Tooltip("Optional usage hint for the loadout editor. Leave null to hide.")]
        [SerializeField] private TextMeshProUGUI loadoutHint;

        [Header("Loadout editor (optional — wire to enable skill picking)")]
        [Tooltip("Parent (Layout Group) for the normal-skill toggle rows. Leave null for read-only text mode.")]
        [SerializeField] private Transform      skillsContainer;
        [SerializeField] private SkillToggleRow skillTogglePrefab;
        [Tooltip("Optional 'Skill: X/N' counter label.")]
        [SerializeField] private TextMeshProUGUI loadoutCountText;

        private readonly List<MemberListButton> _rows      = new List<MemberListButton>();
        private readonly List<SkillToggleRow>   _skillRows = new List<SkillToggleRow>();
        private List<PartyMember> _members = new List<PartyMember>();
        private int _selected = -1;

        /// <summary>True when the interactive loadout editor is wired up.</summary>
        private bool LoadoutInteractive => skillsContainer != null && skillTogglePrefab != null;

        void Awake()
        {
            // Arrow clicks just cycle one step in either direction.
            if (prevButton != null) prevButton.onClick.AddListener(() => Cycle(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Cycle(+1));
        }

        void OnEnable() => Refresh();

        void Update()
        {
            // Keyboard shortcuts for the switcher: Q = previous, E = next.
            // Project runs on the new Input System, so we poll Keyboard.current.
            var kb = Keyboard.current;
            if (kb == null || _members.Count == 0) return;
            if (kb.qKey.wasPressedThisFrame) Cycle(-1);
            if (kb.eKey.wasPressedThisFrame) Cycle(+1);
        }

        // Step through the party, wrapping at both ends.
        private void Cycle(int dir)
        {
            if (_members.Count == 0) return;
            int next = (_selected + dir + _members.Count) % _members.Count;
            if (next == _selected) return; // solo party, nothing to switch to

            Select(next);

            // Little pulse on the name so the switch feels snappy. Optional.
            if (switchMotion != null && switcherNameText != null)
                switcherNameText.rectTransform.Pulse(switchMotion);
        }

        public void Refresh()
        {
            var party = Object.FindFirstObjectByType<PartySystem>();
            _members = party != null ? new List<PartyMember>(party.Members) : new List<PartyMember>();

            bool any = _members.Count > 0;
            if (emptyText != null) emptyText.gameObject.SetActive(!any);
            if (loadoutHint != null)
            {
                loadoutHint.gameObject.SetActive(any);
                loadoutHint.text = LoadoutInteractive
                    ? $"Pilih hingga {PartyMember.MaxEquippedSkills} skill untuk dibawa ke pertarungan."
                    : "Pengaturan skill (loadout) akan hadir di update berikutnya.";
            }

            BuildMemberButtons();

            // Arrows only make sense with 2+ members; gray them out otherwise.
            bool canCycle = _members.Count > 1;
            if (prevButton != null) prevButton.interactable = canCycle;
            if (nextButton != null) nextButton.interactable = canCycle;

            if (any) Select(Mathf.Clamp(_selected, 0, _members.Count - 1));
            else     ClearDetail();
        }

        private void BuildMemberButtons()
        {
            if (memberButtonPrefab == null || membersContainer == null) return;

            while (_rows.Count < _members.Count)
                _rows.Add(Instantiate(memberButtonPrefab, membersContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < _members.Count)
                {
                    int index = i;
                    _rows[i].gameObject.SetActive(true);
                    _rows[i].Bind(_members[i], () => Select(index));
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }

        private void Select(int index)
        {
            _selected = index;
            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i].gameObject.activeSelf) _rows[i].SetSelected(i == index);

            ShowDetail(_members[index]);
        }

        private void ShowDetail(PartyMember m)
        {
            if (portrait != null)
            {
                var icon = m.Base != null ? m.Base.Icon : null;
                portrait.sprite  = icon;
                portrait.enabled = icon != null;
            }

            if (nameText != null) nameText.text = m.Name;
            if (switcherNameText != null) switcherNameText.text = m.Name;

            if (levelText != null) levelText.text = $"Lv {m.Level}";

            if (statsText != null)
            {
                string levelLine = levelText == null ? $"LVL  {m.Level}\n" : "";
                statsText.text =
                    levelLine +
                    $"HP   {m.CurrentHp} / {m.MaxHp}\n" +
                    $"MP   {m.CurrentMp} / {m.MaxMp}\n" +
                    $"ATK  {m.Attack}\n" +
                    $"DEF  {m.Defense}\n" +
                    $"SPD  {m.Speed}";
            }

            if (expSlider != null) expSlider.value = m.ExpNormalized;
            if (expText != null)
                expText.text = m.IsMaxLevel
                    ? "EXP  MAKS"
                    : $"EXP  {m.CurrentExp} / {m.ExpToNextLevel}";

            BuildSkillSection(m);
        }

        // ── Skill section ─────────────────────────────────────────────────────
        // Interactive mode: normal skills become toggle rows; skillsText shows only
        // the (fixed) special skills. Fallback mode: skillsText lists everything.

        private void BuildSkillSection(PartyMember m)
        {
            if (LoadoutInteractive)
            {
                BuildLoadoutRows(m);
                if (skillsText != null) skillsText.text = BuildSpecialList(m);
                UpdateLoadoutCount(m);
            }
            else
            {
                HideAllSkillRows();
                if (skillsText != null) skillsText.text = BuildFullSkillList(m);
                if (loadoutCountText != null) loadoutCountText.text = "";
            }
        }

        private void BuildLoadoutRows(PartyMember m)
        {
            var pool = m.Base != null ? m.Base.Skills : null;
            int count = pool?.Count ?? 0;

            while (_skillRows.Count < count)
                _skillRows.Add(Instantiate(skillTogglePrefab, skillsContainer));

            for (int i = 0; i < _skillRows.Count; i++)
            {
                if (pool != null && i < count && pool[i] != null)
                {
                    var skill = pool[i];
                    bool equipped = m.IsEquipped(skill);
                    // Locked if level-gated (not learned yet) or the loadout is full.
                    bool locked   = !m.IsUnlocked(skill) || (!equipped && !m.CanEquipMore);
                    _skillRows[i].gameObject.SetActive(true);
                    _skillRows[i].Bind(skill, equipped, locked, s => OnToggleSkill(m, s));
                }
                else
                {
                    _skillRows[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnToggleSkill(PartyMember m, SkillData skill)
        {
            m.ToggleEquip(skill);     // honors the cap internally
            BuildLoadoutRows(m);      // re-evaluate equipped/locked states
            UpdateLoadoutCount(m);
        }

        private void UpdateLoadoutCount(PartyMember m)
        {
            if (loadoutCountText != null)
                loadoutCountText.text = $"Skill: {m.EquippedCount}/{PartyMember.MaxEquippedSkills}";
        }

        private void HideAllSkillRows()
        {
            foreach (var r in _skillRows)
                if (r != null) r.gameObject.SetActive(false);
        }

        // Level-locked skills are dimmed and tagged with the level needed to unlock them.
        private static string SkillLine(PartyMember m, SkillData s, string costSuffix, string costColor)
        {
            if (m.IsUnlocked(s))
                return $"• {s.Name}  <color={costColor}>({s.Cost} {costSuffix})</color>";
            return $"<color=#777777>• {s.Name}  (terkunci - Lv {s.UnlockLevel})</color>";
        }

        private static string BuildSpecialList(PartyMember m)
        {
            var data = m?.Base;
            var sb = new StringBuilder();
            sb.AppendLine("<b>Skill Spesial</b>");
            if (data != null && data.SpecialSkills.Count > 0)
                foreach (var s in data.SpecialSkills)
                {
                    if (s == null) continue;
                    sb.AppendLine(SkillLine(m, s, "SP", "#F2A62E"));
                }
            else
                sb.AppendLine("<color=#999999>— belum ada —</color>");
            return sb.ToString();
        }

        private static string BuildFullSkillList(PartyMember m)
        {
            var data = m?.Base;
            var sb = new StringBuilder();

            sb.AppendLine("<b>Skill</b>");
            if (data != null && data.Skills.Count > 0)
                foreach (var s in data.Skills)
                {
                    if (s == null) continue;
                    sb.AppendLine(SkillLine(m, s, "MP", "#5C8FF5"));
                }
            else
                sb.AppendLine("<color=#999999>— belum ada —</color>");

            sb.AppendLine();
            sb.Append(BuildSpecialList(m));
            return sb.ToString();
        }

        private void ClearDetail()
        {
            if (portrait  != null) portrait.enabled = false;
            if (nameText  != null) nameText.text  = "";
            if (switcherNameText != null) switcherNameText.text = "";
            if (statsText != null) statsText.text = "";
            if (skillsText != null) skillsText.text = "";
            if (loadoutCountText != null) loadoutCountText.text = "";
            HideAllSkillRows();
        }
    }
}
