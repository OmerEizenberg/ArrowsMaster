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
        public GameObject pointEffectPrefab;
        private List<GameObject> instantiatedEffects = new List<GameObject>();
        
        // Grid Step size (Standard 1 unit)
        public const float CellSize = 1.0f;
        [SerializeField] private float segmentScale = 1.0f; 

        private bool isMoving = false;
        private Coroutine moveCoroutine;
        
        public int ArrowId { get; private set; }
        private ArrowData cachedData;
        private bool hasReducedLife = false;
        private Vector2Int m_LookDirection = Vector2Int.up;

        // LineRenderer Refactor
        private LineRenderer lineRenderer;
        private LineRenderer previewLineRenderer;

        public void Initialize(ArrowData data)
        {
            PrepareIncrementalInit(data);
            for (int i = 0; i < data.path.Count; i++)
            {
                SpawnSegmentStep(i, true);
            }
        }

        public void PrepareIncrementalInit(ArrowData data)
        {
            cachedData = data;
            ArrowId = data.id;
            
            // Setup LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
            
            lineRenderer.startWidth = 0.2f; 
            lineRenderer.endWidth = 0.2f;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default")); 
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.sortingOrder = 0; 
            hasReducedLife = false;

            // Setup Preview LineRenderer
            GameObject previewObj = new GameObject("PreviewLine");
            previewObj.transform.SetParent(this.transform);
            previewLineRenderer = previewObj.AddComponent<LineRenderer>();
            previewLineRenderer.startWidth = 0.1f;
            previewLineRenderer.endWidth = 0.1f;
            previewLineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            previewLineRenderer.startColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Grey Transparent
            previewLineRenderer.endColor = new Color(0.5f, 0.5f, 0.5f, 0.2f); // Fading
            previewLineRenderer.useWorldSpace = true;
            previewLineRenderer.positionCount = 0;
            previewLineRenderer.sortingOrder = -1; // Behind head

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
            if (cachedData == null || step >= cachedData.path.Count) return;

            // 1. Create a new segment at index 0 (the tail end of the growing path)
            Vector2Int spawnPos = cachedData.path[0].ToVector2Int();
            Vector3 worldSpawnPos = new Vector3(spawnPos.x * CellSize, spawnPos.y * CellSize, 0);

            GameObject segObj = Instantiate(segmentPrefab.gameObject, worldSpawnPos, Quaternion.identity, transform);
            segObj.transform.localScale = Vector3.one;
            Segment newSeg = segObj.GetComponent<Segment>();
            
            segments.Insert(0, newSeg);

            BoxCollider2D box = newSeg.GetComponent<BoxCollider2D>();
            if (box == null) box = newSeg.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f);

            // 2. Determine target positions for all segments in this growth step
            List<Vector3> targets = new List<Vector3>();
            for (int i = 0; i < segments.Count; i++)
            {
                int pathIndex = step - (segments.Count - 1 - i);
                Vector2Int pos = cachedData.path[pathIndex].ToVector2Int();
                segments[i].GridPosition = pos;
                Vector3 targetWorldPos = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
                targets.Add(targetWorldPos);
                
                if (instant) segments[i].transform.position = targetWorldPos;

                GridManager.Instance.RegisterOccupancy(pos, this);
            }

            forceLineUpdate = true;
            if (!instant)
            {
                // If not instant, we rely on the caller to start the animation coroutine
                // with the calculated targets. But for better API, we'll keep the IEnumerator wrapper.
            }
        }

        public IEnumerator UpdateGrowthSlide(int step, float duration)
        {
            if (cachedData == null || step >= cachedData.path.Count) yield break;

            // Use the synch helper to setup data, but don't place instantly
            SpawnSegmentStep(step, false);

            // Calculate targets again for the animation (or we could pass them out)
            List<Vector3> targets = new List<Vector3>();
            for (int i = 0; i < segments.Count; i++)
            {
                targets.Add(new Vector3(segments[i].GridPosition.x * CellSize, segments[i].GridPosition.y * CellSize, 0));
            }

            yield return StartCoroutine(AnimateAllSegments(targets, duration));
        }

        public Vector3 GetHeadPosition()
        {
            if (segments.Count == 0) return transform.position;
            return segments[segments.Count - 1].transform.position;
        }

        private void Update()
        {
            // Update logic (if any)
        }

        private void LateUpdate()
        {
            // Sync visuals in LateUpdate after all positions have been updated in this frame
            UpdateLinePositions();
            UpdateHeadVisuals();
        }

        private List<Vector3> linePoints = new List<Vector3>();
        private Vector3[] linePointsArray;
        private Vector3 lastHeadPos;
        private bool forceLineUpdate = true;

        private void UpdateLinePositions()
        {
            if (lineRenderer == null || segments.Count == 0)
            {
                if (lineRenderer != null) lineRenderer.positionCount = 0;
                return;
            }

            // Performance: Only update if the head has moved significantly or update is forced
            // Using a slightly more sensitive threshold for smooth visuals
            Vector3 currentHeadPos = segments[segments.Count - 1].transform.position;
            if (!forceLineUpdate && (currentHeadPos - lastHeadPos).sqrMagnitude < 0.00001f)
            {
                return;
            }
            lastHeadPos = currentHeadPos;
            forceLineUpdate = false;

            linePoints.Clear();
            
            // Junction Corners (prevent geometry gaps during turns)
            for (int i = 0; i < segments.Count - 1; i++)
            {
                // Current segment visual pos
                linePoints.Add(segments[i].transform.position);

                // Add a corner anchor ONLY if the segments are currently in a turn
                // We detect a turn if their target GridPositions don't share an axis with their visual positions
                Vector3 p1 = segments[i].transform.position;
                Vector3 p2 = segments[i+1].transform.position;
                
                // If they are not aligned on X or Y visually, we need a junction point
                if (Mathf.Abs(p1.x - p2.x) > 0.01f && Mathf.Abs(p1.y - p2.y) > 0.01f)
                {
                    // The junction is at the GridPosition that they both "share" during this step
                    // This is usually the GridPosition of the leading segment (i+1) before it moved
                    // or simply the grid point at the corner.
                    Vector2Int g1 = segments[i].GridPosition;
                    Vector2Int g2 = segments[i+1].GridPosition;
                    
                    // The junction is the target position of the follower segment
                    Vector3 corner = new Vector3(g1.x * CellSize, g1.y * CellSize, currentHeadPos.z);
                    linePoints.Add(corner);
                }
            }
            
            // 3. Always add the final visual position of the head at the VERY end
            linePoints.Add(segments[segments.Count - 1].transform.position);
            
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

        public void OnArrowClicked(Segment clickedSegment)
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanInteract) return;

            // Allow clicking ANY segment
            if (segments.Contains(clickedSegment))
            {
                SoundManager.Instance.PlayArrowSelect();
                GameManager.Instance.ResetHintTimer();

                if (!isMoving)
                {
                    // Check if path is clear BEFORE starting
                    if (CanMoveForward())
                    {
                        isMoving = true;
                        VibrationManager.VibrateSelection();

                        if (CameraController.Instance != null && GameManager.Instance != null)
                        {
                            bool timeCondition = (Time.time - GameManager.Instance.LastArrowSelectionTime) <= 2.0f;
                            bool panCondition = !CameraController.Instance.HasPannedSinceLastReset;

                            // Update state for next pick
                            GameManager.Instance.NotifyArrowSelection();
                            CameraController.Instance.ResetPanState();
                        }
                        float tempProbLike = Random.Range(0f,1f);
                        if (tempProbLike < 0.12f)
                        {
                            SoundManager.Instance.PlayLike();
                            if (pointEffectPrefab != null)
                            {
                                foreach (var seg in segments)
                                {
                                    GameObject effect = Instantiate(pointEffectPrefab, seg.transform.position, Quaternion.identity);
                                    instantiatedEffects.Add(effect);
                                }
                            }
                        }
                        // Instantiate prefabs at each arrow point
                        
                        // Start success color animation (White -> Green -> White)
                        StartCoroutine(SuccessColorAnimation());

                        // Start destruction timer immediately on click (5 seconds)
                        Invoke("DestroySelf", 5.0f);
                        moveCoroutine = StartCoroutine(AutoMoveRoutine());
                        
                        // Notify GameManager that this arrow is moving (solved)
                        GameManager.Instance.NotifyArrowSuccess(); 
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
            Color startFlashColor = Color.black; // #000000
            float duration = 0.5f;
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

            foreach (var seg in segments)
            {
                if (seg.Renderer.enabled) // Usually just the head
                {
                    seg.Renderer.color = color;
                }
            }
        }

        private void DestroySelf()
        {
            GridManager.Instance.UnregisterArrow(this);
            // Destroy all instantiated effects
            foreach (var effect in instantiatedEffects)
            {
                if (effect != null) Destroy(effect);
            }
            instantiatedEffects.Clear();
            Destroy(gameObject, 0.5f);
        }

        private void OnDestroy()
        {
            // Fallback cleanup if destroyed by other means
            foreach (var effect in instantiatedEffects)
            {
                if (effect != null) Destroy(effect);
            }
            instantiatedEffects.Clear();
        }

        private IEnumerator AutoMoveRoutine()
        {
            // Continuous movement - No sleep between steps
            while (true)
            {
                List<Vector3> targets;
                if (!TryMoveForwardStep(out targets))
                {
                    isMoving = false;
                    yield break;
                }
                
                // Animate the batch move and wait for it
                yield return StartCoroutine(AnimateAllSegments(targets, 0.04f));
                
                // Check if completely escaped (no segments left)
                if (segments.Count == 0)
                {
                    yield break;
                }
            }
        }

        public bool CanMoveForward()
        {
            if (segments.Count == 0) return false;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            Vector2 headPos = new Vector2(head.GridPosition.x, head.GridPosition.y);
            Vector2 dirVec = new Vector2(currentDir.x, currentDir.y);

            foreach (var otherArrow in GridManager.Instance.GetAllArrows())
            {
                if (otherArrow == this || otherArrow.isMoving) continue;
                
                // PERFORMANCE: Broad phase check using integer grid coords
                foreach (var seg in otherArrow.segments)
                {
                    Vector2Int gp = seg.GridPosition;
                    bool potentialBlocker = false;
                    if (currentDir.x > 0) potentialBlocker = gp.x > head.GridPosition.x && gp.y == head.GridPosition.y;
                    else if (currentDir.x < 0) potentialBlocker = gp.x < head.GridPosition.x && gp.y == head.GridPosition.y;
                    else if (currentDir.y > 0) potentialBlocker = gp.y > head.GridPosition.y && gp.x == head.GridPosition.x;
                    else if (currentDir.y < 0) potentialBlocker = gp.y < head.GridPosition.y && gp.x == head.GridPosition.x;
                    
                    if (potentialBlocker)
                    {
                        // Found at least one segment in the path, now do exact checks
                        for (int i = 0; i < otherArrow.segments.Count - 1; i++)
                        {
                            Vector2 p1 = new Vector2(otherArrow.segments[i].GridPosition.x, otherArrow.segments[i].GridPosition.y);
                            Vector2 p2 = new Vector2(otherArrow.segments[i+1].GridPosition.x, otherArrow.segments[i+1].GridPosition.y);
                            if (RayIntersectsSegment(headPos, dirVec, p1, p2)) return false;
                        }
                        if (otherArrow.segments.Count == 1)
                        {
                            Vector2 pH = new Vector2(otherArrow.segments[0].GridPosition.x, otherArrow.segments[0].GridPosition.y);
                            if (RayIntersectsPoint(headPos, dirVec, pH)) return false;
                        }
                        break; 
                    }
                }
            }
            
            return true;
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
            Vector2Int targetPos = head.GridPosition + currentDir;
            bool isEscaping = GridManager.Instance.IsOutOfBounds(targetPos);

            Vector2Int oldTailPos = segments[0].GridPosition;
            GridManager.Instance.ReleaseOccupancy(oldTailPos);

            List<Vector2Int> newPositions = new List<Vector2Int>();
            for (int i = 0; i < segments.Count - 1; i++)
            {
                newPositions.Add(segments[i+1].GridPosition); 
            }
            newPositions.Add(targetPos);
            
            if (!isEscaping)
            {
                GridManager.Instance.RegisterOccupancy(targetPos, this);
            }

            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].GridPosition = newPositions[i];
            }
            
            forceLineUpdate = true;

            // Prepare target world positions for batch animation
            targetWorldPositions = new List<Vector3>();
            foreach (var pos in newPositions)
            {
                targetWorldPositions.Add(new Vector3(pos.x * CellSize, pos.y * CellSize, 0));
            }
            
            return true;
        }

        private IEnumerator AnimateAllSegments(List<Vector3> targets, float duration)
        {
            int count = segments.Count;
            if (count == 0 || targets.Count != count) yield break;

            Vector3[] starts = new Vector3[count];
            for (int i = 0; i < count; i++) starts[i] = segments[i].transform.position;

            float elapsed = 0;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                for (int i = 0; i < count; i++)
                {
                    segments[i].transform.position = Vector3.Lerp(starts[i], targets[i], t);
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < count; i++) segments[i].transform.position = targets[i];
        }

        private void UpdateHeadVisuals()
        {
            if (segments.Count == 0) return;
            
            // Ensure only Head is enabled, others disabled
            for (int i = 0; i < segments.Count; i++)
            {
                Segment seg = segments[i];
                if (i == segments.Count - 1)
                {
                    // HEAD
                    seg.Renderer.enabled = true;
                    seg.Renderer.sprite = HeadSprite;
                    seg.Renderer.color = currentArrowColor; // Explicitly set color here
                    seg.Renderer.sortingOrder = 10;   // Ensure on top
                    
                    // Rotation - Use explicit look direction from level data
                    float angle = Mathf.Atan2(m_LookDirection.y, m_LookDirection.x) * Mathf.Rad2Deg - 90f;
                    seg.transform.rotation = Quaternion.Euler(0, 0, angle);
#if UNITY_EDITOR
                    // Helpful for debugging in scene view
                    seg.name = $"Head_{ArrowId}_{cachedData?.lookDirection}";
#endif
                }
                else
                {
                    // BODY
                    seg.Renderer.enabled = false;
                }
            }
        }

        public void ShowPreview()
        {
            if (segments.Count == 0 || isMoving) return;

            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;

            Vector3 startPos = head.transform.position;
            Vector3 endPos = startPos + new Vector3(currentDir.x, currentDir.y, 0) * 20f * CellSize;

            previewLineRenderer.positionCount = 2;
            previewLineRenderer.SetPosition(0, startPos);
            previewLineRenderer.SetPosition(1, endPos);
        }

        public void HidePreview()
        {
            if (previewLineRenderer != null)
            {
                previewLineRenderer.positionCount = 0;
            }
        }

        // ===== Blocked Arrow Animation System =====

        private List<Vector2Int> SaveCurrentPositions()
        {
            List<Vector2Int> positions = new List<Vector2Int>();
            foreach (var seg in segments)
            {
                positions.Add(seg.GridPosition);
            }
            return positions;
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
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            Vector2Int currentHeadPos = head.GridPosition;

            // Check collision with all other arrows
            foreach (var otherArrow in GridManager.Instance.GetAllArrows())
            {
                if (otherArrow == this) continue; // Skip self
                
                // Check intersection with each segment of the other arrow
                for (int i = 0; i < otherArrow.segments.Count - 1; i++)
                {
                    Vector2Int p1 = otherArrow.segments[i].GridPosition;
                    Vector2Int p2 = otherArrow.segments[i+1].GridPosition;
                    
                    if (DoesNextStepCollideWithSegment(currentHeadPos, currentDir, p1, p2))
                    {
                        return true; // Blocked
                    }
                }
                
                // Also check the head of the other arrow if it's the only segment
                if (otherArrow.segments.Count == 1)
                {
                    Vector2Int pH = otherArrow.segments[0].GridPosition;
                    if (DoesNextStepCollideWithPoint(currentHeadPos, currentDir, pH))
                    {
                        return true; // Blocked
                    }
                }
            }
            
            return false; // Not blocked
        }

        private int CalculateStepsUntilBlocked()
        {
            if (segments.Count == 0) return 0;

            int steps = 0;
            const int MAX_SIMULATION_STEPS = 50; // Safety limit
            
            // Save current state
            List<Vector2Int> originalPositions = SaveCurrentPositions();
            
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
            
            List<Vector3> targets = new List<Vector3>();
            foreach (var seg in segments) targets.Add(new Vector3(seg.GridPosition.x * CellSize, seg.GridPosition.y * CellSize, 0));
            
            yield return StartCoroutine(AnimateAllSegments(targets, 0.04f));
        }

        private IEnumerator SimulateReverseStep()
        {
            if (segments.Count == 0) yield break;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            // Reconstruct historical positions by moving in reverse from current Dir;
            
            for (int i = segments.Count - 1; i > 0; i--)
            {
                segments[i].GridPosition = segments[i - 1].GridPosition;
            }
            segments[0].GridPosition = head.GridPosition - currentDir; // The new tail position is one step back from the original head
            
            List<Vector3> targets = new List<Vector3>();
            foreach (var seg in segments) targets.Add(new Vector3(seg.GridPosition.x * CellSize, seg.GridPosition.y * CellSize, 0));
            
            yield return StartCoroutine(AnimateAllSegments(targets, 0.04f));
        }

        private IEnumerator BlockedArrowAnimation()
        {
            // CRITICAL: Save original positions BEFORE any simulation
            List<Vector2Int> originalPositions = SaveCurrentPositions();
            
            // Calculate how many steps we can move before hitting the blocking arrow
            int stepsUntilBlocked = CalculateStepsUntilBlocked();
            
            // Save ALL intermediate positions during forward movement
            List<List<Vector2Int>> forwardPositionHistory = new List<List<Vector2Int>>();
            forwardPositionHistory.Add(new List<Vector2Int>(originalPositions)); // Add starting position
            
            // Phase 1: Forward animation (simulate moving until blocked)
            for (int i = 0; i < stepsUntilBlocked; i++)
            {
                yield return StartCoroutine(SimulateForwardStep());
                
                // Save current positions after this step
                forwardPositionHistory.Add(SaveCurrentPositions());
            }
            
            // Phase 2: Half-step impact toward the blocker
            // Move the head (and only the head) 0.5 units toward the blocking segment
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = m_LookDirection;
            
            Vector3 currentHeadWorldPos = head.transform.position;
            Vector3 impactOffset = new Vector3(currentDir.x * 0.5f * CellSize, currentDir.y * 0.5f * CellSize, 0);
            Vector3 impactPosition = currentHeadWorldPos + impactOffset;
            
            // Animate head to impact position - using centralized animator for consistency
            List<Vector3> impactTargets = new List<Vector3>();
            for(int i=0; i < segments.Count - 1; i++) impactTargets.Add(segments[i].transform.position);
            impactTargets.Add(impactPosition);
            yield return StartCoroutine(AnimateAllSegments(impactTargets, 0.04f));
            
            // Phase 3: Impact feedback - play sound, vibrate, and change color to red
            SoundManager.Instance.PlayArrowBlocked();
            VibrationManager.VibrateSelection();
            SetArrowColor(blockedColor);
            GameManager.Instance.PlayWrongAnimation();
            // Small pause at impact for emphasis
            yield return new WaitForSeconds(0.1f);
            
            // Phase 4: Reverse animation - replay positions in reverse order
            for (int step = forwardPositionHistory.Count - 2; step >= 0; step--)
            {
                List<Vector2Int> targetPositions = forwardPositionHistory[step];
                List<Vector3> targetWorldPositions = new List<Vector3>();
                
                for (int i = 0; i < segments.Count && i < targetPositions.Count; i++)
                {
                    segments[i].GridPosition = targetPositions[i];
                    targetWorldPositions.Add(new Vector3(targetPositions[i].x * CellSize, targetPositions[i].y * CellSize, 0));
                }
                
                yield return StartCoroutine(AnimateAllSegments(targetWorldPositions, 0.04f));
            }
            
            RestorePositions(originalPositions);
            for (int i = 0; i < segments.Count; i++)
            {
                Vector3 originalWorldPos = new Vector3(originalPositions[i].x * CellSize, originalPositions[i].y * CellSize, 0);
                segments[i].transform.position = originalWorldPos;
            }
            UpdateHeadVisuals();
            
            // Keep the arrow colored red after animation completes
            // Color is already set, no need to reset
        }

    }
}
