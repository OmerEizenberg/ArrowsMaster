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
        private SpriteRenderer[,] m_BackgroundCircles;

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
            m_BackgroundCircles = null;
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json)
        {
            LevelData data = JsonUtility.FromJson<LevelData>(json);
            
            // Initialize Grid
            GridManager.Instance.InitializeGrid(data.gridSize.ToVector2Int());

            // Initialize Circles Array
            m_BackgroundCircles = new SpriteRenderer[data.gridSize.x, data.gridSize.y];

            List<ArrowController> arrows = new List<ArrowController>();
            foreach (ArrowData arrowData in data.arrows)
            {
                ArrowController arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
                arrow.PrepareIncrementalInit(arrowData);
                currentLevelObjects.Add(arrow.gameObject);
                arrows.Add(arrow);

                // Spawn Background Circles for this arrow's path
                foreach (var pathPoint in arrowData.path)
                {
                    Vector2Int pos = pathPoint.ToVector2Int();
                    if (pos.x >= 0 && pos.x < data.gridSize.x && pos.y >= 0 && pos.y < data.gridSize.y)
                    {
                        if (m_BackgroundCircles[pos.x, pos.y] == null)
                        {
                            GameObject circleObj = new GameObject($"Circle_{pos.x}_{pos.y}");
                            circleObj.transform.position = new Vector3(pos.x * ArrowController.CellSize, pos.y * ArrowController.CellSize, 0);
                            circleObj.transform.SetParent(this.transform);
                            
                            SpriteRenderer sr = circleObj.AddComponent<SpriteRenderer>();
                            sr.sprite = GameManager.Instance.m_CircleSprite;
                            sr.color = GameManager.Instance.m_CircleColor;
                            sr.sortingOrder = -1; // Behind arrows

                            m_BackgroundCircles[pos.x, pos.y] = sr;
                            currentLevelObjects.Add(circleObj);
                        }
                    }
                }
            }

            StartCoroutine(CoordinatedLevelInitialization(arrows, data));
        }

        private System.Collections.IEnumerator CoordinatedLevelInitialization(List<ArrowController> arrows, LevelData data)
        {
            // 1. Calculate Average Head Position for Camera Focus
            Vector3 avgHeadPos = Vector3.zero;
            if (data.arrows != null && data.arrows.Count > 0)
            {
                foreach (var arrowData in data.arrows)
                {
                    if (arrowData.path != null && arrowData.path.Count > 0)
                    {
                        var headPos = arrowData.path[arrowData.path.Count - 1].ToVector2Int();
                        avgHeadPos += new Vector3(headPos.x * ArrowController.CellSize, headPos.y * ArrowController.CellSize, 0);
                    }
                }
                avgHeadPos /= data.arrows.Count;
            }

            // 2. Start Camera Animation
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetBounds(data.gridSize.ToVector2Int());
                CameraController.Instance.PlayInitializationZoomAnimation(data.gridSize.ToVector2Int(), avgHeadPos);
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
                yield return new WaitForSeconds(0.06f);

                //yield return new WaitForSeconds(stepDelay);
            }
        }
        public void RestartLevel()
        {
            ClearLevel();
            LoadLevelFromResources(currentLevelId);
        }
    }
}
