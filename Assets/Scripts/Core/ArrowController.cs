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
        
        // Grid Step size (Standard 1 unit)
        public const float CellSize = 1.0f;
        [SerializeField] private float segmentScale = 1.0f; 

        private bool isMoving = false;
        private Coroutine moveCoroutine;
        
        public int ArrowId { get; private set; }
        private ArrowData cachedData;
        private bool hasReducedLife = false;

        // LineRenderer Refactor
        private LineRenderer lineRenderer;
        private LineRenderer previewLineRenderer;

        public void Initialize(ArrowData data)
        {
            PrepareIncrementalInit(data);
            for (int i = 0; i < data.path.Count; i++)
            {
                UpdateGrowthSlide(i);
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
            
            GameManager.Instance.RegisterArrow();
        }

        /// <summary>
        /// Slide-in animation step. 
        /// Step 0: Head appears at path[0].
        /// Step 1: Head moves to path[1], new segment appears at path[0].
        /// ...and so on.
        /// </summary>
        public bool UpdateGrowthSlide(int step)
        {
            if (cachedData == null || step >= cachedData.path.Count) return false;

            // 1. Create a new segment at index 0 of the segments list
            Vector2Int spawnPos = cachedData.path[0].ToVector2Int();
            Vector3 worldSpawnPos = new Vector3(spawnPos.x * CellSize, spawnPos.y * CellSize, 0);

            GameObject segObj = Instantiate(segmentPrefab.gameObject, worldSpawnPos, Quaternion.identity, transform);
            segObj.transform.localScale = Vector3.one;
            Segment newSeg = segObj.GetComponent<Segment>();
            
            // Always insert at the start - it represents the "newest" part of the arrow being grown from the tail
            segments.Insert(0, newSeg);

            // Ensure collider is set up for interaction
            BoxCollider2D box = newSeg.GetComponent<BoxCollider2D>();
            if (box == null) box = newSeg.gameObject.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 1f);

            // 2. Shift all segments forward along the path to their current position in this step
            // In step k, we have k+1 segments.
            // segments[0] (newest) should be at path[0].
            // segments[segments.Count - 1] (oldest/Head) should be at path[step].
            for (int i = 0; i < segments.Count; i++)
            {
                int pathIndex = step - (segments.Count - 1 - i);
                Vector2Int pos = cachedData.path[pathIndex].ToVector2Int();
                segments[i].GridPosition = pos;
                
                Vector3 targetWorldPos = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
                // To make it look like a flow, we use MoveTo.
                segments[i].MoveTo(targetWorldPos, 0.08f); 
                
                GridManager.Instance.RegisterOccupancy(pos, this);
            }

            UpdateHeadVisuals();
            UpdateLinePositions();

            return true;
        }

        private void Update()
        {
            // Continuously update line and head visuals to follow the moving segments and color state
            UpdateLinePositions();
            UpdateHeadVisuals();
        }

        private void UpdateLinePositions()
        {
            if (lineRenderer != null && segments.Count > 0)
            {
                // Refinment: Use List to filter out points too close to each other
                // This prevents "zero-length" segments at the corners which look glitchy
                
                List<Vector3> points = new List<Vector3>();
                
                for (int i = 0; i < segments.Count; i++)
                {
                    // 1. Current Position
                    points.Add(segments[i].transform.position);

                    // 2. Corner Anchor
                    if (i < segments.Count - 1)
                    {
                        Vector2Int targetGridPos = segments[i].GridPosition;
                        Vector3 cornerPos = new Vector3(targetGridPos.x * CellSize, targetGridPos.y * CellSize, 0);
                        cornerPos.z = segments[i].transform.position.z;

                        // Only add if there is significant distance to BOTH neighbors
                        // Neighbor 1: Current Segment Position
                        // Neighbor 2: Next Segment Position (which is moving away from this corner)
                        
                        float distToCurrent = Vector3.Distance(segments[i].transform.position, cornerPos);
                        float distToNext = Vector3.Distance(segments[i+1].transform.position, cornerPos);

                        if (distToCurrent > 0.05f && distToNext > 0.05f)
                        {
                            points.Add(cornerPos);
                        }
                    }
                }
                
                lineRenderer.positionCount = points.Count;
                lineRenderer.SetPositions(points.ToArray());
            }
            else if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
        }

        [SerializeField] private Color blockedColor = new Color(0.906f, 0.298f, 0.235f); // #e74c3c
        private Color currentArrowColor = Color.black;

        public void OnArrowClicked(Segment clickedSegment)
        {
            // Allow clicking ANY segment
            if (segments.Contains(clickedSegment))
            {
                SoundManager.Instance.PlayArrowSelect();

                if (!isMoving)
                {
                    // Check if path is clear BEFORE starting
                    if (CanMoveForward())
                    {
                        isMoving = true;
                        VibrationManager.VibrateSelection();
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
                        SoundManager.Instance.PlayArrowBlocked();
                        
                        // Reduce life (only once per arrow per attempt) and show visual feedback
                        if (!hasReducedLife)
                        {
                            GameManager.Instance.LoseLife();
                            hasReducedLife = true;
                            Debug.Log("Arrow Blocked! Life lost.");
                        }
                        
                        SetArrowColor(blockedColor);
                    }
                }
            }
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
            Destroy(gameObject, 0.5f);
        }

        private IEnumerator AutoMoveRoutine()
        {
            // Continuous movement - No sleep between steps
            while (true)
            {
                if (!TryMoveForward())
                {
                    isMoving = false;
                    yield break;
                }
                
                // Wait for the move animation to finish before starting next step
                // To make it continuous, we wait exactly the duration of the move.
                yield return new WaitForSeconds(0.04f); 
                
                // Check if completely escaped (no segments left)
                // Actually, TryMoveForward now clears segments if all OOB.
                if (segments.Count == 0)
                {
                    // We already set a 5s destruction timer on click, 
                    // but if it escapes fast, we might want to just let it finish.
                    // The user said "5 seconds after... pressed".
                    // So the Invoke handles it. 
                    // We can just stop the routine.
                    yield break;
                }
            }
        }

        private bool CanMoveForward()
        {
            if (segments.Count == 0) return false;
            
            Segment head = segments[segments.Count - 1];
            Vector2Int currentDir = Vector2Int.up;
            if (segments.Count >= 2)
            {
                Segment neck = segments[segments.Count - 2];
                currentDir = head.GridPosition - neck.GridPosition;
            }
            
            Vector2 headPos = new Vector2(head.GridPosition.x, head.GridPosition.y);
            Vector2 dirVec = new Vector2(currentDir.x, currentDir.y);

            foreach (var otherArrow in GridManager.Instance.GetAllArrows())
            {
                if (otherArrow == this || otherArrow.isMoving) continue;
                
                // Check intersection with each segment of the other arrow
                for (int i = 0; i < otherArrow.segments.Count - 1; i++)
                {
                    Vector2 p1 = new Vector2(otherArrow.segments[i].GridPosition.x, otherArrow.segments[i].GridPosition.y);
                    Vector2 p2 = new Vector2(otherArrow.segments[i+1].GridPosition.x, otherArrow.segments[i+1].GridPosition.y);
                    
                    if (RayIntersectsSegment(headPos, dirVec, p1, p2))
                    {
                        return false;
                    }
                }
                
                // Also check the head of the other arrow if it's the only segment
                if (otherArrow.segments.Count == 1)
                {
                    Vector2 pH = new Vector2(otherArrow.segments[0].GridPosition.x, otherArrow.segments[0].GridPosition.y);
                    if (RayIntersectsPoint(headPos, dirVec, pH))
                    {
                        return false;
                    }
                }
            }
            
            return true;
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

        private bool TryMoveForward()
        {
            if (!CanMoveForward()) return false;
            
            Segment head = segments[segments.Count - 1];
            // Calculate Dir again or cache it? Recalculating is safe.
            Vector2Int currentDir = Vector2Int.up;
            if (segments.Count >= 2)
            {
                Segment neck = segments[segments.Count - 2];
                currentDir = head.GridPosition - neck.GridPosition;
            }
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
            
            // Only update visuals for the Head
            UpdateHeadVisuals();

            for (int i = 0; i < segments.Count; i++)
            {
                Vector3 newWorldPos = new Vector3(newPositions[i].x * CellSize, newPositions[i].y * CellSize, 0);
                // Faster move speed (0.04f)
                segments[i].MoveTo(newWorldPos, 0.04f);
            }
            
            return true;
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
                    
                    // Rotation
                    if (segments.Count >= 2)
                    {
                        Segment neck = segments[segments.Count - 2];
                        Vector2Int dir = seg.GridPosition - neck.GridPosition;
                        Transform t = seg.transform;
                        if (dir == Vector2Int.up) t.rotation = Quaternion.Euler(0,0,0);
                        else if (dir == Vector2Int.right) t.rotation = Quaternion.Euler(0,0,-90);
                        else if (dir == Vector2Int.down) t.rotation = Quaternion.Euler(0,0,180);
                        else if (dir == Vector2Int.left) t.rotation = Quaternion.Euler(0,0,90);
                    }
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
            Vector2Int currentDir = Vector2Int.up;
            if (segments.Count >= 2)
            {
                Segment neck = segments[segments.Count - 2];
                currentDir = head.GridPosition - neck.GridPosition;
            }

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
    }
}
