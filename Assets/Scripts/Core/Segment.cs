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

        private void Awake()
        {
            CachedTransform = transform;
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Sprite sprite, Color color)
        {
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            Renderer.sprite = sprite;
            Renderer.color = color;
        }

    }
}
