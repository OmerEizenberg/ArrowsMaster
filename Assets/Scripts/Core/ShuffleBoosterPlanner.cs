using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Core
{
    public sealed class ShuffleMovePlan
    {
        public ArrowController Arrow;
        public List<Vector2Int> HeadSteps;
        public HashSet<Vector2Int> OccupiedCells;
    }

    /// <summary>
    /// Plans shuffle moves: prefers free arrows that stay free; falls back when few qualify.
    /// Solvability: each plan's final pose must pass <see cref="IsBodyLayoutFree"/> (clear look-direction ray
    /// on the planning grid). Tier 1 also requires a free start. Later plans reserve resting cells so
    /// earlier shuffles are not placed on top of each other.
    /// </summary>
    public static class ShuffleBoosterPlanner
    {
        private enum PlanMode
        {
            /// <summary>Must start and end unblocked.</summary>
            FreeStayFree,
            /// <summary>May start blocked; must end unblocked (opens tight levels).</summary>
            BecomeFree
        }

        private const int MaxPlans = 5;
        private const int MaxGreedySteps = 24;
        /// <summary>Prefer at least this many cells of head travel (Manhattan) when the grid allows it.</summary>
        private const int MinPreferredHeadTravel = 4;
        private const int TravelScorePerCell = 50;

        private static readonly Vector2Int[] s_Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private static readonly HashSet<Vector2Int> s_BlockedCells = new HashSet<Vector2Int>(256);
        private static readonly HashSet<Vector2Int> s_ReservedCells = new HashSet<Vector2Int>(128);
        private static readonly List<Vector2Int> s_HeadStepsBuffer = new List<Vector2Int>(MaxGreedySteps);
        private static readonly List<ArrowController> s_PoolBuffer = new List<ArrowController>(32);
        private static readonly List<ArrowController> s_ExcludeArrows = new List<ArrowController>(8);
        private static Vector2Int[] s_WorkBody;
        private static Vector2Int[] s_BestBody;
        private static int s_WorkCapacity;
        private static Vector2Int s_PlanStartHead;

        /// <summary>Main entry: tiered planning, up to 5 non-conflicting moves.</summary>
        public static List<ShuffleMovePlan> BuildShufflePlans()
        {
            if (GridManager.Instance == null) return new List<ShuffleMovePlan>();

            GridManager.Instance.RebuildDependencyTree();

            var plans = new List<ShuffleMovePlan>(MaxPlans);
            s_ReservedCells.Clear();

            // Tier 1 — free arrows, still free when resting.
            CollectFreeArrows(s_PoolBuffer);
            SortByShufflePriority(s_PoolBuffer);
            PlanArrowsUpTo(s_PoolBuffer, PlanMode.FreeStayFree, plans, MaxPlans);

            // Tier 2 — blockers that can shuffle into a free resting pose.
            if (plans.Count < MaxPlans)
            {
                CollectBlockerArrows(s_PoolBuffer);
                SortByShufflePriority(s_PoolBuffer);
                PlanArrowsUpTo(s_PoolBuffer, PlanMode.BecomeFree, plans, MaxPlans - plans.Count);
            }

            // Tier 3 — fill remaining slots with a single step per free arrow.
            if (plans.Count < MaxPlans)
            {
                CollectFreeArrows(s_PoolBuffer);
                PlanMinimalSteps(s_PoolBuffer, plans, MaxPlans - plans.Count);
            }

            return plans;
        }

        public static List<ArrowController> SelectArrowsForShuffle()
        {
            var plans = BuildShufflePlans();
            var arrows = new List<ArrowController>(plans.Count);
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i]?.Arrow != null) arrows.Add(plans[i].Arrow);
            }
            return arrows;
        }

        public static List<ShuffleMovePlan> PlanShuffleMoves(List<ArrowController> selected)
        {
            var plans = new List<ShuffleMovePlan>();
            if (selected == null || selected.Count == 0) return plans;

            s_ReservedCells.Clear();
            PlanArrowsUpTo(selected, PlanMode.FreeStayFree, plans, MaxPlans);
            return plans;
        }

        public static List<List<ShuffleMovePlan>> PartitionIntoParallelGroups(List<ShuffleMovePlan> plans)
        {
            var groups = new List<List<ShuffleMovePlan>>();
            if (plans == null) return groups;

            for (int i = 0; i < plans.Count; i++)
            {
                ShuffleMovePlan plan = plans[i];
                if (plan == null) continue;

                bool placed = false;
                for (int g = 0; g < groups.Count; g++)
                {
                    if (!ConflictsWithGroup(plan, groups[g]))
                    {
                        groups[g].Add(plan);
                        placed = true;
                        break;
                    }
                }

                if (!placed)
                {
                    groups.Add(new List<ShuffleMovePlan> { plan });
                }
            }

            return groups;
        }

        public static bool TryPlanMove(ArrowController arrow, out List<Vector2Int> headSteps)
        {
            return TryPlanMove(arrow, null, null, PlanMode.FreeStayFree, out headSteps);
        }

        private static void PlanArrowsUpTo(
            List<ArrowController> arrows,
            PlanMode mode,
            List<ShuffleMovePlan> output,
            int maxAdd)
        {
            if (arrows == null || output == null || maxAdd <= 0) return;

            for (int i = 0; i < arrows.Count && output.Count < maxAdd; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow == null || arrow.IsMoving) continue;
                if (ArrowAlreadyPlanned(arrow, output)) continue;

                s_ExcludeArrows.Clear();
                for (int p = 0; p < output.Count; p++)
                {
                    if (output[p]?.Arrow != null) s_ExcludeArrows.Add(output[p].Arrow);
                }

                if (!TryPlanMove(arrow, s_ExcludeArrows, s_ReservedCells, mode, out List<Vector2Int> headSteps))
                {
                    continue;
                }

                ShuffleMovePlan plan = BuildPlan(arrow, headSteps);
                output.Add(plan);
                AddFinalBodyCells(arrow, headSteps, s_ReservedCells);
            }
        }

        private static void PlanMinimalSteps(List<ArrowController> arrows, List<ShuffleMovePlan> output, int maxAdd)
        {
            if (arrows == null || output == null) return;

            s_ReservedCells.Clear();
            for (int i = 0; i < arrows.Count && output.Count < maxAdd; i++)
            {
                ArrowController arrow = arrows[i];
                if (arrow == null || arrow.IsMoving) continue;
                if (ArrowAlreadyPlanned(arrow, output)) continue;

                s_ExcludeArrows.Clear();
                for (int p = 0; p < output.Count; p++)
                {
                    if (output[p]?.Arrow != null) s_ExcludeArrows.Add(output[p].Arrow);
                }

                if (!TryPlanSingleFreeStep(arrow, s_ExcludeArrows, s_ReservedCells, out List<Vector2Int> headSteps))
                {
                    continue;
                }

                output.Add(BuildPlan(arrow, headSteps));
                AddFinalBodyCells(arrow, headSteps, s_ReservedCells);
            }
        }

        private static bool ArrowAlreadyPlanned(ArrowController arrow, List<ShuffleMovePlan> plans)
        {
            for (int i = 0; i < plans.Count; i++)
            {
                if (plans[i]?.Arrow == arrow) return true;
            }
            return false;
        }

        private static void CollectFreeArrows(List<ArrowController> into)
        {
            into.Clear();
            foreach (ArrowController arrow in GridManager.Instance.GetAllArrows())
            {
                if (arrow == null || arrow.IsMoving || arrow.segments.Count == 0) continue;
                if (!GridManager.Instance.IsArrowFreeByForwardRay(arrow)) continue;
                into.Add(arrow);
            }
        }

        private static void CollectBlockerArrows(List<ArrowController> into)
        {
            into.Clear();
            ArrowDependencyTree tree = GridManager.Instance.DependencyTree;
            foreach (ArrowController arrow in GridManager.Instance.GetAllArrows())
            {
                if (arrow == null || arrow.IsMoving || arrow.segments.Count == 0) continue;
                if (tree != null && tree.GetDependentCount(arrow) <= 0) continue;
                if (GridManager.Instance.IsArrowFreeByForwardRay(arrow)) continue;
                into.Add(arrow);
            }
        }

        private static void SortByShufflePriority(List<ArrowController> arrows)
        {
            arrows.Sort((a, b) =>
            {
                int scoreA = GetArrowShufflePriority(a);
                int scoreB = GetArrowShufflePriority(b);
                return scoreB.CompareTo(scoreA);
            });
        }

        private static int GetArrowShufflePriority(ArrowController arrow)
        {
            if (arrow == null) return int.MinValue;
            int segmentCount = arrow.segments.Count;
            EnsureWorkCapacity(segmentCount);
            BeginPlan(arrow, null, null);
            CopyBody(arrow, s_WorkBody);
            s_PlanStartHead = s_WorkBody[segmentCount - 1];
            int score = ScoreBodyLayout(s_WorkBody, segmentCount, arrow.LookDirection);
            EndPlan(arrow);
            return score;
        }

        private static bool TryPlanMove(
            ArrowController arrow,
            List<ArrowController> alsoExclude,
            HashSet<Vector2Int> extraBlocked,
            PlanMode mode,
            out List<Vector2Int> headSteps)
        {
            headSteps = null;
            if (arrow == null || arrow.segments.Count == 0 || GridManager.Instance == null) return false;

            if (mode == PlanMode.FreeStayFree && !GridManager.Instance.IsArrowFreeByForwardRay(arrow))
            {
                return false;
            }

            int segmentCount = arrow.segments.Count;
            EnsureWorkCapacity(segmentCount);
            BeginPlan(arrow, alsoExclude, extraBlocked);
            CopyBody(arrow, s_WorkBody);
            s_PlanStartHead = s_WorkBody[segmentCount - 1];

            Vector2Int lastMoveDir = arrow.LookDirection;

            int startScore = ScoreBodyLayout(s_WorkBody, segmentCount, lastMoveDir);
            s_HeadStepsBuffer.Clear();
            int bestScore = startScore;

            for (int step = 0; step < MaxGreedySteps; step++)
            {
                if (!TryPickStep(requireImprovement: true, ref bestScore, segmentCount, ref lastMoveDir))
                {
                    break;
                }

                ApplyStep(lastMoveDir, segmentCount);
            }

            for (int step = s_HeadStepsBuffer.Count; step < MaxGreedySteps; step++)
            {
                if (!TryPickStep(requireImprovement: false, ref bestScore, segmentCount, ref lastMoveDir))
                {
                    break;
                }

                ApplyStep(lastMoveDir, segmentCount);
            }

            TryExtendTowardMinTravel(segmentCount, ref lastMoveDir, MinPreferredHeadTravel);

            if (s_HeadStepsBuffer.Count == 0)
            {
                TryPlanSingleFreeStepInternal(segmentCount, ref lastMoveDir);
            }

            EndPlan(arrow);

            if (s_HeadStepsBuffer.Count == 0) return false;
            if (!IsFinalPoseSolvable(s_WorkBody, segmentCount, lastMoveDir, mode)) return false;

            headSteps = new List<Vector2Int>(s_HeadStepsBuffer);
            return true;
        }

        private static bool TryPlanSingleFreeStep(
            ArrowController arrow,
            List<ArrowController> alsoExclude,
            HashSet<Vector2Int> extraBlocked,
            out List<Vector2Int> headSteps)
        {
            headSteps = null;
            if (arrow == null || arrow.segments.Count == 0) return false;

            int segmentCount = arrow.segments.Count;
            EnsureWorkCapacity(segmentCount);
            BeginPlan(arrow, alsoExclude, extraBlocked);
            CopyBody(arrow, s_WorkBody);
            s_PlanStartHead = s_WorkBody[segmentCount - 1];
            Vector2Int lastMoveDir = arrow.LookDirection;
            bool ok = TryPlanSingleFreeStepInternal(segmentCount, ref lastMoveDir);
            EndPlan(arrow);

            if (!ok) return false;
            if (!IsFinalPoseSolvable(s_WorkBody, segmentCount, lastMoveDir, PlanMode.FreeStayFree)) return false;
            headSteps = new List<Vector2Int>(s_HeadStepsBuffer);
            return true;
        }

        /// <summary>Final resting pose must have a clear escape ray (level stays solvable for that arrow).</summary>
        private static bool IsFinalPoseSolvable(Vector2Int[] body, int count, Vector2Int lookDir, PlanMode mode)
        {
            if (!IsBodyLayoutFree(body, count, lookDir)) return false;

            if (mode == PlanMode.FreeStayFree && GridManager.Instance != null)
            {
                Vector2Int head = body[count - 1];
                Vector2Int check = head + lookDir;
                while (!GridManager.Instance.IsOutOfBounds(check))
                {
                    if (s_BlockedCells.Contains(check) && !CellOnBody(check, body, count))
                    {
                        return false;
                    }
                    check += lookDir;
                }
            }

            return true;
        }

        private static void TryExtendTowardMinTravel(int segmentCount, ref Vector2Int lastMoveDir, int minTravel)
        {
            while (HeadTravelDistance(s_WorkBody, segmentCount) < minTravel && s_HeadStepsBuffer.Count < MaxGreedySteps)
            {
                Vector2Int head = s_WorkBody[segmentCount - 1];
                int bestTravel = -1;
                Vector2Int bestMoveDir = lastMoveDir;
                bool found = false;

                for (int d = 0; d < s_Directions.Length; d++)
                {
                    Vector2Int dir = s_Directions[d];
                    Vector2Int nextHead = head + dir;
                    if (!TryBuildNextBody(s_WorkBody, segmentCount, nextHead, s_BestBody)) continue;
                    if (HeadWouldCycle(nextHead)) continue;

                    int travel = Mathf.Abs(nextHead.x - s_PlanStartHead.x) + Mathf.Abs(nextHead.y - s_PlanStartHead.y);
                    if (travel > bestTravel)
                    {
                        bestTravel = travel;
                        bestMoveDir = dir;
                        found = true;
                    }
                }

                if (!found) break;
                ApplyStep(bestMoveDir, segmentCount);
                lastMoveDir = bestMoveDir;
            }
        }

        private static int HeadTravelDistance(Vector2Int[] body, int count)
        {
            Vector2Int head = body[count - 1];
            return Mathf.Abs(head.x - s_PlanStartHead.x) + Mathf.Abs(head.y - s_PlanStartHead.y);
        }

        private static bool TryPlanSingleFreeStepInternal(int segmentCount, ref Vector2Int lastMoveDir)
        {
            Vector2Int head = s_WorkBody[segmentCount - 1];
            int bestScore = int.MinValue;
            int bestDir = -1;
            Vector2Int bestMoveDir = lastMoveDir;

            for (int d = 0; d < s_Directions.Length; d++)
            {
                Vector2Int dir = s_Directions[d];
                Vector2Int nextHead = head + dir;
                if (!TryBuildNextBody(s_WorkBody, segmentCount, nextHead, s_BestBody)) continue;
                if (!IsBodyLayoutFree(s_BestBody, segmentCount, dir)) continue;

                int score = ScoreBodyLayout(s_BestBody, segmentCount, dir);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = d;
                    bestMoveDir = dir;
                }
            }

            if (bestDir < 0) return false;

            s_HeadStepsBuffer.Clear();
            ApplyStep(bestMoveDir, segmentCount);
            lastMoveDir = bestMoveDir;
            return true;
        }

        private static ShuffleMovePlan BuildPlan(ArrowController arrow, List<Vector2Int> headSteps)
        {
            return new ShuffleMovePlan
            {
                Arrow = arrow,
                HeadSteps = headSteps,
                OccupiedCells = CollectOccupiedCells(arrow, headSteps)
            };
        }

        private static HashSet<Vector2Int> CollectOccupiedCells(ArrowController arrow, List<Vector2Int> headSteps)
        {
            var cells = new HashSet<Vector2Int>();
            int count = arrow.segments.Count;
            EnsureWorkCapacity(count);
            CopyBody(arrow, s_WorkBody);
            AddBodyCells(s_WorkBody, count, cells);

            if (headSteps == null) return cells;

            for (int s = 0; s < headSteps.Count; s++)
            {
                if (!TryBuildNextBodyLocal(s_WorkBody, count, headSteps[s], s_BestBody)) continue;
                CopyBody(s_BestBody, s_WorkBody, count);
                AddBodyCells(s_WorkBody, count, cells);
            }

            return cells;
        }

        private static void AddFinalBodyCells(ArrowController arrow, List<Vector2Int> headSteps, HashSet<Vector2Int> reserved)
        {
            int count = arrow.segments.Count;
            EnsureWorkCapacity(count);
            CopyBody(arrow, s_WorkBody);

            if (headSteps != null)
            {
                for (int s = 0; s < headSteps.Count; s++)
                {
                    if (!TryBuildNextBodyLocal(s_WorkBody, count, headSteps[s], s_BestBody)) break;
                    CopyBody(s_BestBody, s_WorkBody, count);
                }
            }

            AddBodyCells(s_WorkBody, count, reserved);
        }

        private static bool TryBuildNextBodyLocal(Vector2Int[] src, int count, Vector2Int nextHead, Vector2Int[] dest)
        {
            if (GridManager.Instance.IsOutOfBounds(nextHead)) return false;

            if (count > 1)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    dest[i] = src[i + 1];
                }
            }
            dest[count - 1] = nextHead;

            for (int i = 0; i < count; i++)
            {
                for (int j = i + 1; j < count; j++)
                {
                    if (dest[i] == dest[j]) return false;
                }
            }

            return true;
        }

        private static void AddBodyCells(Vector2Int[] body, int count, HashSet<Vector2Int> cells)
        {
            for (int i = 0; i < count; i++)
            {
                cells.Add(body[i]);
            }
        }

        private static bool ConflictsWithGroup(ShuffleMovePlan plan, List<ShuffleMovePlan> group)
        {
            for (int i = 0; i < group.Count; i++)
            {
                if (PlansConflict(plan, group[i])) return true;
            }
            return false;
        }

        private static bool PlansConflict(ShuffleMovePlan a, ShuffleMovePlan b)
        {
            if (a == null || b == null || a.OccupiedCells == null || b.OccupiedCells == null) return true;
            foreach (Vector2Int cell in a.OccupiedCells)
            {
                if (b.OccupiedCells.Contains(cell)) return true;
            }
            return false;
        }

        private static void BeginPlan(ArrowController arrow, List<ArrowController> alsoExclude, HashSet<Vector2Int> extraBlocked)
        {
            ClearOccupancy(arrow);
            RefreshBlockedCells(arrow, alsoExclude);
            if (extraBlocked != null)
            {
                foreach (Vector2Int cell in extraBlocked)
                {
                    s_BlockedCells.Add(cell);
                }
            }
        }

        private static void EndPlan(ArrowController arrow)
        {
            RegisterOccupancy(arrow);
        }

        /// <summary>Picks a step; only the final pose must be free (checked after path built).</summary>
        private static bool TryPickStep(bool requireImprovement, ref int bestScore, int segmentCount, ref Vector2Int lastMoveDir)
        {
            int chosenDir = -1;
            Vector2Int head = s_WorkBody[segmentCount - 1];
            int pickScore = requireImprovement ? bestScore : int.MinValue;
            int pickTravel = HeadTravelDistance(s_WorkBody, segmentCount);
            Vector2Int pickDir = lastMoveDir;

            for (int d = 0; d < s_Directions.Length; d++)
            {
                Vector2Int dir = s_Directions[d];
                Vector2Int nextHead = head + dir;
                if (!TryBuildNextBody(s_WorkBody, segmentCount, nextHead, s_BestBody)) continue;
                if (HeadWouldCycle(nextHead)) continue;

                int score = ScoreBodyLayout(s_BestBody, segmentCount, dir);
                int travel = Mathf.Abs(nextHead.x - s_PlanStartHead.x) + Mathf.Abs(nextHead.y - s_PlanStartHead.y);
                bool better = score > pickScore || (score == pickScore && travel > pickTravel);
                if (!better) continue;

                pickScore = score;
                pickTravel = travel;
                chosenDir = d;
                pickDir = dir;
            }

            if (chosenDir < 0) return false;
            if (requireImprovement && pickScore <= bestScore) return false;

            bestScore = pickScore;
            lastMoveDir = pickDir;
            return true;
        }

        private static void ApplyStep(Vector2Int dir, int segmentCount)
        {
            Vector2Int head = s_WorkBody[segmentCount - 1];
            Vector2Int chosenHead = head + dir;
            TryBuildNextBody(s_WorkBody, segmentCount, chosenHead, s_BestBody);
            CopyBody(s_BestBody, s_WorkBody, segmentCount);
            s_HeadStepsBuffer.Add(chosenHead);
        }

        private static bool IsBodyLayoutFree(Vector2Int[] body, int count, Vector2Int lookDir)
        {
            if (lookDir == Vector2Int.zero || count == 0) return false;

            Vector2Int head = body[count - 1];
            Vector2Int check = head + lookDir;

            while (!GridManager.Instance.IsOutOfBounds(check))
            {
                if (s_BlockedCells.Contains(check) && !CellOnBody(check, body, count))
                {
                    return false;
                }
                check += lookDir;
            }

            return true;
        }

        private static bool CellOnBody(Vector2Int cell, Vector2Int[] body, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (body[i] == cell) return true;
            }
            return false;
        }

        private static bool HeadWouldCycle(Vector2Int nextHead)
        {
            for (int i = 0; i < s_HeadStepsBuffer.Count; i++)
            {
                if (s_HeadStepsBuffer[i] == nextHead) return true;
            }
            return false;
        }

        private static void EnsureWorkCapacity(int segmentCount)
        {
            if (s_WorkBody != null && s_WorkCapacity >= segmentCount) return;
            s_WorkCapacity = Mathf.Max(segmentCount, 16);
            s_WorkBody = new Vector2Int[s_WorkCapacity];
            s_BestBody = new Vector2Int[s_WorkCapacity];
        }

        private static void CopyBody(ArrowController arrow, Vector2Int[] dest)
        {
            for (int i = 0; i < arrow.segments.Count; i++)
            {
                dest[i] = arrow.segments[i].GridPosition;
            }
        }

        private static void CopyBody(Vector2Int[] src, Vector2Int[] dest, int count)
        {
            for (int i = 0; i < count; i++)
            {
                dest[i] = src[i];
            }
        }

        private static bool TryBuildNextBody(Vector2Int[] src, int count, Vector2Int nextHead, Vector2Int[] dest)
        {
            var grid = GridManager.Instance;
            if (grid.IsOutOfBounds(nextHead)) return false;

            if (count > 1)
            {
                for (int i = 0; i < count - 1; i++)
                {
                    dest[i] = src[i + 1];
                }
            }
            dest[count - 1] = nextHead;

            for (int i = 0; i < count; i++)
            {
                Vector2Int cell = dest[i];
                if (grid.IsOutOfBounds(cell)) return false;

                for (int j = i + 1; j < count; j++)
                {
                    if (dest[j] == cell) return false;
                }

                if (s_BlockedCells.Contains(cell)) return false;
            }

            return true;
        }

        private static int ScoreBodyLayout(Vector2Int[] body, int count, Vector2Int lookDir)
        {
            Vector2Int head = body[count - 1];
            var grid = GridManager.Instance;
            Vector2Int gridSize = grid.GridSize;

            int travel = Mathf.Abs(head.x - s_PlanStartHead.x) + Mathf.Abs(head.y - s_PlanStartHead.y);

            int escapeScore = 0;
            if (lookDir != Vector2Int.zero)
            {
                Vector2Int check = head + lookDir;
                while (!grid.IsOutOfBounds(check))
                {
                    if (s_BlockedCells.Contains(check) && !CellOnBody(check, body, count)) break;
                    escapeScore++;
                    check += lookDir;
                }
            }

            int edgeDist = Mathf.Min(
                Mathf.Min(head.x, gridSize.x - 1 - head.x),
                Mathf.Min(head.y, gridSize.y - 1 - head.y));

            // Prioritize longer source→goal head travel; keep a small escape/edge tie-break.
            return travel * TravelScorePerCell + escapeScore * 8 + edgeDist;
        }

        private static void RefreshBlockedCells(ArrowController exclude, List<ArrowController> alsoExclude)
        {
            s_BlockedCells.Clear();
            foreach (ArrowController other in GridManager.Instance.GetAllArrows())
            {
                if (other == null || other == exclude || other.IsMoving) continue;
                if (alsoExclude != null && alsoExclude.Contains(other)) continue;

                IList<Segment> segs = other.segments;
                for (int i = 0; i < segs.Count; i++)
                {
                    if (segs[i] != null)
                    {
                        s_BlockedCells.Add(segs[i].GridPosition);
                    }
                }
            }
        }

        private static void ClearOccupancy(ArrowController arrow)
        {
            foreach (var seg in arrow.segments)
            {
                if (seg == null) continue;
                GridManager.Instance.ReleaseOccupancy(seg.GridPosition);
            }
        }

        private static void RegisterOccupancy(ArrowController arrow)
        {
            foreach (var seg in arrow.segments)
            {
                if (seg == null) continue;
                GridManager.Instance.RegisterOccupancy(seg.GridPosition, arrow);
            }
        }
    }
}
