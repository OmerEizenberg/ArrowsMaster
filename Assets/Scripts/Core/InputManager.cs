using UnityEngine;

namespace Assets.Scripts.Core
{
    public class InputManager : MonoBehaviour
    {
        private Segment pendingSegment;
        private Vector2 startTouchPos;
        private bool wasMultiTouch;
        [SerializeField] private float moveThreshold = 20f; // Pixels
        [SerializeField] private float holdThreshold = 0.5f;

        private float mouseDownTime;
        private bool hasTriggeredHold;
        private ArrowController activePreviewArrow;

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
            }

            if (Input.GetMouseButton(0) && pendingSegment != null && !wasMultiTouch && !hasTriggeredHold)
            {
                float dist = Vector2.Distance(startTouchPos, (Vector2)Input.mousePosition);
                if (dist < moveThreshold)
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
                    // If panned too far, cancel potential hold
                    pendingSegment = null;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
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
                if (dist < moveThreshold && !wasMultiTouch)
                {
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
                }
                
                pendingSegment = null;
                wasMultiTouch = false;
            }
        }

        private Segment GetHitSegment()
        {
            if (IsScreenPositionBlocked(Input.mousePosition)) return null;

            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                return hit.collider.GetComponent<Segment>();
            }
            return null;
        }

        private bool IsScreenPositionBlocked(Vector2 screenPos)
        {
            float normalizedY = screenPos.y / Screen.height;
            return normalizedY > 0.85f;
        }
    }
}
