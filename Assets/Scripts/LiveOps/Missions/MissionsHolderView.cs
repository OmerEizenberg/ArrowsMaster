using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Missions
{
    /// <summary>
    /// Optional component on MissionsHolder. Assign all 5 MissionSlotView references here.
    /// </summary>
    public class MissionsHolderView : MonoBehaviour
    {
        [Tooltip("One entry per mission row (Mission (1)..(5)). Mission Index on each slot must match config.")]
        [SerializeField] private MissionSlotView[] m_MissionSlots;

        public MissionSlotView[] MissionSlots => m_MissionSlots;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_MissionSlots != null && m_MissionSlots.Length > 0) return;

            var slots = new List<MissionSlotView>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var slot = transform.GetChild(i).GetComponent<MissionSlotView>();
                if (slot != null)
                    slots.Add(slot);
            }

            if (slots.Count > 0)
                m_MissionSlots = slots.ToArray();
        }
#endif
    }
}
