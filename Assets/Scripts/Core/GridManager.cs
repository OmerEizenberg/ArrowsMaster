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
        public Vector2Int GridSize => gridSize;
        public ArrowDependencyTree DependencyTree { get; private set; }

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
            if (DependencyTree == null) DependencyTree = new ArrowDependencyTree();
            else DependencyTree.Clear();
        }

        public System.Collections.IEnumerator RebuildDependencyTreeAsync()
        {
            if (DependencyTree != null)
            {
                yield return DependencyTree.BuildAsync(allArrows);
            }
        }

        public void RebuildDependencyTree()
        {
            if (DependencyTree != null)
            {
                DependencyTree.Build(allArrows);
            }
        }

        /// <summary>Rebuilds occupancy from segment grid positions (fixes stale cells after shuffle).</summary>
        public void RebuildOccupancyFromSegments()
        {
            occupancyMap.Clear();
            for (int i = 0; i < allArrows.Count; i++)
            {
                ArrowController arrow = allArrows[i];
                if (arrow == null || arrow.segments == null || arrow.segments.Count == 0) continue;

                for (int s = 0; s < arrow.segments.Count; s++)
                {
                    Segment seg = arrow.segments[s];
                    if (seg == null) continue;
                    Vector2Int coord = seg.GridPosition;
                    occupancyMap[coord] = arrow;
                }
            }
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
            if (arrow == null) return;

            if (DependencyTree != null) DependencyTree.OnArrowStartedMoving(arrow);

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
                    if (seg == null) continue;
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
                if (arrow == null || arrow.IsMoving) continue;
                if (!IsArrowFreeByForwardRay(arrow)) continue;
                result.Add(arrow);
                if (count > 0 && result.Count >= count) break;
            }
            return result;
        }

        /// <summary>Clear forward path in look direction (grid walk; moving arrows do not block).</summary>
        public bool IsArrowFreeByForwardRay(ArrowController arrow)
        {
            if (arrow == null || arrow.IsMoving || arrow.segments.Count == 0) return false;

            Vector2Int currentDir = arrow.LookDirection;
            if (currentDir == Vector2Int.zero) return false;

            Vector2Int checkPos = arrow.GetHeadGridPosition() + currentDir;
            while (!IsOutOfBounds(checkPos))
            {
                ArrowController occupant = GetOccupant(checkPos);
                if (occupant != null && occupant != arrow && !occupant.IsMoving)
                {
                    return false;
                }
                checkPos += currentDir;
            }

            return true;
        }
    }
}
