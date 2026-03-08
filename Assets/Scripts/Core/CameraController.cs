using UnityEngine;
using System.Collections;

namespace Assets.Scripts.Core
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 3f;
        [SerializeField] private float minZoom = 9.5f;
        [SerializeField] private float maxZoom = 50f;
        [SerializeField] private float mobileZoomSpeed = 1f;

        [Header("Pan Settings")]
        [SerializeField] private float dragThresholdPercent = 2.0f; // Percentage of screen width to start panning
        
        [Header("Shake Settings")]
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float shakeMagnitude = 0.1f;
        
        [Header("Level Initialization Animation")]
        [SerializeField] private float initZoomInDuration = 0.5f;
        [SerializeField] private float initZoomOutDuration = 0.4f;
        [SerializeField] private float initPaddingMultiplier = 1.2f; // 20% extra space around level
        [SerializeField] private float initExtraZoomBuffer = 0.5f;   // Additional units beyond calculated fit
        [SerializeField] private float targetViewportCenterY = 0.43f; // Shift center for Top Bar UI

        [SerializeField] private float winZoomMultiplier = 3.0f;

        public static CameraController Instance { get; private set; }

        private Camera cam;
        private float defaultZoom;
        private float absoluteMaxZoom;
        private bool isZoomingInteraction = false;
        private bool isInternalAnimation = false;
        private bool isLevelStarted = false;
        private float lastInteractionTime;
        [SerializeField] private float zoomReturnDuration = 0.3f;
        [SerializeField] private float returnStartDelay = 0.15f;

        private Vector3 dragOrigin;
        private Vector3 touchStartPosition;
        private bool isTouching = false;
        private bool isPanningActive = false;
        
        // Bounds
        private Vector2 minBounds;
        private Vector2 maxBounds;
        private bool boundsSet = false;
        public bool HasPannedSinceLastReset { get; private set; }

        // ── Cached screen/camera values (refreshed in OnEnable) ──────────────
        private float cachedScreenWidth;
        private float cachedScreenHeight;
        private float cachedAspect;
        private float cachedDragThreshold; // pixels

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
            absoluteMaxZoom = maxZoom;

            // Target 60 FPS on mobile
            Application.targetFrameRate = 120;
        }

        private void OnEnable()
        {
            RefreshCachedScreenValues();
        }

        /// <summary>Call whenever screen resolution or orientation may have changed.</summary>
        private void RefreshCachedScreenValues()
        {
            cachedScreenWidth    = Screen.width;
            cachedScreenHeight   = Screen.height;
            cachedAspect         = cam != null ? cam.aspect : (float)Screen.width / Screen.height;
            cachedDragThreshold  = cachedScreenWidth * (dragThresholdPercent / 100f);
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

            isZoomingInteraction = false;

            // Read touchCount once per frame
            int touchCount = Input.touchCount;

            HandleDesktopZoom();
            HandleMobileZoom(touchCount);
            HandlePanning(touchCount);

            // Handle smooth return to maxZoom if over-zoomed and not interacting
            bool isInteracting = isZoomingInteraction || isTouching || touchCount > 0 || Input.GetMouseButton(0);
            
            if (isInteracting)
            {
                lastInteractionTime = Time.time;
            }

            if (isLevelStarted && !isInternalAnimation && !isInteracting && cam.orthographicSize > maxZoom)
            {
                if (Time.time - lastInteractionTime >= returnStartDelay)
                {
                    float overZoomRange = Mathf.Max(0.1f, absoluteMaxZoom - maxZoom);
                    float returnSpeed = overZoomRange / zoomReturnDuration;
                    cam.orthographicSize = Mathf.MoveTowards(cam.orthographicSize, maxZoom, returnSpeed * Time.deltaTime);
                }
            }

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

            // Base world bounds for the level's visual center
            minBounds = new Vector2(-1 * cellSize - padding, -1 * cellSize - padding);
            maxBounds = new Vector2(gridSize.x * cellSize + padding, gridSize.y * cellSize + padding);
            
            boundsSet = true;
        }

        private Vector3 prevMousePos;
        private Vector3 cachedPosition; // Reusable for clamping

        private void HandlePanning(int touchCount)
        {
            if (!boundsSet) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (touchCount > 1)
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
                if (touchCount > 1)
                {
                    isTouching = false;
                    isPanningActive = false;
                    return;
                }

                Vector3 currentPos = Input.mousePosition;

                if (!isPanningActive)
                {
                    float distSqr = (touchStartPosition - currentPos).sqrMagnitude;
                    if (distSqr >= cachedDragThreshold * cachedDragThreshold)
                    {
                        isPanningActive = true;
                        prevMousePos = currentPos;
                    }
                    else return;
                }

                if (isPanningActive)
                {
                    HasPannedSinceLastReset = true;
                    
                    float deltaX = currentPos.x - prevMousePos.x;
                    float deltaY = currentPos.y - prevMousePos.y;
                    
                    // Use cached screen/aspect values — no native calls per frame
                    float worldHeight = cam.orthographicSize * 2f;
                    float worldWidth  = worldHeight * cachedAspect;
                    
                    float worldDeltaX = -(deltaX / cachedScreenWidth)  * worldWidth;
                    float worldDeltaY = -(deltaY / cachedScreenHeight) * worldHeight;
                    
                    cachedPosition = transform.position;
                    cachedPosition.x += worldDeltaX * 1.1f;
                    cachedPosition.y += worldDeltaY * 1.1f;
                    
                    float currentYOffset = (0.5f - targetViewportCenterY) * 2f * cam.orthographicSize;
                    float minLimitY = minBounds.y + currentYOffset;
                    float maxLimitY = maxBounds.y + currentYOffset;

                    if (cachedPosition.x < minBounds.x || cachedPosition.x > maxBounds.x)
                        cachedPosition.x = Mathf.Clamp(cachedPosition.x, minBounds.x, maxBounds.x);
                    if (cachedPosition.y < minLimitY || cachedPosition.y > maxLimitY)
                        cachedPosition.y = Mathf.Clamp(cachedPosition.y, minLimitY, maxLimitY);
                    
                    transform.position = cachedPosition;
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
                isZoomingInteraction = true;
                float newSize = cam.orthographicSize - scroll * zoomSpeed;
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, absoluteMaxZoom);
            }
        }

        private void HandleMobileZoom(int touchCount)
        {
            if (touchCount == 2)
            {
                isZoomingInteraction = true;
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne  = Input.GetTouch(1);

                if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began) return;

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos  = touchOne.position  - touchOne.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag     = (touchZero.position - touchOne.position).magnitude;

                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                float newSize = cam.orthographicSize + deltaMagnitudeDiff * (cam.orthographicSize / 500f) * mobileZoomSpeed;
                cam.orthographicSize = Mathf.Clamp(newSize, minZoom, absoluteMaxZoom);
            }
        }

        public void ResetZoom()
        {
            cam.orthographicSize = defaultZoom;
        }

        // ── Entrance Animation ────────────────────────────────────────────────
        // Phase 1: Set camera zoom to half of the level's max zoom (instant)
        // Phase 2: Arrows grow (handled by LevelManager)
        // Phase 3: Zoom OUT to the level's max zoom (smooth, initZoomInDuration)

        /// <summary>
        /// Phase 1: Instantly positions camera at half of the required zoom to fit the level.
        /// </summary>
        public IEnumerator PlayInitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            yield return StartCoroutine(InitializationZoomAnimation(gridSize, focusPosition));
        }

        private IEnumerator InitializationZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            isInternalAnimation = true;
            float cellSize  = ArrowController.CellSize;
            float aspect    = cachedAspect;

            float levelWidth  = gridSize.x * cellSize;
            float levelHeight = gridSize.y * cellSize;

            // Fit zoom = smallest orthographic size that shows the full grid
            float fitVertical   = (levelHeight * initPaddingMultiplier) / 2f;
            float fitHorizontal = (levelWidth  * initPaddingMultiplier) / (2f * aspect);
            float fitZoom       = Mathf.Max(fitVertical, fitHorizontal);

            // Compute the final "gameplay" zoom (what the player sees after animation)
            float finalZoom = fitZoom;//Mathf.Min(25f, fitZoom);
            finalZoom       = Mathf.Max(finalZoom, minZoom);

            // Phase 1 start zoom: half the max zoom for the level
            float startZoom = finalZoom * 0.5f;

            // Store zoom limits for gameplay
            maxZoom         = finalZoom;
            absoluteMaxZoom = Mathf.Max(25f, fitZoom + initExtraZoomBuffer);
            defaultZoom     = finalZoom;

            SetBounds(gridSize);

            Vector3 centerPos = GetViewportOffsetPos(focusPosition, startZoom);

            // Instantly snap to initial zoomed-in view
            cam.orthographicSize = startZoom;
            transform.position   = centerPos;

            yield return null;
            isInternalAnimation = false;
            isLevelStarted      = false;
        }

        /// <summary>
        /// After arrows finish growing, smoothly zoom out from the initial position
        /// directly to the final default zoom (max zoom of the level).
        /// </summary>
        public IEnumerator AnimateToDefaultZoom(Vector3 focusPosition, float duration = -1f)
        {
            isInternalAnimation = true;

            float startZoom  = cam.orthographicSize;
            float targetZoom = defaultZoom; // final gameplay zoom (already clamped to maxZoom)

            Vector3 startPos  = transform.position;
            Vector3 targetPos = new Vector3(focusPosition.x, focusPosition.y, transform.position.z);

            float elapsed  = 0f;
            if (duration < 0) duration = initZoomInDuration;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

                float currentZoom = Mathf.Lerp(startZoom, targetZoom, smoothT);
                cam.orthographicSize = currentZoom;
                
                // Recalculate targetPos with current zoom to keep centering consistent during zoom
                Vector3 currentTargetPos = GetViewportOffsetPos(focusPosition, currentZoom);
                transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);

                yield return null;
            }

            cam.orthographicSize = targetZoom;
            transform.position   = GetViewportOffsetPos(focusPosition, targetZoom);
            isInternalAnimation  = false;
            isLevelStarted       = true;
        }

        public void PlayWinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            StartCoroutine(WinZoomAnimation(gridSize, focusPosition));
        }

        private IEnumerator WinZoomAnimation(Vector2Int gridSize, Vector3 focusPosition)
        {
            isLevelStarted = false; // Disable zoom-back mechanic immediately
            isInternalAnimation = true;
            float duration = 0.33f;
            float elapsed  = 0f;
            
            float startZoom = cam.orthographicSize;
            Vector3 startPos = transform.position;
            
            float padding    = 2f;
            float cellSize   = ArrowController.CellSize;
            float aspectRatio = cachedAspect;

            float fitVertical   = (gridSize.y * cellSize + padding * 2) / 2f;
            float fitHorizontal = (gridSize.x * cellSize + padding * 2) / (2f * aspectRatio);
            
            // Only zoom out: targetZoom must be at least startZoom
            float targetZoom    = Mathf.Max(fitVertical, fitHorizontal) * winZoomMultiplier;
            targetZoom = Mathf.Max(targetZoom, startZoom);
                
            Vector3 gridCenter = new Vector3((gridSize.x - 1) * cellSize / 2f, (gridSize.y - 1) * cellSize / 2f, transform.position.z);
            Vector3 targetPos  = gridCenter;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                
                float currentZoom = Mathf.Lerp(startZoom, targetZoom, smoothT);
                cam.orthographicSize = currentZoom;
                
                Vector3 currentTargetPos = GetViewportOffsetPos(gridCenter, currentZoom);
                transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);
                
                yield return null;
            }

            cam.orthographicSize = targetZoom;
            transform.position   = GetViewportOffsetPos(gridCenter, targetZoom);
            isInternalAnimation  = false;
        }

        public void FocusOn(Vector3 worldPosition, float duration)
        {
            StartCoroutine(FocusCoroutine(worldPosition, duration));
        }

        private IEnumerator FocusCoroutine(Vector3 worldPosition, float duration)
        {
            isInternalAnimation = true;
            float elapsed  = 0f;
            Vector3 startPos  = transform.position;
            Vector3 targetPos = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                
                Vector3 currentTargetPos = GetViewportOffsetPos(worldPosition, cam.orthographicSize);
                transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);
                yield return null;
            }

            transform.position  = GetViewportOffsetPos(worldPosition, cam.orthographicSize);
            isInternalAnimation = false;
        }

        private Vector3 GetViewportOffsetPos(Vector3 worldPos, float orthoSize)
        {
            float yOffset = (0.5f - targetViewportCenterY) * 2f * orthoSize;
            return new Vector3(worldPos.x, worldPos.y + yOffset, transform.position.z);
        }
    }
}
