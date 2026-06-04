using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nusantara.UI
{
    /// <summary>
    /// Party tab. Shows the current roster and lets the player pick who fights:
    /// tap a member to toggle ACTIVE (sent into battle) vs RESERVE. Selection is
    /// capped at <see cref="PartySystem.MaxActiveBattle"/> and at least one member
    /// must stay active — both enforced by <see cref="PartySystem.SetActive"/>.
    ///
    /// The active state shows via each row's "selected" highlight + sub-label. Battle
    /// reads <see cref="PartySystem.ActiveHealthyBattleMembers"/> (see GameController),
    /// and the choice persists through the save system.
    ///
    /// ── Unity setup ──────────────────────────────────────────────────────────
    ///   PartyPanel (this component)
    ///     ├ RosterContainer (Grid/Vertical Layout)  → rosterContainer
    ///     │    (MemberListButton prefab pooled here — its SelectedHighlight = "active" marker)
    ///     ├ EmptyText (TMP, optional)               → emptyText
    ///     └ Hint (TMP, optional)                    → hint  (shows "Bertarung: X/N" + tip)
    /// </summary>
    public class PartyPanel : MonoBehaviour
    {
        [SerializeField] private Transform        rosterContainer;
        [SerializeField] private MemberListButton memberButtonPrefab;
        [SerializeField] private TextMeshProUGUI  emptyText;
        [Tooltip("Optional active-count + tip label. Leave null to hide.")]
        [SerializeField] private TextMeshProUGUI  hint;

        private readonly List<MemberListButton> _rows = new List<MemberListButton>();

        void OnEnable() => Refresh();

        public void Refresh()
        {
            var party = Object.FindFirstObjectByType<PartySystem>();
            var members = party != null ? new List<PartyMember>(party.Members) : new List<PartyMember>();

            bool any = members.Count > 0;
            if (emptyText != null) emptyText.gameObject.SetActive(!any);
            if (hint != null)
            {
                hint.gameObject.SetActive(any);
                if (party != null)
                    hint.text = $"Bertarung: {party.ActiveCount}/{party.MaxActiveBattle}  —  ketuk untuk pilih.";
            }

            if (memberButtonPrefab == null || rosterContainer == null) return;

            while (_rows.Count < members.Count)
                _rows.Add(Instantiate(memberButtonPrefab, rosterContainer));

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < members.Count)
                {
                    var m = members[i];
                    _rows[i].gameObject.SetActive(true);

                    string hp     = m.IsFainted ? "<color=#C04040>Pingsan</color>" : $"HP {m.CurrentHp}/{m.MaxHp}";
                    string tag    = m.IsActiveInBattle ? "<color=#E23A1E>Bertarung</color>" : "<color=#999999>Cadangan</color>";
                    string status = $"{tag}  ·  {hp}";

                    _rows[i].Bind(m, onClick: () => OnToggleMember(party, m), sub: status);
                    _rows[i].SetSelected(m.IsActiveInBattle);
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnToggleMember(PartySystem party, PartyMember m)
        {
            if (party == null) return;
            party.ToggleActive(m);   // enforces cap + at-least-one
            Refresh();               // reflect new state (and any rejected toggle)
        }
    }
}
