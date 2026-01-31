using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        private Dictionary<Vector2Int, ArrowController> occupancyMap = new Dictionary<Vector2Int, ArrowController>();
        private List<ArrowController> allArrows = new List<ArrowController>();
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
            if (!allArrows.Contains(arrow))
            {
                allArrows.Add(arrow);
            }
        }

        public void UnregisterArrow(ArrowController arrow)
        {
            if (allArrows.Contains(arrow))
            {
                allArrows.Remove(arrow);
            }
            
            // Also clear its occupancy
            List<Vector2Int> keysToRemove = new List<Vector2Int>();
            foreach (var kvp in occupancyMap)
            {
                if (kvp.Value == arrow)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                occupancyMap.Remove(key);
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
    }
}
