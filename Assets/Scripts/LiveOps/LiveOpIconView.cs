using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Assets.Scripts.Core;

namespace Assets.Scripts.LiveOps
{
    public class LiveOpIconView : MonoBehaviour
    {
        [SerializeField] private List<Image> m_ImagesToFade;
        [SerializeField] private List<TextMeshProUGUI> m_TimerTexts;
        [SerializeField] private Button m_IconButton;
        
        [Header("Lock Settings")]
        [SerializeField] private GameObject m_LockIcon;
        [SerializeField] private GameObject m_UnlockTooltip;
        [SerializeField] private TextMeshProUGUI m_UnlockTooltipText;

        private ALiveOpService service;
        private bool isLocked = false;
        private Coroutine tooltipCoroutine;

        public void Initialize(ALiveOpService service)
        {
            this.service = service;
            
            if (m_UnlockTooltip != null) m_UnlockTooltip.SetActive(false);
            
            if (m_IconButton != null)
            {
                m_IconButton.onClick.RemoveAllListeners();
                m_IconButton.onClick.AddListener(OnIconClicked);
            }
            
            CheckUnlockState();
            RefreshUI();
        }

        private void Update()
        {
            if (service != null)
            {
                CheckUnlockState();
                
                if (m_TimerTexts != null && m_TimerTexts.Count > 0)
                {
                    UpdateTimers();
                }
            }
        }

        private void CheckUnlockState()
        {
            int currentLevel = UserDataManager.Instance.CurrentLevel;
            bool newlyUnlocked = isLocked && currentLevel >= service.SO.UnlockLevel;
            isLocked = currentLevel < service.SO.UnlockLevel;

            if (m_LockIcon != null) m_LockIcon.SetActive(isLocked);
            
            float targetAlpha = isLocked ? 0.5f : 1.0f;
            ApplyAlpha(targetAlpha);

            if (newlyUnlocked)
            {
                // Play unlock effect or animation if needed
                Debug.Log($"[LiveOpIconView] {service.SO.EventID} Unlocked!");
            }
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

            if (m_TimerTexts != null)
            {
                foreach (var text in m_TimerTexts)
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

        private void RefreshUI()
        {
            // Set image if needed 
        }

        private void UpdateTimers()
        {
            DateTime now = DateTime.Now;
            DateTime start = new DateTime(now.Year, now.Month, now.Day, service.SO.ActivationHour, 0, 0);
            DateTime end = start.AddHours(service.SO.DurationHours);
            
            TimeSpan remaining = end - now;
            string timeStr = "0h 0m";
            
            if (remaining.TotalSeconds > 0)
            {
                int hours = (int)remaining.TotalHours;
                int minutes = remaining.Minutes;
                timeStr = $"{hours}h {minutes}m";
            }

            foreach (var text in m_TimerTexts)
            {
                if (text != null) text.text = timeStr;
            }
        }

        private void OnIconClicked()
        {
            if (service == null) return;
            
            if (isLocked)
            {
                ShowTooltip();
                return;
            }

            // Instantiate popup from Resources
            GameObject popupPrefab = Resources.Load<GameObject>(service.SO.PopupPrefabName);
            if (popupPrefab != null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    GameObject popup = Instantiate(popupPrefab, null);
                    popup.SetActive(true);
                    popup.transform.SetAsLastSibling();
                }
            }
        }

        private void ShowTooltip()
        {
            if (m_UnlockTooltip == null) return;
            
            if (m_UnlockTooltipText != null)
            {
                m_UnlockTooltipText.text = $"Unlocked at Level {service.SO.UnlockLevel}";
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
