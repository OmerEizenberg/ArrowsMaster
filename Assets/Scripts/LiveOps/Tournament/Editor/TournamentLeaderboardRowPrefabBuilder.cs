#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament.Editor
{
    public static class TournamentLeaderboardRowPrefabBuilder
    {
        private const string PrefabPath = "Assets/Resources/TournamentLeaderboardRow.prefab";

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

        [MenuItem("LiveOps/Tournament/Build Leaderboard Row Prefab")]
        public static void BuildPrefab()
        {
            TMP_FontAsset font = null;
            var titleGuids = AssetDatabase.FindAssets("LilitaOne-Regular SDF t:TMP_FontAsset");
            if (titleGuids != null && titleGuids.Length > 0)
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(titleGuids[0]));
            if (font == null)
                font = TMP_Settings.defaultFontAsset;

            Sprite bg = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/NativeBG.png");

            var view = TournamentLeaderboardRowFactory.Create(null, font, bg);
            GameObject go = view.gameObject;

            var so = new SerializedObject(view);
            so.FindProperty("m_Background").objectReferenceValue = view.Background;
            so.FindProperty("m_Button").objectReferenceValue = view.Button;
            so.FindProperty("m_PlaceText").objectReferenceValue = view.PlaceText;
            so.FindProperty("m_PlaceTextBg").objectReferenceValue = view.PlaceTextBg;
            so.FindProperty("m_NameText").objectReferenceValue = view.NameText;
            so.FindProperty("m_NameTextBg").objectReferenceValue = view.NameTextBg;
            so.FindProperty("m_RewardIcon").objectReferenceValue = view.RewardIcon;
            so.FindProperty("m_RewardAmountText").objectReferenceValue = view.RewardAmountText;
            so.FindProperty("m_RewardAmountTextBg").objectReferenceValue = view.RewardAmountTextBg;
            so.FindProperty("m_RewardRoot").objectReferenceValue = view.RewardRoot;
            so.FindProperty("m_ScoreText").objectReferenceValue = view.ScoreText;
            so.FindProperty("m_ScoreTextBg").objectReferenceValue = view.ScoreTextBg;
            so.FindProperty("m_PlaceBg").objectReferenceValue = view.PlaceBg;
            so.FindProperty("m_NameBg").objectReferenceValue = view.NameBg;
            so.FindProperty("m_RewardBg").objectReferenceValue = view.RewardBg;
            so.FindProperty("m_ScoreBg").objectReferenceValue = view.ScoreBg;
            so.FindProperty("m_NameLayout").objectReferenceValue = view.NameLayout;
            so.FindProperty("m_RewardLayout").objectReferenceValue = view.RewardLayout;
            so.ApplyModifiedPropertiesWithoutUndo();

            string folder = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Tournament] Saved row prefab to {PrefabPath}. Edit look there, then play.");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
