using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoftForceUpdateView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_DescriptionText;
    [SerializeField] private Button m_ActionButton;
    [SerializeField] private Button m_SkipButton;

    public void Setup( bool isForce)
     {
    //     if (m_DescriptionText != null)
    //         m_DescriptionText.text = message;

        if (m_ActionButton != null)
            m_ActionButton.gameObject.SetActive(true);

        if (m_SkipButton != null)
            m_SkipButton.gameObject.SetActive(!isForce);
    }

    public void OnActionButtonClicked()
    {
        #if UNITY_ANDROID
        Application.OpenURL("https://play.google.com/store/apps/details?id=com.everybodygames.arrowsmaster" );
        #elif UNITY_IOS
        Application.OpenURL("https://apps.apple.com/us/app/arrows-legend-puzzle-escape/id6758734966");
        #endif
    }

    public void OnSkipButtonClicked()
    {
        Destroy(gameObject);
    }
}
