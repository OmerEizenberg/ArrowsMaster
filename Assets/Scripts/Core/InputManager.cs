using UnityEngine;

namespace Assets.Scripts.Core
{
    public class InputManager : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandleClick();
            }
        }

        void HandleClick()
        {
            Vector2 worldPoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(worldPoint, Vector2.zero);

            if (hit.collider != null)
            {
                // Check if we hit a Segment
                Segment segment = hit.collider.GetComponent<Segment>();
                if (segment != null)
                {
                    // Find the parent ArrowController
                    ArrowController arrow = segment.GetComponentInParent<ArrowController>();
                    if (arrow != null)
                    {
                        // Only trigger if we clicked the head? 
                        // Prompt said "clicks on the Arrow Head".
                        // Logic for checking if it is head is in ArrowController or we check here.
                        // Let's call a method on ArrowController that decides.
                        arrow.OnArrowClicked(segment);
                    }
                }
            }
        }
    }
}
