using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.LiveOps
{
    public class FeatureUnlockView : MonoBehaviour
    {
        [SerializeField] private int m_UnlockLevel;
        [SerializeField] private List<Image> m_ImagesToFade;
        [SerializeField] private List<TextMeshProUGUI> m_TextsToFade;
        [SerializeField] private GameObject m_LockIcon;
        
        [Header("Tooltip Settings")]
        [SerializeField] private GameObject m_UnlockTooltip;
        [SerializeField] private TextMeshProUGUI m_UnlockTooltipText;

        private bool isLocked = false;
        private Coroutine tooltipCoroutine;
        private Button m_Button;

        private void Start()
        {
            m_Button = GetComponent<Button>();
            if (m_Button != null)
            {
                m_Button.onClick.AddListener(OnButtonClicked);
            }
            
            if (m_UnlockTooltip != null) m_UnlockTooltip.SetActive(false);
            
            CheckUnlockState();
        }

        private void Update()
        {
            CheckUnlockState();
        }

        private void CheckUnlockState()
        {
            int currentLevel = UserDataManager.Instance.CurrentLevel;
            isLocked = currentLevel < m_UnlockLevel;

            if (m_LockIcon != null) m_LockIcon.SetActive(isLocked);
            
            float targetAlpha = isLocked ? 0.5f : 1.0f;
            ApplyAlpha(targetAlpha);
        }

        private void ApplyAlpha(float alpha)
        {
            if (m_ImagesToFade != null)
            {
                foreach (var img in m_ImagesToFade)
                {
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = alpha;
                        img.color = c;
                    }
                }
            }

            if (m_TextsToFade != null)
            {
                foreach (var text in m_TextsToFade)
                {
                    if (text != null)
                    {
                        Color c = text.color;
                        c.a = alpha;
                        text.color = c;
                    }
                }
            }
        }

        private void OnButtonClicked()
        {
            if (isLocked)
            {
                ShowTooltip();
            }
        }

        public bool IsLocked() => isLocked;

        private void ShowTooltip()
        {
            if (m_UnlockTooltip == null) return;
            
            if (m_UnlockTooltipText != null)
            {
                m_UnlockTooltipText.text = $"Unlocked at Level {m_UnlockLevel}";
            }
            
            if (tooltipCoroutine != null) StopCoroutine(tooltipCoroutine);
            tooltipCoroutine = StartCoroutine(TooltipCoroutine());
        }

        private IEnumerator TooltipCoroutine()
        {
            m_UnlockTooltip.SetActive(true);
            yield return new WaitForSeconds(3f);
            m_UnlockTooltip.SetActive(false);
            tooltipCoroutine = null;
        }
    }
}
