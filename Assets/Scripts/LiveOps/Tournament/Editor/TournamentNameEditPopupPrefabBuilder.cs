#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament.Editor
{
    public static class TournamentNameEditPopupPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/TournamentNameEditPopup.prefab";

        [InitializeOnLoadMethod]
        private static void EnsurePrefabExists()
        {
            if (File.Exists(PrefabPath))
                return;

            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(PrefabPath))
                    BuildPrefab();
            };
        }

        [MenuItem("LiveOps/Tournament/Build Name Edit Popup Prefab")]
        public static void BuildPrefab()
        {
            TMP_FontAsset font = null;
            var titleGuids = AssetDatabase.FindAssets("LilitaOne-Regular SDF t:TMP_FontAsset");
            if (titleGuids != null && titleGuids.Length > 0)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(titleGuids[0]));
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            Sprite box = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/NativeBG.png");
            Sprite button = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/HomeScreen/ButtonGreen.png");

            var view = TournamentNameEditPopupFactory.Create(null, font, box, button);
            GameObject go = view.gameObject;
            go.SetActive(true);

            var so = new SerializedObject(view);
            so.FindProperty("m_Dim").objectReferenceValue = view.Dim;
            so.FindProperty("m_Box").objectReferenceValue = view.Box;
            so.FindProperty("m_Title").objectReferenceValue = view.Title;
            so.FindProperty("m_TitleBg").objectReferenceValue = view.TitleBg;
            so.FindProperty("m_Input").objectReferenceValue = view.Input;
            so.FindProperty("m_InputText").objectReferenceValue = view.InputTextComponent;
            so.FindProperty("m_InputTextBg").objectReferenceValue = view.InputTextBg;
            so.FindProperty("m_Placeholder").objectReferenceValue = view.Placeholder;
            so.FindProperty("m_PlaceholderBg").objectReferenceValue = view.PlaceholderBg;
            so.FindProperty("m_Error").objectReferenceValue = view.Error;
            so.FindProperty("m_ErrorBg").objectReferenceValue = view.ErrorBg;
            so.FindProperty("m_SaveButton").objectReferenceValue = view.SaveButton;
            so.FindProperty("m_SaveLabel").objectReferenceValue = view.SaveLabel;
            so.FindProperty("m_SaveLabelBg").objectReferenceValue = view.SaveLabelBg;
            so.FindProperty("m_CancelButton").objectReferenceValue = view.CancelButton;
            so.FindProperty("m_CancelLabel").objectReferenceValue = view.CancelLabel;
            so.FindProperty("m_CancelLabelBg").objectReferenceValue = view.CancelLabelBg;
            so.ApplyModifiedPropertiesWithoutUndo();

            string folder = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Tournament] Saved name-edit prefab to {PrefabPath}. Edit look there, then play.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
