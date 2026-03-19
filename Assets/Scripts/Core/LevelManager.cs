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

        private List<ArrowController> arrows = new List<ArrowController>();
        private List<GameObject> currentLevelObjects = new List<GameObject>();
        private List<SpriteRenderer> m_BackgroundCircles = new List<SpriteRenderer>();
        public event System.Action OnEntranceAnimationFinished;
        public event System.Action OnEntranceAnimationStarted;
        public string CurrentLevelId => currentLevelId;
        private string currentLevelId;
        private HashSet<Vector2Int> m_SpawnedCirclePositions = new HashSet<Vector2Int>();
        private Vector3 m_LevelCenter;
        private Vector2Int m_CurrentGridSize;
        private float m_WinCirclesAlpha = 1.0f;
        private int m_TotalPointsInLevel;
        public int TotalPointsInLevel => m_TotalPointsInLevel;

        // Cached yield instructions — avoids per-frame allocation
        private static readonly WaitForSeconds s_GrowthWait = new WaitForSeconds(0.04f);

        // Start removed to prevent auto-loading. Level is loaded via GameManager.StartLevel.

        private int m_MaxLevelIndex = -1;

        private void Awake()
        {
            InitializeLevelCount();
        }

        public void InitializeLevelCount()
        {
            // Only initialize if not already done
            if (m_MaxLevelIndex != -1) return;

            int i = 1;
            while (true)
            {
                // lightly check if file exists by trying to load it
                // We stop when we don't find the next level
                string levelName = $"Levels/Level{i}";
                TextAsset levelAsset = Resources.Load<TextAsset>(levelName);
                if (levelAsset == null)
                {
                    m_MaxLevelIndex = i - 1;
                    break;
                }
                // Unload immediately to save memory during check if needed, 
                // though Resources.Load caches it anyway. 
                // Resources.UnloadAsset(levelAsset); 
                i++;
            }
            Debug.Log($"[LevelManager] Max Level Index initialized to: {m_MaxLevelIndex}");
        }

        public TextAsset GetLevelTextAsset(string levelId)
        {
            if (m_MaxLevelIndex == -1) InitializeLevelCount();

            TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{levelId}");
            if (jsonFile == null)
            {
                int currentIdx = ExtractNumber(levelId);
                if (currentIdx != -1 && m_MaxLevelIndex > 0)
                {
                    // Loop Logic:
                    // We assume levels 1-10 are tutorials/intro and we want to loop from Level 11 to Max.
                    // If Max is small (<11), we loop from 1.
                    
                    int startLoopIdx = 175;
                    if (m_MaxLevelIndex < startLoopIdx) startLoopIdx = 1;

                    // Standard 0-indexed modulo arithmetic mapped to our range [startLoopIdx, m_MaxLevelIndex]
                    // Range size
                    int range = m_MaxLevelIndex - startLoopIdx + 1;

                    // Avoid division by zero
                    if (range > 0)
                    {
                        // We offset currentIdx so that startLoopIdx corresponds to 0 in modulo space
                        // But we must handle that currentIdx > m_MaxLevelIndex.
                        // Actually, just mapping (currentIdx - startLoopIdx) % range + startLoopIdx 
                        // works for any currentIdx >= startLoopIdx.
                        
                        int tempIdx = currentIdx - startLoopIdx;
                        // Handle potential negative result if currentIdx < startLoopIdx (unlikely since file missing means > max)
                        // but strictly speaking modulo in C# retains sign.
                         int newIdx = (tempIdx % range) + startLoopIdx;
                         
                        string newLevelId = "Level" + newIdx;
                        jsonFile = Resources.Load<TextAsset>($"Levels/{newLevelId}");
                    }
                }
            }
            return jsonFile;
        }

        public void LoadLevelFromResources(string levelId, List<int> pickedArrows = null)
        {
            currentLevelId = levelId;
            TextAsset jsonFile = GetLevelTextAsset(levelId);

            if (jsonFile != null)
            {
                ClearLevel();
                LoadLevel(jsonFile.text, pickedArrows);
            }
            else
            {
                Debug.LogError($"Level with ID {levelId} not found in Resources (Max Level: {m_MaxLevelIndex}).");
            }
        }

        private int ExtractNumber(string input)
        {
            if (string.IsNullOrEmpty(input)) return -1;
            string b = "";
            foreach (char c in input)
            {
                if (char.IsDigit(c)) b += c;
            }
            if (int.TryParse(b, out int result)) return result;
            return -1;
        }

          public void LoadChallengeLevelFromResources(string levelId, List<int> pickedArrows = null)
        {
            currentLevelId = levelId;
            TextAsset jsonFile = Resources.Load<TextAsset>($"ChallengeLevels/{levelId}");
            if (jsonFile != null)
            {
                ClearLevel();
                LoadLevel(jsonFile.text, pickedArrows);
            }
            else
            {
                Debug.LogError($"Level with ID {levelId} not found in Resources.");
            }
        }

        public void ClearLevel()
        {
            // Stop all running coroutines (including win animations) before clearing
            StopAllCoroutines();
            
            foreach (GameObject obj in currentLevelObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            currentLevelObjects.Clear();
            m_BackgroundCircles.Clear();
            m_BackgroundCircleInfos.Clear();
            m_SpawnedCirclePositions.Clear();
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json, List<int> pickedArrows = null)
        {
            LevelData data = JsonUtility.FromJson<LevelData>(json);
            // Initialize timer if level has duration
            if (GameManager.Instance != null && data.duration > 0)
            {
                GameManager.Instance.InitializeTimer(data.duration);
            }
            // Initialize Grid
            m_CurrentGridSize = data.gridSize.ToVector2Int();
            GridManager.Instance.InitializeGrid(m_CurrentGridSize);

            // Initialize Circles Tracking
            m_SpawnedCirclePositions.Clear();

            m_TotalPointsInLevel = 0;
            if (data.arrows != null)
            {
                foreach (var arrow in data.arrows)
                {
                    if (arrow.path != null) m_TotalPointsInLevel += arrow.path.Count;
                }
            }

            arrows = new List<ArrowController>();
            foreach (ArrowData arrowData in data.arrows)
            {
                if (pickedArrows != null && pickedArrows.Contains(arrowData.id)) continue;

                ArrowController arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
                arrow.PrepareIncrementalInit(arrowData);
                currentLevelObjects.Add(arrow.gameObject);
                arrows.Add(arrow);
            }
            
          

            StartCoroutine(CoordinatedLevelInitialization(arrows, data));
        }

        private struct BackgroundCircleInfo
        {
            public SpriteRenderer renderer;
            public Transform transform;
            public float distanceFromCenter;
        }
        private List<BackgroundCircleInfo> m_BackgroundCircleInfos = new List<BackgroundCircleInfo>();

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

            // 1. Set camera to zoomed in position (half max zoom)
            if (CameraController.Instance != null)
            {
                yield return StartCoroutine(CameraController.Instance.PlayInitializationZoomAnimation(data.gridSize.ToVector2Int(), levelCenter));
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayLevelInitialized();
            }

            // 2. Animate Zoom and Arrow Growth in parallel
            int maxPath = 0;
            foreach (var arrow in data.arrows) maxPath = Mathf.Max(maxPath, arrow.path.Count);
            float growthDuration = maxPath * 0.04f;

            Coroutine zoomCoroutine = null;
            if (CameraController.Instance != null)
            {
                OnEntranceAnimationStarted?.Invoke();
                // Start camera zoom-out in parallel with growth
                zoomCoroutine = StartCoroutine(CameraController.Instance.AnimateToDefaultZoom(levelCenter, growthDuration));
            }

            for (int i = 0; i < maxPath; i++)
            {
                foreach (var arrow in arrows)
                {
                    StartCoroutine(arrow.UpdateGrowthSlide(i, 0.04f));
                }
                yield return s_GrowthWait;
            }

            // 3. Ensure camera finishing zoom before spawning background circles
            if (zoomCoroutine != null) yield return zoomCoroutine;

            // 4. Spawn Background Circles AFTER animation
            m_BackgroundCircleInfos.Clear();
            int spawnCount = 0;
            foreach (var arrowData in data.arrows)
            {
                foreach (var pathPoint in arrowData.path)
                {
                    Vector2Int pos = pathPoint.ToVector2Int();
                    if (!m_SpawnedCirclePositions.Contains(pos))
                    {
                        GameObject circleObj = new GameObject($"Circle_{pos.x}_{pos.y}");
                        circleObj.transform.position = new Vector3(pos.x * ArrowController.CellSize, pos.y * ArrowController.CellSize, 0);
                        circleObj.transform.SetParent(this.transform);

                        SpriteRenderer sr = circleObj.AddComponent<SpriteRenderer>();
                        sr.sprite = m_CircleSprite;
                        sr.color = m_CircleColor;
                        sr.sortingOrder = -1; 

                        m_BackgroundCircleInfos.Add(new BackgroundCircleInfo {
                            renderer = sr,
                            transform = circleObj.transform,
                            distanceFromCenter = Vector3.Distance(circleObj.transform.position, m_LevelCenter)
                        });
                        
                        m_SpawnedCirclePositions.Add(pos);
                        currentLevelObjects.Add(circleObj);

                        spawnCount++;
                        // OPTIMIZED: Yield every 12 circles instead of 50 for smoother initialization
                        if (spawnCount % 12 == 0) yield return null;
                    }
                }
            }

            OnEntranceAnimationFinished?.Invoke();
        }

        public void HideArrows()
        {
            foreach (ArrowController AC in arrows)
            {
                if(AC != null)
                {
                    AC.gameObject.SetActive(false);
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
            m_WinCirclesAlpha = 1.0f;
            if (CameraController.Instance != null)
            {
                CameraController.Instance.PlayWinZoomAnimation(m_CurrentGridSize, m_LevelCenter);
            }

            foreach (var arrow in arrows)
            {
                if (arrow != null)
                {
                    arrow.StartWinScaleAnimation(0.6f, 0.33f); // Scale by 50% over 0.33s (camera zoom duration)
                }
            }

            StartCoroutine(DoRippleEffect());
            StartCoroutine(FadeOutCircles(3.0f));
        }

        private System.Collections.IEnumerator FadeOutCircles(float delay)
        {
            yield return new WaitForSeconds(delay);
            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                m_WinCirclesAlpha = Mathf.Lerp(1.0f, 0f, elapsed / duration);
                yield return null;
            }
            m_WinCirclesAlpha = 0f;
        }

        private System.Collections.IEnumerator DoRippleEffect()
        {
            if (m_BackgroundCircleInfos == null || m_BackgroundCircleInfos.Count == 0) yield break;

            Color targetColor = new Color(0.373f, 0.153f, 0.804f); // #5f27cd
            float rippleSpeed = 4.0f; 
            float maxDist = 0;
            
            foreach(var info in m_BackgroundCircleInfos)
            {
                if (info.distanceFromCenter > maxDist) maxDist = info.distanceFromCenter;
            }

            for (int repeat = 0; repeat < 2; repeat++)
            {
                float duration = (maxDist / rippleSpeed) + 0.5f; 
                float elapsed = 0;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float waveFront = elapsed * rippleSpeed;

                    foreach (var info in m_BackgroundCircleInfos)
                    {
                        // Check if objects still exist
                        if (info.transform == null || info.renderer == null) continue;

                        float dist = info.distanceFromCenter;
                        float proximity = Mathf.Clamp01(1.0f - Mathf.Abs(dist - waveFront) / 2.0f);
                        
                        if (proximity > 0)
                        {
                            float scale = 1.0f;
                            if (proximity > 0.5f)
                                scale = Mathf.Lerp(1.0f, 1.3f, (proximity - 0.5f) * 2f);
                            else
                                scale = Mathf.Lerp(0.5f, 1.0f, proximity * 2f);

                            info.transform.localScale = Vector3.one * scale;
                            Color c = Color.Lerp(m_CircleColor, targetColor, proximity);
                            //c.a *= m_WinCirclesAlpha;
                            info.renderer.color = c;
                        }
                        else if (waveFront > dist)
                        {
                            info.transform.localScale = Vector3.one;
                            Color c = m_CircleColor;
                            //c.a *= m_WinCirclesAlpha;
                            info.renderer.color = c;
                        }
                    }
                    yield return null;
                }
                if (repeat == 0) yield return new WaitForSeconds(0.2f);
            }

            foreach (var info in m_BackgroundCircleInfos)
            {
                if (info.renderer != null && info.transform != null)
                {
                    info.transform.localScale = Vector3.one;
                    Color c = m_CircleColor;
                    c.a = m_WinCirclesAlpha;
                    info.renderer.color = c;
                }
            }
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }
    }
}
