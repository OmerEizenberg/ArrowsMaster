using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        private Dictionary<Vector2Int, ArrowController> occupancyMap = new Dictionary<Vector2Int, ArrowController>();
        private List<ArrowController> allArrows = new List<ArrowController>();
        // HashSet for O(1) Contains checks — List kept for ordered iteration
        private HashSet<ArrowController> allArrowsSet = new HashSet<ArrowController>();
        private Vector2Int gridSize;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void InitializeGrid(Vector2Int size)
        {
            gridSize = size;
            occupancyMap.Clear();
            allArrows.Clear();
            allArrowsSet.Clear();
        }

        public bool IsOutOfBounds(Vector2Int coord)
        {
            return coord.x < 0 || coord.x >= gridSize.x || coord.y < 0 || coord.y >= gridSize.y;
        }

        public bool IsCellOccupied(Vector2Int coord)
        {
            return occupancyMap.ContainsKey(coord);
        }

        public void RegisterArrow(ArrowController arrow)
        {
            if (!allArrowsSet.Contains(arrow))
            {
                allArrows.Add(arrow);
                allArrowsSet.Add(arrow);
            }
        }

        public void UnregisterArrow(ArrowController arrow)
        {
            if (allArrowsSet.Contains(arrow))
            {
                allArrows.Remove(arrow);
                allArrowsSet.Remove(arrow);
            }
            
            // Optimization: Only clear keys this arrow actually occupies
            if (arrow.segments != null)
            {
                foreach (var seg in arrow.segments)
                {
                    if (occupancyMap.TryGetValue(seg.GridPosition, out var occupant) && occupant == arrow)
                    {
                        occupancyMap.Remove(seg.GridPosition);
                    }
                }
            }
        }

        public List<ArrowController> GetAllArrows()
        {
            return allArrows;
        }

        public void RegisterOccupancy(Vector2Int coord, ArrowController arrow)
        {
            if (!occupancyMap.ContainsKey(coord))
            {
                occupancyMap.Add(coord, arrow);
            }
            else
            {
                occupancyMap[coord] = arrow;
            }
            
            RegisterArrow(arrow);
        }

        public void ReleaseOccupancy(Vector2Int coord)
        {
            if (occupancyMap.ContainsKey(coord))
            {
                occupancyMap.Remove(coord);
            }
        }

        public void RegisterMove(Vector2Int oldPos, Vector2Int newPos, ArrowController arrow)
        {
            ReleaseOccupancy(oldPos);
            RegisterOccupancy(newPos, arrow);
        }

        public ArrowController GetOccupant(Vector2Int coord)
        {
            if (occupancyMap.TryGetValue(coord, out ArrowController arrow))
            {
                return arrow;
            }
            return null;
        }
        public List<ArrowController> GetNonBlockedArrows(int count)
        {
            List<ArrowController> result = new List<ArrowController>();
            foreach (var arrow in allArrows)
            {
                if (arrow != null && !arrow.IsMoving && arrow.CanMoveForward())
                {
                    result.Add(arrow);
                    if (result.Count >= count) break;
                }
            }
            return result;
        }
    }
}
