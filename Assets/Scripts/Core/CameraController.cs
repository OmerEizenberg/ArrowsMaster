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
        
        [Header("Inertia Settings")]
        [SerializeField] private float inertiaDeceleration = 0.9f; // Friction: velocity *= factor
        [SerializeField] private float minInertiaVelocity = 0.1f;
        [SerializeField] private float velocitySmoothFactor = 15f; // For smoothing the input velocity
        
        [Header("Shake Settings")]
        [SerializeField] private float shakeDuration = 0.1f;
        [SerializeField] private float shakeMagnitude = 0.1f;
        
        [Header("Level Initialization Animation")]
        [SerializeField] private float initZoomInDuration = 0.5f;
        [SerializeField] private float initZoomOutDuration = 0.4f;
        [SerializeField] private float initPaddingMultiplier = 1.2f; // 20% extra space around level
        [SerializeField] private float initExtraZoomBuffer = 0.5f;   // Additional units beyond calculated fit
        [SerializeField] private float targetViewportCenterY = 0.43f; // Shift center for Top Bar UI

        [SerializeField] private float winZoomMultiplier = 1.0f;

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
        private Vector3 lastWorldDelta;
        private Vector3 smoothedVelocity;
        private bool isRollingInertia = false;
        private Vector3 inertiaVelocity;
        private float zoomReturnVelocity;

        
        // Bounds
        private Vector2 minBounds;
        private Vector2 maxBounds;
        private bool boundsSet = false;
        public bool HasPannedSinceLastReset { get; private set; }

        // ── Performance Optimization Caches ──────────────
        private Transform m_Transform;
        private float m_ViewportYFactor;
        private float m_CurrentOrthoSize;
        private Vector3 m_CurrentMousePos;
        private int m_CurrentTouchCount;

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

            m_Transform = transform;
            cam = GetComponent<Camera>();
            defaultZoom = cam.orthographicSize;
            absoluteMaxZoom = maxZoom;
            
            // Precalculate Y-offset factor: (0.5 - target) * 2
            m_ViewportYFactor = (0.5f - targetViewportCenterY) * 2f;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = DevicePerformanceProfile.TargetFrameRate;
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
            m_Transform.position -= lastShakeOffset;
            lastShakeOffset = Vector3.zero;

            isZoomingInteraction = false;

            // Refresh cached values if resolution changed
            if (Screen.width != (int)cachedScreenWidth || Screen.height != (int)cachedScreenHeight)
            {
                RefreshCachedScreenValues();
            }

            // Cache frame-level inputs and properties
            m_CurrentTouchCount = Input.touchCount;
            m_CurrentMousePos   = Input.mousePosition;
            m_CurrentOrthoSize  = cam.orthographicSize;

            HandleDesktopZoom();
            HandleMobileZoom(m_CurrentTouchCount);
            HandlePanning(m_CurrentTouchCount);

            // Handle smooth return to maxZoom if over-zoomed and not interacting
            bool isInteracting = isZoomingInteraction || isTouching || m_CurrentTouchCount > 0 || Input.GetMouseButton(0);
            
            if (isInteracting)
            {
                lastInteractionTime = Time.time;
            }

            if (isLevelStarted && !isInternalAnimation && !isInteracting && m_CurrentOrthoSize > maxZoom)
            {
                if (Time.time - lastInteractionTime >= returnStartDelay)
                {
                    m_CurrentOrthoSize = Mathf.SmoothDamp(m_CurrentOrthoSize, maxZoom, ref zoomReturnVelocity, zoomReturnDuration);
                    cam.orthographicSize = m_CurrentOrthoSize;
                }
            }
            else
            {
                zoomReturnVelocity = 0f;
            }


            // Apply current shake offset
            lastShakeOffset = shakeOffset;
            m_Transform.position += lastShakeOffset;
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
                isRollingInertia = false; // Stop any ongoing inertia
                inertiaVelocity = Vector3.zero;

                if (touchCount > 1)
                {
                    isTouching = false;
                    isPanningActive = false;
                    return;
                }

                touchStartPosition = m_CurrentMousePos;
                prevMousePos = touchStartPosition;
                isTouching = true;
                isPanningActive = false;
                smoothedVelocity = Vector3.zero;
            }

            if (Input.GetMouseButton(0) && isTouching)
            {
                if (touchCount > 1)
                {
                    isTouching = false;
                    isPanningActive = false;
                    return;
                }

                Vector3 currentPos = m_CurrentMousePos;

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
                    float worldHeight = m_CurrentOrthoSize * 2f;
                    float worldWidth  = worldHeight * cachedAspect;
                    
                    float worldDeltaX = -(deltaX / cachedScreenWidth)  * worldWidth;
                    float worldDeltaY = -(deltaY / cachedScreenHeight) * worldHeight;
                    
                    Vector3 worldDelta = new Vector3(worldDeltaX * 1.1f, worldDeltaY * 1.1f, 0);
                    
                    // Calculate and smooth velocity
                    if (Time.deltaTime > 0)
                    {
                        Vector3 frameVelocity = worldDelta / Time.deltaTime;
                        smoothedVelocity = Vector3.Lerp(smoothedVelocity, frameVelocity, Time.deltaTime * velocitySmoothFactor);
                    }

                    cachedPosition = m_Transform.position;
                    cachedPosition += worldDelta;
                    
                    cachedPosition = ClampToBounds(cachedPosition);
                    
                    m_Transform.position = cachedPosition;
                    prevMousePos = currentPos;
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                if (isPanningActive && smoothedVelocity.magnitude > minInertiaVelocity)
                {
                    isRollingInertia = true;
                    inertiaVelocity = smoothedVelocity;
                }
                
                isTouching = false;
                isPanningActive = false;
            }

            if (isRollingInertia && !isTouching)
            {
                ApplyInertia();
            }
        }

        private void ApplyInertia()
        {
            if (inertiaVelocity.magnitude < minInertiaVelocity)
            {
                isRollingInertia = false;
                inertiaVelocity = Vector3.zero;
                return;
            }

            // Apply movement
            Vector3 movement = inertiaVelocity * Time.deltaTime;
            Vector3 newPos = m_Transform.position + movement;
            
            // Clamp and check if we hit boundaries
            Vector3 clampedPos = ClampToBounds(newPos);
            
            // If we hit a boundary, stop inertia in that axis or altogether
            if (Mathf.Abs(clampedPos.x - newPos.x) > 0.001f) inertiaVelocity.x = 0;
            if (Mathf.Abs(clampedPos.y - newPos.y) > 0.001f) inertiaVelocity.y = 0;

            m_Transform.position = clampedPos;

            // Apply friction (frame-rate independent)
            inertiaVelocity *= Mathf.Pow(inertiaDeceleration, Time.deltaTime * 60f);
            
            // If we are at the edge, the friction should probably be higher or just stop
            if (inertiaVelocity.magnitude < minInertiaVelocity)
            {
                isRollingInertia = false;
                inertiaVelocity = Vector3.zero;
            }
        }

        private Vector3 ClampToBounds(Vector3 targetPos)
        {
            float currentYOffset = m_ViewportYFactor * m_CurrentOrthoSize;
            float minLimitY = minBounds.y + currentYOffset;
            float maxLimitY = maxBounds.y + currentYOffset;

            if (targetPos.x < minBounds.x || targetPos.x > maxBounds.x)
                targetPos.x = Mathf.Clamp(targetPos.x, minBounds.x, maxBounds.x);
            if (targetPos.y < minLimitY || targetPos.y > maxLimitY)
                targetPos.y = Mathf.Clamp(targetPos.y, minLimitY, maxLimitY);
            
            return targetPos;
        }

        private void HandleDesktopZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0.0f)
            {
                isZoomingInteraction = true;
                isRollingInertia = false; // Stop inertia when zooming
                m_CurrentOrthoSize = Mathf.Clamp(m_CurrentOrthoSize - scroll * zoomSpeed, minZoom, absoluteMaxZoom);
                cam.orthographicSize = m_CurrentOrthoSize;
            }
        }

        private void HandleMobileZoom(int touchCount)
        {
            if (touchCount == 2)
            {
                isZoomingInteraction = true;
                isRollingInertia = false; // Stop inertia when zooming
                Touch touchZero = Input.GetTouch(0);
                Touch touchOne  = Input.GetTouch(1);

                if (touchZero.phase == TouchPhase.Began || touchOne.phase == TouchPhase.Began) return;

                Vector2 touchZeroPrevPos = touchZero.position - touchZero.deltaPosition;
                Vector2 touchOnePrevPos  = touchOne.position  - touchOne.deltaPosition;

                float prevTouchDeltaMag = (touchZeroPrevPos - touchOnePrevPos).magnitude;
                float touchDeltaMag     = (touchZero.position - touchOne.position).magnitude;

                float deltaMagnitudeDiff = prevTouchDeltaMag - touchDeltaMag;

                m_CurrentOrthoSize = Mathf.Clamp(m_CurrentOrthoSize + deltaMagnitudeDiff * (m_CurrentOrthoSize / 500f) * mobileZoomSpeed, minZoom, absoluteMaxZoom);
                cam.orthographicSize = m_CurrentOrthoSize;
            }
        }

        public void ResetZoom()
        {
            m_CurrentOrthoSize = defaultZoom;
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
            float fitZoom       = Mathf.Max(fitVertical, fitHorizontal) * 0.85f; // 20% closer zoom base

            // Compute the final "gameplay" zoom (what the player sees after animation)
            float finalZoom = fitZoom;//Mathf.Min(25f, fitZoom);
            finalZoom       = Mathf.Max(finalZoom, minZoom);

            // Phase 1 start zoom: half the max zoom for the level
            float startZoom = finalZoom * 0.5f;

            // Store zoom limits for gameplay
            if (UserDataManager.Instance.IsDynamicMaxZoom)
            {
                maxZoom         = finalZoom;
                absoluteMaxZoom = Mathf.Max(25f, fitZoom + initExtraZoomBuffer);
            }
            
            defaultZoom     = finalZoom;


            SetBounds(gridSize);

            Vector3 centerPos = GetViewportOffsetPos(focusPosition, startZoom);

            // Instantly snap to initial zoomed-in view
            cam.orthographicSize = startZoom;
            m_CurrentOrthoSize   = startZoom;
            m_Transform.position = centerPos;

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
                m_CurrentOrthoSize   = currentZoom;
                
                // Recalculate targetPos with current zoom to keep centering consistent during zoom
                Vector3 currentTargetPos = GetViewportOffsetPos(focusPosition, currentZoom);
                m_Transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);

                yield return null;
            }

            cam.orthographicSize = targetZoom;
            m_CurrentOrthoSize   = targetZoom;
            m_Transform.position = GetViewportOffsetPos(focusPosition, targetZoom);
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
            float duration = 1.0f;
            float elapsed  = 0f;
            
            float startZoom = cam.orthographicSize;
            Vector3 startPos = transform.position;
            
            // Target exactly 10% more zoom out than the default "fit" zoom
            float multiplier = 1.1f;
            if (gridSize.x < 20 && gridSize.y < 20)
            {
                multiplier *= 1.5f; // Zoom out 1.5x more for small levels
            }
            float targetZoom = defaultZoom * multiplier;
            
            // Ensure we don't zoom IN if the player was already zoomed out further
            targetZoom = Mathf.Max(targetZoom, startZoom);
            
            // Use the provided focusPosition instead of recalculating based on gridSize
            // (gridSize might be larger than the actual area occupied by arrows)
            Vector3 targetCenter = new Vector3(focusPosition.x, focusPosition.y, transform.position.z);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float smoothT = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                
                float currentZoom = Mathf.Lerp(startZoom, targetZoom, smoothT);
                cam.orthographicSize = currentZoom;
                m_CurrentOrthoSize   = currentZoom;
                
                // Keep the viewport offset consistent with gameplay (0.43 shift)
                // but focused on the actual level center
                Vector3 currentTargetPos = GetViewportOffsetPos(targetCenter, currentZoom);
                m_Transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);
                
                yield return null;
            }

            cam.orthographicSize = targetZoom;
            m_CurrentOrthoSize   = targetZoom;
            m_Transform.position = GetViewportOffsetPos(targetCenter, targetZoom);
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
                m_Transform.position = Vector3.Lerp(startPos, currentTargetPos, smoothT);
                yield return null;
            }

            m_Transform.position = GetViewportOffsetPos(worldPosition, cam.orthographicSize);
            isInternalAnimation = false;
        }

        private Vector3 GetViewportOffsetPos(Vector3 worldPos, float orthoSize)
        {
            float yOffset = m_ViewportYFactor * orthoSize;
            return new Vector3(worldPos.x, worldPos.y + yOffset, m_Transform.position.z);
        }
    }
}
