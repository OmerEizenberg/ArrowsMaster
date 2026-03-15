using UnityEngine;
using TMPro;
using System.Collections;

namespace Assets.Scripts.Lobby
{
    public class RollingLevelEffect : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI m_TargetText;
        [SerializeField] private float m_Duration = 0.6f;
        [SerializeField] private AnimationCurve m_Curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private bool m_IsAnimating = false;
        private GameObject m_CurrentRoller;
        private int m_FinalLevel;

        private void Awake()
        {
            if (m_TargetText == null)
            {
                m_TargetText = GetComponent<TextMeshProUGUI>();
            }
        }

        public void AnimateLevel(int oldLevel, int newLevel)
        {
            m_FinalLevel = newLevel;
            if (m_IsAnimating) return;
            if (m_TargetText == null) return;

            if (oldLevel == newLevel)
            {
                m_TargetText.text = $"Level {newLevel}";
                return;
            }

            StartCoroutine(RollingRoutine(oldLevel, newLevel));
        }

        private IEnumerator RollingRoutine(int oldLevel, int newLevel)
        {
            m_IsAnimating = true;

            string oldStr = oldLevel.ToString();
            string newStr = newLevel.ToString();

            // We assume the prefix is "Level "
            string prefix = "Level ";
            
            // If levels have different digit counts, just snap for now, 
            // but the unit digit always exists.
            
            string oldUnit = oldStr.Substring(oldStr.Length - 1);
            string newUnit = newStr.Substring(newStr.Length - 1);
            
            string oldPrefixDigits = oldStr.Length > 1 ? oldStr.Substring(0, oldStr.Length - 1) : "";
            string newPrefixDigits = newStr.Length > 1 ? newStr.Substring(0, newStr.Length - 1) : "";

            // If the prefix digits changed (e.g. 19 -> 20), we'll update them immediately or keep them static
            // User only asked for the unit digit to roll.
            
            // Set the full text but make the last digit transparent
            m_TargetText.text = $"{prefix}{newPrefixDigits}<color=#00000000>{newUnit}</color>";
            m_TargetText.ForceMeshUpdate();

            // Find the position of the last character
            TMP_TextInfo textInfo = m_TargetText.textInfo;
            int lastCharIndex = textInfo.characterCount - 1;
            
            // Wait a frame to ensure mesh is updated if needed
            yield return null;
            textInfo = m_TargetText.textInfo;
            lastCharIndex = textInfo.characterCount - 1;

            if (lastCharIndex < 0 || lastCharIndex >= textInfo.characterInfo.Length)
            {
                // Fallback if something is wrong
                m_TargetText.text = $"{prefix}{newLevel}";
                m_IsAnimating = false;
                yield break;
            }

            TMP_CharacterInfo charInfo = textInfo.characterInfo[lastCharIndex];
            Vector3 charCenter = (charInfo.bottomLeft + charInfo.topRight) * 0.5f;
            
            // Create a container for the roller
            GameObject container = new GameObject("RollerContainer", typeof(RectTransform));
            m_CurrentRoller = container;
            container.transform.SetParent(m_TargetText.transform, false);
            RectTransform containerRect = container.GetComponent<RectTransform>();
            containerRect.localScale = Vector3.one;
            containerRect.localPosition = charCenter;
            
            // Set size to roughly match the character
            float charWidth = charInfo.topRight.x - charInfo.topLeft.x;
            float charHeight = charInfo.topLeft.y - charInfo.bottomLeft.y;
            containerRect.sizeDelta = new Vector2(charWidth * 1.5f, charHeight * 1.2f);

            // Add Mask
            container.AddComponent<UnityEngine.UI.RectMask2D>();

            // Create Old Digit Text
            GameObject oldDigitObj = new GameObject("OldDigit", typeof(RectTransform));
            oldDigitObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI oldDigitText = oldDigitObj.AddComponent<TextMeshProUGUI>();
            CopySettings(m_TargetText, oldDigitText);
            oldDigitText.text = oldUnit;
            oldDigitText.alignment = TextAlignmentOptions.Center;
            ((RectTransform)oldDigitObj.transform).sizeDelta = containerRect.sizeDelta;

            // Create New Digit Text
            GameObject newDigitObj = new GameObject("NewDigit", typeof(RectTransform));
            newDigitObj.transform.SetParent(container.transform, false);
            TextMeshProUGUI newDigitText = newDigitObj.AddComponent<TextMeshProUGUI>();
            CopySettings(m_TargetText, newDigitText);
            newDigitText.text = newUnit;
            newDigitText.alignment = TextAlignmentOptions.Center;
            ((RectTransform)newDigitObj.transform).sizeDelta = containerRect.sizeDelta;

            // Positioning
            float height = containerRect.sizeDelta.y;
            Vector3 startPosOld = Vector3.zero;
            Vector3 endPosOld = new Vector3(0, -height, 0);
            
            Vector3 startPosNew = new Vector3(0, height, 0);
            Vector3 endPosNew = Vector3.zero;

            float elapsed = 0f;
            while (elapsed < m_Duration)
            {
                elapsed += Time.deltaTime;
                float t = m_Curve.Evaluate(elapsed / m_Duration);
                
                oldDigitObj.transform.localPosition = Vector3.Lerp(startPosOld, endPosOld, t);
                newDigitObj.transform.localPosition = Vector3.Lerp(startPosNew, endPosNew, t);
                
                yield return null;
            }

            // Cleanup
            if (container != null) Destroy(container);
            m_CurrentRoller = null;
            m_TargetText.text = $"{prefix}{newLevel}";
            m_IsAnimating = false;
        }

        private void OnDisable()
        {
            if (!m_IsAnimating && m_CurrentRoller == null) return;

            StopAllCoroutines();
            if (m_CurrentRoller != null)
            {
                Destroy(m_CurrentRoller);
                m_CurrentRoller = null;
            }

            if (m_TargetText != null && m_FinalLevel > 0)
            {
                m_TargetText.text = $"Level {m_FinalLevel}";
            }
            m_IsAnimating = false;
        }

        private void CopySettings(TextMeshProUGUI source, TextMeshProUGUI target)
        {
            target.font = source.font;
            target.fontSize = source.fontSize;
            target.color = source.color;
            target.fontStyle = source.fontStyle;
            target.fontWeight = source.fontWeight;
            target.enableAutoSizing = source.enableAutoSizing;
            target.fontSizeMin = source.fontSizeMin;
            target.fontSizeMax = source.fontSizeMax;
            // Add other settings if necessary
        }
    }
}
