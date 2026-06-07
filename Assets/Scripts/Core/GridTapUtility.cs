using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Grid-based tap resolution — avoids Physics2D raycasts and segment colliders.
    /// </summary>
    public static class GridTapUtility
    {
        public static bool TryGetSegmentAtWorldPosition(Vector3 worldPos, out Segment segment, out ArrowController arrow)
        {
            segment = null;
            arrow = null;

            GridManager grid = GridManager.Instance;
            if (grid == null) return false;

            Vector2Int gridPos = new Vector2Int(
                Mathf.RoundToInt(worldPos.x),
                Mathf.RoundToInt(worldPos.y));

            arrow = grid.GetOccupant(gridPos);
            if (arrow == null) return false;

            segment = GetClosestSegmentOnArrow(arrow, worldPos);
            return segment != null;
        }

        public static Segment GetClosestSegmentOnArrow(ArrowController arrow, Vector3 worldPos)
        {
            if (arrow == null || arrow.segments == null || arrow.segments.Count == 0) return null;

            Segment best = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < arrow.segments.Count; i++)
            {
                Segment seg = arrow.segments[i];
                if (seg == null) continue;

                float sqrDist = (seg.CachedTransform.position - worldPos).sqrMagnitude;
                if (sqrDist < minSqrDist)
                {
                    minSqrDist = sqrDist;
                    best = seg;
                }
            }

            return best;
        }
    }
}
