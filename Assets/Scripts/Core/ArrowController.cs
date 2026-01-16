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
        public Sprite BodySprite;
        public Sprite CornerSprite;
        public Sprite TailSprite; 
        
        // Grid Step size (Standard 1 unit)
        public const float CellSize = 1.0f;
        [SerializeField] private float segmentScale = 1.0f; 

        private bool isMoving = false;
        private Coroutine moveCoroutine;
        
        public int ArrowId { get; private set; }

        public void Initialize(ArrowData data)
        {
            ArrowId = data.id;
            Color color = Color.white;

            // Instantiate segments from data.path (Tail to Head)
            for (int i = 0; i < data.path.Count; i++)
            {
                Vector2Int pos = data.path[i].ToVector2Int();
                
                // Convert Grid Coordinate to World Position
                Vector3 worldPos = new Vector3(pos.x * CellSize, pos.y * CellSize, 0);
                
                GameObject segObj = Instantiate(segmentPrefab.gameObject, worldPos, Quaternion.identity, transform);
                
                // Apply Scale
                segObj.transform.localScale = new Vector3(segmentScale, segmentScale, 1f);

                Segment seg = segObj.GetComponent<Segment>();
                seg.GridPosition = pos;
                
                seg.Initialize(BodySprite, color);
                
                // Ensure tight collider
                BoxCollider2D box = seg.GetComponent<BoxCollider2D>();
                if (box == null) box = seg.gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 1f); // Tight fit for 1 unit sprite
                
                segments.Add(seg);
                
                GridManager.Instance.RegisterOccupancy(pos, this);
            }
            
            UpdateSegmentVisuals();
        }

        public void OnArrowClicked(Segment clickedSegment)
        {
            // Allow clicking ANY segment
            if (segments.Contains(clickedSegment))
            {
                if (!isMoving)
                {
                    // Check if path is clear BEFORE starting
                    if (CanMoveForward())
                    {
                        isMoving = true;
                        // Start destruction timer immediately on click (3 seconds)
                        Invoke("DestroySelf", 3.0f);
                        moveCoroutine = StartCoroutine(AutoMoveRoutine());
                    }
                    else
                    {
                        // Optional: Shake feedback?
                        Debug.Log("Arrow Blocked!");
                    }
                }
            }
        }

        private void DestroySelf()
        {
            Destroy(gameObject);
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
                    // We already set a 3s destruction timer on click, 
                    // but if it escapes fast, we might want to just let it finish.
                    // The user said "3 seconds after... pressed".
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

            // CRITICAL: Update visuals BEFORE starting the move animation.
            // This ensures segments look like their DESTINATION shape while sliding.
            UpdateSegmentVisuals();

            for (int i = 0; i < segments.Count; i++)
            {
                Vector3 newWorldPos = new Vector3(newPositions[i].x * CellSize, newPositions[i].y * CellSize, 0);
                // Faster move speed (0.04f)
                segments[i].MoveTo(newWorldPos, 0.04f);
            }

            bool allOOB = true;
            foreach(var seg in segments)
            {
                if (!GridManager.Instance.IsOutOfBounds(seg.GridPosition))
                {
                    allOOB = false;
                    break;
                }
            }
            
            if (allOOB)
            {
                // Simpler: Just destroy the object 6 seconds after the HEAD exits? Or the TAIL?
                // Probably Tail.
                // Let's check strict "All OOB".
            }
            // But wait, if I don't remove segments from the list, `segments.Count` is never 0.
            // So my previous code `if (segments.Count == 0)` was dead code unless I removed them.
            // I should probably NOT remove them, just let them exist in OOB coordinates.
            
            UpdateSegmentVisuals();
            return true;
        }

        private void UpdateSegmentVisuals()
        {
            if (segments.Count < 2) return;

            for (int i = 0; i < segments.Count; i++)
            {
                Segment seg = segments[i];
                Transform t = seg.transform;
                t.rotation = Quaternion.identity;
                
                Vector2Int current = seg.GridPosition;
                Vector2Int prev = (i > 0) ? segments[i-1].GridPosition : current;
                Vector2Int next = (i < segments.Count - 1) ? segments[i+1].GridPosition : current;

                if (i == segments.Count - 1) // Head
                {
                    seg.Renderer.sprite = HeadSprite;
                    RotateComponent(t, current - prev);
                }
                else if (i == 0) // Tail
                {
                    seg.Renderer.sprite = TailSprite;
                    RotateComponent(t, next - current);
                }
                else 
                {
                     Vector2Int dirIn = current - prev;
                     Vector2Int dirOut = next - current;
                     
                     if (dirIn == dirOut) 
                     {
                         seg.Renderer.sprite = BodySprite;
                         RotateComponent(t, dirIn);
                     }
                     else 
                     {
                         seg.Renderer.sprite = CornerSprite;
                         Vector2Int sum = (prev - current) + (next - current);
                         
                         if (sum == new Vector2Int(-1, -1)) t.rotation = Quaternion.Euler(0,0,0);       
                         else if (sum == new Vector2Int(1, -1)) t.rotation = Quaternion.Euler(0,0,90);  
                         else if (sum == new Vector2Int(1, 1)) t.rotation = Quaternion.Euler(0,0,180);  
                         else if (sum == new Vector2Int(-1, 1)) t.rotation = Quaternion.Euler(0,0,270); 
                     }
                }
            }
        }
        
        private void RotateComponent(Transform t, Vector2Int dir)
        {
            if (dir == Vector2Int.up) t.rotation = Quaternion.Euler(0,0,0);
            else if (dir == Vector2Int.right) t.rotation = Quaternion.Euler(0,0,-90);
            else if (dir == Vector2Int.down) t.rotation = Quaternion.Euler(0,0,180);
            else if (dir == Vector2Int.left) t.rotation = Quaternion.Euler(0,0,90);
        }
    }
}
