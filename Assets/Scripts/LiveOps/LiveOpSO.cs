using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.LiveOps
{
    [CreateAssetMenu(fileName = "NewLiveOp", menuName = "LiveOps/LiveOpSO")]
    public class LiveOpSO : ScriptableObject
    {
        [Header("Identity")]
        public string EventID; // e.g. "Race", "DailyMissions"
        
        [Header("Scheduling")]
        public List<DayOfWeek> ActiveDays;
        [Range(0, 23)]
        public int ActivationHour; // 0-23
        public int DurationHours;

        [Header("Progression")]
        public int ShowLevel;
        public int UnlockLevel;
        
        [Header("Visuals")]
        public string IconPrefabName; // name in Resources
        public string PopupPrefabName; // name in Resources
        
        [Header("Logic")]
        public string ServiceClassName; // Full class name of the ALiveOpService implementation
    }
}
