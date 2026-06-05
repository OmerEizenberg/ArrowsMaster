using System.Collections;
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
        private BackgroundCirclesMesh m_CirclesMesh;
        private readonly List<Vector3> m_CirclePositionBuffer = new List<Vector3>(512);
        private readonly List<CirclePopAnimation> m_ActivePopAnimations = new List<CirclePopAnimation>(128);

        private struct CirclePopAnimation
        {
            public int Index;
            public float Elapsed;
            public float StartScale;
            public Color PopColor;
        }
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
        // Start removed to prevent auto-loading. Level is loaded via GameManager.StartLevel.

        private int m_MaxLevelIndex = -1;

        private void Awake()
        {
            ArrowPoolManager pool = ArrowPoolManager.Instance;
            if (pool == null)
            {
                pool = gameObject.AddComponent<ArrowPoolManager>();
            }
            
            // ALWAYS sync prefabs to handle stale singleton instances from previous scenes
            pool.ArrowPrefab = arrowPrefab;
            if (arrowPrefab != null) pool.SegmentPrefab = arrowPrefab.segmentPrefab;
            
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
                Resources.UnloadAsset(levelAsset);
                i++;
            }
            Debug.Log($"[LevelManager] Max Level Index initialized to: {m_MaxLevelIndex}");
        }

        public TextAsset GetLevelTextAsset(string levelId)
        {
            if (m_MaxLevelIndex == -1) InitializeLevelCount();

            TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{levelId}");
            
            // Case-insensitive fallback
            if (jsonFile == null)
            {
                if (levelId.StartsWith("level")) 
                    jsonFile = Resources.Load<TextAsset>($"Levels/{levelId.Replace("level", "Level")}");
                else if (levelId.StartsWith("Level"))
                    jsonFile = Resources.Load<TextAsset>($"Levels/{levelId.Replace("Level", "level")}");
            }

            if (jsonFile == null)
            {
                int currentIdx = ExtractNumber(levelId);
                if (currentIdx != -1 && m_MaxLevelIndex > 0)
                {
                    // Loop Logic:
                    // We assume levels 1-10 are tutorials/intro and we want to loop from Level 11 to Max.
                    // If Max is small (<11), we loop from 1.
                    
                    int startLoopIdx = 280;
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
                Debug.LogError($"jsonFile NOT FOUND for levelId: {levelId}");
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

        public void ReleaseArrow(ArrowController arrow)
        {
            if (arrow == null) return;
            arrows.Remove(arrow);
        }

        public void ClearLevel()
        {
            if (GameManager.Instance != null) GameManager.Instance.ResetLevelState();
            
            // Stop all running coroutines (including win animations) before clearing
            StopAllCoroutines();
            
            // Snapshot first — ReturnArrow removes from arrows and would break foreach.
            List<ArrowController> arrowsToReturn = new List<ArrowController>(arrows);
            arrows.Clear();

            for (int i = 0; i < arrowsToReturn.Count; i++)
            {
                ArrowController arrow = arrowsToReturn[i];
                if (arrow == null) continue;

                if (ArrowPoolManager.Instance != null)
                {
                    if (!ArrowPoolManager.Instance.IsArrowInPool(arrow))
                    {
                        ArrowPoolManager.Instance.ReturnArrow(arrow);
                    }
                }
                else
                {
                    Destroy(arrow.gameObject);
                }
            }

            if (ArrowPoolManager.Instance != null)
            {
                ArrowPoolManager.Instance.PurgeToBaseline();
            }

            if (m_CirclesMesh != null)
            {
                m_CirclesMesh.Clear();
            }
            m_ActivePopAnimations.Clear();
            m_SpawnedCirclePositions.Clear();
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json, List<int> pickedArrows = null)
        {
            try
            {
                LevelData data = JsonUtility.FromJson<LevelData>(json);
                if (data == null)
                {
                    Debug.LogError("Failed to deserialize LevelData.");
                    return;
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

            // Initialize timer: use JSON duration for challenge levels, compute from points for normal levels when AllLevelsTimer is on
            if (GameManager.Instance != null)
            {
                float timerDuration = data.duration;
                Debug.Log($"[LevelManager] Timer check: dataDuration={data.duration}, totalPoints={m_TotalPointsInLevel}, isProgression={GameManager.Instance.p_isLevelProgression}, allLevelsTimer={GameManager.Instance.IsAllLevelsTimerEnabled}, ptsMul={GameManager.Instance.PointsToSecondsMultiplier}");
                if (timerDuration <= 0 && GameManager.Instance.p_isLevelProgression && GameManager.Instance.IsAllLevelsTimerEnabled)
                {
                    timerDuration = m_TotalPointsInLevel * GameManager.Instance.PointsToSecondsMultiplier;
                    Debug.Log($"[LevelManager] Computed timer from points: {m_TotalPointsInLevel} * {GameManager.Instance.PointsToSecondsMultiplier} = {timerDuration}s");
                }
                if (timerDuration > 0)
                {
                    GameManager.Instance.InitializeTimer(timerDuration);
                }
            }

            HashSet<int> pickedArrowSet = null;
            if (pickedArrows != null && pickedArrows.Count > 0)
            {
                pickedArrowSet = new HashSet<int>(pickedArrows);
            }

            int levelArrowCount = 0;
            int levelPathPoints = 0;
            if (data.arrows != null)
            {
                foreach (ArrowData arrowData in data.arrows)
                {
                    if (pickedArrowSet != null && pickedArrowSet.Contains(arrowData.id)) continue;

                    levelArrowCount++;
                    if (arrowData.path != null) levelPathPoints += arrowData.path.Count;
                }
            }

            if (ArrowPoolManager.Instance != null)
            {
                ArrowPoolManager.Instance.EnsureCapacityForLevel(levelArrowCount, levelPathPoints);
            }

            arrows = new List<ArrowController>();
            foreach (ArrowData arrowData in data.arrows)
            {
                if (pickedArrowSet != null && pickedArrowSet.Contains(arrowData.id)) continue;

                ArrowController arrow;
                if (ArrowPoolManager.Instance != null)
                {
                    arrow = ArrowPoolManager.Instance.GetArrow(Vector3.zero, Quaternion.identity, null);
                }
                else
                {
                    arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
                }
                
                arrow.PrepareIncrementalInit(arrowData);
                arrows.Add(arrow);
            }
            
          

                StartCoroutine(CoordinatedLevelInitialization(arrows, data));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"CRITICAL ERROR during LoadLevel: {e.Message}\n{e.StackTrace}");
            }
        }

        private BackgroundCirclesMesh CirclesMesh
        {
            get
            {
                if (m_CirclesMesh == null)
                {
                    Transform child = transform.Find("BackgroundCirclesMesh");
                    if (child == null)
                    {
                        GameObject go = new GameObject("BackgroundCirclesMesh");
                        go.transform.SetParent(transform, false);
                        m_CirclesMesh = go.AddComponent<BackgroundCirclesMesh>();
                    }
                    else
                    {
                        m_CirclesMesh = child.GetComponent<BackgroundCirclesMesh>();
                        if (m_CirclesMesh == null)
                        {
                            m_CirclesMesh = child.gameObject.AddComponent<BackgroundCirclesMesh>();
                        }
                    }
                }
                return m_CirclesMesh;
            }
        }

        private bool HasBackgroundCircles => m_CirclesMesh != null && m_CirclesMesh.Count > 0;

        private System.Collections.IEnumerator CoordinatedLevelInitialization(List<ArrowController> arrows, LevelData data)
        {
#if UNITY_EDITOR
            Debug.Log($"[DIAGNOSTIC] CoordinatedLevelInitialization started. Arrows: {arrows.Count}");
#endif
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
            else
            {
                Debug.LogError("[DIAGNOSTIC] CameraController.Instance is NULL!");
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlayLevelInitialized();
            }

            // 2. Animate Zoom and Arrow Growth in parallel
            // Keep a constant "cells per second" speed for each arrow:
            // shorter paths finish faster, longer paths take longer.
            // Also slightly speed up the whole entrance sequence.
            const float kEntranceCellDuration = 0.03f; // was 0.04f
            float maxArrowDuration = 0f;
            foreach (var arrow in arrows)
            {
                if (arrow == null || arrow.PathCount <= 0) continue;
                float arrowDuration = arrow.PathCount * kEntranceCellDuration;
                maxArrowDuration = Mathf.Max(maxArrowDuration, arrowDuration);
            }
            float growthDuration = maxArrowDuration;

            Coroutine zoomCoroutine = null;
            if (CameraController.Instance != null)
            {
                OnEntranceAnimationStarted?.Invoke();
                // Start camera zoom-out in parallel with growth
                zoomCoroutine = StartCoroutine(CameraController.Instance.AnimateToDefaultZoom(levelCenter, growthDuration));
            }

            if (DevicePerformanceProfile.UseInstantEntrance)
            {
                foreach (var arrow in arrows)
                {
                    if (arrow != null && arrow.PathCount > 0)
                    {
                        arrow.SpawnEntranceInstant();
                    }
                }
                yield return null;
            }
            else
            {
                foreach (var arrow in arrows)
                {
                    if (arrow.PathCount > 0)
                    {
                        float arrowDuration = arrow.PathCount * kEntranceCellDuration;
                        StartCoroutine(arrow.PlayEntranceGrowth(arrowDuration));
                    }
                }

                if (growthDuration > 0f) yield return new WaitForSeconds(growthDuration);
            }

            // 3. Ensure camera finishing zoom before spawning background circles
            if (zoomCoroutine != null) yield return zoomCoroutine;

            // 4. Build background circles mesh AFTER animation (single draw call)
            m_CirclePositionBuffer.Clear();
            foreach (var arrowData in data.arrows)
            {
                foreach (var pathPoint in arrowData.path)
                {
                    Vector2Int pos = pathPoint.ToVector2Int();
                    if (!m_SpawnedCirclePositions.Contains(pos))
                    {
                        m_CirclePositionBuffer.Add(new Vector3(pos.x * ArrowController.CellSize, pos.y * ArrowController.CellSize, 0f));
                        m_SpawnedCirclePositions.Add(pos);
                    }
                }
            }

            if (m_CircleSprite != null && m_CirclePositionBuffer.Count > 0)
            {
                CirclesMesh.BuildFromPositions(m_CircleSprite, m_CircleColor, m_CirclePositionBuffer, m_LevelCenter, -1);
            }
            yield return null;

            // 5. Build Dependency Tree for O(1) performance
            yield return StartCoroutine(GridManager.Instance.RebuildDependencyTreeAsync());

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

            int levelNum = ExtractNumber(currentLevelId);
            int animIndex;

            if (DevicePerformanceProfile.UseSimplifiedWinEffects)
            {
                animIndex = Random.Range(0, 2); // ripple or explosion only
            }
            else if (levelNum > 0 && levelNum < 20)
            {
                animIndex = Random.Range(0, 2) == 0 ? 0 : 4;
            }
            else
            {
                animIndex = Random.Range(0, 5);
            }

            switch (animIndex)
            {
                case 0: StartCoroutine(DoRippleEffect()); break;
                case 1: StartCoroutine(DoSpiralVortex()); break;
                case 2: StartCoroutine(DoDiagonalCascade()); break;
                case 3: StartCoroutine(DoExplosionEffect()); break;
                case 4: StartCoroutine(DoRandomPopcorn()); break;
                default: StartCoroutine(DoRippleEffect()); break;
            }
            
            StartCoroutine(FadeOutCircles(10.0f)); // Further increased delay to support longer popcorn
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
                if (HasBackgroundCircles)
                {
                    m_CirclesMesh.ApplyFinishState(m_CircleColor, m_WinCirclesAlpha, 2.0f);
                }
                yield return null;
            }
            m_WinCirclesAlpha = 0f;
            if (HasBackgroundCircles)
            {
                m_CirclesMesh.ApplyFinishState(m_CircleColor, m_WinCirclesAlpha, 2.0f);
            }
        }

        private System.Collections.IEnumerator DoRippleEffect()
        {
            if (!HasBackgroundCircles) yield break;

            Color targetColor = new Color(0.373f, 0.153f, 0.804f); // #5f27cd
            float rippleSpeed = 24.0f; // 2x Faster
            float maxDist = 0;
            int circleCount = m_CirclesMesh.Count;

            for (int i = 0; i < circleCount; i++)
            {
                float dist = m_CirclesMesh.GetDistanceFromCenter(i);
                if (dist > maxDist) maxDist = dist;
            }

            // 1. Initial Growth
            float startGrowthDuration = 0.2f; // 2x Faster
            float gElapsed = 0f;
            while (gElapsed < startGrowthDuration)
            {
                gElapsed += Time.deltaTime;
                float t = gElapsed / startGrowthDuration;
                float currentScale = Mathf.Lerp(1.0f, 1.5f, t);
                m_CirclesMesh.SetScaleAll(currentScale);
                m_CirclesMesh.ApplyIfDirty();
                yield return null;
            }

            for (int repeat = 0; repeat < 4; repeat++) // 2x Longer (4 repeats instead of 2)
            {
                float duration = (maxDist / rippleSpeed) + 0.25f;
                float elapsed = 0;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float waveFront = maxDist - (elapsed * rippleSpeed);

                    for (int i = 0; i < circleCount; i++)
                    {
                        float dist = m_CirclesMesh.GetDistanceFromCenter(i);
                        float proximity = Mathf.Clamp01(1.0f - Mathf.Abs(dist - waveFront) / 4.5f);

                        if (proximity > 0)
                        {
                            float baseScale = (dist < waveFront) ? 1.5f : 2.0f;
                            float peakScale = 2.5f;
                            float scale = Mathf.Lerp(baseScale, peakScale, proximity);
                            m_CirclesMesh.SetScale(i, scale);
                            m_CirclesMesh.SetColor(i, Color.Lerp(m_CircleColor, targetColor, proximity));
                        }
                        else
                        {
                            float finalScale = (dist < waveFront) ? 1.5f : 2.0f;
                            m_CirclesMesh.SetScale(i, finalScale);
                            m_CirclesMesh.SetColor(i, m_CircleColor);
                        }
                    }

                    m_CirclesMesh.ApplyIfDirty();
                    yield return null;
                }
            }

            FinishAnimation();
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }

        private System.Collections.IEnumerator DoSpiralVortex()
        {
            if (!HasBackgroundCircles) yield break;

            Color targetColor = new Color(0.117f, 0.741f, 0.886f); // Cyan #1ecadb
            float duration = 3.6f; // 2x Longer
            float elapsed = 0;
            int circleCount = m_CirclesMesh.Count;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                for (int i = 0; i < circleCount; i++)
                {
                    Vector3 pos = m_CirclesMesh.GetWorldPosition(i) - m_LevelCenter;
                    float angle = Mathf.Atan2(pos.y, pos.x) * Mathf.Rad2Deg;
                    float dist = m_CirclesMesh.GetDistanceFromCenter(i);

                    float spiralFactor = Mathf.Sin(t * 16f - dist * 0.5f + angle * 0.05f);
                    float proximity = Mathf.Clamp01((spiralFactor + 1f) / 2f);

                    float scale = Mathf.Lerp(1.2f, 2.4f, proximity);
                    m_CirclesMesh.SetScale(i, scale);
                    m_CirclesMesh.SetColor(i, Color.Lerp(m_CircleColor, targetColor, proximity));
                }

                m_CirclesMesh.ApplyIfDirty();
                yield return null;
            }

            FinishAnimation();
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }

        private System.Collections.IEnumerator DoDiagonalCascade()
        {
            if (!HasBackgroundCircles) yield break;

            Color targetColor = new Color(1f, 0.435f, 0.38f); // Coral #ff6f61
            float cascadeSpeed = 40.0f; // Additional 10% faster (approx 40.0f)
            float minVal = float.MaxValue, maxVal = float.MinValue;
            int circleCount = m_CirclesMesh.Count;

            for (int i = 0; i < circleCount; i++)
            {
                Vector3 pos = m_CirclesMesh.GetWorldPosition(i);
                float val = pos.x + pos.y;
                if (val < minVal) minVal = val;
                if (val > maxVal) maxVal = val;
            }

            float gap = 5.0f; // Gap between the 10 lines
            int lineCount = 10;

            for (int repeat = 0; repeat < 2; repeat++)
            {
                float duration = (maxVal - minVal + (gap * (lineCount + 1))) / cascadeSpeed + 0.5f;
                float elapsed = 0;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float waveFront = minVal + (elapsed * cascadeSpeed);

                    for (int i = 0; i < circleCount; i++)
                    {
                        Vector3 pos = m_CirclesMesh.GetWorldPosition(i);
                        float val = pos.x + pos.y;

                        float proximity = 0;
                        for (int line = 0; line < lineCount; line++)
                        {
                            float p = Mathf.Clamp01(1.0f - Mathf.Abs(val - (waveFront - line * gap)) / 3.5f);
                            if (p > proximity) proximity = p;
                        }

                        if (proximity > 0)
                        {
                            float scale = Mathf.Lerp(1.2f, 2.5f, proximity);
                            m_CirclesMesh.SetScale(i, scale);
                            m_CirclesMesh.SetColor(i, Color.Lerp(m_CircleColor, targetColor, proximity));
                        }
                    }

                    m_CirclesMesh.ApplyIfDirty();
                    yield return null;
                }
            }

            FinishAnimation();
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }

        private System.Collections.IEnumerator DoExplosionEffect()
        {
            if (!HasBackgroundCircles) yield break;

            Color targetColor = new Color(1f, 0.843f, 0f); // Gold #ffd700
            float duration = 2.4f; // 2x Longer
            float elapsed = 0;
            int circleCount = m_CirclesMesh.Count;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float waveRadius = (t * 50.0f) % 30.0f;

                for (int i = 0; i < circleCount; i++)
                {
                    float dist = m_CirclesMesh.GetDistanceFromCenter(i);
                    float proximity = Mathf.Clamp01(1.0f - Mathf.Abs(dist - waveRadius) / 6.0f);

                    if (proximity > 0)
                    {
                        float scale = Mathf.Lerp(1.5f, 3.5f, proximity);
                        m_CirclesMesh.SetScale(i, scale);
                        m_CirclesMesh.SetColor(i, Color.Lerp(m_CircleColor, targetColor, proximity));
                    }
                }

                m_CirclesMesh.ApplyIfDirty();
                yield return null;
            }

            FinishAnimation();
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }

        private System.Collections.IEnumerator DoRandomPopcorn()
        {
            if (!HasBackgroundCircles) yield break;

            Color targetColor = new Color(0.6f, 0.4f, 1f);
            float totalDuration = DevicePerformanceProfile.IsLowEnd ? 5.0f : 8.0f;
            float elapsed = 0f;
            float spawnTimer = 0f;
            const float spawnInterval = 0.04f;
            const float popDuration = 0.2f;
            int circleCount = m_CirclesMesh.Count;

            List<int> indices = new List<int>(circleCount);
            for (int i = 0; i < circleCount; i++) indices.Add(i);

            for (int i = 0; i < indices.Count; i++)
            {
                int temp = indices[i];
                int randomIndex = Random.Range(i, indices.Count);
                indices[i] = indices[randomIndex];
                indices[randomIndex] = temp;
            }

            int count = 0;
            int circlesPerBatch = Mathf.Max(1, circleCount / (DevicePerformanceProfile.IsLowEnd ? 150 : 100));
            m_ActivePopAnimations.Clear();

            while (elapsed < totalDuration || m_ActivePopAnimations.Count > 0)
            {
                elapsed += Time.deltaTime;
                spawnTimer += Time.deltaTime;

                if (spawnTimer >= spawnInterval && count < indices.Count && elapsed < totalDuration)
                {
                    spawnTimer = 0f;
                    int toPop = Mathf.Min(circlesPerBatch, indices.Count - count);
                    for (int i = 0; i < toPop; i++)
                    {
                        int idx = indices[count + i];
                        m_ActivePopAnimations.Add(new CirclePopAnimation
                        {
                            Index = idx,
                            Elapsed = 0f,
                            StartScale = m_CirclesMesh.GetScale(idx),
                            PopColor = targetColor
                        });
                    }
                    count += toPop;
                }

                UpdateActivePopAnimations(popDuration);
                m_CirclesMesh.ApplyIfDirty();
                yield return null;
            }

            yield return new WaitForSeconds(DevicePerformanceProfile.IsLowEnd ? 0.5f : 2.0f);
            FinishAnimation();
            yield return new WaitForSeconds(0.5f);
            HideArrows();
        }

        private void UpdateActivePopAnimations(float popDuration)
        {
            if (!HasBackgroundCircles || m_ActivePopAnimations.Count == 0) return;

            for (int i = m_ActivePopAnimations.Count - 1; i >= 0; i--)
            {
                CirclePopAnimation pop = m_ActivePopAnimations[i];
                pop.Elapsed += Time.deltaTime;

                if (pop.Elapsed >= popDuration)
                {
                    m_CirclesMesh.SetScale(pop.Index, 2.0f);
                    m_CirclesMesh.SetColor(pop.Index, m_CircleColor);
                    m_ActivePopAnimations.RemoveAt(i);
                    continue;
                }

                float t = pop.Elapsed / popDuration;
                float curve = Mathf.Sin(t * Mathf.PI);
                m_CirclesMesh.SetScale(pop.Index, Mathf.Lerp(pop.StartScale, 2.8f, curve));
                m_CirclesMesh.SetColor(pop.Index, Color.Lerp(m_CircleColor, pop.PopColor, curve));
                m_ActivePopAnimations[i] = pop;
            }
        }

        private void FinishAnimation()
        {
            if (!HasBackgroundCircles) return;
            m_CirclesMesh.ApplyFinishState(m_CircleColor, m_WinCirclesAlpha, 2.0f);
        }
    }
}
