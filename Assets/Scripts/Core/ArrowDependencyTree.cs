using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Optimization: A Dependency Tree (DAG) that represents the relationships between arrows.
    /// It tracks which arrows block which other arrows.
    /// This allows checking if an arrow is free in O(1) time.
    /// </summary>
    public class ArrowDependencyTree
    {
        private class DependencyNode
        {
            public ArrowController Arrow;
            public HashSet<ArrowController> Blockers = new HashSet<ArrowController>();
            public HashSet<ArrowController> Dependents = new HashSet<ArrowController>();

            public DependencyNode(ArrowController arrow)
            {
                Arrow = arrow;
            }
        }

        private Dictionary<ArrowController, DependencyNode> nodes = new Dictionary<ArrowController, DependencyNode>();
        private HashSet<ArrowController> freeArrows = new HashSet<ArrowController>();

        public System.Collections.IEnumerator BuildAsync(List<ArrowController> allArrows)
        {
            nodes.Clear();
            freeArrows.Clear();

            System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            // 1. Create nodes for all arrows
            foreach (var arrow in allArrows)
            {
                if (arrow == null) continue;
                nodes[arrow] = new DependencyNode(arrow);
                
                if (sw.Elapsed.TotalMilliseconds > 0.5)
                {
                    yield return null;
                    sw.Restart();
                }
            }

            // 2. Calculate dependencies
            for (int i = 0; i < allArrows.Count; i++)
            {
                var arrow = allArrows[i];
                if (arrow == null) continue;

                CalculateDependencies(arrow);

                // Time budget check: Yield if we've spent more than 0.5ms this frame
                if (sw.Elapsed.TotalMilliseconds > 0.5)
                {
                    yield return null;
                    sw.Restart();
                }
            }

            // 3. Identify free arrows
            foreach (var node in nodes.Values)
            {
                if (node.Blockers.Count == 0)
                {
                    freeArrows.Add(node.Arrow);
                }
            }
        }

        public void Build(List<ArrowController> allArrows)
        {
            // Keep synchronous version for small levels or testing
            var iterator = BuildAsync(allArrows);
            while (iterator.MoveNext()) { /* Run to completion */ }
        }

        private void CalculateDependencies(ArrowController arrow)
        {
            if (arrow == null || arrow.segments.Count == 0) return;

            // Trace the path of the arrow until it hits a boundary
            // Any arrow encountered along this path is a blocker
            Vector2Int currentDir = arrow.LookDirection;
            if (currentDir == Vector2Int.zero) return;

            Vector2Int checkPos = arrow.GetHeadGridPosition() + currentDir;

            while (!GridManager.Instance.IsOutOfBounds(checkPos))
            {
                ArrowController occupant = GridManager.Instance.GetOccupant(checkPos);
                if (occupant != null && occupant != arrow && !occupant.IsMoving)
                {
                    AddDependency(occupant, arrow); // occupant blocks arrow
                }
                checkPos += currentDir;
            }
        }

        private void AddDependency(ArrowController blocker, ArrowController blocked)
        {
            if (nodes.TryGetValue(blocker, out var blockerNode) && nodes.TryGetValue(blocked, out var blockedNode))
            {
                if (blockerNode.Dependents.Add(blocked))
                {
                    blockedNode.Blockers.Add(blocker);
                }
            }
        }

        /// <summary>
        /// Call this when an arrow starts moving (escaping).
        /// It will no longer block its dependents.
        /// </summary>
        public void OnArrowStartedMoving(ArrowController arrow)
        {
            if (nodes.TryGetValue(arrow, out var node))
            {
                // This arrow is no longer a blocker for others
                foreach (var dependent in node.Dependents)
                {
                    if (nodes.TryGetValue(dependent, out var dependentNode))
                    {
                        dependentNode.Blockers.Remove(arrow);
                        if (dependentNode.Blockers.Count == 0)
                        {
                            freeArrows.Add(dependent);
                        }
                    }
                }
                node.Dependents.Clear();
                
                // This arrow is definitely not free anymore (it's moving)
                freeArrows.Remove(arrow);
            }
        }

        public bool IsArrowFree(ArrowController arrow)
        {
            if (nodes.TryGetValue(arrow, out var node))
            {
                return node.Blockers.Count == 0;
            }
            return false;
        }

        public List<ArrowController> GetAllFreeArrows()
        {
            List<ArrowController> result = new List<ArrowController>();
            foreach (var node in nodes.Values)
            {
                if (node.Arrow != null && !node.Arrow.IsMoving && node.Blockers.Count == 0)
                {
                    result.Add(node.Arrow);
                }
            }
            return result;
        }

        public int GetDependentCount(ArrowController arrow)
        {
            if (nodes.TryGetValue(arrow, out var node))
            {
                return node.Dependents.Count;
            }
            return 0;
        }

        public IReadOnlyCollection<ArrowController> GetDirectBlockers(ArrowController arrow)
        {
            if (nodes.TryGetValue(arrow, out var node))
            {
                return node.Blockers;
            }
            return System.Array.Empty<ArrowController>();
        }

        /// <summary>Blockers move before the arrows they block (topological order).</summary>
        public List<ArrowController> OrderByBlockerChain(List<ArrowController> arrows)
        {
            if (arrows == null || arrows.Count <= 1) return arrows;

            var set = new HashSet<ArrowController>(arrows);
            var inDegree = new Dictionary<ArrowController, int>();
            foreach (var arrow in arrows)
            {
                inDegree[arrow] = 0;
            }

            foreach (var blocked in arrows)
            {
                foreach (var blocker in GetDirectBlockers(blocked))
                {
                    if (set.Contains(blocker))
                    {
                        inDegree[blocked]++;
                    }
                }
            }

            var queue = new Queue<ArrowController>();
            foreach (var arrow in arrows)
            {
                if (inDegree[arrow] == 0)
                {
                    queue.Enqueue(arrow);
                }
            }

            var ordered = new List<ArrowController>(arrows.Count);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                ordered.Add(current);

                if (!nodes.TryGetValue(current, out var node)) continue;
                foreach (var dependent in node.Dependents)
                {
                    if (!set.Contains(dependent) || !inDegree.ContainsKey(dependent)) continue;
                    inDegree[dependent]--;
                    if (inDegree[dependent] == 0)
                    {
                        queue.Enqueue(dependent);
                    }
                }
            }

            if (ordered.Count < arrows.Count)
            {
                foreach (var arrow in arrows)
                {
                    if (!ordered.Contains(arrow))
                    {
                        ordered.Add(arrow);
                    }
                }
            }

            return ordered;
        }

        public void Clear()
        {
            nodes.Clear();
            freeArrows.Clear();
        }
    }
}
