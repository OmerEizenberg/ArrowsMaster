using UnityEngine;

namespace LiftEngine.Samples
{
    /// <summary>
    /// Optional dev harness: attach to a GameObject in a test scene, or call from AdsManager.Initialize.
    /// </summary>
    public sealed class LiftEngineSampleDriver : MonoBehaviour
    {
        [SerializeField] private bool initializeOnStart = true;
        [SerializeField] private bool showRewardedWhenReady = false;

        private void Start()
        {
            if (!initializeOnStart)
                return;

            LiftEngineSdk.SetVerboseLogging(true);
            LiftEngineSdk.Initialize();

            LiftEngineSdkCallbacks.OnSdkInitializedEvent += status =>
            {
                Debug.Log("[LiftEngineSampleDriver] Init: " + status);

                if (showRewardedWhenReady)
                    InvokeRepeating(nameof(TryShowRewarded), 2f, 5f);
            };
        }

        private void TryShowRewarded()
        {
            if (!LiftEngineSdk.IsAdReady(LiftEngineAdFormat.Rewarded))
            {
                Debug.Log("[LiftEngineSampleDriver] Rewarded not ready yet: " +
                          LiftEngineSdk.GetPrewarmState(LiftEngineAdFormat.Rewarded));
                return;
            }

            CancelInvoke(nameof(TryShowRewarded));
            LiftEngineSdk.ShowAd(LiftEngineAdFormat.Rewarded, null, new LiftEngineShowAdCallbacks
            {
                OnAdDisplayed = () => Debug.Log("[LiftEngineSampleDriver] Rewarded displayed"),
                OnAdHidden = () => Debug.Log("[LiftEngineSampleDriver] Rewarded hidden"),
                OnAdRewarded = () => Debug.Log("[LiftEngineSampleDriver] User rewarded"),
                OnAdDisplayFailed = msg => Debug.LogWarning("[LiftEngineSampleDriver] Failed: " + msg)
            });
        }
    }
}
