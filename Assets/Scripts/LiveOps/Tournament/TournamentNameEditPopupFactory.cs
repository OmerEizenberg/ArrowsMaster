using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// Builds the name-edit overlay hierarchy for the editor prefab generator
    /// and as a runtime fallback when the prefab is missing.
    /// </summary>
    public static class TournamentNameEditPopupFactory
    {
        public static TournamentNameEditPopupView Create(
            Transform parent,
            TMP_FontAsset font,
            Sprite boxSprite,
            Sprite buttonSprite = null)
        {
            var root = new GameObject("TournamentNameEditPopup", typeof(RectTransform), typeof(Image));
            if (parent != null)
                root.transform.SetParent(parent, false);

            var rootRect = root.GetComponent<RectTransform>();
            Stretch(rootRect);

            var dim = root.GetComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.75f);
            dim.raycastTarget = true;

            var view = root.AddComponent<TournamentNameEditPopupView>();
            view.Dim = dim;

            var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
            box.transform.SetParent(root.transform, false);
            var boxRect = box.GetComponent<RectTransform>();
            boxRect.anchorMin = boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(700f, 420f);
            var boxImg = box.GetComponent<Image>();
            boxImg.sprite = boxSprite;
            boxImg.type = Image.Type.Sliced;
            boxImg.color = Color.white;
            view.Box = boxImg;

            view.TitleBg = CreateTmp(box.transform, "TitleBG", "Edit your name", 40f,
                new Vector2(0f, 137f), new Vector2(640f, 50f), font, new Color(0f, 0f, 0f, 0.55f));
            view.Title = CreateTmp(box.transform, "Title", "Edit your name", 40f,
                new Vector2(0f, 140f), new Vector2(640f, 50f), font, Color.white);

            var inputGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            inputGo.transform.SetParent(box.transform, false);
            var inputRect = inputGo.GetComponent<RectTransform>();
            inputRect.anchorMin = inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.sizeDelta = new Vector2(560f, 80f);
            inputRect.anchoredPosition = new Vector2(0f, 30f);
            var inputBg = inputGo.GetComponent<Image>();
            inputBg.color = new Color(0.15f, 0.15f, 0.2f, 1f);

            var textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
            textArea.transform.SetParent(inputGo.transform, false);
            var taRect = textArea.GetComponent<RectTransform>();
            Stretch(taRect);
            taRect.offsetMin = new Vector2(16f, 8f);
            taRect.offsetMax = new Vector2(-16f, -8f);

            var textBgGo = new GameObject("TextBG", typeof(RectTransform));
            textBgGo.transform.SetParent(textArea.transform, false);
            Stretch(textBgGo.GetComponent<RectTransform>());
            textBgGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -2f);
            var textBg = textBgGo.AddComponent<TextMeshProUGUI>();
            textBg.font = font != null ? font : TMP_Settings.defaultFontAsset;
            textBg.fontSize = 36f;
            textBg.color = new Color(0f, 0f, 0f, 0.55f);
            textBg.alignment = TextAlignmentOptions.MidlineLeft;
            textBg.raycastTarget = false;
            view.InputTextBg = textBg;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(textArea.transform, false);
            Stretch(textGo.GetComponent<RectTransform>());
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = textBg.font;
            text.fontSize = 36f;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            view.InputTextComponent = text;

            var phBgGo = new GameObject("PlaceholderBG", typeof(RectTransform));
            phBgGo.transform.SetParent(textArea.transform, false);
            Stretch(phBgGo.GetComponent<RectTransform>());
            phBgGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -2f);
            var placeholderBg = phBgGo.AddComponent<TextMeshProUGUI>();
            placeholderBg.font = text.font;
            placeholderBg.fontSize = 36f;
            placeholderBg.fontStyle = FontStyles.Italic;
            placeholderBg.color = new Color(0f, 0f, 0f, 0.35f);
            placeholderBg.text = "Your name";
            placeholderBg.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderBg.raycastTarget = false;
            view.PlaceholderBg = placeholderBg;

            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textArea.transform, false);
            Stretch(placeholderGo.GetComponent<RectTransform>());
            var placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholder.font = text.font;
            placeholder.fontSize = 36f;
            placeholder.fontStyle = FontStyles.Italic;
            placeholder.color = new Color(1f, 1f, 1f, 0.35f);
            placeholder.text = "Your name";
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            view.Placeholder = placeholder;

            var input = inputGo.GetComponent<TMP_InputField>();
            input.textViewport = taRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = TournamentNameFilter.MaxLength;
            view.Input = input;

            view.ErrorBg = CreateTmp(box.transform, "ErrorBG", "", 26f,
                new Vector2(0f, -53f), new Vector2(640f, 40f), font, new Color(0f, 0f, 0f, 0.55f));
            view.Error = CreateTmp(box.transform, "Error", "", 26f,
                new Vector2(0f, -50f), new Vector2(640f, 40f), font, new Color(1f, 0.45f, 0.45f, 1f));

            view.SaveButton = CreateButton(box.transform, "Save", "SAVE",
                new Vector2(-140f, -140f), font, buttonSprite, out var saveLabel, out var saveLabelBg);
            view.SaveLabel = saveLabel;
            view.SaveLabelBg = saveLabelBg;
            view.CancelButton = CreateButton(box.transform, "Cancel", "CANCEL",
                new Vector2(140f, -140f), font, buttonSprite, out var cancelLabel, out var cancelLabelBg);
            view.CancelLabel = cancelLabel;
            view.CancelLabelBg = cancelLabelBg;

            root.SetActive(false);
            return view;
        }

        private static Button CreateButton(
            Transform parent,
            string objectName,
            string label,
            Vector2 pos,
            TMP_FontAsset font,
            Sprite sprite,
            out TextMeshProUGUI labelText,
            out TextMeshProUGUI labelBg)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(220f, 80f);

            var img = go.GetComponent<Image>();
            if (sprite != null)
            {
                img.sprite = sprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
            else
            {
                img.color = new Color(0.35f, 0.75f, 0.35f, 1f);
            }

            labelBg = CreateTmp(go.transform, objectName + "BG", label, 32f,
                new Vector2(0f, -3f), new Vector2(200f, 70f), font, new Color(0f, 0f, 0f, 0.55f));
            labelText = CreateTmp(go.transform, objectName, label, 32f,
                Vector2.zero, new Vector2(200f, 70f), font, Color.white);
            return go.GetComponent<Button>();
        }

        private static TextMeshProUGUI CreateTmp(
            Transform parent,
            string objectName,
            string value,
            float size,
            Vector2 pos,
            Vector2 sizeDelta,
            TMP_FontAsset font,
            Color color)
        {
            var go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = sizeDelta;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : TMP_Settings.defaultFontAsset;
            tmp.text = value;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = color;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
