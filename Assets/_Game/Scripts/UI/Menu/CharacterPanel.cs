using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Nusantara.UI
{
    /// <summary>
    /// Character tab. Left: a list of party members (click to inspect). Right: the
    /// selected member's portrait, stats, and skill loadout (normal + special).
    ///
    /// v1 is READ-ONLY — it surfaces each character's known skills. The interactive
    /// "choose which skills to bring" loadout editor is a follow-up: it needs a
    /// persisted equipped-loadout list on the member (see PROGRESS 2026-06-02 design
    /// note) which doesn't exist yet. The <see cref="loadoutHint"/> label marks that.
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
    ///          ├ SkillsText (TMP)                → skillsText
    ///          └ LoadoutHint (TMP, optional)     → loadoutHint
    /// </summary>
    public class CharacterPanel : MonoBehaviour
    {
        [Header("Member list")]
        [SerializeField] private Transform        membersContainer;
        [SerializeField] private MemberListButton memberButtonPrefab;

        [Header("Detail")]
        [SerializeField] private Image           portrait;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI statsText;
        [SerializeField] private TextMeshProUGUI skillsText;
        [SerializeField] private TextMeshProUGUI emptyText;   // "Belum ada anggota party"
        [Tooltip("Optional note that loadout editing is coming. Leave null to hide.")]
        [SerializeField] private TextMeshProUGUI loadoutHint;

        private readonly List<MemberListButton> _rows = new List<MemberListButton>();
        private List<PartyMember> _members = new List<PartyMember>();
        private int _selected = -1;

        void OnEnable() => Refresh();

        public void Refresh()
        {
            var party = Object.FindFirstObjectByType<PartySystem>();
            _members = party != null ? new List<PartyMember>(party.Members) : new List<PartyMember>();

            bool any = _members.Count > 0;
            if (emptyText != null) emptyText.gameObject.SetActive(!any);
            if (loadoutHint != null)
            {
                loadoutHint.gameObject.SetActive(any);
                loadoutHint.text = "Pengaturan skill (loadout) akan hadir di update berikutnya.";
            }

            BuildMemberButtons();

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

            if (statsText != null)
                statsText.text =
                    $"HP   {m.CurrentHp} / {m.MaxHp}\n" +
                    $"MP   {m.CurrentMp} / {m.MaxMp}\n" +
                    $"ATK  {m.Attack}\n" +
                    $"DEF  {m.Defense}\n" +
                    $"SPD  {m.Speed}";

            if (skillsText != null)
                skillsText.text = BuildSkillList(m);
        }

        private static string BuildSkillList(PartyMember m)
        {
            var sb = new StringBuilder();
            var data = m.Base;

            sb.AppendLine("<b>Skill</b>");
            if (data != null && data.Skills.Count > 0)
                foreach (var s in data.Skills)
                {
                    if (s == null) continue;
                    sb.AppendLine($"• {s.Name}  <color=#5C8FF5>({s.Cost} MP)</color>");
                }
            else
                sb.AppendLine("<color=#999999>— belum ada —</color>");

            sb.AppendLine();
            sb.AppendLine("<b>Skill Spesial</b>");
            if (data != null && data.SpecialSkills.Count > 0)
                foreach (var s in data.SpecialSkills)
                {
                    if (s == null) continue;
                    sb.AppendLine($"• {s.Name}  <color=#F2A62E>({s.Cost} SP)</color>");
                }
            else
                sb.AppendLine("<color=#999999>— belum ada —</color>");

            return sb.ToString();
        }

        private void ClearDetail()
        {
            if (portrait  != null) portrait.enabled = false;
            if (nameText  != null) nameText.text  = "";
            if (statsText != null) statsText.text = "";
            if (skillsText != null) skillsText.text = "";
        }
    }
}
