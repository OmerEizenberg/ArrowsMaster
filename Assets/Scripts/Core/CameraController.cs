using UnityEngine;
using System.Collections;

namespace Assets.Scripts.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minZoom = 2f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float mobileZoomSpeed = 1f;

        [Header("Pan Settings")]
        [SerializeField] private float dragThresholdPercent = 3.0f; // Percentage of screen width to start panning
        
        [Header("Shake Settings")]
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float shakeMagnitude = 0.1f;
        
        [Header("Level Initialization Animation")]
        [SerializeField] private float initZoomMultiplier = 1.3f;
        [SerializeField] private float initZoomInDuration = 0.8f;
        [SerializeField] private float initPaddingMultiplier = 1.1f; // How much extra space around level (1.0 = exact fit, 1.2 = 20% extra)
        [SerializeField] private float initExtraZoomBuffer = 0.5f; // Additional units of zoom out beyond calculated fit

        [SerializeField] private float winZoomMultiplier = 3.0f;

        public static CameraController Instance { get; private set; }

        private Camera cam;
        private float defaultZoom;

        private Vector3 dragOrigin;
        private Vector3 touchStartPosition;
        private bool isTouching = false;
        private bool isPanningActive = false;
        
        // Bounds
        private Vector2 minBounds;
        private Vector2 maxBounds;
        private bool boundsSet = false;
        public bool HasPannedSinceLastReset { get; private set; }


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

        private Vector3 shakeOffset;
        private Vector3 lastShakeOffset;

        private void Update()
        {
            // Update logic moved to LateUpdate for better camera smoothness
        }

        private void LateUpdate()
        {
            // Restore position from previous frame's shake
            transform.position -= lastShakeOffset;
            lastShakeOffset = Vector3.zero;

            HandleDesktopZoom();
            HandleMobileZoom();
            HandlePanning();

            // Apply current shake offset
            lastShakeOffset = shakeOffset;
            transform.position += lastShakeOffset;
        }

        private Coroutine shakeCoroutine;

        public void Shake()
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeCoroutine(shakeDuration, shakeMagnitude));
        }

        public void Shake(float duration = 0.15f, float magnitude = 0.15f)
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeCoroutine(duration, magnitude));
        }

        private IEnumerator ShakeCoroutine(float duration, float magnitude)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // Scale magnitude by current zoom level so it feels consistent
                float scaledMagnitude = magnitude * (cam.orthographicSize / defaultZoom);
                shakeOffset = new Vector3(
                    Random.Range(-1f, 1f) * scaledMagnitude,
                    Random.Range(-1f, 1f) * scaledMagnitude,
                    0
                );
                elapsed += Time.deltaTime;
                yield return null;
            }
            shakeOffset = Vector3.zero;
            shakeCoroutine = null;
        }

        public void ResetPanState()
        {
            HasPannedSinceLastReset = false;
        }


        public void SetBounds(Vector2Int gridSize)
        {
            float padding = 2f;
            float cellSize = ArrowController.CellSize;

            minBounds = new Vector2(-1 * cellSize - padding, -1 * cellSize - padding);
            maxBounds = new Vector2(gridSize.x * cellSize + padding, gridSize.y * cellSize + padding);
            
            boundsSet = true;
        }

        private Vector3 prevMousePos;

        private void HandlePanning()
        {
            if (!boundsSet) return;

            // Handle Mouse/Touch Pan (Drag)
            if (Input.GetMouseButtonDown(0))
            {
                if (Input.touchCount > 1)
                {
                    isTouching = false;
                    isPanningActive = false;
                    return;
                }

                touchStartPosition = Input.mousePosition;
                prevMousePos = touchStartPosition;
                isTouching = true;
                isPanningActive = false;
            }

            if (Input.GetMouseButton(0) && isTouching)
            {
                if (Input.touchCount > 1)
                {
                    isTouching = false;
                    isPanningActive = false;
                    return;
                }

                Vector3 currentPos = Input.mousePosition;

                if (!isPanningActive)
                {
                    float distSqr = (touchStartPosition - currentPos).sqrMagnitude;
                    float threshold = Screen.width * (dragThresholdPercent / 100f);
                    
                    if (distSqr >= threshold * threshold)
                    {
                        isPanningActive = true;
                        prevMousePos = currentPos;
                    }
                    else return;
                }

                if (isPanningActive)
                {
                    HasPannedSinceLastReset = true;
                    
                    // Perfect Panning: Calculate delta in world space
                    // We only need ScreenToWorldPoint for the delta
                    Vector3 currentWorldPos = cam.ScreenToWorldPoint(new Vector3(currentPos.x, currentPos.y, cam.nearClipPlane));
                    Vector3 lastWorldPos = cam.ScreenToWorldPoint(new Vector3(prevMousePos.x, prevMousePos.y, cam.nearClipPlane));
                    Vector3 worldDelta = lastWorldPos - currentWorldPos;
                    
                    transform.position += worldDelta * 1.1f;
                    
                    // Clamp
                    Vector3 clampedPos = transform.position;
                    clampedPos.x = Mathf.Clamp(clampedPos.x, minBounds.x, maxBounds.x);
                    clampedPos.y = Mathf.Clamp(clampedPos.y, minBounds.y, maxBounds.y);
                    transform.position = clampedPos;

                    prevMousePos = currentPos;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                isTouching = false;
                isPanningActive = false;
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
            if (Input.touchCount == 2)
            {
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne = Input.GetTouch(1);

                if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began) return;

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos = touchOne.position - touchOne.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag = (touchZero.position - touchOne.position).magnitude;

                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                // REMOVED Time.deltaTime! Zoom should be absolute to finger move distance
                float newSize = cam.orthographicSize + deltaMagnitudeDiff * (cam.orthographicSize / 500f) * mobileZoomSpeed; 
                
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
            }
        }

        public void ResetZoom()
        {
            cam.orthographicSize = defaultZoom;
        }

        public IEnumerator PlayInitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            yield return StartCoroutine(InitializationZoomAnimation(gridSize, focusPosition));
        }

        private System.Collections.IEnumerator InitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            float cellSize = ArrowController.CellSize;
            float aspectRatio = cam.aspect;

            // Calculate the actual level bounds (from 0,0 to gridSize)
            float levelWidth = gridSize.x * cellSize;
            float levelHeight = gridSize.y * cellSize;

            // Calculate minimum zoom to fit level with padding multiplier
            // For vertical fit: we need to show levelHeight, so orthographicSize = (levelHeight * paddingMultiplier) / 2
            // For horizontal fit: we need to show levelWidth, accounting for aspect ratio
            float fitVertical = (levelHeight * initPaddingMultiplier) / 2f;
            float fitHorizontal = (levelWidth * initPaddingMultiplier) / (2f * aspectRatio);
            
            // Take the larger of the two to ensure entire level fits
            float minZoomToFit = Mathf.Max(fitVertical, fitHorizontal);
            
            // Add the extra buffer for a bit more breathing room
            float startZoom = minZoomToFit + initExtraZoomBuffer;

            // Update dynamic zoom settings
            maxZoom = startZoom;
            defaultZoom = Mathf.Max(startZoom / 2f, 7f);

            Vector3 centerPos = new Vector3(focusPosition.x, focusPosition.y, transform.position.z);
            
            // Immediately set to zoomed out view
            cam.orthographicSize = startZoom;
            transform.position = centerPos;
            
            // This coroutine will be yielded by LevelManager, so we just hold the zoomed out state
            // The zoom-in will happen AFTER arrows finish animating
            yield return null;
        }
        
        public IEnumerator ZoomInToDefault(Vector3 focusPosition)
        {
            float duration = initZoomInDuration;
            float startZoom = cam.orthographicSize;
            float targetZoom = defaultZoom;
            Vector3 startPos = transform.position;
            Vector3 targetPos = new Vector3(focusPosition.x, focusPosition.y, transform.position.z);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                
                cam.orthographicSize = Mathf.Lerp(startZoom, targetZoom, t);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                
                yield return null;
            }
            
            cam.orthographicSize = targetZoom;
            transform.position = targetPos;
        }

        public void PlayWinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            StartCoroutine(WinZoomAnimation(gridSize, focusPosition));
        }

        private System.Collections.IEnumerator WinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            float duration = 0.33f;
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
