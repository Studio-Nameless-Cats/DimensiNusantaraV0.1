using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Nusantara.UI
{
    /// <summary>
    /// Party tab. Shows the current party roster (portrait + name + HP). This is the
    /// home for the future "pick who to bring into battle" selection.
    ///
    /// v1 is READ-ONLY. Battle currently spawns every healthy member up to the number
    /// of player spawn points (see BattleSystem.SetupBattle), so there's no active-vs-
    /// reserve distinction to edit yet. Adding selection means: an "active party"
    /// flag/list on PartySystem, persisting it in SaveData, and having the battle read
    /// the active list instead of HealthyMembers. The <see cref="hint"/> label marks this.
    ///
    /// ── Unity setup ──────────────────────────────────────────────────────────
    ///   PartyPanel (this component)
    ///     ├ RosterContainer (Grid/Vertical Layout)  → rosterContainer
    ///     │    (MemberListButton prefab pooled here)
    ///     ├ EmptyText (TMP, optional)               → emptyText
    ///     └ Hint (TMP, optional)                    → hint
    /// </summary>
    public class PartyPanel : MonoBehaviour
    {
        [SerializeField] private Transform        rosterContainer;
        [SerializeField] private MemberListButton memberButtonPrefab;
        [SerializeField] private TextMeshProUGUI  emptyText;
        [Tooltip("Optional note that battle-selection is coming. Leave null to hide.")]
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
                hint.text = "Semua anggota sehat ikut bertarung. Pemilihan party akan hadir nanti.";
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
                    string status = m.IsFainted ? "<color=#C04040>Pingsan</color>" : $"HP {m.CurrentHp}/{m.MaxHp}";
                    _rows[i].Bind(m, onClick: null, sub: status);
                }
                else
                {
                    _rows[i].gameObject.SetActive(false);
                }
            }
        }
    }
}
