using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.GAE
{
    public static class GAECurrencyFlyAnimation
    {
        public static IEnumerator Play(
            RectTransform source,
            RectTransform target,
            Canvas canvas,
            Sprite iconSprite,
            int iconCount = 6,
            float duration = 0.85f,
            float scatterRadius = 40f)
        {
            if (source == null || target == null || canvas == null || iconSprite == null)
            {
                yield break;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            if (!TryGetLocalPoint(source, canvasRect, uiCamera, out Vector2 startLocal))
            {
                yield break;
            }

            if (!TryGetLocalPoint(target, canvasRect, uiCamera, out Vector2 endLocal))
            {
                yield break;
            }

            var icons = new List<RectTransform>(iconCount);
            var images = new List<Image>(iconCount);
            var startPositions = new List<Vector2>(iconCount);

            for (int i = 0; i < iconCount; i++)
            {
                GameObject iconObj = new GameObject("GaeFlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(canvasRect, false);

                Image image = iconObj.GetComponent<Image>();
                image.sprite = iconSprite;
                image.raycastTarget = false;
                image.SetNativeSize();

                RectTransform rect = iconObj.GetComponent<RectTransform>();
                rect.sizeDelta *= 0.65f;
                Vector2 scatter = Random.insideUnitCircle * scatterRadius;
                Vector2 iconStart = startLocal + scatter;
                rect.anchoredPosition = iconStart;

                icons.Add(rect);
                images.Add(image);
                startPositions.Add(iconStart);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                for (int i = 0; i < icons.Count; i++)
                {
                    icons[i].anchoredPosition = Vector2.Lerp(startPositions[i], endLocal, eased);

                    Color c = images[i].color;
                    c.a = t > 0.75f ? Mathf.Lerp(1f, 0f, (t - 0.75f) / 0.25f) : 1f;
                    images[i].color = c;
                }

                yield return null;
            }

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null)
                {
                    Object.Destroy(icons[i].gameObject);
                }
            }
        }

        public static IEnumerator PlayStaggered(
            RectTransform source,
            RectTransform target,
            Canvas canvas,
            Sprite iconSprite,
            int iconCount = 8,
            float iconScale = 0.4f,
            float staggerDelay = 0.09f,
            float spawnGap = 24f,
            float flyDuration = 0.65f,
            float scatterRadius = 10f,
            GAEFlyEffectRunner effectRunner = null)
        {
            if (source == null || target == null || canvas == null || iconSprite == null)
            {
                yield break;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

            if (!TryGetLocalPoint(source, canvasRect, uiCamera, out Vector2 startLocal) ||
                !TryGetLocalPoint(target, canvasRect, uiCamera, out Vector2 endLocal))
            {
                yield break;
            }

            iconCount = Mathf.Max(1, iconCount);
            iconScale = Mathf.Max(0.1f, iconScale);
            staggerDelay = Mathf.Max(0.02f, staggerDelay);
            flyDuration = Mathf.Max(0.1f, flyDuration);

            var flyingIcons = new List<StaggeredFlyIcon>(iconCount);
            int launchedCount = 0;
            float elapsed = 0f;
            float nextLaunchTime = 0f;

            while (launchedCount < iconCount || flyingIcons.Count > 0)
            {
                elapsed += Time.deltaTime;

                while (launchedCount < iconCount && elapsed >= nextLaunchTime)
                {
                    float centeredIndex = launchedCount - (iconCount - 1) * 0.5f;
                    Vector2 spawnOffset = new Vector2(centeredIndex * spawnGap, 0f);
                    spawnOffset += Random.insideUnitCircle * scatterRadius;

                    StaggeredFlyIcon flyIcon = CreateStaggeredFlyIcon(
                        canvasRect,
                        iconSprite,
                        startLocal + spawnOffset,
                        iconScale,
                        elapsed,
                        effectRunner);

                    flyingIcons.Add(flyIcon);

                    launchedCount++;
                    nextLaunchTime += staggerDelay;
                }

                for (int i = flyingIcons.Count - 1; i >= 0; i--)
                {
                    StaggeredFlyIcon icon = flyingIcons[i];
                    float flyElapsed = elapsed - icon.LaunchElapsed;
                    float t = Mathf.Clamp01(flyElapsed / flyDuration);
                    float eased = 1f - Mathf.Pow(1f - t, 3f);

                    icon.Rect.anchoredPosition = Vector2.Lerp(icon.StartPosition, endLocal, eased);

                    Color c = icon.Image.color;
                    c.a = t > 0.82f ? Mathf.Lerp(1f, 0f, (t - 0.82f) / 0.18f) : 1f;
                    icon.Image.color = c;

                    if (t >= 1f)
                    {
                        Object.Destroy(icon.Rect.gameObject);
                        flyingIcons.RemoveAt(i);
                    }
                }

                yield return null;
            }
        }

        private static StaggeredFlyIcon CreateStaggeredFlyIcon(
            RectTransform canvasRect,
            Sprite iconSprite,
            Vector2 startPosition,
            float iconScale,
            float launchElapsed,
            GAEFlyEffectRunner effectRunner)
        {
            GameObject iconObj = new GameObject("GaeStaggerFlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            iconObj.transform.SetParent(canvasRect, false);
            effectRunner?.Track(iconObj);

            Image image = iconObj.GetComponent<Image>();
            image.sprite = iconSprite;
            image.raycastTarget = false;
            image.SetNativeSize();

            RectTransform rect = iconObj.GetComponent<RectTransform>();
            rect.localScale = Vector3.one * iconScale;
            rect.anchoredPosition = startPosition;

            return new StaggeredFlyIcon
            {
                Rect = rect,
                Image = image,
                StartPosition = startPosition,
                LaunchElapsed = launchElapsed
            };
        }

        private class StaggeredFlyIcon
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 StartPosition;
            public float LaunchElapsed;
        }

        private static bool TryGetLocalPoint(RectTransform source, RectTransform canvasRect, Camera uiCamera, out Vector2 localPoint)
        {
            Vector3 worldCenter = source.TransformPoint(source.rect.center);
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, worldCenter);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out localPoint);
        }

        public static IEnumerator PlayFromScreenPoint(
            Vector2 screenPoint,
            Vector2 screenOffset,
            RectTransform target,
            Canvas canvas,
            Camera uiCamera,
            Sprite iconSprite,
            int iconCount = 1,
            float duration = 0.55f,
            float scatterRadius = 18f)
        {
            if (target == null || canvas == null || iconSprite == null)
            {
                yield break;
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            Camera canvasCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : uiCamera;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPoint + screenOffset,
                    canvasCamera,
                    out Vector2 startLocal))
            {
                yield break;
            }

            if (!TryGetLocalPoint(target, canvasRect, canvasCamera, out Vector2 endLocal))
            {
                yield break;
            }

            iconCount = Mathf.Max(1, iconCount);
            var icons = new List<RectTransform>(iconCount);
            var images = new List<Image>(iconCount);
            var startPositions = new List<Vector2>(iconCount);

            for (int i = 0; i < iconCount; i++)
            {
                GameObject iconObj = new GameObject("GaePickFlyIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(canvasRect, false);

                Image image = iconObj.GetComponent<Image>();
                image.sprite = iconSprite;
                image.raycastTarget = false;
                image.SetNativeSize();

                RectTransform rect = iconObj.GetComponent<RectTransform>();
                rect.sizeDelta *= 0.55f;
                Vector2 scatter = Random.insideUnitCircle * scatterRadius;
                Vector2 iconStart = startLocal + scatter;
                rect.anchoredPosition = iconStart;

                icons.Add(rect);
                images.Add(image);
                startPositions.Add(iconStart);
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);

                for (int i = 0; i < icons.Count; i++)
                {
                    icons[i].anchoredPosition = Vector2.Lerp(startPositions[i], endLocal, eased);

                    Color c = images[i].color;
                    c.a = t > 0.8f ? Mathf.Lerp(1f, 0f, (t - 0.8f) / 0.2f) : 1f;
                    images[i].color = c;
                }

                yield return null;
            }

            for (int i = 0; i < icons.Count; i++)
            {
                if (icons[i] != null)
                {
                    Object.Destroy(icons[i].gameObject);
                }
            }
        }
    }
}
