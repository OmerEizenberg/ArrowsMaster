#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Assets.Scripts.LiveOps.Tournament.Editor
{
    /// <summary>
    /// Bakes tournament table chrome into TournamentLeaderboardPopup.prefab and strips
    /// leftover Daily Missions children so runtime never rebuilds layout.
    /// </summary>
    public static class TournamentLeaderboardTablePrefabBuilder
    {
        private const string PopupPrefabPath = "Assets/Resources/TournamentLeaderboardPopup.prefab";

        private static readonly HashSet<string> KeepUnderPopup = new HashSet<string>
        {
            "Title", "Description", "GreenShadow", "LeaderboardScroll", "ColumnHeaders", "PurpleBG"
        };

        [InitializeOnLoadMethod]
        private static void EnsureBaked()
        {
            EditorApplication.delayCall += () =>
            {
                if (Application.isPlaying) return;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
                if (prefab == null) return;
                var view = prefab.GetComponent<TournamentLeaderboardPopupView>();
                if (view == null) return;

                var so = new SerializedObject(view);
                if (so.FindProperty("m_ColumnHeadersRoot").objectReferenceValue == null
                    || so.FindProperty("m_RowsParent").objectReferenceValue == null)
                {
                    BakeIntoPopupPrefab();
                }
            };
        }

        [MenuItem("LiveOps/Tournament/Bake Table + Column Headers Into Leaderboard Popup")]
        public static void BakeIntoPopupPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(PopupPrefabPath);
            if (root == null)
            {
                Debug.LogError($"[Tournament] Could not open {PopupPrefabPath}");
                return;
            }

            try
            {
                var view = root.GetComponent<TournamentLeaderboardPopupView>();
                if (view == null)
                    view = root.AddComponent<TournamentLeaderboardPopupView>();

                Transform popup = FindDeep(root.transform, "Popup");
                if (popup == null)
                {
                    Debug.LogError("[Tournament] Popup child missing on TournamentLeaderboardPopup.");
                    return;
                }

                // Strip Daily Missions leftovers from the prefab itself.
                StripMissionLeftovers(popup);

                TMP_FontAsset font = FindTitleFont(root) ?? TMP_Settings.defaultFontAsset;
                Sprite bg = null;
                var popupImg = popup.GetComponent<Image>();
                if (popupImg != null)
                    bg = popupImg.sprite;

                EnsurePurpleTitleBar(popup, bg);

                Transform oldHeaders = popup.Find("ColumnHeaders");
                if (oldHeaders != null)
                    Object.DestroyImmediate(oldHeaders.gameObject);
                Transform oldScroll = popup.Find("LeaderboardScroll");
                if (oldScroll != null)
                    Object.DestroyImmediate(oldScroll.gameObject);

                var built = TournamentLeaderboardTableFactory.Create(popup, font, bg);

                // Static copy in the prefab (runtime may still refresh timer text).
                var title = FindPopupTitle(popup);
                if (title != null)
                    title.text = "Golden Tournament";

                var description = FindDeep(popup, "Description");
                TextMeshProUGUI timer = description != null ? description.GetComponent<TextMeshProUGUI>() : null;
                if (timer != null)
                    timer.text = "Don't give up\n— left";

                var close = FindDeep(popup, "GreenShadow");
                Button closeButton = close != null ? close.GetComponent<Button>() : null;
                if (closeButton != null)
                {
                    foreach (var label in closeButton.GetComponentsInChildren<TextMeshProUGUI>(true))
                    {
                        if (label != null)
                            label.text = "Let's Go!";
                    }
                }

                var rowPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TournamentLeaderboardRow.prefab");
                var nameEditPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/TournamentNameEditPopup.prefab");

                var so = new SerializedObject(view);
                so.FindProperty("m_CloseButton").objectReferenceValue = closeButton;
                so.FindProperty("m_Title").objectReferenceValue = title;
                so.FindProperty("m_TimerText").objectReferenceValue = timer;
                so.FindProperty("m_TableRoot").objectReferenceValue = built.ScrollRect.transform;
                so.FindProperty("m_ScrollRect").objectReferenceValue = built.ScrollRect;
                so.FindProperty("m_TableBackground").objectReferenceValue = built.TableBackground;
                so.FindProperty("m_RowsParent").objectReferenceValue = built.RowsParent;
                so.FindProperty("m_ColumnHeadersRoot").objectReferenceValue = built.ColumnHeadersRoot;
                so.FindProperty("m_PlaceHeaderText").objectReferenceValue = built.PlaceHeader;
                so.FindProperty("m_PlaceHeaderTextBg").objectReferenceValue = built.PlaceHeaderBg;
                so.FindProperty("m_NameHeaderText").objectReferenceValue = built.NameHeader;
                so.FindProperty("m_NameHeaderTextBg").objectReferenceValue = built.NameHeaderBg;
                so.FindProperty("m_RewardHeaderText").objectReferenceValue = built.RewardHeader;
                so.FindProperty("m_RewardHeaderTextBg").objectReferenceValue = built.RewardHeaderBg;
                so.FindProperty("m_ScoreHeaderText").objectReferenceValue = built.ScoreHeader;
                so.FindProperty("m_ScoreHeaderTextBg").objectReferenceValue = built.ScoreHeaderBg;

                if (rowPrefab != null)
                    so.FindProperty("m_RowPrefab").objectReferenceValue = rowPrefab.GetComponent<TournamentLeaderboardRowView>();
                if (nameEditPrefab != null)
                    so.FindProperty("m_NameEditPrefab").objectReferenceValue = nameEditPrefab.GetComponent<TournamentNameEditPopupView>();

                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PopupPrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("[Tournament] Prefab baked: ColumnHeaders + LeaderboardScroll + cleaned mission leftovers. Edit layout on TournamentLeaderboardPopup.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPrefabPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void StripMissionLeftovers(Transform popup)
        {
            for (int i = popup.childCount - 1; i >= 0; i--)
            {
                Transform child = popup.GetChild(i);
                if (child == null) continue;
                if (KeepUnderPopup.Contains(child.name))
                    continue;
                Object.DestroyImmediate(child.gameObject);
            }

            foreach (var slot in popup.GetComponentsInChildren<Assets.Scripts.LiveOps.Missions.MissionSlotView>(true))
            {
                if (slot != null)
                    Object.DestroyImmediate(slot.gameObject);
            }
            foreach (var holder in popup.GetComponentsInChildren<Assets.Scripts.LiveOps.Missions.MissionsHolderView>(true))
            {
                if (holder != null)
                    Object.DestroyImmediate(holder.gameObject);
            }
        }

        private static void EnsurePurpleTitleBar(Transform popup, Sprite bg)
        {
            Transform existing = popup.Find("PurpleBG");
            GameObject purpleGo;
            if (existing != null)
            {
                purpleGo = existing.gameObject;
                purpleGo.SetActive(true);
            }
            else
            {
                purpleGo = new GameObject("PurpleBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                purpleGo.transform.SetParent(popup, false);
                purpleGo.transform.SetAsFirstSibling();

                var rt = purpleGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(0f, 280f);

                var img = purpleGo.GetComponent<Image>();
                img.sprite = bg;
                img.type = Image.Type.Sliced;
                img.color = new Color(0.49019608f, 0.37254903f, 1f, 1f);
                img.raycastTarget = false;
            }
        }

        private static TMP_FontAsset FindTitleFont(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t != null && t.gameObject.name == "Title" && t.font != null)
                    return t.font;
            }
            return null;
        }

        private static TextMeshProUGUI FindPopupTitle(Transform popup)
        {
            foreach (var t in popup.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t != null && t.gameObject.name == "Title" && t.transform.parent == popup)
                    return t;
            }
            foreach (var t in popup.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t != null && t.gameObject.name == "Title")
                    return t;
            }
            return null;
        }

        private static Transform FindDeep(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    return t;
            }
            return null;
        }
    }
}
#endif
