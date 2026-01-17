using UnityEngine;

namespace Assets.Scripts.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minZoom = 2f;
        [SerializeField] private float maxZoom = 15f;
        [SerializeField] private float mobileZoomSpeed = 0.1f;

        [Header("Pan Settings")]
        [SerializeField] private float panSensitivity = 0.05f; // Adjusted for pixel delta
        
        [Header("Level Initialization Animation")]
        [SerializeField] private float initZoomMultiplier = 2.0f;
        [SerializeField] private float initZoomOutDuration = 0.3f;
        [SerializeField] private float initWaitDuration = 1.2f;
        [SerializeField] private float initZoomInDuration = 0.25f;

        public static CameraController Instance { get; private set; }

        private Camera cam;
        private float defaultZoom;

        private Vector3 dragOrigin;
        private bool isDragging = false;
        
        // Bounds
        private Vector2 minBounds;
        private Vector2 maxBounds;
        private bool boundsSet = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            cam = GetComponent<Camera>();
            defaultZoom = cam.orthographicSize;
        }

        private void Update()
        {
            HandleDesktopZoom();
            HandleMobileZoom();
            HandlePanning();
        }

        public void SetBounds(Vector2Int gridSize)
        {
            // Calculate bounds based on grid size + padding
            // Grid 0,0 is usually at 0,0 world.
            // Width = gridSize.x, Height = gridSize.y
            
            float padding = 2f;
            
            // Min X: -1 (wall) - padding
            // Max X: gridSize.x (wall) + padding
            // Min Y: -1 (wall) - padding
            // Max Y: gridSize.y (wall) + padding
            
            float cellSize = ArrowController.CellSize;

            minBounds = new Vector2(-1 * cellSize - padding, -1 * cellSize - padding);
            maxBounds = new Vector2(gridSize.x * cellSize + padding, gridSize.y * cellSize + padding);
            
            boundsSet = true;
        }

        private void HandlePanning()
        {
            if (!boundsSet) return;

            // Handle Mouse/Touch Pan (Drag)
            if (Input.GetMouseButtonDown(0))
            {
                dragOrigin = Input.mousePosition;
                isDragging = true;
            }

            if (Input.GetMouseButton(0) && isDragging)
            {
                Vector3 currentPos = Input.mousePosition;
                Vector3 delta = dragOrigin - currentPos; // Drag World style (Move mouse Left -> Camera Right)
                
                // Convert screen delta to world delta roughly, or just use sensitivity
                // Proper way: (ScreenToWorld(dragOrigin) - ScreenToWorld(currentPos))
                // But simplified:
                
                Vector3 move = new Vector3(delta.x * panSensitivity * (cam.orthographicSize/5f), delta.y * panSensitivity * (cam.orthographicSize/5f), 0);
                
                transform.position += move;
                
                // Clamp
                Vector3 clampedPos = transform.position;
                clampedPos.x = Mathf.Clamp(clampedPos.x, minBounds.x, maxBounds.x);
                clampedPos.y = Mathf.Clamp(clampedPos.y, minBounds.y, maxBounds.y);
                transform.position = clampedPos;

                dragOrigin = currentPos;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isDragging = false;
            }
        }

        private void HandleDesktopZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0.0f)
            {
                float newSize = cam.orthographicSize - scroll * zoomSpeed;
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
        }

        private void HandleMobileZoom()
        {
            // If there are two touches on the device...
            if (Input.touchCount == 2)
            {
                // Store both touches.
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                // Find the position in the previous frame of each touch.
                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                // Find the magnitude of the vector (the distance) between the touches in each frame.
                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                float newSize = cam.orthographicSize + deltaMagnitudeDiff * mobileZoomSpeed * Time.deltaTime; // Scaling
                
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
        }

        public void ResetZoom()
        {
            cam.orthographicSize = defaultZoom;
        }

        public void PlayInitializationZoomAnimation(Vector2Int gridSize)
        {
            StartCoroutine(InitializationZoomAnimation(gridSize));
        }

        private System.Collections.IEnumerator InitializationZoomAnimation(Vector2Int gridSize)
        {
            float padding = 2f;
            float cellSize = ArrowController.CellSize;
            float aspectRatio = cam.aspect;

            // Calculate target zoom to fit grid
            float fitVertical = (gridSize.y * cellSize + padding * 2) / 2f;
            float fitHorizontal = (gridSize.x * cellSize + padding * 2) / (2f * aspectRatio);
            
            // Zoom out even more (multiplier)
            float targetZoom = Mathf.Max(fitVertical, fitHorizontal) * initZoomMultiplier;
            
            // Limit target zoom by max zoom
            targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

            Vector3 gridCenter = new Vector3(gridSize.x * cellSize / 2f, gridSize.y * cellSize / 2f, transform.position.z);
            Vector3 finalPos = transform.position;
            float finalZoom = cam.orthographicSize;

            // Start almost zoomed out (85% of target zoom for more movement)
            float startZoom = targetZoom * 0.85f;
            Vector3 startPos = Vector3.Lerp(gridCenter, finalPos, 0.15f);

            float totalDuration = initZoomOutDuration + initWaitDuration + initZoomInDuration;
            
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.deltaTime;
                
                if (elapsed < initZoomOutDuration)
                {
                    // Phase 1: Zoom Out
                    float t = elapsed / initZoomOutDuration;
                    cam.orthographicSize = Mathf.Lerp(startZoom, targetZoom, t);
                    transform.position = Vector3.Lerp(startPos, gridCenter, t);
                }
                else if (elapsed < initZoomOutDuration + initWaitDuration)
                {
                    // Phase 2: Wait
                    cam.orthographicSize = targetZoom;
                    transform.position = gridCenter;
                }
                else
                {
                    // Phase 3: Zoom In
                    float t = (elapsed - initZoomOutDuration - initWaitDuration) / initZoomInDuration;
                    cam.orthographicSize = Mathf.Lerp(targetZoom, finalZoom, t);
                    transform.position = Vector3.Lerp(gridCenter, finalPos, t);
                }

                yield return null;
            }

            cam.orthographicSize = finalZoom;
            transform.position = finalPos;
        }
    }
}
