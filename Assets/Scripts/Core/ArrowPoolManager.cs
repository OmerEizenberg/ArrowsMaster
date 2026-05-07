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

        [Header("Settings")]
        public int targetArrowCount = 500;
        public int targetSegmentCount = 3000;

        private Queue<ArrowController> arrowPool = new Queue<ArrowController>();
        private Queue<Segment> segmentPool = new Queue<Segment>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Pre-warm the pool gradually
            StartCoroutine(InitialPreWarmRoutine());
        }

        private IEnumerator InitialPreWarmRoutine()
        {
            // Give the scene a moment to settle
            yield return null;

            while (arrowPool.Count < targetArrowCount)
            {
                ReplenishArrow();
                if (arrowPool.Count % 20 == 0) yield return null;
            }

            while (segmentPool.Count < targetSegmentCount)
            {
                ReplenishSegment();
                if (segmentPool.Count % 100 == 0) yield return null;
            }
            
            Debug.Log($"[ArrowPoolManager] Buffer initialized: {arrowPool.Count} arrows, {segmentPool.Count} segments.");
        }

        private void ReplenishArrow()
        {
            ArrowController arrow = Instantiate(arrowPrefab);
            arrow.gameObject.SetActive(false);
            arrow.transform.SetParent(this.transform);
            arrowPool.Enqueue(arrow);
        }

        private void ReplenishSegment()
        {
            Segment seg = Instantiate(segmentPrefab);
            seg.gameObject.SetActive(false);
            seg.transform.SetParent(this.transform);
            segmentPool.Enqueue(seg);
        }

        public ArrowController GetArrow(Vector3 position, Quaternion rotation, Transform parent)
        {
            ArrowController arrow;
            if (arrowPool.Count > 0)
            {
                arrow = arrowPool.Dequeue();
                arrow.transform.SetParent(parent);
                arrow.transform.position = position;
                arrow.transform.rotation = rotation;
                arrow.gameObject.SetActive(true);
            }
            else
            {
                arrow = Instantiate(arrowPrefab, position, rotation, parent);
            }
            return arrow;
        }

        public void ReturnArrow(ArrowController arrow)
        {
            if (arrow == null) return;

            // One-time use strategy: Destroy the old one
            // This ensures every use starts with a fresh, untainted object
            Destroy(arrow.gameObject);

            // Immediately replenish the pool to maintain the 500 buffer
            ReplenishArrow();
        }

        public Segment GetSegment(Vector3 position, Quaternion rotation, Transform parent)
        {
            Segment seg;
            if (segmentPool.Count > 0)
            {
                seg = segmentPool.Dequeue();
                seg.transform.SetParent(parent);
                seg.transform.position = position;
                seg.transform.rotation = rotation;
                seg.gameObject.SetActive(true);
            }
            else
            {
                seg = Instantiate(segmentPrefab, position, rotation, parent);
            }
            return seg;
        }

        public void ReturnSegment(Segment seg)
        {
            if (seg == null) return;
            
            // One-time use strategy: Destroy
            Destroy(seg.gameObject);
            
            // Immediately replenish
            ReplenishSegment();
        }
    }
}
