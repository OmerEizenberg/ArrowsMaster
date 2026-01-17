using UnityEngine;

namespace Assets.Scripts.Core
{
    public class InputManager : MonoBehaviour
    {
        private Segment pendingSegment;
        private Vector2 startTouchPos;
        private bool wasMultiTouch;
        [SerializeField] private float moveThreshold = 10f; // Pixels

        void Update()
        {
            if (Input.touchCount > 1) wasMultiTouch = true;

            if (Input.GetMouseButtonDown(0))
            {
                pendingSegment = GetHitSegment();
                startTouchPos = Input.mousePosition;
                wasMultiTouch = false;
            }

            if (Input.GetMouseButtonUp(0))
            {
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
            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                return hit.collider.GetComponent<Segment>();
            }
            return null;
        }
    }
}
