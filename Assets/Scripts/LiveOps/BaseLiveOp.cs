using System;
using UnityEngine;

namespace Assets.Scripts.LiveOps
{
    /// <summary>
    /// Base class for all Live Operations (Events).
    /// Defines the essential properties required for any LiveOp.
    /// </summary>
    [Serializable]
    public class BaseLiveOp
    {
        [Header("LiveOp Schedule")]
        public string StartDate; // Format: "yyyy-MM-dd HH:mm:ss"
        public string EndDate;   // Format: "yyyy-MM-dd HH:mm:ss"

        [Header("Visuals")]
        public string LobbyIconName; // name of the sprite to load from resources
        public string LiveopPopupPrefabName; // name of the prefab to load from resources

        [Header("Event Data")]
        [Tooltip("JSON string containing all relevant data for event declaration")]
        public string DataObj; // Placeholder for event-specific JSON data

        /// <summary>
        /// Checks if the LiveOp is currently active based on the system clock.
        /// </summary>
        /// <returns>True if the current time is between StartDate and EndDate.</returns>
        public virtual bool IsActive()
        {
            if (string.IsNullOrEmpty(StartDate) || string.IsNullOrEmpty(EndDate))
                return false;

            if (DateTime.TryParse(StartDate, out DateTime start) && DateTime.TryParse(EndDate, out DateTime end))
            {
                DateTime now = DateTime.UtcNow;
                return now >= start && now <= end;
            }

            return false;
        }
    }
}
