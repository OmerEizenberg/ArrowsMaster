using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using System;

namespace Assets.Scripts.Lobby
{
    public class TermsAndConditionsPopup : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI m_MainText;
        [SerializeField] private TextMeshProUGUI m_SmallText;
        [SerializeField] private Button m_ContinueButton;

        public Action OnAgreed;

        private void Start()
        {
            if (m_ContinueButton != null)
            {
                m_ContinueButton.onClick.AddListener(OnContinuePressed);
            }

            SetupText();
        }

        private void SetupText()
        {
            if (m_MainText == null) return;

            // Using TMP link tags for clickable regions, underlined as requested.
            string termsLink = "<link=\"terms\"><u>Terms & Conditions</u></link>";
            string privacyLink = "<link=\"privacy\"><u>Privacy Policy</u></link>";

            m_MainText.text = $"To use Arrows Legend you must agree to our {termsLink} and affirm you have reviewed our {privacyLink}";
            
            if (m_SmallText != null)
            {
                m_SmallText.text = "Please press \"Continue\" if you choose to start using the app";
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Check if user clicked on a link in the main text
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(m_MainText, eventData.position, null);
            if (linkIndex != -1)
            {
                TMP_LinkInfo linkInfo = m_MainText.textInfo.linkInfo[linkIndex];
                string linkId = linkInfo.GetLinkID();

                if (linkId == "terms")
                {
                    Application.OpenURL("https://everybody-games-ltd-e374d1aa.base44.app/Terms");
                }
                else if (linkId == "privacy")
                {
                    Application.OpenURL("https://everybody-games-ltd-e374d1aa.base44.app/Privacy");
                }
            }
        }

        private void OnContinuePressed()
        {
            PlayerPrefs.SetInt("TermsAgreed", 1);
            PlayerPrefs.Save();
            OnAgreed?.Invoke();
            Destroy(gameObject);
        }
    }
}
