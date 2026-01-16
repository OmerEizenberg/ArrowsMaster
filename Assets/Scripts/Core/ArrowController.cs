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

        // LineRenderer Refactor
        private LineRenderer lineRenderer;

        public void Initialize(ArrowData data)
        {
            ArrowId = data.id;
            Color color = Color.white;
            
            // Setup LineRenderer
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null) lineRenderer = gameObject.AddComponent<LineRenderer>();
            
            lineRenderer.startWidth = 0.2f; // 20 pixels (0.2 units at 100 PPU)
            lineRenderer.endWidth = 0.2f;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 5;
            lineRenderer.numCornerVertices = 5;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default")); 
            lineRenderer.startColor = Color.black;
            lineRenderer.endColor = Color.black;
            lineRenderer.sortingOrder = 0; // Behind head

            // Instantiate segments from data.path (Tail to Head)
            for (int i = 0; i < data.path.Count; i++)
            {
                Vector2Int pos = data.path[i].ToVector2Int();
                Vector3 worldPos = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
                
                GameObject segObj = Instantiate(segmentPrefab.gameObject, worldPos, Quaternion.identity, transform);
                segObj.transform.localScale = Vector3.one; 

                Segment seg = segObj.GetComponent<Segment>();
                seg.GridPosition = pos;
                
                if (i == data.path.Count - 1)
                {
                    // HEAD: Keep Sprite, Set Order
                    seg.Renderer.sprite = HeadSprite;
                    seg.Renderer.enabled = true;
                    seg.Renderer.sortingOrder = 1; // Above line
                }
                else
                {
                    // But keep Collider!
                    seg.Renderer.enabled = false;
                }
                
                // Ensure tight collider
                BoxCollider2D box = seg.GetComponent<BoxCollider2D>();
                if (box == null) box = seg.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 1f); 
                
                segments.Add(seg);
                
                GridManager.Instance.RegisterOccupancy(pos, this);
            }
            
            UpdateHeadVisuals();
            UpdateLinePositions();
            
            GameManager.Instance.RegisterArrow();
        }
        
        private void Update()
        {
            // Continuously update line to follow the moving segments
            UpdateLinePositions();
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
                        // Start destruction timer immediately on click (5 seconds)
                        Invoke("DestroySelf", 5.0f);
                        moveCoroutine = StartCoroutine(AutoMoveRoutine());
                    }
                    else
                    {
                        SoundManager.Instance.PlayArrowBlocked();
                        // Optional: Shake feedback?
                        Debug.Log("Arrow Blocked!");
                    }
                }
            }
        }

        private void DestroySelf()
        {
            GameManager.Instance.NotifyArrowSuccess();
            Destroy(gameObject,1.0f);
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
            
            // Check FULL PATH until Out of Bounds
            Vector2Int checkPos = head.GridPosition + currentDir;
            
            while (!GridManager.Instance.IsOutOfBounds(checkPos))
            {
                if (GridManager.Instance.IsCellOccupied(checkPos))
                {
                    // Blocked by something (another arrow or self, though self-intersection straight ahead is impossible)
                    return false;
                }
                checkPos += currentDir;
            }
            
            // If we loop until OutOfBounds without hitting anything, path is clear.
            return true;
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
                    seg.Renderer.color = Color.black; // Match line color
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
    }
}
