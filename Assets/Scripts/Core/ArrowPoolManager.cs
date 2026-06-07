using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class ArrowPoolManager : MonoBehaviour
    {
        public static ArrowPoolManager Instance { get; private set; }

        [Header("Prefabs")]
        public ArrowController arrowPrefab;
        public Segment segmentPrefab;

        [Header("Pool Baseline (kept after level clear)")]
        public int baselineArrowCount = 70;
        public int baselineSegmentMultiplier = 6;

        [SerializeField] private int arrowsCount;
        private Queue<ArrowController> arrowPool = new Queue<ArrowController>();
        private Queue<Segment> segmentPool = new Queue<Segment>();
        private readonly HashSet<ArrowController> pooledArrows = new HashSet<ArrowController>();

        private int ActiveBaselineArrowCount =>
            DevicePerformanceProfile.BaselineArrowCount > 0
                ? DevicePerformanceProfile.BaselineArrowCount
                : baselineArrowCount;

        private int ActiveBaselineSegmentMultiplier =>
            DevicePerformanceProfile.BaselineSegmentMultiplier > 0
                ? DevicePerformanceProfile.BaselineSegmentMultiplier
                : baselineSegmentMultiplier;

        private int BaselineSegmentCount => ActiveBaselineArrowCount * ActiveBaselineSegmentMultiplier;

        public ArrowController ArrowPrefab { get => arrowPrefab; set { arrowPrefab = value; CheckPreWarm(); } }
        public Segment SegmentPrefab { get => segmentPrefab; set { segmentPrefab = value; CheckPreWarm(); } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            CheckPreWarm();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void CheckPreWarm()
        {
            if (arrowPrefab != null && segmentPrefab != null && arrowPool.Count == 0)
            {
                StopAllCoroutines();
                StartCoroutine(InitialPreWarmRoutine());
            }
        }

        public void Initialize(ArrowController arrow, Segment segment)
        {
            arrowPrefab = arrow;
            segmentPrefab = segment;
            StopAllCoroutines();
            StartCoroutine(InitialPreWarmRoutine());
        }

        private IEnumerator InitialPreWarmRoutine()
        {
            yield return null;

            int arrowBaseline = ActiveBaselineArrowCount;
            while (arrowPool.Count < arrowBaseline)
            {
                ReplenishArrow();
                if (arrowPool.Count % 20 == 0) yield return null;
            }

            int baselineSegments = BaselineSegmentCount;
            while (segmentPool.Count < baselineSegments)
            {
                ReplenishSegment();
                if (segmentPool.Count % 100 == 0) yield return null;
            }

            EnsureSharedLineMaterial();
            Debug.Log($"[ArrowPoolManager] Buffer initialized: {arrowPool.Count} arrows, {segmentPool.Count} segments.");
        }

        /// <summary>Grow pools to fit the upcoming level; does not shrink.</summary>
        public void EnsureCapacityForLevel(int arrowCount, int totalPathPoints)
        {
            int requiredArrows = arrowCount + Mathf.Max(8, arrowCount / 10);
            int requiredSegments = totalPathPoints + arrowCount + Mathf.Max(16, totalPathPoints / 20);

            PruneDestroyedArrowRefs();
            PruneDestroyedSegmentRefs();

            while (arrowPool.Count < requiredArrows)
            {
                ReplenishArrow();
            }

            while (segmentPool.Count < requiredSegments)
            {
                ReplenishSegment();
            }

            arrowsCount = arrowPool.Count;
        }

        public void PurgeToBaseline()
        {
            PurgeAndReplenishArrows(ActiveBaselineArrowCount);
            PurgeAndReplenishSegments(BaselineSegmentCount);
        }

        private void ReplenishArrow()
        {
            if (arrowPrefab == null) return;
            ArrowController arrow = Instantiate(arrowPrefab);
            arrow.gameObject.SetActive(false);
            arrow.transform.SetParent(transform);
            arrowPool.Enqueue(arrow);
            arrowsCount = arrowPool.Count;
        }

        private void ReplenishSegment()
        {
            if (segmentPrefab == null) return;
            Segment seg = Instantiate(segmentPrefab);
            seg.gameObject.SetActive(false);
            seg.transform.SetParent(transform);
            segmentPool.Enqueue(seg);
        }

        public bool IsArrowInPool(ArrowController arrow)
        {
            return arrow != null && pooledArrows.Contains(arrow);
        }

        public void PurgeAndReplenishArrows()
        {
            PurgeAndReplenishArrows(ActiveBaselineArrowCount);
        }

        public void PurgeAndReplenishArrows(int keepCount)
        {
            PruneDestroyedArrowRefs();
            TrimArrowPoolTo(keepCount);

            while (arrowPool.Count < keepCount && arrowPrefab != null)
            {
                ReplenishArrow();
            }

            arrowsCount = arrowPool.Count;
        }

        public void PurgeAndReplenishSegments(int keepCount)
        {
            PruneDestroyedSegmentRefs();
            TrimSegmentPoolTo(keepCount);

            while (segmentPool.Count < keepCount && segmentPrefab != null)
            {
                ReplenishSegment();
            }
        }

        private void TrimArrowPoolTo(int keepCount)
        {
            while (arrowPool.Count > keepCount)
            {
                ArrowController arrow = arrowPool.Dequeue();
                if (arrow != null)
                {
                    pooledArrows.Remove(arrow);
                    Destroy(arrow.gameObject);
                }
            }
        }

        private void TrimSegmentPoolTo(int keepCount)
        {
            while (segmentPool.Count > keepCount)
            {
                Segment seg = segmentPool.Dequeue();
                if (seg != null)
                {
                    Destroy(seg.gameObject);
                }
            }
        }

        private void PruneDestroyedArrowRefs()
        {
            int queueSize = arrowPool.Count;
            for (int i = 0; i < queueSize; i++)
            {
                ArrowController candidate = arrowPool.Dequeue();
                if (candidate != null)
                {
                    arrowPool.Enqueue(candidate);
                }
            }

            var snapshot = new List<ArrowController>(pooledArrows);
            for (int i = 0; i < snapshot.Count; i++)
            {
                ArrowController arrow = snapshot[i];
                if (!arrow)
                {
                    pooledArrows.Remove(arrow);
                }
            }
        }

        private void PruneDestroyedSegmentRefs()
        {
            int queueSize = segmentPool.Count;
            for (int i = 0; i < queueSize; i++)
            {
                Segment candidate = segmentPool.Dequeue();
                if (candidate != null)
                {
                    segmentPool.Enqueue(candidate);
                }
            }
        }

        private ArrowController TakeArrowFromPool()
        {
            while (arrowPool.Count > 0)
            {
                ArrowController candidate = arrowPool.Dequeue();
                if (candidate == null)
                {
                    continue;
                }

                pooledArrows.Remove(candidate);
                return candidate;
            }

            return null;
        }

        public ArrowController GetArrow(Vector3 position, Quaternion rotation, Transform parent)
        {
            ArrowController arrow = TakeArrowFromPool();
            if (arrow != null)
            {
                arrow.transform.SetParent(parent);
                arrow.transform.position = position;
                arrow.transform.rotation = rotation;
                arrow.PrepareForReuse();
                arrow.gameObject.SetActive(true);
            }
            else
            {
                if (arrowPrefab == null)
                {
                    Debug.LogError("[ArrowPoolManager] Cannot GetArrow: arrowPrefab is NULL!");
                    return null;
                }
                arrow = Instantiate(arrowPrefab, position, rotation, parent);
                arrow.PrepareForReuse();
            }

            return arrow;
        }

        public void ReturnArrow(ArrowController arrow)
        {
            if (arrow == null) return;
            if (IsArrowInPool(arrow)) return;

            arrow.ReturnToPool();
            arrow.gameObject.SetActive(false);
            arrow.transform.SetParent(transform);

            pooledArrows.Add(arrow);
            arrowPool.Enqueue(arrow);
            arrowsCount = arrowPool.Count;

            ReleaseArrowFromLevel(arrow);
        }

        public void NotifyArrowDestroyed(ArrowController arrow)
        {
            if (arrow == null || pooledArrows == null) return;
            pooledArrows.Remove(arrow);
            ReleaseArrowFromLevel(arrow);
        }

        private static void ReleaseArrowFromLevel(ArrowController arrow)
        {
            if (arrow == null) return;

            var gameManager = GameManager.Instance;
            if (gameManager == null) return;

            var levelManager = gameManager.levelManager;
            if (levelManager == null) return;

            levelManager.ReleaseArrow(arrow);
        }

        public Segment GetSegment(Vector3 position, Quaternion rotation, Transform parent)
        {
            Segment seg = TakeSegmentFromPool();
            if (seg != null)
            {
                seg.transform.SetParent(parent);
                seg.transform.position = position;
                seg.transform.rotation = rotation;
                seg.ResetForPool();
                seg.gameObject.SetActive(true);
            }
            else
            {
                if (segmentPrefab == null)
                {
                    Debug.LogError("[ArrowPoolManager] Cannot GetSegment: segmentPrefab is NULL!");
                    return null;
                }
                seg = Instantiate(segmentPrefab, position, rotation, parent);
            }
            return seg;
        }

        private Segment TakeSegmentFromPool()
        {
            while (segmentPool.Count > 0)
            {
                Segment candidate = segmentPool.Dequeue();
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }

        public void ReturnSegment(Segment seg)
        {
            if (seg == null) return;

            seg.ResetForPool();

            seg.gameObject.SetActive(false);
            seg.transform.SetParent(transform);
            segmentPool.Enqueue(seg);
        }

        public static void EnsureSharedLineMaterial()
        {
            ArrowController.EnsureSharedLineMaterialFromPrefab(Instance != null ? Instance.arrowPrefab : null);
        }
    }
}
