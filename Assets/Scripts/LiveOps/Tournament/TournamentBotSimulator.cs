using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.LiveOps.Tournament
{
    public enum BotArchetype
    {
        SteadyGrinder = 0,
        Casual = 1,
        SleeperBurst = 2,
        FrontRunner = 3,
        ComebackKid = 4,
        DaySkipper = 5,
        Ghost = 6,
        Spiky = 7
    }

    /// <summary>
    /// Builds bot score schedules calibrated to real play:
    /// ~150 golden arrows per level, avg ~5 levels/day, top players ~10-12 levels/day.
    /// Final #1 target ≈ AvgArrowsPerLevel × TopLevelsPerDay × remainingDaysAtJoin
    /// (e.g. 150 × 10 × 3.5 = 5250 when joining at tournament start).
    /// Each tournament also gets intensity / gap / personality variance so rounds feel different.
    /// </summary>
    public static class TournamentBotSimulator
    {
        private const int BotCount = 24;
        private const int TopCompetitiveCount = 5;

        public static List<TournamentBotData> CreateBotsOnJoin(
            TournamentConfigSO config,
            DateTime tournamentStartUtc,
            DateTime tournamentEndUtc,
            DateTime joinUtc,
            string uniqueId)
        {
            int seed = unchecked(uniqueId.GetHashCode() ^ joinUtc.Ticks.GetHashCode());
            var rng = new System.Random(seed);

            float arrowsPerLevel = config != null ? Mathf.Max(1f, config.AvgGoldenArrowsPerLevel) : 150f;
            float topMin = config != null ? Mathf.Max(1f, config.TopLevelsPerDayMin) : 10f;
            float topMax = config != null ? Mathf.Max(topMin, config.TopLevelsPerDayMax) : 12f;
            float avgLevels = config != null ? Mathf.Max(0.5f, config.AvgLevelsPerDay) : 5f;

            float intensityVar = config != null ? Mathf.Clamp01(config.TournamentIntensityVariance) : 0.12f;
            float botTargetVar = config != null ? Mathf.Clamp01(config.BotTargetVariance) : 0.10f;
            float minGapPct = config != null ? Mathf.Clamp(config.MinPaceGapPercent, 0f, 0.2f) : 0.04f;
            float arrowsVar = config != null ? Mathf.Clamp01(config.ArrowsPerLevelVariance) : 0.15f;

            // Per-tournament mood: hotter/cooler field so consecutive rounds don't feel identical.
            float tournamentIntensity = 1f + SignedUnit(rng) * intensityVar;

            // Competitive window = time left when the player joins (not full tournament length if late).
            DateTime scheduleStart = joinUtc < tournamentStartUtc ? tournamentStartUtc : joinUtc;
            if (scheduleStart >= tournamentEndUtc)
                scheduleStart = tournamentEndUtc.AddMinutes(-1);

            double remainingDays = Math.Max(1d / 24d, (tournamentEndUtc - scheduleStart).TotalDays);

            float[] paces = BuildDailyLevelPaces(BotCount, topMin, topMax, avgLevels, tournamentIntensity, rng);
            EnforceMinimumGaps(paces, minGapPct);
            Shuffle(paces, rng);

            // Shuffle archetypes separately so behavior mix changes every tournament.
            var archetypes = BuildShuffledArchetypes(BotCount, rng);

            var names = TournamentBotNames.PickUnique(BotCount, seed ^ 9176);
            var bots = new List<TournamentBotData>(BotCount);

            for (int i = 0; i < BotCount; i++)
            {
                var archetype = archetypes[i];
                float pace = paces[i];

                // Per-bot arrows efficiency + target jitter (independent of pace band).
                float botArrows = arrowsPerLevel * (1f + SignedUnit(rng) * arrowsVar);
                botArrows = Mathf.Max(1f, botArrows);

                float targetMul = 1f + SignedUnit(rng) * botTargetVar;
                int targetEnd = Mathf.Max(0, Mathf.RoundToInt(pace * botArrows * (float)remainingDays * targetMul));

                var bot = new TournamentBotData
                {
                    Name = names[i],
                    Archetype = (int)archetype,
                    Seed = rng.Next(),
                    JoinUtcTicks = scheduleStart.Ticks
                };

                float timingSkew = SignedUnit(rng) * 0.2f; // ±20% timing aggression
                float batchSizeSkew = SignedUnit(rng) * 0.25f; // ±25% batch size spread
                BuildScheduleToTarget(
                    bot,
                    archetype,
                    scheduleStart,
                    tournamentEndUtc,
                    targetEnd,
                    botArrows,
                    timingSkew,
                    batchSizeSkew,
                    rng);
                bots.Add(bot);
            }

            return bots;
        }

        public static int GetBotScoreAt(TournamentBotData bot, DateTime utcNow)
        {
            if (bot?.Events == null || bot.Events.Count == 0)
                return 0;

            long ticks = utcNow.Ticks;
            var events = bot.Events;

            // Time usually only moves forward — continue from the last cursor.
            int startIndex = 0;
            int sum = 0;
            if (ticks < bot.ScoreCacheTicks)
            {
                // Rare (debug rewind): recompute from scratch.
                startIndex = 0;
                sum = 0;
            }
            else if (bot.ScoreCacheIndex >= 0 && bot.ScoreCacheIndex <= events.Count)
            {
                startIndex = bot.ScoreCacheIndex;
                sum = bot.ScoreCacheValue;
            }

            int i = startIndex;
            while (i < events.Count && events[i].UtcTicks <= ticks)
            {
                sum += events[i].Amount;
                i++;
            }

            bot.ScoreCacheTicks = ticks;
            bot.ScoreCacheValue = sum;
            bot.ScoreCacheIndex = i;
            return sum;
        }

        /// <summary>
        /// Index 0..4 ≈ top-player band (10-12 levels/day). Mid pack ≈ avg. Tail is low.
        /// Array is shuffled by caller so which named bot is #1 varies per tournament.
        /// </summary>
        private static float[] BuildDailyLevelPaces(
            int count,
            float topMin,
            float topMax,
            float avgLevels,
            float tournamentIntensity,
            System.Random rng)
        {
            var paces = new float[count];
            float topSpan = Mathf.Max(0.01f, topMax - topMin);

            // Top 5: all in / near the top-player band (10-12 levels/day).
            // #1 design target ≈ TopLevelsPerDayMin (e.g. 150 × 10 × remainingDays).
            paces[0] = topMin + topSpan * (0.35f + (float)rng.NextDouble() * 0.65f); // ~10.7-12
            paces[1] = topMin + topSpan * (0.20f + (float)rng.NextDouble() * 0.45f); // ~10.4-11.3
            paces[2] = topMin + topSpan * (0.05f + (float)rng.NextDouble() * 0.35f); // ~10.1-10.7
            paces[3] = topMin + topSpan * (0.00f + (float)rng.NextDouble() * 0.25f); // ~10.0-10.5
            paces[4] = topMin * (0.90f + (float)rng.NextDouble() * 0.12f);           // ~9.0-10.2

            // Mid pack (~avg 5 levels/day).
            for (int i = TopCompetitiveCount; i < 15 && i < count; i++)
            {
                float variance = 0.55f + (float)rng.NextDouble() * 0.90f; // 0.55x-1.45x avg
                paces[i] = Mathf.Max(1.5f, avgLevels * variance);
            }

            // Lower pack / casuals / ghosts.
            for (int i = 15; i < count; i++)
            {
                paces[i] = 0.3f + (float)rng.NextDouble() * (avgLevels * 0.55f); // ~0.3-2.75
            }

            // Apply tournament intensity, then keep top band roughly on design targets.
            for (int i = 0; i < count; i++)
                paces[i] *= tournamentIntensity;

            return paces;
        }

        /// <summary>
        /// After sorting high→low, push neighbors apart by at least <paramref name="minGapPercent"/>
        /// so places don't clump into near-ties every round.
        /// </summary>
        private static void EnforceMinimumGaps(float[] paces, float minGapPercent)
        {
            if (paces == null || paces.Length < 2 || minGapPercent <= 0f)
                return;

            Array.Sort(paces, (a, b) => b.CompareTo(a));

            for (int i = 1; i < paces.Length; i++)
            {
                float maxAllowed = paces[i - 1] * (1f - minGapPercent);
                if (paces[i] > maxAllowed)
                    paces[i] = Mathf.Max(0.05f, maxAllowed);
            }
        }

        private static BotArchetype[] BuildShuffledArchetypes(int count, System.Random rng)
        {
            var archetypes = new BotArchetype[count];
            var values = (BotArchetype[])Enum.GetValues(typeof(BotArchetype));
            for (int i = 0; i < count; i++)
                archetypes[i] = values[i % values.Length];

            for (int i = archetypes.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (archetypes[i], archetypes[j]) = (archetypes[j], archetypes[i]);
            }

            return archetypes;
        }

        private static void BuildScheduleToTarget(
            TournamentBotData bot,
            BotArchetype archetype,
            DateTime from,
            DateTime end,
            int targetScore,
            float arrowsPerLevel,
            float timingSkew,
            float batchSizeSkew,
            System.Random rng)
        {
            bot.Events = new List<TournamentScoreEvent>();
            if (targetScore <= 0 || end <= from)
                return;

            int perLevel = Mathf.Max(1, Mathf.RoundToInt(arrowsPerLevel));
            int batchCount = Mathf.Max(1, Mathf.RoundToInt(targetScore / (float)perLevel));

            // Session density varies: some bots fewer bigger sessions, others many small ones.
            float density = 1f + SignedUnit(rng) * 0.35f;
            batchCount = Mathf.RoundToInt(batchCount * density);
            batchCount = Mathf.Clamp(batchCount, 1, 180);

            int[] amounts = SplitScoreIntoBatches(targetScore, batchCount, perLevel, batchSizeSkew, rng);
            double[] fracs = SampleTimeFractions(batchCount, archetype, timingSkew, rng);
            Array.Sort(fracs);

            double totalHours = Math.Max(0.1, (end - from).TotalHours);
            for (int i = 0; i < batchCount; i++)
            {
                if (amounts[i] <= 0)
                    continue;

                // Keep events inside (from, end), slightly off the endpoints.
                double frac = Mathf.Clamp01((float)fracs[i]);
                double hours = frac * totalHours;
                hours = Math.Max(0.02, Math.Min(totalHours - 0.02, hours));

                bot.Events.Add(new TournamentScoreEvent
                {
                    UtcTicks = from.AddHours(hours).Ticks,
                    Amount = amounts[i]
                });
            }

            bot.Events.Sort((a, b) => a.UtcTicks.CompareTo(b.UtcTicks));
        }

        private static int[] SplitScoreIntoBatches(
            int targetScore,
            int batchCount,
            int perLevel,
            float batchSizeSkew,
            System.Random rng)
        {
            var amounts = new int[batchCount];
            int remaining = targetScore;
            float spread = Mathf.Clamp(0.2f + Mathf.Abs(batchSizeSkew), 0.1f, 0.45f);

            for (int i = 0; i < batchCount; i++)
            {
                int batchesLeft = batchCount - i;
                if (batchesLeft == 1)
                {
                    amounts[i] = Mathf.Max(0, remaining);
                    break;
                }

                int baseAmt = Mathf.Max(1, perLevel);
                int minAmt = Mathf.Max(1, Mathf.RoundToInt(baseAmt * (1f - spread)));
                int maxAmt = Mathf.Max(minAmt, Mathf.RoundToInt(baseAmt * (1f + spread)));
                int ideal = remaining / batchesLeft;
                int amount = Mathf.Clamp(ideal + rng.Next(-perLevel / 4, perLevel / 4 + 1), minAmt, maxAmt);
                amount = Mathf.Min(amount, remaining - (batchesLeft - 1));
                amount = Mathf.Max(1, amount);
                amounts[i] = amount;
                remaining -= amount;
            }

            return amounts;
        }

        private static double[] SampleTimeFractions(
            int count,
            BotArchetype archetype,
            float timingSkew,
            System.Random rng)
        {
            var fracs = new double[count];
            // Skew softens/hardens archetype bias per bot (−: steadier, +: more extreme).
            float bias = Mathf.Clamp(1.7f + timingSkew, 1.15f, 2.4f);
            float skipStart = Mathf.Clamp(0.35f + timingSkew * 0.15f, 0.15f, 0.55f);

            for (int i = 0; i < count; i++)
            {
                double u = rng.NextDouble();
                switch (archetype)
                {
                    case BotArchetype.FrontRunner:
                        fracs[i] = Math.Pow(u, bias);
                        break;
                    case BotArchetype.ComebackKid:
                        fracs[i] = 1.0 - Math.Pow(1.0 - u, bias);
                        break;
                    case BotArchetype.SleeperBurst:
                    {
                        float quietShare = Mathf.Clamp(0.25f + timingSkew * 0.1f, 0.1f, 0.4f);
                        fracs[i] = u < quietShare
                            ? u * 0.45
                            : 0.45 + (u - quietShare) / (1.0 - quietShare) * 0.55;
                        break;
                    }
                    case BotArchetype.DaySkipper:
                        fracs[i] = skipStart + u * (1.0 - skipStart);
                        break;
                    case BotArchetype.Spiky:
                    {
                        int clusters = 3 + rng.Next(0, 3); // 3-5 burst windows, varies per bot/round
                        int cluster = rng.Next(0, clusters);
                        fracs[i] = (cluster + rng.NextDouble()) / clusters;
                        break;
                    }
                    case BotArchetype.Ghost:
                    case BotArchetype.Casual:
                        fracs[i] = Math.Pow(u, 0.85 + timingSkew * 0.2);
                        break;
                    default:
                        // Steady grinders: mild drift so they aren't perfectly even.
                        fracs[i] = Mathf.Clamp01((float)(u + SignedUnit(rng) * 0.08f * (1f + Mathf.Abs(timingSkew))));
                        break;
                }
            }

            return fracs;
        }

        private static float SignedUnit(System.Random rng)
        {
            return (float)(rng.NextDouble() * 2.0 - 1.0);
        }

        private static void Shuffle(float[] values, System.Random rng)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}
