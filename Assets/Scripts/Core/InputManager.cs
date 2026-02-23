using UnityEngine;
using System.Collections.Generic;

namespace Assets.Scripts.Core
{
    public class InputManager : MonoBehaviour
    {
        private Segment pendingSegment;
        private Vector2 startTouchPos;
        private bool wasMultiTouch;
        // Cached at startup — screen size doesn't change mid-session
        private float m_MoveThreshold;
        [SerializeField] private float holdThreshold = 0.5f;

        [Header("Effects")]
        [SerializeField] private GameObject clickEffectPrefab;
        [SerializeField] private Transform clickEffectParent;

        private float mouseDownTime;
        private bool hasTriggeredHold;
        private ArrowController activePreviewArrow;

        // Cached camera reference — avoids FindObjectOfType on every call
        private Camera m_Camera;
        private ArrowController highlightedArrow;

        private void Awake()
        {
            m_Camera = Camera.main;
            m_MoveThreshold = Mathf.Max(40f, Screen.height * 0.03f);
        }

        void Update()
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanInteract) return;

            if (Input.touchCount > 1) wasMultiTouch = true;

            if (Input.GetMouseButtonDown(0))
            {
                pendingSegment = GetHitSegment();
                startTouchPos = Input.mousePosition;
                wasMultiTouch = false;
                mouseDownTime = Time.time;
                hasTriggeredHold = false;
                activePreviewArrow = null;

                // Provide immediate feedback on what is being pressed
                if (pendingSegment != null)
                {
                    highlightedArrow = pendingSegment.GetComponentInParent<ArrowController>();
                }
                else 
                {
                    Segment closest;
                    highlightedArrow = GetClosestArrow(startTouchPos, out closest);
                }

                if (highlightedArrow != null)
                {
                    highlightedArrow.SetPressedStyle();
                }
            }

            if (Input.GetMouseButton(0) && pendingSegment != null && !wasMultiTouch && !hasTriggeredHold)
            {
                float dist = Vector2.Distance(startTouchPos, (Vector2)Input.mousePosition);
                if (dist < m_MoveThreshold)
                {
                    if (Time.time - mouseDownTime > holdThreshold)
                    {
                        hasTriggeredHold = true;
                        activePreviewArrow = pendingSegment.GetComponentInParent<ArrowController>();
                        if (activePreviewArrow != null)
                        {
                            activePreviewArrow.ShowPreview();
                        }
                    }
                }
                else
                {
                    // If panned too far, cancel potential hold and highlight
                    if (highlightedArrow != null)
                    {
                        highlightedArrow.ResetPressedStyle();
                        highlightedArrow = null;
                    }
                    pendingSegment = null;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                // Always clear highlight on release
                if (highlightedArrow != null)
                {
                    highlightedArrow.ResetPressedStyle();
                    highlightedArrow = null;
                }

                // Always hide preview if one was active
                if (activePreviewArrow != null)
                {
                    activePreviewArrow.HidePreview();
                }

                Vector2 endTouchPos = Input.mousePosition;
                float dist = Vector2.Distance(startTouchPos, endTouchPos);

                // Only count as click if:
                // 1. Didn't move much (panning)
                // 2. Only used one finger (no zoom)
                // 3. Same segment hit at start and end
                if (dist < m_MoveThreshold && !wasMultiTouch)
                {
                    // Spawn click effect if assigned
                    if (clickEffectPrefab != null)
                    {
                        Vector3 worldPos = m_Camera.ScreenToWorldPoint(endTouchPos);
                        worldPos.z = 0; // Ensure it spawns at z=0 (standard 2D plane)
                        GameObject temp = Instantiate(clickEffectPrefab, worldPos, Quaternion.identity, clickEffectParent);
                        temp.transform.localScale = Vector3.one;
                    }

                    Segment upSegment = GetHitSegment();
                    if (upSegment != null && upSegment == pendingSegment)
                    {
                        // Find the parent ArrowController
                        ArrowController arrow = upSegment.GetComponentInParent<ArrowController>();
                        if (arrow != null)
                        {
                            // Start timer on first touch (if it's a timed level)
                            if (GameManager.Instance != null && GameManager.Instance.IsTimedLevel)
                            {
                                GameManager.Instance.StartTimer();
                            }
                            
                            arrow.OnArrowClicked(upSegment);
                        }
                    }
                    else if (upSegment == null && pendingSegment == null)
                    {
                        // If we missed both at start and end, try closest arrow selection
                        TrySelectClosestArrow(endTouchPos);
                    }
                }
                
                pendingSegment = null;
                wasMultiTouch = false;
            }
        }

        private Segment GetHitSegment()
        {
            if (IsScreenPositionBlocked(Input.mousePosition)) return null;

            Vector2 worldPoint = m_Camera.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                return hit.collider.GetComponent<Segment>();
            }
            return null;
        }

        private void TrySelectClosestArrow(Vector2 screenPos)
        {
            Segment closestSegment;
            ArrowController closestArrow = GetClosestArrow(screenPos, out closestSegment);

            if (closestArrow != null)
            {
                // Start timer on first touch (if it's a timed level)
                if (GameManager.Instance != null && GameManager.Instance.IsTimedLevel)
                {
                    GameManager.Instance.StartTimer();
                }
                
                closestArrow.OnArrowClicked(closestSegment);
            }
        }

        private ArrowController GetClosestArrow(Vector2 screenPos, out Segment closestSegment)
        {
            closestSegment = null;
            if (IsScreenPositionBlocked(screenPos)) return null;
            if (GridManager.Instance == null) return null;

            // Use fixed world distance of 1.0 (approximately 1 grid cell size)
            // This ensures the radius "scales" with zoom (visually consistent in world space)
            float worldThreshold = 0.9f; 
            float wrongArrowThreshold = 0.1f; 
            float minDistance = worldThreshold;
            
            // Convert click to world space for distance check
            Vector3 worldClickPos = m_Camera.ScreenToWorldPoint(screenPos);
            worldClickPos.z = 0; // Flatten z

            ArrowController closestArrow = null;

            List<ArrowController> allArrows = GridManager.Instance.GetAllArrows();
            foreach (var arrow in allArrows)
            {
                if (arrow == null || arrow.segments == null) continue;
                foreach (var segment in arrow.segments)
                {
                    if (segment == null) continue;
                    
                    // Calculate distance in World Space
                    float dist = Vector2.Distance(worldClickPos, segment.transform.position);
                    
                    if (dist < minDistance && (arrow.CanMoveForward() || dist < wrongArrowThreshold))
                    {
                        minDistance = dist;
                        closestArrow = arrow;
                        closestSegment = segment;
                    }
                }
            }
            return closestArrow;
        }

        private bool IsScreenPositionBlocked(Vector2 screenPos)
        {
            float normalizedY = screenPos.y / Screen.height;
            return normalizedY > 0.85f;
        }
    }
}
