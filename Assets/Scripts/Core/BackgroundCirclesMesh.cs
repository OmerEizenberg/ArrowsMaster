using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Renders all level background dots in a single dynamic mesh (one draw call).
    /// Supports per-circle scale/color animation for win effects.
    /// </summary>
    public class BackgroundCirclesMesh : MonoBehaviour
    {
        private Mesh _mesh;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;

        private Vector3[] _basePositions;
        private float[] _scales;
        private Color[] _circleColors;
        private float[] _distancesFromCenter;

        private Vector3[] _vertices;
        private Color[] _vertexColors;
        private Vector2[] _uvs;
        private int[] _triangles;

        private Vector2 _uvMin;
        private Vector2 _uvMax;
        private Vector2 _spriteHalfSize;
        private bool _meshDirty;
        private int _rebuildFrameCounter;

        public int Count => _basePositions != null ? _basePositions.Length : 0;

        public float GetDistanceFromCenter(int index) => _distancesFromCenter[index];

        public Vector3 GetWorldPosition(int index) => _basePositions[index];

        public float GetScale(int index) => _scales[index];

        public void SetScale(int index, float scale)
        {
            if (_scales == null || index < 0 || index >= _scales.Length) return;
            _scales[index] = scale;
            _meshDirty = true;
        }

        public void SetColor(int index, Color color)
        {
            if (_circleColors == null || index < 0 || index >= _circleColors.Length) return;
            _circleColors[index] = color;
            _meshDirty = true;
        }

        public void SetScaleAll(float scale)
        {
            if (_scales == null) return;
            for (int i = 0; i < _scales.Length; i++)
            {
                _scales[i] = scale;
            }
            _meshDirty = true;
        }

        public void ApplyIfDirty()
        {
            if (!_meshDirty || _basePositions == null || _basePositions.Length == 0) return;

            if (DevicePerformanceProfile.IsLowEnd)
            {
                _rebuildFrameCounter++;
                if (_rebuildFrameCounter % 2 != 0) return;
            }

            RebuildMesh();
            _meshDirty = false;
        }

        public void BuildFromPositions(Sprite sprite, Color color, IList<Vector3> positions, Vector3 levelCenter, int sortingOrder)
        {
            Clear();

            if (sprite == null || positions == null || positions.Count == 0) return;

            EnsureComponents(sprite, sortingOrder);

            int count = positions.Count;
            _basePositions = new Vector3[count];
            _scales = new float[count];
            _circleColors = new Color[count];
            _distancesFromCenter = new float[count];

            for (int i = 0; i < count; i++)
            {
                _basePositions[i] = positions[i];
                _scales[i] = 1f;
                _circleColors[i] = color;
                _distancesFromCenter[i] = Vector3.Distance(positions[i], levelCenter);
            }

            CacheSpriteGeometry(sprite);
            AllocateMeshBuffers(count);
            _meshDirty = true;
            ApplyIfDirty();
            _meshRenderer.enabled = true;
        }

        public void ApplyFinishState(Color baseColor, float alpha, float scale)
        {
            if (_circleColors == null) return;

            Color c = baseColor;
            c.a = alpha;
            for (int i = 0; i < _circleColors.Length; i++)
            {
                _scales[i] = scale;
                _circleColors[i] = c;
            }
            _meshDirty = true;
            ApplyIfDirty();
        }

        public void Clear()
        {
            _basePositions = null;
            _scales = null;
            _circleColors = null;
            _distancesFromCenter = null;
            _vertices = null;
            _vertexColors = null;
            _uvs = null;
            _triangles = null;
            _meshDirty = false;

            if (_mesh != null)
            {
                _mesh.Clear();
            }

            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
        }

        private void EnsureComponents(Sprite sprite, int sortingOrder)
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
                _mesh = new Mesh { name = "BackgroundCirclesMesh" };
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }

            if (_material == null)
            {
                _material = new Material(Shader.Find("Sprites/Default"));
            }

            _material.mainTexture = sprite.texture;
            _meshRenderer.sharedMaterial = _material;
            _meshRenderer.sortingOrder = sortingOrder;
        }

        private void CacheSpriteGeometry(Sprite sprite)
        {
            Vector2 size = sprite.bounds.size;
            _spriteHalfSize = size * 0.5f;

            Rect rect = sprite.textureRect;
            Texture texture = sprite.texture;
            float tw = texture.width;
            float th = texture.height;
            _uvMin = new Vector2(rect.x / tw, rect.y / th);
            _uvMax = new Vector2((rect.x + rect.width) / tw, (rect.y + rect.height) / th);
        }

        private void AllocateMeshBuffers(int circleCount)
        {
            int vertexCount = circleCount * 4;
            int triangleCount = circleCount * 6;

            if (_vertices == null || _vertices.Length != vertexCount)
            {
                _vertices = new Vector3[vertexCount];
                _vertexColors = new Color[vertexCount];
                _uvs = new Vector2[vertexCount];
                _triangles = new int[triangleCount];
            }

            int tri = 0;
            for (int i = 0; i < circleCount; i++)
            {
                int v = i * 4;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 1;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v;
                _triangles[tri++] = v + 2;
                _triangles[tri++] = v + 3;

                _uvs[v] = new Vector2(_uvMin.x, _uvMin.y);
                _uvs[v + 1] = new Vector2(_uvMax.x, _uvMin.y);
                _uvs[v + 2] = new Vector2(_uvMax.x, _uvMax.y);
                _uvs[v + 3] = new Vector2(_uvMin.x, _uvMax.y);
            }
        }

        private void RebuildMesh()
        {
            int circleCount = _basePositions.Length;
            int vertexCount = circleCount * 4;

            for (int i = 0; i < circleCount; i++)
            {
                Vector3 center = _basePositions[i];
                float scale = _scales[i];
                Vector2 half = _spriteHalfSize * scale;
                Color color = _circleColors[i];

                int v = i * 4;
                _vertices[v] = center + new Vector3(-half.x, -half.y, 0f);
                _vertices[v + 1] = center + new Vector3(half.x, -half.y, 0f);
                _vertices[v + 2] = center + new Vector3(half.x, half.y, 0f);
                _vertices[v + 3] = center + new Vector3(-half.x, half.y, 0f);

                _vertexColors[v] = color;
                _vertexColors[v + 1] = color;
                _vertexColors[v + 2] = color;
                _vertexColors[v + 3] = color;
            }

            _mesh.Clear();
            _mesh.vertices = _vertices;
            _mesh.colors = _vertexColors;
            _mesh.uv = _uvs;
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                Destroy(_mesh);
                _mesh = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }
        }
    }
}
