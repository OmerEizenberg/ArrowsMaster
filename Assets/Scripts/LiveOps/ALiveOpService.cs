using System;
using UnityEngine;
using Assets.Scripts.Core;

namespace Assets.Scripts.LiveOps
{
    public abstract class ALiveOpService
    {
        public LiveOpSO SO { get; private set; }
        public string UniqueID { get; private set; }
        public GameObject IconInstance { get; set; }
        
        public virtual void Initialize(LiveOpSO so, string uniqueID)
        {
            SO = so;
            UniqueID = uniqueID;
        }
        
        public abstract void OnActivate();
        public abstract void OnDeactivate();
        
        public virtual void OnTick() { } // Optional: for time-based logic or timers
        
        protected void SaveProgress(string json)
        {
            UserDataManager.Instance.SaveLiveOpData(UniqueID, json);
        }
        
        protected string LoadProgress()
        {
            return UserDataManager.Instance.GetLiveOpData(UniqueID);
        }
        
        public virtual TimeSpan GetRemainingTime()
        {
            // Logic to calculate remaining time relative to ActivationHour and DurationHours
            // This is a helper for the IconView
            DateTime now = DateTime.Now;
            // Simplified: Assuming it started today at ActivationHour
            DateTime start = new DateTime(now.Year, now.Month, now.Day, SO.ActivationHour, 0, 0);
            
            // If now is before start, it might have started yesterday? 
            // Better to handle this in LiveOpManager and pass the precise end time
            return TimeSpan.Zero; // Placeholder
        }
    }
}
