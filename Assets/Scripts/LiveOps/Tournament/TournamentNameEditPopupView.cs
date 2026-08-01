using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Prefab-friendly "edit tournament name" overlay.
    /// Edit look in Assets/Resources/TournamentNameEditPopup.prefab.
    /// Optional *Bg fields mirror main text (shadow/outline style).
    /// </summary>
    public class TournamentNameEditPopupView : MonoBehaviour
    {
        [Header("Chrome")]
        [SerializeField] private Image m_Dim;
        [SerializeField] private Image m_Box;

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI m_Title;
        [SerializeField] private TextMeshProUGUI m_TitleBg;

        [Header("Input")]
        [SerializeField] private TMP_InputField m_Input;
        [SerializeField] private TextMeshProUGUI m_InputText;
        [SerializeField] private TextMeshProUGUI m_InputTextBg;
        [SerializeField] private TextMeshProUGUI m_Placeholder;
        [SerializeField] private TextMeshProUGUI m_PlaceholderBg;

        [Header("Error")]
        [SerializeField] private TextMeshProUGUI m_Error;
        [SerializeField] private TextMeshProUGUI m_ErrorBg;

        [Header("Save")]
        [SerializeField] private Button m_SaveButton;
        [SerializeField] private TextMeshProUGUI m_SaveLabel;
        [SerializeField] private TextMeshProUGUI m_SaveLabelBg;

        [Header("Cancel")]
        [SerializeField] private Button m_CancelButton;
        [SerializeField] private TextMeshProUGUI m_CancelLabel;
        [SerializeField] private TextMeshProUGUI m_CancelLabelBg;

        public Image Dim { get => m_Dim; set => m_Dim = value; }
        public Image Box { get => m_Box; set => m_Box = value; }
        public TextMeshProUGUI Title { get => m_Title; set => m_Title = value; }
        public TextMeshProUGUI TitleBg { get => m_TitleBg; set => m_TitleBg = value; }
        public TMP_InputField Input { get => m_Input; set => m_Input = value; }
        public TextMeshProUGUI InputTextComponent { get => m_InputText; set => m_InputText = value; }
        public TextMeshProUGUI InputTextBg { get => m_InputTextBg; set => m_InputTextBg = value; }
        public TextMeshProUGUI Placeholder { get => m_Placeholder; set => m_Placeholder = value; }
        public TextMeshProUGUI PlaceholderBg { get => m_PlaceholderBg; set => m_PlaceholderBg = value; }
        public TextMeshProUGUI Error { get => m_Error; set => m_Error = value; }
        public TextMeshProUGUI ErrorBg { get => m_ErrorBg; set => m_ErrorBg = value; }
        public Button SaveButton { get => m_SaveButton; set => m_SaveButton = value; }
        public TextMeshProUGUI SaveLabel { get => m_SaveLabel; set => m_SaveLabel = value; }
        public TextMeshProUGUI SaveLabelBg { get => m_SaveLabelBg; set => m_SaveLabelBg = value; }
        public Button CancelButton { get => m_CancelButton; set => m_CancelButton = value; }
        public TextMeshProUGUI CancelLabel { get => m_CancelLabel; set => m_CancelLabel = value; }
        public TextMeshProUGUI CancelLabelBg { get => m_CancelLabelBg; set => m_CancelLabelBg = value; }

        public event Action OnSaveClicked;
        public event Action OnCancelClicked;

        private bool m_Wired;

        public string InputText => m_Input != null ? m_Input.text : string.Empty;

        private void Awake()
        {
            TryAutoWireTextBgs();
        }

        private void OnDestroy()
        {
            if (m_Input != null)
                m_Input.onValueChanged.RemoveListener(OnInputValueChanged);
        }

        public void WireButtons()
        {
            if (m_Wired) return;
            m_Wired = true;

            TryAutoWireTextBgs();

            if (m_SaveButton != null)
            {
                m_SaveButton.onClick.RemoveAllListeners();
                m_SaveButton.onClick.AddListener(() => OnSaveClicked?.Invoke());
            }

            if (m_CancelButton != null)
            {
                m_CancelButton.onClick.RemoveAllListeners();
                m_CancelButton.onClick.AddListener(() =>
                {
                    OnCancelClicked?.Invoke();
                    Hide();
                });
            }

            if (m_Input != null)
            {
                m_Input.characterLimit = TournamentNameFilter.MaxLength;
                if (m_InputText != null)
                    m_Input.textComponent = m_InputText;
                if (m_Placeholder != null)
                    m_Input.placeholder = m_Placeholder;
                m_Input.onValueChanged.RemoveListener(OnInputValueChanged);
                m_Input.onValueChanged.AddListener(OnInputValueChanged);
            }
        }

        public void Show(string currentName)
        {
            WireButtons();
            if (m_Input != null)
                m_Input.text = currentName ?? string.Empty;
            SyncInputShadows();
            SetError(string.Empty);
            gameObject.SetActive(true);
            if (m_Input != null)
                m_Input.ActivateInputField();
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetError(string error)
        {
            SetPairedText(m_Error, m_ErrorBg, error ?? string.Empty);
        }

        public void SetTitle(string value)
        {
            SetPairedText(m_Title, m_TitleBg, value ?? string.Empty);
        }

        public void SetPlaceholder(string value)
        {
            SetPairedText(m_Placeholder, m_PlaceholderBg, value ?? string.Empty);
        }

        public void SetSaveLabel(string value)
        {
            SetPairedText(m_SaveLabel, m_SaveLabelBg, value ?? string.Empty);
        }

        public void SetCancelLabel(string value)
        {
            SetPairedText(m_CancelLabel, m_CancelLabelBg, value ?? string.Empty);
        }

        private void OnInputValueChanged(string _)
        {
            SyncInputShadows();
        }

        private void SyncInputShadows()
        {
            if (m_InputTextBg != null)
            {
                if (m_InputText != null)
                    m_InputTextBg.text = m_InputText.text;
                else if (m_Input != null && m_Input.textComponent != null)
                    m_InputTextBg.text = m_Input.textComponent.text;
                else if (m_Input != null)
                    m_InputTextBg.text = m_Input.text;
            }

            if (m_PlaceholderBg != null)
            {
                bool showPlaceholder = m_Placeholder != null
                    ? m_Placeholder.enabled && m_Placeholder.gameObject.activeSelf
                    : m_Input == null || string.IsNullOrEmpty(m_Input.text);
                m_PlaceholderBg.enabled = showPlaceholder;
                if (m_PlaceholderBg.gameObject.activeSelf != showPlaceholder && m_Placeholder != null)
                    m_PlaceholderBg.gameObject.SetActive(m_Placeholder.gameObject.activeSelf);
            }
        }

        private static void SetPairedText(TextMeshProUGUI main, TextMeshProUGUI bg, string value)
        {
            if (main != null)
                main.text = value;
            if (bg != null)
                bg.text = value;
        }

        private void TryAutoWireTextBgs()
        {
            if (m_TitleBg == null)
                m_TitleBg = FindChildTmp("TitleBG");
            if (m_ErrorBg == null)
                m_ErrorBg = FindChildTmp("ErrorBG");
            if (m_InputTextBg == null)
                m_InputTextBg = FindChildTmp("TextBG");
            if (m_PlaceholderBg == null)
                m_PlaceholderBg = FindChildTmp("PlaceholderBG");
            if (m_SaveLabelBg == null)
                m_SaveLabelBg = FindChildTmp("SaveBG");
            if (m_CancelLabelBg == null)
                m_CancelLabelBg = FindChildTmp("CancelBG");

            // Fix Title accidentally wired to TitleBG.
            if (m_Title != null && m_Title.gameObject.name == "TitleBG")
            {
                var title = FindChildTmp("Title");
                if (title != null)
                {
                    if (m_TitleBg == null)
                        m_TitleBg = m_Title;
                    m_Title = title;
                }
            }
            else if (m_Title == null)
            {
                m_Title = FindChildTmp("Title");
            }

            if (m_InputText == null)
                m_InputText = FindChildTmp("Text");
            if (m_Placeholder == null)
                m_Placeholder = FindChildTmp("Placeholder");

            if (m_SaveLabel == null && m_SaveButton != null)
                m_SaveLabel = FindDirectChildTmp(m_SaveButton.transform, "Save")
                              ?? FindDirectChildTmp(m_SaveButton.transform, "Label");

            if (m_CancelLabel == null && m_CancelButton != null)
                m_CancelLabel = FindDirectChildTmp(m_CancelButton.transform, "Cancel")
                                ?? FindDirectChildTmp(m_CancelButton.transform, "Label");
        }

        private TextMeshProUGUI FindChildTmp(string objectName)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null || t.name != objectName)
                    continue;
                var tmp = t.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                    return tmp;
            }
            return null;
        }

        private static TextMeshProUGUI FindDirectChildTmp(Transform parent, string objectName)
        {
            if (parent == null) return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child == null || child.name != objectName)
                    continue;
                return child.GetComponent<TextMeshProUGUI>();
            }
            return null;
        }
    }
}
