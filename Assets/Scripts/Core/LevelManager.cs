using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Core
{
    public class LevelManager : MonoBehaviour
    {
        public TextAsset levelJsonFile; // Drag JSON here
        public ArrowController arrowPrefab; // Drag Prefab here

        private List<GameObject> currentLevelObjects = new List<GameObject>();
        private string currentLevelId;

        // Start removed to prevent auto-loading. Level is loaded via GameManager.StartLevel.

        public void LoadLevelFromResources(string levelId)
        {
            currentLevelId = levelId;
            TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{levelId}");
            if (jsonFile != null)
            {
                ClearLevel();
                LoadLevel(jsonFile.text);
            }
            else
            {
                Debug.LogError($"Level with ID {levelId} not found in Resources.");
            }
        }

        public void ClearLevel()
        {
            foreach (GameObject obj in currentLevelObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            currentLevelObjects.Clear();
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json)
        {
            LevelData data = JsonUtility.FromJson<LevelData>(json);
            
            // Initialize Grid
            GridManager.Instance.InitializeGrid(data.gridSize.ToVector2Int());
            
            List<ArrowController> arrows = new List<ArrowController>();
            foreach (ArrowData arrowData in data.arrows)
            {
                ArrowController arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
                arrow.PrepareIncrementalInit(arrowData);
                currentLevelObjects.Add(arrow.gameObject);
                arrows.Add(arrow);
            }

            StartCoroutine(CoordinatedLevelInitialization(arrows, data));
        }

        private System.Collections.IEnumerator CoordinatedLevelInitialization(List<ArrowController> arrows, LevelData data)
        {
            // 1. Start Camera Animation
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetBounds(data.gridSize.ToVector2Int());
                CameraController.Instance.PlayInitializationZoomAnimation(data.gridSize.ToVector2Int());
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayLevelInitialized();
            }

            // 2. Animate Arrow Growth
            // Spread growth over ~1.2 seconds (during the camera "wait" phase)
            int maxPath = 0;
            foreach (var arrow in data.arrows) maxPath = Mathf.Max(maxPath, arrow.path.Count);

            float totalGrowthTime = 1.0f; // Finish slightly before zoom-in starts
            float stepDelay = totalGrowthTime / Mathf.Max(1, maxPath);

            for (int i = 0; i < maxPath; i++)
            {
                foreach (var arrow in arrows)
                {
                    arrow.UpdateGrowthSlide(i);
                }
                yield return new WaitForSeconds(stepDelay);
            }
        }
        public void RestartLevel()
        {
            ClearLevel();
            LoadLevelFromResources(currentLevelId);
        }
    }
}
