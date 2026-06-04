using UnityEngine;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.GameUI
{
    public class LevelCurrencyDisplay : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI m_CurrencyText;
        [SerializeField] private GameObject m_FloatingTextPrefab;
        
        [Header("Animation Settings")]
        [SerializeField] private float m_TextPunchScale = 1.2f;
        [SerializeField] private float m_PunchDuration = 0.2f;
        [SerializeField] private float m_FloatDuration = 1.0f;
        [SerializeField] private float m_FloatDistance = 50f;
        [SerializeField] private Vector2 m_SpawnOffsetRange = new Vector2(-20f, 20f);
        [SerializeField] private float m_SpawnOffsetYRange = 200f;

        private int m_CurrentAmount = 0;
        private Coroutine m_PunchCoroutine;
        private Vector3 m_OriginalScale;
        private Vector2 m_FloatingDistance = new Vector2(0f, 0f);

        private RectTransform m_FloatingParentRect;
        private RectTransform m_CurrencyTextRect;
        private Camera m_UICamera;
        private CoinsRewardFloatingView m_FloatingView;

        private void Start()
        {
             m_FloatingDistance = new Vector2(-Screen.width/23f, Screen.height/20f);

            if (m_CurrencyText != null)
            {
                m_OriginalScale = m_CurrencyText.transform.localScale;
                m_CurrencyText.text = "0";
                CacheUICanvasReferences();
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCurrencyChanged += UpdateCurrencyDisplay;
                m_CurrentAmount = GameManager.Instance.CollectedLevelCurrency;
                if (m_CurrencyText != null) m_CurrencyText.text = m_CurrentAmount.ToString("N0");
            }
        }

        private void CacheUICanvasReferences()
        {
            m_CurrencyTextRect = m_CurrencyText.rectTransform;
            Transform floatingParent = m_CurrencyText.transform.parent;
            m_FloatingParentRect = floatingParent as RectTransform;

            Canvas canvas = m_CurrencyText.GetComponentInParent<Canvas>();
            m_UICamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                m_UICamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }
        }

        private void EnsureFloatingView()
        {
            if (m_FloatingView != null || m_FloatingTextPrefab == null || m_FloatingParentRect == null)
            {
                return;
            }

            GameObject floatingObj = Instantiate(m_FloatingTextPrefab, m_FloatingParentRect);
            m_FloatingView = floatingObj.GetComponent<CoinsRewardFloatingView>();
            if (m_FloatingView == null)
            {
                m_FloatingView = floatingObj.AddComponent<CoinsRewardFloatingView>();
            }

            floatingObj.SetActive(false);
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null && m_CurrencyText != null)
            {
                m_CurrentAmount = GameManager.Instance.CollectedLevelCurrency;
                m_CurrencyText.text = m_CurrentAmount.ToString("N0");
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCurrencyChanged -= UpdateCurrencyDisplay;
            }

            if (m_FloatingView != null)
            {
                Destroy(m_FloatingView.gameObject);
                m_FloatingView = null;
            }
        }

        private void UpdateCurrencyDisplay(int newAmount, Vector2 clickPos)
        {
            int difference = newAmount - m_CurrentAmount;
            
            if (difference > 0)
            {
                if (m_FloatingTextPrefab != null)
                {
                    ShowFloatingText(difference, clickPos);
                }

                if (m_CurrencyText != null)
                {
                    if (m_PunchCoroutine != null) StopCoroutine(m_PunchCoroutine);
                    m_PunchCoroutine = StartCoroutine(AnimatePunch());
                }
            }

            m_CurrentAmount = newAmount;
            
            if (m_CurrencyText != null)
            {
                m_CurrencyText.text = newAmount.ToString("N0");
            }
        }

        private void ShowFloatingText(int amount, Vector2 clickPos)
        {
            if (m_CurrencyTextRect == null)
            {
                CacheUICanvasReferences();
            }

            EnsureFloatingView();
            if (m_FloatingView == null)
            {
                return;
            }

            m_FloatingView.Show(
                amount,
                clickPos,
                m_FloatingParentRect,
                m_CurrencyTextRect,
                m_UICamera,
                m_FloatingDistance,
                m_SpawnOffsetRange,
                m_SpawnOffsetYRange,
                m_FloatDuration,
                m_FloatDistance);
        }

        private System.Collections.IEnumerator AnimatePunch()
        {
            float elapsed = 0f;
            Vector3 targetScale = m_OriginalScale * m_TextPunchScale;

            while (elapsed < m_PunchDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (m_PunchDuration / 2);
                m_CurrencyText.transform.localScale = Vector3.Lerp(m_OriginalScale, targetScale, t);
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < m_PunchDuration / 2)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (m_PunchDuration / 2);
                m_CurrencyText.transform.localScale = Vector3.Lerp(targetScale, m_OriginalScale, t);
                yield return null;
            }

            m_CurrencyText.transform.localScale = m_OriginalScale;
            m_PunchCoroutine = null;
        }
    }
}
