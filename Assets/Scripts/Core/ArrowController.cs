using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Core
{
    public class ArrowController : MonoBehaviour
    {
        public List<Segment> segments = new List<Segment>();
        public Segment segmentPrefab;
        
        [Header("Visuals")]
        public Sprite HeadSprite;
        public Vector3 HeadScale = new Vector3(0.477f, 0.477f, 0.477f);
        public GameObject pointEffectPrefab;
        private List<GameObject> instantiatedEffects = new List<GameObject>();
        private Segment m_LastHeadSegment;
        private Segment m_GrowthHeadSegment;
        private bool m_IsEntranceGrowing;
        private Vector2Int m_LastAppliedVisualDirection = Vector2Int.zero;
        private bool forceVisualsUpdate = true;
        
        // Grid Step size (Standard 1 unit)
        public const float CellSize = 1.0f;
        [SerializeField] private float segmentScale = 1.0f; 

        private bool isMoving = false;
        public bool IsMoving => isMoving;
        private Coroutine moveCoroutine;
        private Coroutine poolReturnCoroutine;
        private bool isInPool;
        
        public int ArrowId { get; private set; }
        private ArrowData cachedData;
        private bool hasReducedLife = false;
        public Vector2Int LookDirection => m_LookDirection;
        private Vector2Int m_LookDirection = Vector2Int.up;
        
        private static Material s_SharedLineMaterial;

        public static void EnsureSharedLineMaterialFromPrefab(ArrowController prefab)
        {
            if (s_SharedLineMaterial != null) return;

            if (prefab != null)
            {
                LineRenderer prefabLine = prefab.GetComponent<LineRenderer>();
                if (prefabLine != null && prefabLine.sharedMaterial != null)
                {
                    s_SharedLineMaterial = prefabLine.sharedMaterial;
                    return;
                }
            }

            if (s_SharedLineMaterial == null)
            {
                s_SharedLineMaterial = new Material(Shader.Find("Sprites/Default"));
            }
        }

        private LineRenderer lineRenderer;
        private LineRenderer previewLineRenderer;
        private Vector2Int m_CurrentVisualDirection = Vector2Int.up;

        // ── Reusable allocation-free buffers ─────────────────────────────────
        // One shared list per arrow instance — cleared and reused each step
        private readonly List<Vector2Int> _newPositions       = new List<Vector2Int>(16);
        private readonly List<Vector3>    _targetWorldPos     = new List<Vector3>(16);
        private readonly List<Vector3>    _impactTargets      = new List<Vector3>(16);
        private readonly List<Vector2Int> _savedPositions     = new List<Vector2Int>(16);
        private Vector3[] _animationStarts = new Vector3[16]; // Reuse buffer for segment starts
        // Cached WaitForSeconds to avoid per-frame allocation in blocked animation
        private static readonly WaitForSeconds s_BlockedPause = new WaitForSeconds(0.07f);

        // Pre-allocated buffers for blocked animation history (to avoid GC)
        private readonly List<Vector2Int[]> m_ForwardHistoryBuffer = new List<Vector2Int[]>();
        private static readonly Stack<Vector2Int[]> s_ArrayPool = new Stack<Vector2Int[]>();

        private Vector2Int[] GetArrayFromPool(int size)
        {
            if (s_ArrayPool.Count > 0)
            {
                var arr = s_ArrayPool.Pop();
                if (arr.Length >= size) return arr;
            }
            return new Vector2Int[Mathf.Max(size, 16)];
        }

        private void ReturnArrayToPool(Vector2Int[] arr)
        {
            s_ArrayPool.Push(arr);
        }

        // --- Speed & Acceleration Constants ---
        private const float K_LegacyStepDuration = 0.027f; // "What it is today"
        private const float K_InitialSpeedMultiplier = 1.0f; // Starts at 80%
        private const float K_TargetSpeedMultiplier = 2.0f;  // Reaches 180%
        private const float K_AccelerationTime = 0.9f;      // Over 2 seconds
        private const float K_BaseMoveDuration = K_LegacyStepDuration / K_InitialSpeedMultiplier; // Snappier base

        public void Initialize(ArrowData data)
        {
            PrepareIncrementalInit(data);
            for (int i = 0; i < data.path.Count; i++)
            {
                SpawnSegmentStep(i, true);
            }
            UpdateVisuals(); // Initial visual sync
        }

        public void PrepareForReuse()
        {
            isInPool = false;
            m_LastHeadSegment = null;
            m_LastAppliedVisualDirection = Vector2Int.zero;
            m_IsEntranceGrowing = false;
            ReleaseGrowthHeadSegment();
            forceVisualsUpdate = true;
            forceLineUpdate = true;
            highlightCoroutine = null;
            currentArrowColor = Color.black;
            m_OriginalColor = Color.black;
            m_IsMarkedBlocked = false;
            hasReducedLife = false;
            isMoving = false;
            moveCoroutine = null;
            m_CurrentVisualDirection = Vector2Int.up;
            m_LookDirection = Vector2Int.up;

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
                lineRenderer.sortingOrder = 0;
                lineRenderer.startColor = Color.black;
                lineRenderer.endColor = Color.black;
            }

            if (previewLineRenderer != null)
            {
                previewLineRenderer.positionCount = 0;
                previewLineRenderer.gameObject.SetActive(false);
            }
        }

        public void PrepareIncrementalInit(ArrowData data)
        {
            PrepareForReuse();
            cachedData = data;
            ArrowId = data.id;
            
            // Setup LineRenderer
            if (lineRenderer == null)
            {
                lineRenderer = GetComponent<LineRenderer>();
                if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
            
            // arrowWidth of 0 (or absent from JSON) means use the default width.
            float lineWidth = (data.arrowWidth > 0f) ? data.arrowWidth : 0.2f;
            lineRenderer.startWidth = lineWidth; 
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            
            if (s_SharedLineMaterial == null)
            {
                ArrowController prefabRef = ArrowPoolManager.Instance != null ? ArrowPoolManager.Instance.arrowPrefab : null;
                EnsureSharedLineMaterialFromPrefab(prefabRef);
            }
            lineRenderer.material = s_SharedLineMaterial;

            lineRenderer.sortingOrder = 0; 
            hasReducedLife = false;
            m_IsMarkedBlocked = false;

            // Parse color from data
            m_OriginalColor = Color.black;
            m_OriginalColor.a = 1.0f;

            if (!string.IsNullOrWhiteSpace(data.color))
            {
                if (ColorUtility.TryParseHtmlString(data.color, out Color parsedColor))
                {
                    m_OriginalColor = parsedColor;
                }
            }
            SetArrowColor(m_OriginalColor);

            // Setup Preview LineRenderer
            if (previewLineRenderer == null)
            {
                Transform existingPreview = transform.Find("PreviewLine");
                GameObject previewObj;
                if (existingPreview != null)
                {
                    previewObj = existingPreview.gameObject;
                    previewLineRenderer = previewObj.GetComponent<LineRenderer>();
                }
                else
                {
                    previewObj = new GameObject("PreviewLine");
                    previewObj.transform.SetParent(this.transform);
                    previewLineRenderer = previewObj.AddComponent<LineRenderer>();
                }
                
                previewLineRenderer.startWidth = 0.1f;
                previewLineRenderer.endWidth = 0.1f;
                previewLineRenderer.material = s_SharedLineMaterial; // Share here too
                previewLineRenderer.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Grey Transparent
                previewLineRenderer.endColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Fading
                previewLineRenderer.useWorldSpace = true;
                previewLineRenderer.sortingOrder = -1; // Behind head
            }
            previewLineRenderer.positionCount = 0;
            previewLineRenderer.gameObject.SetActive(false);

            segments.Clear();

            // Set head direction from data
            m_LookDirection = Vector2Int.up;
            if (!string.IsNullOrEmpty(data.lookDirection))
            {
                switch (data.lookDirection.ToLower())
                {
                    case "up": m_LookDirection = Vector2Int.up; break;
                    case "down": m_LookDirection = Vector2Int.down; break;
                    case "left": m_LookDirection = Vector2Int.left; break;
                    case "right": m_LookDirection = Vector2Int.right; break;
                }
            }
            else if (data.path.Count >= 2)
            {
                // Fallback to path-based direction
                m_LookDirection = data.path[data.path.Count - 1].ToVector2Int() - data.path[data.path.Count - 2].ToVector2Int();
            }
            
            m_CurrentVisualDirection = m_LookDirection;
            GameManager.Instance.RegisterArrow();
        }

        /// <summary>
        /// Slide-in animation step. 
        /// Step 0: Head appears at path[0].
        /// Step 1: Head moves to path[1], new segment appears at path[0].
        /// ...and so on.
        /// </summary>
        private void SpawnSegmentStep(int step, bool instant)
        {
            if (this == null || cachedData == null || step >= cachedData.path.Count) return;

            // 1. Create a new segment at index 0 (the tail end of the growing path)
            Vector2Int spawnPos = cachedData.path[0].ToVector2Int();
            Vector3 worldSpawnPos = new Vector3(spawnPos.x * CellSize, spawnPos.y * CellSize, 0);

            Segment newSeg;
            if (ArrowPoolManager.Instance != null)
            {
                newSeg = ArrowPoolManager.Instance.GetSegment(worldSpawnPos, Quaternion.identity, transform);
            }
            else
            {
                GameObject segObj = Instantiate(segmentPrefab.gameObject, worldSpawnPos, Quaternion.identity, transform);
                newSeg = segObj.GetComponent<Segment>();
            }
            
            newSeg.transform.localScale = Vector3.one;
            newSeg.ParentArrow = this;
            
            segments.Insert(0, newSeg);

            BoxCollider2D box = newSeg.GetComponent<BoxCollider2D>();
            if (box == null) box = newSeg.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f);

            // 2. Determine target positions for all segments in this growth step
            _targetWorldPos.Clear();
            for (int i = 0; i < segments.Count; i++)
            {
                int pathIndex = step - (segments.Count - 1 - i);
                Vector2Int pos = cachedData.path[pathIndex].ToVector2Int();
                segments[i].GridPosition = pos;
                Vector3 targetWorldPos = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
                _targetWorldPos.Add(targetWorldPos);
                
                if (instant) segments[i].transform.position = targetWorldPos;

                GridManager.Instance.RegisterOccupancy(pos, this);
            }

            m_CurrentVisualDirection = GetGrowthStepDirection(step);
            forceVisualsUpdate = true;
            forceLineUpdate = true;
            if (!instant)
            {
                // If not instant, we rely on the caller to start the animation coroutine
                // with the calculated targets. But for better API, we'll keep the IEnumerator wrapper.
            }
        }

        private Vector2Int GetGrowthStepDirection(int step)
        {
            if (cachedData == null || cachedData.path == null || cachedData.path.Count < 2)
                return m_LookDirection;

            int fromIndex = step > 0 ? step - 1 : 0;
            int toIndex = step > 0 ? step : 1;
            Vector2Int delta = cachedData.path[toIndex].ToVector2Int() - cachedData.path[fromIndex].ToVector2Int();
            return delta != Vector2Int.zero ? delta : m_LookDirection;
        }

        public int PathCount => cachedData?.path?.Count ?? 0;

        /// <summary>
        /// Smooth entrance growth: interpolates along the path and only materializes segments at the end.
        /// </summary>
        public IEnumerator PlayEntranceGrowth(float totalDuration)
        {
            if (this == null || cachedData == null || cachedData.path.Count == 0) yield break;

            m_IsEntranceGrowing = true;
            EnsureGrowthHeadSegment();
            forceLineUpdate = true;
            forceVisualsUpdate = true;

            float elapsed = 0f;
            while (elapsed < totalDuration)
            {
                if (this == null) yield break;
                float growthT = totalDuration > 0f ? Mathf.Clamp01(elapsed / totalDuration) : 1f;
                UpdateEntranceGrowthVisuals(growthT);
                elapsed += Time.deltaTime;
                yield return null;
            }

            FinalizeEntranceGrowth();
        }

        private void EnsureGrowthHeadSegment()
        {
            if (m_GrowthHeadSegment != null) return;

            Vector2Int spawnPos = cachedData.path[0].ToVector2Int();
            Vector3 worldSpawnPos = new Vector3(spawnPos.x * CellSize, spawnPos.y * CellSize, 0);

            if (ArrowPoolManager.Instance != null)
            {
                m_GrowthHeadSegment = ArrowPoolManager.Instance.GetSegment(worldSpawnPos, Quaternion.identity, transform);
            }
            else
            {
                GameObject segObj = Instantiate(segmentPrefab.gameObject, worldSpawnPos, Quaternion.identity, transform);
                m_GrowthHeadSegment = segObj.GetComponent<Segment>();
            }

            m_GrowthHeadSegment.ParentArrow = this;
            m_GrowthHeadSegment.transform.localScale = Vector3.one;
        }

        private void ReleaseGrowthHeadSegment()
        {
            if (m_GrowthHeadSegment == null) return;

            if (ArrowPoolManager.Instance != null)
            {
                ArrowPoolManager.Instance.ReturnSegment(m_GrowthHeadSegment);
            }
            else
            {
                Destroy(m_GrowthHeadSegment.gameObject);
            }

            m_GrowthHeadSegment = null;
        }

        private void ComputeGrowthWorldPoints(float growthT, List<Vector3> outPoints)
        {
            outPoints.Clear();
            if (cachedData == null || cachedData.path == null || cachedData.path.Count == 0) return;

            int pathCount = cachedData.path.Count;
            float progress = growthT * pathCount;
            int count = Mathf.CeilToInt(progress);
            if (count <= 0) return;

            count = Mathf.Min(count, pathCount);
            for (int i = 0; i < count; i++)
            {
                Vector2Int p = cachedData.path[i].ToVector2Int();
                outPoints.Add(new Vector3(p.x * CellSize, p.y * CellSize, 0f));
            }

            if (count > 1 && progress < pathCount)
            {
                float partial = progress - (count - 1);
                if (partial > 0f && partial < 1f)
                {
                    Vector3 prev = outPoints[outPoints.Count - 2];
                    Vector3 last = outPoints[outPoints.Count - 1];
                    outPoints[outPoints.Count - 1] = Vector3.Lerp(prev, last, partial);
                }
            }
        }

        private Vector2Int GetGrowthDirectionAtProgress(float progress)
        {
            if (cachedData == null || cachedData.path == null || cachedData.path.Count < 2)
                return m_LookDirection;

            int pathCount = cachedData.path.Count;
            int fromIndex = Mathf.Clamp(Mathf.FloorToInt(progress), 0, pathCount - 2);
            int toIndex = fromIndex + 1;

            Vector2Int delta = cachedData.path[toIndex].ToVector2Int() - cachedData.path[fromIndex].ToVector2Int();
            return delta != Vector2Int.zero ? delta : m_LookDirection;
        }

        private void UpdateEntranceGrowthVisuals(float growthT)
        {
            ComputeGrowthWorldPoints(growthT, linePoints);
            if (linePoints.Count == 0)
            {
                if (lineRenderer != null) lineRenderer.positionCount = 0;
                if (m_GrowthHeadSegment != null && m_GrowthHeadSegment.Renderer != null)
                    m_GrowthHeadSegment.Renderer.enabled = false;
                return;
            }

            float pathProgress = growthT * cachedData.path.Count;
            Vector2Int direction = GetGrowthDirectionAtProgress(pathProgress);
            m_CurrentVisualDirection = direction;

            Vector3 headPos = linePoints[linePoints.Count - 1];
            Vector3 headOffset = new Vector3(direction.x, direction.y, 0f) * -0.12f * CellSize;
            Vector3 lineEnd = headPos + headOffset;

            if (linePoints.Count == 1)
            {
                linePoints[0] = lineEnd - new Vector3(direction.x, direction.y, 0f) * 0.1f * CellSize;
                linePoints.Add(lineEnd);
            }
            else
            {
                linePoints[linePoints.Count - 1] = lineEnd;
            }

            if (linePointsArray == null || linePointsArray.Length != linePoints.Count)
            {
                linePointsArray = new Vector3[linePoints.Count];
            }

            linePoints.CopyTo(linePointsArray);
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = linePointsArray.Length;
                lineRenderer.SetPositions(linePointsArray);
                if (lineRenderer.sortingOrder != 5) lineRenderer.sortingOrder = 5;
            }

            UpdateGrowthHeadVisual(headPos, direction);
        }

        private void UpdateGrowthHeadVisual(Vector3 headPos, Vector2Int direction)
        {
            if (m_GrowthHeadSegment == null) return;

            Transform headTransform = m_GrowthHeadSegment.CachedTransform;
            headTransform.position = headPos;
            headTransform.localScale = HeadScale;

            if (m_GrowthHeadSegment.Renderer != null)
            {
                m_GrowthHeadSegment.Renderer.enabled = true;
                m_GrowthHeadSegment.Renderer.sprite = HeadSprite;
                m_GrowthHeadSegment.Renderer.color = currentArrowColor;
                m_GrowthHeadSegment.Renderer.sortingOrder = 10;

                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                headTransform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        private void FinalizeEntranceGrowth()
        {
            m_IsEntranceGrowing = false;
            ReleaseGrowthHeadSegment();

            for (int i = 0; i < cachedData.path.Count; i++)
            {
                SpawnSegmentStep(i, true);
            }

            m_LastHeadSegment = null;
            m_LastAppliedVisualDirection = Vector2Int.zero;
            forceLineUpdate = true;
            forceVisualsUpdate = true;
            UpdateVisuals();
        }

        public Vector2Int GetHeadGridPosition()
        {
            if (segments.Count == 0) return Vector2Int.zero;
            return segments[segments.Count - 1].GridPosition;
        }

        public Vector3 GetHeadPosition()
        {
            if (segments.Count == 0) return transform.position;
            return segments[segments.Count - 1].CachedTransform.position;
        }

        private const int MaxLikeEffectsPerArrow = 10;

        private void SpawnLikeEffectsAlongArrow()
        {
            int segmentCount = segments.Count;
            int likeCount = Mathf.Min(segmentCount, MaxLikeEffectsPerArrow);

            for (int i = 0; i < likeCount; i++)
            {
                int segmentIndex = likeCount == 1
                    ? 0
                    : Mathf.RoundToInt(i * (segmentCount - 1f) / (likeCount - 1f));

                Segment seg = segments[segmentIndex];
                if (seg == null)
                {
                    continue;
                }

                GameObject effect = GameManager.Instance.SpawnEffect(
                    pointEffectPrefab,
                    seg.CachedTransform.position,
                    Quaternion.identity,
                    null);

                if (effect != null)
                {
                    instantiatedEffects.Add(effect);
                }
            }
        }

        private void UpdateVisuals()
        {
            if (m_IsEntranceGrowing) return;

            UpdateLinePositions();
            UpdateHeadVisuals();
        }

        private List<Vector3> linePoints = new List<Vector3>(16);
        private Vector3[] linePointsArray;
        private Vector3 lastHeadPos;
        private bool forceLineUpdate = true;
        private Camera m_cachedMainCam;
        private Camera p_MainCam {
            get {
                if (m_cachedMainCam == null) m_cachedMainCam = Camera.main;
                return m_cachedMainCam;
            }
        }

        private void UpdateLinePositions()
        {
            int segCount = segments.Count;
            if (lineRenderer == null || segCount == 0)
            {
                if (lineRenderer != null) lineRenderer.positionCount = 0;
                return;
            }

            // Performance: Only update if the head has moved significantly or update is forced
            Vector3 currentHeadPos = segments[segCount - 1].CachedTransform.position;
            if (!forceLineUpdate && (currentHeadPos - lastHeadPos).sqrMagnitude < 0.000001f)
            {
                return;
            }
            lastHeadPos = currentHeadPos;
            forceLineUpdate = false;

            linePoints.Clear();
            float snapThreshold = 0.001f;
            float z = currentHeadPos.z;

            for (int i = 0; i < segCount - 1; i++)
            {
                Vector3 p1 = segments[i].CachedTransform.position;
                Vector3 p2 = segments[i+1].CachedTransform.position;
                
                // Snap points to grid if they are very close
                Vector3 target1 = new Vector3(segments[i].GridPosition.x * CellSize, segments[i].GridPosition.y * CellSize, z);
                Vector3 target2 = new Vector3(segments[i+1].GridPosition.x * CellSize, segments[i+1].GridPosition.y * CellSize, z);
                
                if (Vector3.SqrMagnitude(p1 - target1) < snapThreshold * snapThreshold) p1 = target1;
                if (Vector3.SqrMagnitude(p2 - target2) < snapThreshold * snapThreshold) p2 = target2;

                // Add p1 if it's not a duplicate of the previous point
                if (linePoints.Count == 0 || Vector3.SqrMagnitude(linePoints[linePoints.Count - 1] - p1) > snapThreshold * snapThreshold)
                {
                    linePoints.Add(p1);
                }

                // Inject a corner anchor if segments are in a turn (not aligned on either axis)
                // Using a smaller threshold to avoid diagonal artifacts during animation
                if (Mathf.Abs(p1.x - p2.x) > snapThreshold && Mathf.Abs(p1.y - p2.y) > snapThreshold)
                {
                    Vector2Int g1 = segments[i].GridPosition;
                    Vector2Int g2 = segments[i+1].GridPosition;
                    
                    Vector3 c1 = new Vector3(g1.x * CellSize, g1.y * CellSize, z);
                    Vector3 c2 = new Vector3(g2.x * CellSize, g2.y * CellSize, z);
                    
                    // Manhattan distance logic to pick the correct elbow of the turn
                    float s1 = Mathf.Abs(p1.x - c1.x) + Mathf.Abs(p1.y - c1.y) + Mathf.Abs(p2.x - c1.x) + Mathf.Abs(p2.y - c1.y);
                    float s2 = Mathf.Abs(p1.x - c2.x) + Mathf.Abs(p1.y - c2.y) + Mathf.Abs(p2.x - c2.x) + Mathf.Abs(p2.y - c2.y);
                    
                    Vector3 corner = (s1 <= s2) ? c1 : c2;
                    if (Vector3.Distance(linePoints[linePoints.Count - 1], corner) > snapThreshold)
                    {
                        linePoints.Add(corner);
                    }
                }
            }
            
            // Final head position with offset
            // Adjusted: Moved 0.08 units forward (into the head) from the previous -0.2f offset
            Vector3 headOffset = new Vector3(m_CurrentVisualDirection.x, m_CurrentVisualDirection.y, 0) * -0.12f * CellSize;
            Vector3 finalPoint = currentHeadPos + headOffset;
            
            if (linePoints.Count == 0)
            {
                // Ensure at least two points for visibility even for 1-segment arrows
                linePoints.Add(finalPoint - new Vector3(m_LookDirection.x, m_LookDirection.y, 0) * 0.1f);
                linePoints.Add(finalPoint);
            }
            else if (Vector3.SqrMagnitude(linePoints[linePoints.Count - 1] - finalPoint) > snapThreshold * snapThreshold)
            {
                linePoints.Add(finalPoint);
            }
            
            if (linePointsArray == null || linePointsArray.Length != linePoints.Count)
            {
                linePointsArray = new Vector3[linePoints.Count];
            }
            
            linePoints.CopyTo(linePointsArray);
            lineRenderer.positionCount = linePointsArray.Length;
            lineRenderer.SetPositions(linePointsArray);
        }

        [SerializeField] private Color blockedColor = new Color(0.906f, 0.298f, 0.235f); // #e74c3c
        private Color currentArrowColor = Color.black;
        private Color m_OriginalColor = Color.black;
        private bool m_IsMarkedBlocked = false;
        private Coroutine highlightCoroutine;

        public void SetPressedStyle()
        {
            if (isMoving) return;
            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
            SetArrowColor(new Color(0.169f, 0.667f, 0.384f)); // #27ae60
        }

        public void ResetPressedStyle()
        {
            if (isMoving) return;
            if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
            
            Color targetColor = m_IsMarkedBlocked ? blockedColor : m_OriginalColor;
            highlightCoroutine = StartCoroutine(AnimateColorReset(targetColor, 0.12f));
        }

        private IEnumerator AnimateColorReset(Color targetColor, float duration)
        {
            Color startColor = currentArrowColor;
            float elapsed = 0;
            while (elapsed < duration)
            {
                SetArrowColor(Color.Lerp(startColor, targetColor, elapsed / duration));
                elapsed += Time.deltaTime;
                yield return null;
            }
            SetArrowColor(targetColor);
            highlightCoroutine = null;
        }

        public void OnArrowClicked(Segment clickedSegment, Vector2 clickPosition)
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanInteract) return;

            // Allow clicking ANY segment
            if (segments.Contains(clickedSegment))
            {
                SoundManager.Instance.PlayArrowSelect();
                GameManager.Instance.ResetHintTimer();

                if (!isMoving)
                {
                    if (highlightCoroutine != null) StopCoroutine(highlightCoroutine);
                    // Check if path is clear BEFORE starting
                    if (CanMoveForward())
                    {
                        isMoving = true;
                        VibrationManager.VibrateSelection();

                        if (CameraController.Instance != null && GameManager.Instance != null)
                        {
                            if (GridManager.Instance.DependencyTree != null)
                            {
                                GridManager.Instance.DependencyTree.OnArrowStartedMoving(this);
                            }
                            var gameManager = GameManager.Instance;
                            bool timeCondition = (Time.time - gameManager.LastArrowSelectionTime) <= GameUIContoleer.StreakTimeThreshold;
                            //bool panCondition = !CameraController.Instance.HasPannedSinceLastReset;
                            if (timeCondition && UserDataManager.Instance.CurrentLevel >= 6)
                            {
                                gameManager.IncrementStreak();
                                SoundManager.Instance.PlayStreak(gameManager.p_StreakCount - 1);

                                int displayStreak = gameManager.p_StreakCount - 1;
                                gameManager.ShowComboFeedback(displayStreak, gameManager.p_StreakCount);

                                if (displayStreak == 3 || displayStreak == 7 || displayStreak == 11)
                                {
                                    gameManager.ShowVoiceFeedback();
                                }
                            }
                            else
                            {
                                gameManager.ResetStreak();
                            }
                            // Update state for next pick
                            gameManager.NotifyArrowSelection();
                            CameraController.Instance.ResetPanState();
                        }
                        float tempProbLike = Random.Range(0f,1f);
                        if (tempProbLike < 0.12f)
                        {
                            SoundManager.Instance.PlayLike();
                            if (pointEffectPrefab != null && segments.Count > 0)
                            {
                                SpawnLikeEffectsAlongArrow();
                            }
                        }
                        // Instantiate prefabs at each arrow point
                        
                        // Start success color animation (White -> Green -> White)
                        StartCoroutine(SuccessColorAnimation());

                        SchedulePoolReturn(5.0f);
                        moveCoroutine = StartCoroutine(AutoMoveRoutine());
                        
                        // Notify GameManager that this arrow is moving (solved)
                        GameManager.Instance.NotifyArrowSuccess(clickPosition, ArrowId); 
                    }
                    else
                    {
                        // Arrow is blocked - trigger blocked arrow animation
                        isMoving = true; // Prevent multiple clicks during animation
                        
                        // Reduce life (only once per arrow per attempt)
                        if (!hasReducedLife)
                        {
                            GameManager.Instance.LoseLife();
                            hasReducedLife = true;
                            Debug.Log("Arrow Blocked! Life lost.");
                        }
                        
                        // Start the blocked arrow animation
                        VibrationManager.VibrateError();
                        StartCoroutine(BlockedArrowAnimationWithCleanup());
                    }
                }
            }
        }

        private IEnumerator BlockedArrowAnimationWithCleanup()
        {
            yield return StartCoroutine(BlockedArrowAnimation());
            isMoving = false; // Allow clicking again after animation completes
        }


        private IEnumerator SuccessColorAnimation()
        {
            Color successColor = new Color(0.18f, 0.8f, 0.44f); // #2ecc71
            Color startFlashColor = m_OriginalColor; 
            float duration = 0.33f;
            float halfDuration = duration / 2f;

            // Flash to Green
            float elapsed = 0;
            while (elapsed < halfDuration)
            {
                SetArrowColor(Color.Lerp(startFlashColor, successColor, elapsed / halfDuration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Green back to White (or Black?)
            // The user said back to #FFFFFF. Let's stick to that.
            elapsed = 0;
            while (elapsed < halfDuration)
            {
                SetArrowColor(Color.Lerp(successColor, startFlashColor, elapsed / halfDuration));
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Finally reset to black (default line color) as it escapes?
            // Or keep it white? The user said back to #FFFFFF.
            // But the line is black. Let's settle on black at the very end so it matches the tray?
            // "back to #FFFFFF"
            SetArrowColor(startFlashColor);
            
            // After escape finishes, it's destroyed anyway.
        }

        public void StartWinScaleAnimation(float targetScaleFactor, float duration)
        {
            StartCoroutine(WinScaleAnimation(targetScaleFactor, duration));
        }

        private IEnumerator WinScaleAnimation(float targetScaleFactor, float duration)
        {
            float elapsed = 0f;
            List<Vector3> startScales = new List<Vector3>();
            foreach (var seg in segments)
            {
                startScales.Add(seg.transform.localScale);
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    if (segments[i] != null)
                    {
                        segments[i].transform.localScale = Vector3.Lerp(startScales[i], startScales[i] * targetScaleFactor, t);
                    }
                }
                yield return null;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i] != null)
                {
                    segments[i].transform.localScale = startScales[i] * targetScaleFactor;
                }
            }
        }

        private void SetArrowColor(Color color)
        {
            currentArrowColor = color;
            SetArrowColorRaw(color);
        }

        private void SetArrowColorRaw(Color color)
        {
            if (lineRenderer != null)
            {
                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                var seg = segments[i];
                // OPTIMIZATION #3: Only update enabled renderers (heads)
                if (seg.Renderer != null && seg.Renderer.enabled)
                {
                    seg.Renderer.color = color;
                }
            }
        }

        private void SchedulePoolReturn(float delaySeconds)
        {
            CancelPendingPoolReturn();
            poolReturnCoroutine = StartCoroutine(PoolReturnAfterDelay(delaySeconds));
        }

        private void CancelPendingPoolReturn()
        {
            CancelInvoke(nameof(DestroySelf));
            if (poolReturnCoroutine != null)
            {
                StopCoroutine(poolReturnCoroutine);
                poolReturnCoroutine = null;
            }
        }

        private IEnumerator PoolReturnAfterDelay(float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            poolReturnCoroutine = null;
            if (!isInPool)
            {
                DestroySelf();
            }
        }

        /// <summary>Reset and return to pool without destroying the GameObject.</summary>
        public void ReturnToPool()
        {
            if (isInPool) return;

            isInPool = true;
            CancelPendingPoolReturn();

            if (GridManager.Instance != null)
            {
                GridManager.Instance.UnregisterArrow(this);
            }

            foreach (var effect in instantiatedEffects)
            {
                if (effect != null && GameManager.Instance != null)
                {
                    GameManager.Instance.ReturnEffect(effect);
                }
            }
            instantiatedEffects.Clear();

            StopAllCoroutines();
            CancelInvoke();
            isMoving = false;
            moveCoroutine = null;

            ReleaseGrowthHeadSegment();

            while (segments.Count > 0)
            {
                Segment seg = segments[0];
                segments.RemoveAt(0);
                if (ArrowPoolManager.Instance != null)
                {
                    ArrowPoolManager.Instance.ReturnSegment(seg);
                }
                else if (seg != null)
                {
                    Destroy(seg.gameObject);
                }
            }

            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }

            if (previewLineRenderer != null)
            {
                previewLineRenderer.positionCount = 0;
                previewLineRenderer.gameObject.SetActive(false);
            }

            forceLineUpdate = true;
            forceVisualsUpdate = true;
            m_LastHeadSegment = null;
            highlightCoroutine = null;
            currentArrowColor = Color.black;
            m_OriginalColor = Color.black;
            hasReducedLife = false;
            m_IsMarkedBlocked = false;
        }

        private void DestroySelf()
        {
            if (isInPool) return;

            if (ArrowPoolManager.Instance != null)
            {
                ArrowPoolManager.Instance.ReturnArrow(this);
            }
            else
            {
                ReturnToPool();
                Destroy(gameObject, 0.5f);
            }
        }

        private void OnDestroy()
        {
            CancelPendingPoolReturn();

            // ReturnToPool already unregisters; only needed when destroyed outside the pool path.
            if (!isInPool)
            {
                var gridManager = GridManager.Instance;
                if (gridManager != null)
                {
                    gridManager.UnregisterArrow(this);
                }
            }

            var poolManager = ArrowPoolManager.Instance;
            if (poolManager != null)
            {
                poolManager.NotifyArrowDestroyed(this);
            }

            if (instantiatedEffects == null || instantiatedEffects.Count == 0)
            {
                return;
            }

            // Cache once — GameManager.Instance may stop resolving during app/scene teardown.
            var gameManager = GameManager.Instance;
            for (int i = 0; i < instantiatedEffects.Count; i++)
            {
                GameObject effect = instantiatedEffects[i];
                if (effect != null && gameManager != null)
                {
                    gameManager.ReturnEffect(effect);
                }
            }
            instantiatedEffects.Clear();
        }

        private IEnumerator AutoMoveRoutine()
        {
            float moveStartTime = Time.time;
            
            // Continuous movement - No sleep between steps
            while (true)
            {
                List<Vector3> targets;
                if (!TryMoveForwardStep(out targets))
                {
                    isMoving = false;
                    yield break;
                }
                
                // Calculate accelerated speed
                float elapsed = Time.time - moveStartTime;
                float t = Mathf.Clamp01(elapsed / K_AccelerationTime);
                float currentSpeedMult = Mathf.Lerp(K_InitialSpeedMultiplier, K_TargetSpeedMultiplier, t);
                float stepDuration = K_LegacyStepDuration / currentSpeedMult;
                
                // Animate the batch move and wait for it
                yield return StartCoroutine(AnimateAllSegments(targets, stepDuration));
                
                // Check if completely escaped (no segments left)
                if (segments.Count == 0)
                {
                    isMoving = false;
                    CancelPendingPoolReturn();
                    DestroySelf();
                    yield break;
                }
            }
        }

        public bool CanMoveForward()
        {
            if (segments.Count == 0 || GridManager.Instance == null) return false;

            ArrowDependencyTree tree = GridManager.Instance.DependencyTree;
            if (tree != null && tree.IsArrowFree(this))
            {
                return true;
            }

            return GridManager.Instance.IsArrowFreeByForwardRay(this);
        }

        /// <summary>
        /// Checks if the next step position would collide with a segment.
        /// Unlike RayIntersectsSegment which checks an infinite ray, this only checks
        /// the immediate next grid position.
        /// </summary>
        private bool DoesNextStepCollideWithSegment(Vector2Int currentPos, Vector2Int direction, Vector2Int segmentStart, Vector2Int segmentEnd)
        {
            Vector2Int nextPos = currentPos + direction;
            
            // Check if nextPos lies on the line segment between segmentStart and segmentEnd
            // For cardinal directions, segments are either horizontal or vertical
            
            if (segmentStart.x == segmentEnd.x) // Vertical segment
            {
                if (nextPos.x != segmentStart.x) return false;
                int minY = Mathf.Min(segmentStart.y, segmentEnd.y);
                int maxY = Mathf.Max(segmentStart.y, segmentEnd.y);
                return nextPos.y >= minY && nextPos.y <= maxY;
            }
            else if (segmentStart.y == segmentEnd.y) // Horizontal segment
            {
                if (nextPos.y != segmentStart.y) return false;
                int minX = Mathf.Min(segmentStart.x, segmentEnd.x);
                int maxX = Mathf.Max(segmentStart.x, segmentEnd.x);
                return nextPos.x >= minX && nextPos.x <= maxX;
            }
            
            return false;
        }

        /// <summary>
        /// Checks if the next step position would collide with a point.
        /// </summary>
        private bool DoesNextStepCollideWithPoint(Vector2Int currentPos, Vector2Int direction, Vector2Int point)
        {
            Vector2Int nextPos = currentPos + direction;
            return nextPos == point;
        }

        private bool RayIntersectsSegment(Vector2 rayOrigin, Vector2 rayDir, Vector2 p1, Vector2 p2)
        {
            // Cardinal directions only
            if (rayDir.x != 0) // Horizontal ray
            {
                if (p1.x == p2.x) // Vertical segment
                {
                    float minSY = Mathf.Min(p1.y, p2.y);
                    float maxSY = Mathf.Max(p1.y, p2.y);
                    if (rayDir.x > 0) return p1.x > rayOrigin.x && rayOrigin.y >= minSY && rayOrigin.y <= maxSY;
                    else return p1.x < rayOrigin.x && rayOrigin.y >= minSY && rayOrigin.y <= maxSY;
                }
                else if (p1.y == p2.y) // Horizontal segment
                {
                    if (Mathf.Abs(p1.y - rayOrigin.y) > 0.01f) return false;
                    float minSX = Mathf.Min(p1.x, p2.x);
                    float maxSX = Mathf.Max(p1.x, p2.x);
                    if (rayDir.x > 0) return maxSX > rayOrigin.x;
                    else return minSX < rayOrigin.x;
                }
            }
            else // Vertical ray
            {
                if (p1.y == p2.y) // Horizontal segment
                {
                    float minSX = Mathf.Min(p1.x, p2.x);
                    float maxSX = Mathf.Max(p1.x, p2.x);
                    if (rayDir.y > 0) return p1.y > rayOrigin.y && rayOrigin.x >= minSX && rayOrigin.x <= maxSX;
                    else return p1.y < rayOrigin.y && rayOrigin.x >= minSX && rayOrigin.x <= maxSX;
                }
                else if (p1.x == p2.x) // Vertical segment
                {
                    if (Mathf.Abs(p1.x - rayOrigin.x) > 0.01f) return false;
                    float minSY = Mathf.Min(p1.y, p2.y);
                    float maxSY = Mathf.Max(p1.y, p2.y);
                    if (rayDir.y > 0) return maxSY > rayOrigin.y;
                    else return minSY < rayOrigin.y;
                }
            }
            return false;
        }

        private bool RayIntersectsPoint(Vector2 rayOrigin, Vector2 rayDir, Vector2 point)
        {
            if (rayDir.x > 0) return Mathf.Abs(point.y - rayOrigin.y) < 0.01f && point.x > rayOrigin.x;
            if (rayDir.x < 0) return Mathf.Abs(point.y - rayOrigin.y) < 0.01f && point.x < rayOrigin.x;
            if (rayDir.y > 0) return Mathf.Abs(point.x - rayOrigin.x) < 0.01f && point.y > rayOrigin.y;
            if (rayDir.y < 0) return Mathf.Abs(point.x - rayOrigin.x) < 0.01f && point.y < rayOrigin.y;
            return false;
        }

        private bool TryMoveForwardStep(out List<Vector3> targetWorldPositions)
        {
            targetWorldPositions = null;
            if (!CanMoveForward()) return false;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            m_CurrentVisualDirection = currentDir;
            Vector2Int targetPos = head.GridPosition + currentDir;
            bool isEscaping = GridManager.Instance.IsOutOfBounds(targetPos);

            Vector2Int oldTailPos = segments[0].GridPosition;
            GridManager.Instance.ReleaseOccupancy(oldTailPos);

            // Reuse shared buffer — no per-step allocation
            _newPositions.Clear();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                _newPositions.Add(segments[i+1].GridPosition); 
            }
            _newPositions.Add(targetPos);
            
            if (!isEscaping)
            {
                GridManager.Instance.RegisterOccupancy(targetPos, this);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].GridPosition = _newPositions[i];
            }
            
            forceLineUpdate = true;

            // Reuse shared target buffer
            _targetWorldPos.Clear();
            foreach (var pos in _newPositions)
            {
                _targetWorldPos.Add(new Vector3(pos.x * CellSize, pos.y * CellSize, 0));
            }
            targetWorldPositions = _targetWorldPos;
            
            return true;
        }

        private int animationFrameCounter = 0;
        
        private IEnumerator AnimateAllSegments(List<Vector3> targets, float duration)
        {
            int count = segments.Count;
            if (count == 0 || targets == null || targets.Count != count) yield break;

            if (_animationStarts.Length < count) _animationStarts = new Vector3[count + 8];
            for (int i = 0; i < count; i++)
            {
                if (i < segments.Count && segments[i] != null)
                    _animationStarts[i] = segments[i].CachedTransform.position;
            }

            // Entrance/growth stays every frame; fast movement can skip every other visual rebuild.
            int updateFrequency = duration <= 0.05f ? 2 : 1;
            animationFrameCounter = 0;

            float elapsed = 0;
            while (elapsed < duration)
            {
                if (this == null) yield break; // Object destroyed during yield
                float t = elapsed / duration;
                for (int i = 0; i < count; i++)
                {
                    // CRITICAL: Check bounds as segments or targets might change during yield (e.g. DestroySelf)
                    if (i < segments.Count && i < targets.Count)
                    {
                        if (segments[i] != null)
                            segments[i].CachedTransform.position = Vector3.Lerp(_animationStarts[i], targets[i], t);
                    }
                }
                
                animationFrameCounter++;
                if (animationFrameCounter >= updateFrequency)
                {
                    UpdateVisuals();
                    animationFrameCounter = 0;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (this == null) yield break;

            for (int i = 0; i < count; i++)
            {
                if (i < segments.Count && i < targets.Count)
                {
                    if (segments[i] != null) segments[i].CachedTransform.position = targets[i];
                }
            }
            UpdateVisuals(); // Final sync
        }

        private void UpdateHeadVisuals()
        {
            int count = segments.Count;
            if (count == 0) return;
            
            Segment headSegment = segments[count - 1];
            
            bool directionChanged = m_CurrentVisualDirection != m_LastAppliedVisualDirection;
            if (m_LastHeadSegment == headSegment && !forceVisualsUpdate && !directionChanged) return;
            m_LastHeadSegment = headSegment;
            m_LastAppliedVisualDirection = m_CurrentVisualDirection;
            forceVisualsUpdate = false;
            
            for (int i = 0; i < count; i++)
            {
                Segment seg = segments[i];
                if (i == count - 1)
                {
                    // HEAD
                    if (seg.Renderer != null)
                    {
                        seg.Renderer.enabled = true;
                        seg.Renderer.sprite = HeadSprite;
                        seg.Renderer.color = currentArrowColor;
                        seg.Renderer.sortingOrder = 10;
                        seg.CachedTransform.localScale = HeadScale;
                        
                        float angle = Mathf.Atan2(m_CurrentVisualDirection.y, m_CurrentVisualDirection.x) * Mathf.Rad2Deg - 90f;
                        seg.CachedTransform.rotation = Quaternion.Euler(0, 0, angle);
                    }
                }
                else
                {
                    // BODY - Hide redundant renderers
                    if (seg.Renderer != null && seg.Renderer.enabled)
                    {
                        seg.Renderer.enabled = false;
                    }
                }
            }
            
            if (lineRenderer != null && lineRenderer.sortingOrder != 5) 
                lineRenderer.sortingOrder = 5;
        }

        public void ShowPreview()
        {
            if (segments.Count == 0 || isMoving) return;

            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;

            Vector3 startPos = head.transform.position;
            Vector3 endPos = startPos + new Vector3(currentDir.x, currentDir.y, 0) * 60f * CellSize;

            previewLineRenderer.gameObject.SetActive(true);
            previewLineRenderer.positionCount = 2;
            previewLineRenderer.SetPosition(0, startPos);
            previewLineRenderer.SetPosition(1, endPos);
        }

        public void HidePreview()
        {
            if (previewLineRenderer != null)
            {
                previewLineRenderer.positionCount = 0;
                previewLineRenderer.gameObject.SetActive(false);
            }
        }

        // ===== Blocked Arrow Animation System =====

        private void SaveCurrentPositions(List<Vector2Int> targetList)
        {
            targetList.Clear();
            for (int i = 0; i < segments.Count; i++)
            {
                targetList.Add(segments[i].GridPosition);
            }
        }

        private void RestorePositions(List<Vector2Int> positions)
        {
            for (int i = 0; i < segments.Count && i < positions.Count; i++)
            {
                segments[i].GridPosition = positions[i];
            }
        }

        /// <summary>
        /// Checks if the next step forward would collide with another arrow.
        /// This is different from CanMoveForward - it only checks the immediate next position,
        /// not whether the arrow is legally allowed to move.
        /// Used for simulating blocked arrow movement.
        /// </summary>
        private bool IsNextStepBlocked()
        {
            if (segments.Count == 0) return true;
            
            Vector2Int currentDir = m_LookDirection;
            Vector2Int nextPos = segments[segments.Count - 1].GridPosition + currentDir;

            if (GridManager.Instance.IsOutOfBounds(nextPos)) 
                return true;

            ArrowController occupant = GridManager.Instance.GetOccupant(nextPos);
            if (occupant != null && occupant != this)
            {
                return true;
            }
            
            return false;
        }

        private int CalculateStepsUntilBlocked()
        {
            if (segments.Count == 0) return 0;

            int steps = 0;
            const int MAX_SIMULATION_STEPS = 50; // Safety limit
            
            // Save current state
            SaveCurrentPositions(_savedPositions);
            List<Vector2Int> originalPositions = new List<Vector2Int>(_savedPositions);
            
            // Simulate forward movement until blocked
            while (steps < MAX_SIMULATION_STEPS)
            {
                // Check if the next step would hit an obstacle
                if (IsNextStepBlocked())
                {
                    break;
                }

                
                // Simulate one step forward (update positions without visual animation)
                Segment head = segments[segments.Count - 1];
                Vector2Int currentDir = m_LookDirection;
                Vector2Int targetPos = head.GridPosition + currentDir;
                
                // Shift all segments forward
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    segments[i].GridPosition = segments[i + 1].GridPosition;
                }
                segments[segments.Count - 1].GridPosition = targetPos;
                
                steps++;
            }
            
            // Restore original state
            RestorePositions(originalPositions);
            
            return steps;
        }

        private IEnumerator SimulateForwardStep()
        {
            if (segments.Count == 0) yield break;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            Vector2Int targetPos = head.GridPosition + currentDir;
            
            for (int i = 0; i < segments.Count - 1; i++)
            {
                segments[i].GridPosition = segments[i + 1].GridPosition;
            }
            segments[segments.Count - 1].GridPosition = targetPos;
            
            // Reuse shared buffer
            _targetWorldPos.Clear();
            foreach (var seg in segments)
                _targetWorldPos.Add(new Vector3(seg.GridPosition.x * CellSize, seg.GridPosition.y * CellSize, 0));
            
            yield return StartCoroutine(AnimateAllSegments(_targetWorldPos, K_BaseMoveDuration));
        }

        private IEnumerator SimulateReverseStep()
        {
            if (segments.Count == 0) yield break;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            for (int i = segments.Count - 1; i > 0; i--)
            {
                segments[i].GridPosition = segments[i - 1].GridPosition;
            }
            segments[0].GridPosition = head.GridPosition - currentDir;
            
            // Reuse shared buffer
            _targetWorldPos.Clear();
            foreach (var seg in segments)
                _targetWorldPos.Add(new Vector3(seg.GridPosition.x * CellSize, seg.GridPosition.y * CellSize, 0));
            
            yield return StartCoroutine(AnimateAllSegments(_targetWorldPos, K_BaseMoveDuration));
        }

        private IEnumerator BlockedArrowAnimation()
        {
            // CRITICAL: Save original positions BEFORE any simulation
            // OPTIMIZATION #2: Reuse _savedPositions buffer
            SaveCurrentPositions(_savedPositions);
            List<Vector2Int> originalPositionsSnapshot = new List<Vector2Int>(_savedPositions);
            
            // Calculate how many steps we can move before hitting the blocking arrow
            int stepsUntilBlocked = CalculateStepsUntilBlocked();
            
            // Clear history and store starting position
            foreach (var arr in m_ForwardHistoryBuffer) ReturnArrayToPool(arr);
            m_ForwardHistoryBuffer.Clear();

            Vector2Int[] startArr = GetArrayFromPool(segments.Count);
            for (int i = 0; i < segments.Count; i++) startArr[i] = segments[i].GridPosition;
            m_ForwardHistoryBuffer.Add(startArr);
            
            // Phase 1: Forward animation (simulate moving until blocked)
            for (int i = 0; i < stepsUntilBlocked; i++)
            {
                yield return StartCoroutine(SimulateForwardStep());
                
                // Save current positions after this step into history pool
                Vector2Int[] stepArr = GetArrayFromPool(segments.Count);
                for (int j = 0; j < segments.Count; j++) stepArr[j] = segments[j].GridPosition;
                m_ForwardHistoryBuffer.Add(stepArr);
            }
            
            // Phase 2: Half-step impact toward the blocker
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            Vector3 currentHeadWorldPos = head.transform.position;
            Vector3 impactOffset   = new Vector3(currentDir.x * 0.5f * CellSize, currentDir.y * 0.5f * CellSize, 0);
            Vector3 impactPosition = currentHeadWorldPos + impactOffset;
            
            // Reuse shared impact buffer
            _impactTargets.Clear();
            for (int i = 0; i < segments.Count - 1; i++) _impactTargets.Add(segments[i].transform.position);
            _impactTargets.Add(impactPosition);
            yield return StartCoroutine(AnimateAllSegments(_impactTargets, K_BaseMoveDuration));
            
            // Phase 3: Impact feedback
            SoundManager.Instance.PlayArrowBlocked();
            VibrationManager.VibrateSelection();
            SetArrowColor(blockedColor);
            m_IsMarkedBlocked = true;
            GameManager.Instance.PlayWrongAnimation();
            yield return s_BlockedPause;
            
            // Phase 4: Reverse animation - replay positions in reverse order
            for (int step = m_ForwardHistoryBuffer.Count - 2; step >= 0; step--)
            {
                Vector2Int[] targetPositions = m_ForwardHistoryBuffer[step];
                // Reuse shared buffer
                _targetWorldPos.Clear();
                
                for (int i = 0; i < segments.Count; i++)
                {
                    segments[i].GridPosition = targetPositions[i];
                    _targetWorldPos.Add(new Vector3(targetPositions[i].x * CellSize, targetPositions[i].y * CellSize, 0));
                }
                
                yield return StartCoroutine(AnimateAllSegments(_targetWorldPos, K_BaseMoveDuration));
            }
            
            RestorePositions(originalPositionsSnapshot);
            for (int i = 0; i < segments.Count; i++)
            {
                Vector3 originalWorldPos = new Vector3(originalPositionsSnapshot[i].x * CellSize, originalPositionsSnapshot[i].y * CellSize, 0);
                segments[i].transform.position = originalWorldPos;
            }
            forceLineUpdate = true;
            UpdateVisuals();
            
            // Cleanup history pool
            foreach (var arr in m_ForwardHistoryBuffer) ReturnArrayToPool(arr);
            m_ForwardHistoryBuffer.Clear();
        }

        /// <summary>Shuffle booster: snake-walk along planned head cells (4-way), same step animation as tap.</summary>
        public IEnumerator ShuffleRelocateRoutine(IReadOnlyList<Vector2Int> headSteps)
        {
            if (headSteps == null || headSteps.Count == 0 || segments.Count == 0) yield break;

            isMoving = true;
            float moveStartTime = Time.time;

            for (int s = 0; s < headSteps.Count; s++)
            {
                if (!TryApplyShuffleStep(headSteps[s], out List<Vector3> targets))
                {
                    break;
                }

                float elapsed = Time.time - moveStartTime;
                float t = Mathf.Clamp01(elapsed / K_AccelerationTime);
                float speedMult = Mathf.Lerp(K_InitialSpeedMultiplier, K_TargetSpeedMultiplier, t);
                float stepDuration = K_LegacyStepDuration / speedMult;

                yield return StartCoroutine(AnimateAllSegments(targets, stepDuration));
            }

            ResetShuffleInteractionState();
        }

        /// <summary>Restores tap input after shuffle (grid sync + clears isMoving).</summary>
        public void ResetShuffleInteractionState()
        {
            isMoving = false;
            SyncSegmentsToGridPositions();
            forceLineUpdate = true;
            forceVisualsUpdate = true;
            UpdateVisuals();
        }

        /// <summary>Aligns transforms with grid after shuffle so physics/input match occupancy.</summary>
        public void SyncSegmentsToGridPositions()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                Segment seg = segments[i];
                if (seg == null) continue;
                Vector3 worldPos = new Vector3(seg.GridPosition.x * CellSize, seg.GridPosition.y * CellSize, 0f);
                seg.CachedTransform.position = worldPos;
            }
            forceLineUpdate = true;
        }

        private bool TryApplyShuffleStep(Vector2Int newHeadCell, out List<Vector3> targetWorldPositions)
        {
            targetWorldPositions = null;
            if (segments.Count == 0 || GridManager.Instance == null) return false;

            Vector2Int currentHead = segments[segments.Count - 1].GridPosition;
            Vector2Int delta = newHeadCell - currentHead;
            if (delta == Vector2Int.zero) return false;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1) return false;

            ArrowController occupant = GridManager.Instance.GetOccupant(newHeadCell);
            if (occupant != null && occupant != this) return false;

            // Head sprite and escape line must both face the movement direction.
            m_LookDirection = delta;
            m_CurrentVisualDirection = delta;
            forceVisualsUpdate = true;

            _newPositions.Clear();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                _newPositions.Add(segments[i + 1].GridPosition);
            }
            _newPositions.Add(newHeadCell);

            for (int i = 0; i < segments.Count; i++)
            {
                GridManager.Instance.ReleaseOccupancy(segments[i].GridPosition);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].GridPosition = _newPositions[i];
                GridManager.Instance.RegisterOccupancy(_newPositions[i], this);
            }

            forceLineUpdate = true;

            _targetWorldPos.Clear();
            foreach (var pos in _newPositions)
            {
                _targetWorldPos.Add(new Vector3(pos.x * CellSize, pos.y * CellSize, 0));
            }
            targetWorldPositions = _targetWorldPos;
            return true;
        }

        public void SetLookDirectionToNearestEscape()
        {
            if (segments.Count == 0 || GridManager.Instance == null) return;

            Vector2Int head = GetHeadGridPosition();
            Vector2Int bestDir = m_LookDirection;
            int bestClear = -1;

            Vector2Int[] dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            for (int i = 0; i < dirs.Length; i++)
            {
                Vector2Int dir = dirs[i];
                Vector2Int check = head + dir;
                int clear = 0;
                while (!GridManager.Instance.IsOutOfBounds(check))
                {
                    ArrowController occupant = GridManager.Instance.GetOccupant(check);
                    if (occupant != null && occupant != this) break;
                    clear++;
                    check += dir;
                }
                if (clear > bestClear)
                {
                    bestClear = clear;
                    bestDir = dir;
                }
            }

            m_LookDirection = bestDir;
            m_CurrentVisualDirection = bestDir;
        }

    }
}
