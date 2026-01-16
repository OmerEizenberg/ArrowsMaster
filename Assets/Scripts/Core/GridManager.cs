using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        private Dictionary<Vector2Int, ArrowController> occupancyMap = new Dictionary<Vector2Int, ArrowController>();
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
        }

        public bool IsOutOfBounds(Vector2Int coord)
        {
            return coord.x < 0 || coord.x >= gridSize.x || coord.y < 0 || coord.y >= gridSize.y;
        }

        public bool IsCellOccupied(Vector2Int coord)
        {
            // Only check dictionary. Bounds check should be done separately by the caller if needed
            // or we assume OOB is NOT "occupied" in the sense of another arrow being there, 
            // but might be "invalid" for movement unless escaping.
            // However, to keep existing logic safe, let's say OOB is NOT occupied by an arrow.
            
            return occupancyMap.ContainsKey(coord);
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
    }
}
