using System.Collections;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class Segment : MonoBehaviour
    {
        public Vector2Int GridPosition { get; set; }
        public SpriteRenderer Renderer;

        private void Awake()
        {
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
        }

        public void Initialize(Sprite sprite, Color color)
        {
            if (Renderer == null) Renderer = GetComponent<SpriteRenderer>();
            Renderer.sprite = sprite;
            Renderer.color = color;
        }

        public void MoveTo(Vector3 worldPos, float duration)
        {
            StartCoroutine(AnimateMove(worldPos, duration));
        }

        private IEnumerator AnimateMove(Vector3 targetPos, float duration)
        {
            Vector3 startPos = transform.position;
            float elapsed = 0;

            while (elapsed < duration)
            {
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            transform.position = targetPos;
        }
    }
}
