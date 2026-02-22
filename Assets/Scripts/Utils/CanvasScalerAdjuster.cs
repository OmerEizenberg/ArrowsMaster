using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.Utils
{
    [RequireComponent(typeof(CanvasScaler))]
    public class CanvasScalerAdjuster : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("The aspect ratio threshold (Width / Height) to trigger the change.")]
        [SerializeField] private float aspectRatioThreshold = 0.6f;
        
        [Tooltip("The value to set for Match Width Or Height if current aspect ratio is greater than threshold.")]
        [SerializeField] private float targetMatchValue = 0.5f;

        private void OnEnable()
        {
            AdjustCanvasScaler();
        }

        private void AdjustCanvasScaler()
        {
            CanvasScaler scaler = GetComponent<CanvasScaler>();
            if (scaler == null) return;

            // Only relevant if Screen Match Mode is set to Match Width Or Height
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize || 
                scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight)
            {
                return;
            }

            float currentAspectRatio = (float)Screen.width / Screen.height;

            if (currentAspectRatio > aspectRatioThreshold)
            {
                scaler.matchWidthOrHeight = targetMatchValue;
                Debug.Log($"[CanvasScalerAdjuster] Aspect ratio {currentAspectRatio:F2} is greater than {aspectRatioThreshold}, setting match to {targetMatchValue}");
            }
        }
    }
}
