using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Core
{
    public class LevelManager : MonoBehaviour
    {
        public TextAsset levelJsonFile; // Drag JSON here
        public ArrowController arrowPrefab; // Drag Prefab here
        public Sprite m_CircleSprite;
        public Color m_CircleColor;

        private List<GameObject> currentLevelObjects = new List<GameObject>();
        private List<SpriteRenderer> m_BackgroundCircles = new List<SpriteRenderer>();
        public string CurrentLevelId => currentLevelId;
        private string currentLevelId;
        private HashSet<Vector2Int> m_SpawnedCirclePositions = new HashSet<Vector2Int>();
        private Vector3 m_LevelCenter;
        private Vector2Int m_CurrentGridSize;

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
          public void LoadChallengeLevelFromResources(string levelId)
        {
            currentLevelId = levelId;
            TextAsset jsonFile = Resources.Load<TextAsset>($"ChallengeLevels/{levelId}");
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
            m_BackgroundCircles.Clear();
            m_SpawnedCirclePositions.Clear();
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json)
        {
            LevelData data = JsonUtility.FromJson<LevelData>(json);
            
            // Initialize Grid
            m_CurrentGridSize = data.gridSize.ToVector2Int();
            GridManager.Instance.InitializeGrid(m_CurrentGridSize);

            // Initialize Circles Tracking
            m_SpawnedCirclePositions.Clear();

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
            // 1. Calculate Bounding Box of all arrows for Camera Focus
            Vector3 levelCenter = Vector3.zero;
            if (data.arrows != null && data.arrows.Count > 0)
            {
                Vector3 minP = new Vector3(float.MaxValue, float.MaxValue, 0);
                Vector3 maxP = new Vector3(float.MinValue, float.MinValue, 0);
                bool hasPoints = false;

                foreach (var arrowData in data.arrows)
                {
                    foreach (var pathPoint in arrowData.path)
                    {
                        Vector2Int p = pathPoint.ToVector2Int();
                        Vector3 worldP = new Vector3(p.x * ArrowController.CellSize, p.y * ArrowController.CellSize, 0);
                        minP = Vector3.Min(minP, worldP);
                        maxP = Vector3.Max(maxP, worldP);
                        hasPoints = true;
                    }
                }
                if (hasPoints) levelCenter = (minP + maxP) / 2f;
            }
            m_LevelCenter = levelCenter;

            // 2. Start Camera Animation
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetBounds(data.gridSize.ToVector2Int());
                CameraController.Instance.PlayInitializationZoomAnimation(data.gridSize.ToVector2Int(), levelCenter);
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

            // 3. Spawn Background Circles AFTER animation
            foreach (var arrowData in data.arrows)
            {
                foreach (var pathPoint in arrowData.path)
                {
                    Vector2Int pos = pathPoint.ToVector2Int();
                    // Removed bounds check to allow circles for all path points
                    if (!m_SpawnedCirclePositions.Contains(pos))
                    {
                        GameObject circleObj = new GameObject($"Circle_{pos.x}_{pos.y}");
                        circleObj.transform.position = new Vector3(pos.x * ArrowController.CellSize, pos.y * ArrowController.CellSize, 0);
                        circleObj.transform.SetParent(this.transform);

                        SpriteRenderer sr = circleObj.AddComponent<SpriteRenderer>();
                        sr.sprite = m_CircleSprite;
                        sr.color = m_CircleColor;
                        sr.sortingOrder = -1; // Behind arrows

                        m_SpawnedCirclePositions.Add(pos);
                        currentLevelObjects.Add(circleObj);
                        m_BackgroundCircles.Add(sr);
                    }
                }
            }
        }
        public void RestartLevel()
        {
            ClearLevel();
            LoadLevelFromResources(currentLevelId);
        }

        public void PlayWinAnimation()
        {
            if (CameraController.Instance != null)
            {
                CameraController.Instance.PlayWinZoomAnimation(m_CurrentGridSize, m_LevelCenter);
            }
            StartCoroutine(DoRippleEffect());
        }

        private System.Collections.IEnumerator DoRippleEffect()
        {
            if (m_BackgroundCircles == null || m_BackgroundCircles.Count == 0) yield break;

            Color targetColor = new Color(0.373f, 0.153f, 0.804f); // #5f27cd
            float rippleSpeed = 4.0f; // Speed of the wave
            float maxDist = 0;
            
            foreach(var sr in m_BackgroundCircles)
            {
                float dist = Vector3.Distance(sr.transform.position, m_LevelCenter);
                if (dist > maxDist) maxDist = dist;
            }

            // Repeat the effect twice
            for (int repeat = 0; repeat < 2; repeat++)
            {
                // We'll use a time-based wave approach
                float duration = (maxDist / rippleSpeed) + 0.5f; // Duration of the whole effect
                float elapsed = 0;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;

                    foreach (var sr in m_BackgroundCircles)
                    {
                        float dist = Vector3.Distance(sr.transform.position, m_LevelCenter);
                        
                        // The "wave" is at dist = elapsed * rippleSpeed
                        // Calculate a local phase 0->1 based on proximity to the wave front
                        float waveFront = elapsed * rippleSpeed;
                        float proximity = Mathf.Clamp01(1.0f - Mathf.Abs(dist - waveFront) / 2.0f);
                        
                        if (proximity > 0)
                        {
                            // Scale: 100% -> 130% -> 50% based on proximity curve
                            float scale = 1.0f;
                            if (proximity > 0.5f) // Scaling up part
                                scale = Mathf.Lerp(1.0f, 1.3f, (proximity - 0.5f) * 2f);
                            else // Scaling down part
                                scale = Mathf.Lerp(0.5f, 1.0f, proximity * 2f);

                            sr.transform.localScale = Vector3.one * scale;
                            sr.color = Color.Lerp(m_CircleColor, targetColor, proximity);
                        }
                        else if (waveFront > dist)
                        {
                            sr.transform.localScale = Vector3.one;
                            sr.color = m_CircleColor;
                        }
                    }
                    yield return null;
                }

                // Brief pause before next ripple if it's the first one
                if (repeat == 0) yield return new WaitForSeconds(0.2f);
            }

            // Final Cleanup/Reset
            foreach (var sr in m_BackgroundCircles)
            {
                if (sr != null)
                {
                    sr.transform.localScale = Vector3.one;
                    sr.color = m_CircleColor;
                }
            }
        }
    }
}
