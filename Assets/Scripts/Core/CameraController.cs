using UnityEngine;

namespace Assets.Scripts.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minZoom = 2f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float mobileZoomSpeed = 0.3f;

        [Header("Pan Settings")]
        [SerializeField] private float panSensitivity = 0.01f; // Adjusted for pixel delta
        
        [Header("Level Initialization Animation")]
        [SerializeField] private float initZoomMultiplier = 2.0f;
        [SerializeField] private float initZoomOutDuration = 0.3f;
        [SerializeField] private float initWaitDuration = 1.2f;
        [SerializeField] private float initZoomInDuration = 0.25f;

        [SerializeField] private float winZoomMultiplier = 3.0f;

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
                // If we are starting with multiple fingers, ignore panning
                if (Input.touchCount > 1)
                {
                    isDragging = false;
                    return;
                }

                dragOrigin = Input.mousePosition;
                isDragging = true;
            }

            if (Input.GetMouseButton(0) && isDragging)
            {
                // If a second finger is added while dragging, cancel the drag to allow zooming
                if (Input.touchCount > 1)
                {
                    isDragging = false;
                    return;
                }

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
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                // If any touch just began, skip this frame to establish a clean baseline
                // and prevent the "jump" caused by the first finger's existing deltaPosition.
                if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began)
                {
                    return;
                }

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

        public void PlayInitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            StartCoroutine(InitializationZoomAnimation(gridSize, focusPosition));
        }

        private System.Collections.IEnumerator InitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
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

            Vector3 targetAnimPos = new Vector3(focusPosition.x, focusPosition.y, transform.position.z);
            Vector3 finalPos = targetAnimPos;
            float finalZoom = cam.orthographicSize;

            // Start almost zoomed out (85% of target zoom for more movement)
            float startZoom = targetZoom * 0.85f;
            // Slightly offset start position to create some initial movement
            Vector3 startPos = Vector3.Lerp(targetAnimPos, transform.position, 0.15f);

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
                    transform.position = Vector3.Lerp(startPos, targetAnimPos, t);
                }
                else if (elapsed < initZoomOutDuration + initWaitDuration)
                {
                    // Phase 2: Wait
                    cam.orthographicSize = targetZoom;
                    transform.position = targetAnimPos;
                }
                else
                {
                    // Phase 3: Zoom In
                    float t = (elapsed - initZoomOutDuration - initWaitDuration) / initZoomInDuration;
                    cam.orthographicSize = Mathf.Lerp(targetZoom, finalZoom, t);
                    transform.position = Vector3.Lerp(targetAnimPos, finalPos, t);
                }

                yield return null;
            }

            cam.orthographicSize = finalZoom;
            transform.position = finalPos;
        }

        public void PlayWinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            StartCoroutine(WinZoomAnimation(gridSize, focusPosition));
        }

        private System.Collections.IEnumerator WinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            float duration = 0.5f;
            float elapsed = 0f;
            
            float startZoom = cam.orthographicSize;
            Vector3 startPos = transform.position;
            
            float padding = 2f;
            float cellSize = ArrowController.CellSize;
            float aspectRatio = cam.aspect;

            // Calculate target zoom to fit grid
            float fitVertical = (gridSize.y * cellSize + padding * 2) / 2f;
            float fitHorizontal = (gridSize.x * cellSize + padding * 2) / (2f * aspectRatio);
            
            // Use a multiplier to ensure we see the whole level and a bit more for the "wow" factor
            // The user mentioned portrait mode needs extra zoom out. 
            // Max(fitVertical, fitHorizontal) already accounts for aspect ratio.
            float targetZoom = Mathf.Max(fitVertical, fitHorizontal) * winZoomMultiplier; 
                
            // Calculate Grid Center to ensure the whole level is visible
            Vector3 gridCenter = new Vector3((gridSize.x - 1) * cellSize / 2f, (gridSize.y - 1) * cellSize / 2f, transform.position.z);
            Vector3 targetPos = gridCenter;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Use SmoothStep for a nice feel
                float smoothT = Mathf.SmoothStep(0, 1, t);
                
                cam.orthographicSize = Mathf.Lerp(startZoom, targetZoom, smoothT);
                transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                
                yield return null;
            }

            cam.orthographicSize = targetZoom;
            transform.position = targetPos;
        }

        public void FocusOn(Vector3 worldPosition, float duration)
        {
            StartCoroutine(FocusCoroutine(worldPosition, duration));
        }

        private System.Collections.IEnumerator FocusCoroutine(Vector3 worldPosition, float duration)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float smoothT = Mathf.SmoothStep(0, 1, t);
                
                transform.position = Vector3.Lerp(startPos, targetPos, smoothT);
                yield return null;
            }

            transform.position = targetPos;
        }
    }
}
