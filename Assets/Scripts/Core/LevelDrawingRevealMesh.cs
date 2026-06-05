using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Win reveal inferred from dot layout: outline dots + enclosed interior fill.
    /// Painted with soft circular brush stamps (not square tiles).
    /// </summary>
    public class LevelDrawingRevealMesh : MonoBehaviour
    {
        /// <summary>Max missing dots between consecutive path cells before we stop bridging.</summary>
        public static int MaxMissingDotsBetweenPath = 1;

        /// <summary>Brush radius in grid dots (world radius = value * CellSize).</summary>
        public static float BrushRadiusInDots = 4f;

        private const int BrushCircleSegments = 12;
        private const int VerticesPerCell = BrushCircleSegments + 1;
        private const int TrianglesPerCell = BrushCircleSegments * 3;

        private struct RevealCell
        {
            public Vector2Int GridPos;
            public Vector3 WorldCenter;
            public Color TargetColor;
            public float Reveal;
        }

        private static readonly Color OutlineColor = Color.black;
        private static readonly Color InteriorColor = Color.white;

        private static readonly Vector2Int[] Neighbor4 =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        private RevealCell[] _cells;
        private Vector3[] _pathWaypoints;
        private float[] _pathCumulativeDist;
        private float _pathTotalLength;
        private int _minGridX;
        private int _minGridY;
        private int _maxGridX;
        private int _maxGridY;

        private Vector3[] _vertices;
        private Color[] _vertexColors;
        private int[] _triangles;
        private bool _meshDirty;
        private int _rebuildFrameCounter;
        private float _globalAlpha = 1f;
        private Color _faintDotColor = Color.gray;

        private Transform _markerTransform;
        private SpriteRenderer _markerRenderer;

        public int CellCount => _cells != null ? _cells.Length : 0;

        public float PathTotalLength => _pathTotalLength;

        public void Clear()
        {
            _cells = null;
            _pathWaypoints = null;
            _pathCumulativeDist = null;
            _pathTotalLength = 0f;
            _minGridX = 0;
            _minGridY = 0;
            _maxGridX = 0;
            _maxGridY = 0;
            _meshDirty = false;
            if (_mesh != null) _mesh.Clear();
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            if (_markerTransform != null) _markerTransform.gameObject.SetActive(false);
        }

        public bool TryBuildFromDots(
            ICollection<Vector2Int> dotCells,
            IReadOnlyList<IReadOnlyList<Vector2Int>> arrowPaths,
            Color faintDotColor,
            float cellSize,
            int sortingOrder,
            int maxMissingDotsBetweenPath = -1)
        {
            Clear();
            if (dotCells == null || dotCells.Count == 0) return false;

            int maxGap = maxMissingDotsBetweenPath >= 0
                ? maxMissingDotsBetweenPath
                : MaxMissingDotsBetweenPath;

            var dotSet = dotCells as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(dotCells);
            var included = new HashSet<Vector2Int>(dotSet);
            var bridgeCells = new HashSet<Vector2Int>();

            if (arrowPaths != null)
            {
                for (int p = 0; p < arrowPaths.Count; p++)
                {
                    IReadOnlyList<Vector2Int> path = arrowPaths[p];
                    if (path == null || path.Count < 2) continue;

                    for (int i = 0; i < path.Count - 1; i++)
                    {
                        Vector2Int a = path[i];
                        Vector2Int b = path[i + 1];
                        int manhattan = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
                        if (manhattan <= 1)
                        {
                            included.Add(a);
                            included.Add(b);
                            continue;
                        }

                        if (manhattan <= 1 + maxGap && IsCardinalAligned(a, b))
                        {
                            AddCardinalBridgeCells(a, b, included, bridgeCells);
                        }
                    }
                }
            }

            var components = FindDotComponents(dotSet);
            for (int i = 0; i < components.Count; i++)
            {
                AddInteriorCells(components[i], dotSet, included);
            }

            if (included.Count < 8) return false;

            var cellList = new List<RevealCell>(included.Count);
            _minGridX = int.MaxValue;
            _minGridY = int.MaxValue;
            _maxGridX = int.MinValue;
            _maxGridY = int.MinValue;

            foreach (Vector2Int gridPos in included)
            {
                if (gridPos.x < _minGridX) _minGridX = gridPos.x;
                if (gridPos.y < _minGridY) _minGridY = gridPos.y;
                if (gridPos.x > _maxGridX) _maxGridX = gridPos.x;
                if (gridPos.y > _maxGridY) _maxGridY = gridPos.y;

                bool isOutline = dotSet.Contains(gridPos) || bridgeCells.Contains(gridPos);
                cellList.Add(new RevealCell
                {
                    GridPos = gridPos,
                    WorldCenter = new Vector3(gridPos.x * cellSize, gridPos.y * cellSize, 0f),
                    TargetColor = isOutline ? OutlineColor : InteriorColor,
                    Reveal = 0f
                });
            }

            SortDiagonalZigZag(cellList);
            _cells = cellList.ToArray();
            BuildMarkerPath();

            EnsureComponents(sortingOrder);
            AllocateMeshBuffers(_cells.Length);

            _faintDotColor = faintDotColor;
            _globalAlpha = 1f;
            _meshDirty = true;
            ApplyIfDirty(faintDotColor);
            _meshRenderer.enabled = true;
            EnsureMarker();
            return true;
        }

        public void SetGlobalAlpha(float alpha, Color faintDotColor)
        {
            _globalAlpha = Mathf.Clamp01(alpha);
            _faintDotColor = faintDotColor;
            _meshDirty = true;
            ApplyIfDirty(_faintDotColor);
        }

        /// <summary>
        /// Scales brush radius from level size so the reveal can finish inside the post-win window.
        /// </summary>
        public float ComputeDynamicBrushRadiusInDots(
            float targetDurationSeconds,
            float cellSize,
            float baseBrushDots,
            float maxBrushDots)
        {
            if (_cells == null || _cells.Length == 0 || cellSize <= 0f || targetDurationSeconds <= 0f)
            {
                return baseBrushDots;
            }

            int cellCount = _cells.Length;
            float maxSpanWorld = GetMaxGridSpanWorld(cellSize);
            float spanCells = maxSpanWorld / cellSize;

            const float referenceCellCount = 150f;
            const float referenceSpanCells = 24f;
            const float referenceDurationSeconds = 2.2f;

            float cellCountFactor = Mathf.Sqrt(cellCount / referenceCellCount);
            float spanFactor = spanCells / referenceSpanCells;
            float sizeFactor = Mathf.Max(cellCountFactor, spanFactor * 0.9f);

            float durationFactor = Mathf.Sqrt(referenceDurationSeconds / targetDurationSeconds);

            float pathFactor = 1f;
            if (_pathTotalLength > 0f)
            {
                float referencePathLength = referenceCellCount * cellSize * 1.15f;
                pathFactor = Mathf.Sqrt(_pathTotalLength / referencePathLength);
                sizeFactor = Mathf.Max(sizeFactor, pathFactor * 0.85f);
            }

            float brushDots = baseBrushDots * sizeFactor * durationFactor;
            return Mathf.Clamp(brushDots, baseBrushDots, maxBrushDots);
        }

        private float GetMaxGridSpanWorld(float cellSize)
        {
            if (_cells == null || _cells.Length == 0) return 0f;

            float width = (_maxGridX - _minGridX + 1) * cellSize;
            float height = (_maxGridY - _minGridY + 1) * cellSize;
            return Mathf.Max(width, height);
        }

        public IEnumerator AnimateMarkerReveal(Color faintDotColor, float duration, float brushRadius)
        {
            if (_cells == null || _cells.Length == 0 || _pathWaypoints == null || _pathWaypoints.Length == 0)
            {
                yield break;
            }

            float brushRadiusSqr = brushRadius * brushRadius;

            if (_markerTransform != null)
            {
                _markerTransform.gameObject.SetActive(true);
                _markerTransform.position = _pathWaypoints[0] + Vector3.forward * -0.05f;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                float eased = EaseInOutSmooth(t);
                Vector3 markerPos = SampleCurvyPath(eased, t);

                if (_markerTransform != null)
                {
                    _markerTransform.position = markerPos + Vector3.forward * -0.05f;
                }

                UpdateBrushMarkerVisual(brushRadius);

                for (int i = 0; i < _cells.Length; i++)
                {
                    RevealCell cell = _cells[i];
                    float distSqr = (cell.WorldCenter - markerPos).sqrMagnitude;
                    if (distSqr > brushRadiusSqr) continue;

                    float dist = Mathf.Sqrt(distSqr);
                    float normalized = Mathf.Clamp01(1f - dist / brushRadius);
                    float brushReveal = normalized * normalized * (3f - 2f * normalized);

                    cell.Reveal = Mathf.Max(cell.Reveal, brushReveal);
                    _cells[i] = cell;
                }

                _meshDirty = true;
                ApplyIfDirty(faintDotColor);
                yield return null;
            }

            if (_markerTransform != null)
            {
                _markerTransform.gameObject.SetActive(false);
            }
        }

        private Vector3 SampleCurvyPath(float eased, float rawT)
        {
            if (_pathTotalLength <= 0f) return _pathWaypoints[0];

            float targetDist = eased * _pathTotalLength;
            for (int i = 1; i < _pathCumulativeDist.Length; i++)
            {
                if (_pathCumulativeDist[i] < targetDist) continue;

                float segStart = _pathCumulativeDist[i - 1];
                float segEnd = _pathCumulativeDist[i];
                float segT = segEnd > segStart ? (targetDist - segStart) / (segEnd - segStart) : 0f;
                segT = EaseInOutSmooth(Mathf.Clamp01(segT));

                Vector3 a = _pathWaypoints[i - 1];
                Vector3 b = _pathWaypoints[i];
                Vector3 pos = Vector3.Lerp(a, b, segT);

                Vector3 tangent = (b - a).normalized;
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f);
                float wobble = Mathf.Sin(rawT * Mathf.PI * 6f + i * 0.35f) * 0.12f;
                return pos + normal * wobble;
            }

            return _pathWaypoints[_pathWaypoints.Length - 1];
        }

        private void BuildMarkerPath()
        {
            if (_cells == null || _cells.Length == 0) return;

            _pathWaypoints = new Vector3[_cells.Length];
            for (int i = 0; i < _cells.Length; i++)
            {
                _pathWaypoints[i] = _cells[i].WorldCenter;
            }

            _pathCumulativeDist = new float[_pathWaypoints.Length];
            _pathCumulativeDist[0] = 0f;
            for (int i = 1; i < _pathWaypoints.Length; i++)
            {
                _pathCumulativeDist[i] = _pathCumulativeDist[i - 1] + Vector3.Distance(_pathWaypoints[i - 1], _pathWaypoints[i]);
            }

            _pathTotalLength = _pathCumulativeDist[_pathCumulativeDist.Length - 1];
        }

        /// <summary>Diagonal zigzag from top-left (high y, low x) to bottom-right (low y, high x).</summary>
        private static void SortDiagonalZigZag(List<RevealCell> cells)
        {
            if (cells.Count <= 1) return;

            int minDiag = int.MaxValue;
            int maxDiag = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                int d = cells[i].GridPos.y - cells[i].GridPos.x;
                if (d < minDiag) minDiag = d;
                if (d > maxDiag) maxDiag = d;
            }

            var buckets = new Dictionary<int, List<RevealCell>>();
            for (int i = 0; i < cells.Count; i++)
            {
                int d = cells[i].GridPos.y - cells[i].GridPos.x;
                if (!buckets.TryGetValue(d, out List<RevealCell> bucket))
                {
                    bucket = new List<RevealCell>();
                    buckets[d] = bucket;
                }
                bucket.Add(cells[i]);
            }

            cells.Clear();
            bool reverse = false;
            for (int d = maxDiag; d >= minDiag; d--)
            {
                if (!buckets.TryGetValue(d, out List<RevealCell> bucket)) continue;
                bucket.Sort((a, b) => reverse
                    ? b.GridPos.x.CompareTo(a.GridPos.x)
                    : a.GridPos.x.CompareTo(b.GridPos.x));
                cells.AddRange(bucket);
                reverse = !reverse;
            }
        }

        private static List<HashSet<Vector2Int>> FindDotComponents(HashSet<Vector2Int> dotSet)
        {
            var components = new List<HashSet<Vector2Int>>();
            var visited = new HashSet<Vector2Int>();

            foreach (Vector2Int start in dotSet)
            {
                if (visited.Contains(start)) continue;

                var component = new HashSet<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(start);
                visited.Add(start);
                component.Add(start);

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    for (int i = 0; i < Neighbor4.Length; i++)
                    {
                        Vector2Int next = current + Neighbor4[i];
                        if (!dotSet.Contains(next) || visited.Contains(next)) continue;
                        visited.Add(next);
                        component.Add(next);
                        queue.Enqueue(next);
                    }
                }

                components.Add(component);
            }

            return components;
        }

        private static void AddInteriorCells(
            HashSet<Vector2Int> componentDots,
            HashSet<Vector2Int> allDots,
            HashSet<Vector2Int> included)
        {
            if (componentDots.Count == 0) return;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Vector2Int p in componentDots)
            {
                if (p.x < minX) minX = p.x;
                if (p.y < minY) minY = p.y;
                if (p.x > maxX) maxX = p.x;
                if (p.y > maxY) maxY = p.y;
            }

            var outside = new HashSet<Vector2Int>();
            var queue = new Queue<Vector2Int>();

            for (int x = minX; x <= maxX; x++)
            {
                TryEnqueueOutside(new Vector2Int(x, minY), allDots, minX, maxX, minY, maxY, outside, queue);
                TryEnqueueOutside(new Vector2Int(x, maxY), allDots, minX, maxX, minY, maxY, outside, queue);
            }

            for (int y = minY; y <= maxY; y++)
            {
                TryEnqueueOutside(new Vector2Int(minX, y), allDots, minX, maxX, minY, maxY, outside, queue);
                TryEnqueueOutside(new Vector2Int(maxX, y), allDots, minX, maxX, minY, maxY, outside, queue);
            }

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();
                for (int i = 0; i < Neighbor4.Length; i++)
                {
                    Vector2Int next = current + Neighbor4[i];
                    if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY) continue;
                    if (allDots.Contains(next) || outside.Contains(next)) continue;
                    outside.Add(next);
                    queue.Enqueue(next);
                }
            }

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int pos = new Vector2Int(x, y);
                    if (allDots.Contains(pos) || outside.Contains(pos)) continue;
                    included.Add(pos);
                }
            }
        }

        private static bool IsCardinalAligned(Vector2Int a, Vector2Int b)
        {
            return a.x == b.x || a.y == b.y;
        }

        private static void AddCardinalBridgeCells(
            Vector2Int a,
            Vector2Int b,
            HashSet<Vector2Int> included,
            HashSet<Vector2Int> bridgeCells)
        {
            if (a.x == b.x)
            {
                int step = a.y < b.y ? 1 : -1;
                for (int y = a.y; y != b.y; y += step)
                {
                    Vector2Int pos = new Vector2Int(a.x, y);
                    included.Add(pos);
                    bridgeCells.Add(pos);
                }
            }
            else if (a.y == b.y)
            {
                int step = a.x < b.x ? 1 : -1;
                for (int x = a.x; x != b.x; x += step)
                {
                    Vector2Int pos = new Vector2Int(x, a.y);
                    included.Add(pos);
                    bridgeCells.Add(pos);
                }
            }

            included.Add(a);
            included.Add(b);
        }

        private void UpdateBrushMarkerVisual(float brushRadius)
        {
            if (_markerTransform == null) return;
            float diameter = brushRadius * 2f;
            _markerTransform.localScale = new Vector3(diameter, diameter, 1f);
        }

        private static void TryEnqueueOutside(
            Vector2Int pos,
            HashSet<Vector2Int> allDots,
            int minX, int maxX, int minY, int maxY,
            HashSet<Vector2Int> outside,
            Queue<Vector2Int> queue)
        {
            if (pos.x < minX || pos.x > maxX || pos.y < minY || pos.y > maxY) return;
            if (allDots.Contains(pos) || outside.Contains(pos)) return;
            outside.Add(pos);
            queue.Enqueue(pos);
        }

        private void ApplyIfDirty(Color faintDotColor)
        {
            if (!_meshDirty || _cells == null || _cells.Length == 0) return;

            if (DevicePerformanceProfile.IsLowEnd)
            {
                _rebuildFrameCounter++;
                if (_rebuildFrameCounter % 2 != 0) return;
            }

            RebuildMesh(faintDotColor);
            _meshDirty = false;
        }

        private void RebuildMesh(Color faintDotColor)
        {
            float cellSize = ArrowController.CellSize;
            float baseRadius = cellSize * 0.52f;

            for (int i = 0; i < _cells.Length; i++)
            {
                RevealCell cell = _cells[i];
                float reveal = Mathf.Clamp01(cell.Reveal);
                float radius = reveal <= 0.001f
                    ? 0.001f
                    : baseRadius * Mathf.Lerp(0.45f, 1.05f, reveal);

                Color centerColor = Color.Lerp(faintDotColor, cell.TargetColor, reveal);
                centerColor.a *= _globalAlpha * reveal;
                Color edgeColor = centerColor;
                edgeColor.a = 0f;

                int baseVertex = i * VerticesPerCell;
                Vector3 center = cell.WorldCenter;
                _vertices[baseVertex] = center;
                _vertexColors[baseVertex] = centerColor;

                for (int s = 0; s < BrushCircleSegments; s++)
                {
                    float angle = (s / (float)BrushCircleSegments) * Mathf.PI * 2f;
                    int ringVertex = baseVertex + 1 + s;
                    _vertices[ringVertex] = center + new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle) * radius,
                        0f);
                    _vertexColors[ringVertex] = edgeColor;
                }
            }

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.colors = _vertexColors;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();
        }

        private static float EaseInOutSmooth(float t)
        {
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private void EnsureComponents(int sortingOrder)
        {
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.GetComponent<MeshFilter>();
                if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.GetComponent<MeshRenderer>();
                if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "LevelDrawingRevealMesh" };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_material == null)
            {
                _material = new Material(Shader.Find("Sprites/Default"));
                _material.mainTexture = Texture2D.whiteTexture;
                _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _material.SetInt("_ZWrite", 0);
                _material.renderQueue = 3000;
            }

            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.sortingOrder = sortingOrder;
        }

        private void AllocateMeshBuffers(int cellCount)
        {
            int vertexCount = cellCount * VerticesPerCell;
            int triangleCount = cellCount * TrianglesPerCell;

            _vertices = new Vector3[vertexCount];
            _vertexColors = new Color[vertexCount];
            _triangles = new int[triangleCount];

            int tri = 0;
            for (int i = 0; i < cellCount; i++)
            {
                int baseVertex = i * VerticesPerCell;
                for (int s = 0; s < BrushCircleSegments; s++)
                {
                    int next = (s + 1) % BrushCircleSegments;
                    _triangles[tri++] = baseVertex;
                    _triangles[tri++] = baseVertex + 1 + s;
                    _triangles[tri++] = baseVertex + 1 + next;
                }
            }
        }

        private void EnsureMarker()
        {
            if (_markerTransform != null) return;

            GameObject markerGo = new GameObject("RevealMarker");
            markerGo.transform.SetParent(transform, false);
            _markerTransform = markerGo.transform;
            _markerRenderer = markerGo.AddComponent<SpriteRenderer>();
            _markerRenderer.sortingOrder = 20;
            _markerRenderer.color = new Color(1f, 0.85f, 0.2f, 0.55f);
            _markerRenderer.sprite = CreateSoftCircleSprite(64);
            _markerTransform.localScale = Vector3.one;
            markerGo.SetActive(false);
        }

        private static Sprite CreateSoftCircleSprite(int size)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float center = (size - 1) * 0.5f;
            float radius = center;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float alpha = Mathf.Clamp01(1f - dist / radius);
                    alpha = alpha * alpha * (3f - 2f * alpha);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }
    }
}
