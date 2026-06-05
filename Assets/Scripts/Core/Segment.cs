using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class Segment : MonoBehaviour
    {
        public Vector2Int GridPosition { get; set; }
        public SpriteRenderer Renderer;
        public ArrowController ParentArrow { get; set; }
        public Transform CachedTransform { get; private set; }

        private Sprite poolDefaultSprite;
        private Color poolDefaultColor = Color.white;
        private int poolDefaultSortingOrder;

        private void Awake()
        {
            CachedTransform = transform;
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            if (Renderer != null)
            {
                poolDefaultSprite = Renderer.sprite;
                poolDefaultColor = Renderer.color;
                poolDefaultSortingOrder = Renderer.sortingOrder;
            }

            // Gameplay uses grid tap detection — colliders are never needed at runtime.
            SetColliderEnabled(false);
        }

        public void Initialize(Sprite sprite, Color color)
        {
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            Renderer.sprite = sprite;
            Renderer.color = color;
        }

        /// <summary>Clear head/blocked/win visual state before segment pool reuse.</summary>
        public void ResetForPool()
        {
            ParentArrow = null;
            GridPosition = Vector2Int.zero;
            CachedTransform.localScale = Vector3.one;
            CachedTransform.rotation = Quaternion.identity;
            SetColliderEnabled(false);

            if (Renderer == null) return;

            Renderer.enabled = false;
            Renderer.sprite = poolDefaultSprite;
            Renderer.color = poolDefaultColor;
            Renderer.sortingOrder = poolDefaultSortingOrder;
        }

        public void SetColliderEnabled(bool enabled)
        {
            BoxCollider2D col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                col.enabled = enabled;
            }
        }

    }
}
