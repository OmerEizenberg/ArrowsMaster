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
        // Typically opens App Store or Play Store
        #if UNITY_ANDROID
        Application.OpenURL("market://details?id=" + Application.identifier);
        #elif UNITY_IOS
        // You would replace this with your actual app id
        // Application.OpenURL("itms-apps://itunes.apple.com/app/idYOUR_APP_ID");
        #endif
    }

    public void OnSkipButtonClicked()
    {
        Destroy(gameObject);
    }
}
