using UnityEngine;
using System;

namespace Assets.Scripts.LiveOps
{
    public class RaceLiveOpService : ALiveOpService
    {
        public override void OnActivate()
        {
            Debug.Log($"[RaceLiveOpService] Race Event Activated: {UniqueID}");
            string data = LoadProgress();
            if (string.IsNullOrEmpty(data))
            {
                Debug.Log("[RaceLiveOpService] No previous progress found. Starting fresh.");
                // Initialize fresh data
            }
            else
            {
                Debug.Log($"[RaceLiveOpService] Progress loaded: {data}");
            }
        }

        public override void OnDeactivate()
        {
            Debug.Log($"[RaceLiveOpService] Race Event Deactivated: {UniqueID}");
        }

        public void AddRaceProgress(int score)
        {
            // Example of updating progress
            string currentData = LoadProgress();
            // ... update logic ...
            SaveProgress("ExampleScore_" + score);
        }
    }
}
