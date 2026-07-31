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

    public static class TournamentBotSimulator
    {
        private const int BotCount = 24;

        public static List<TournamentBotData> CreateBotsOnJoin(
            TournamentConfigSO config,
            DateTime tournamentStartUtc,
            DateTime tournamentEndUtc,
            DateTime joinUtc,
            string uniqueId)
        {
            int seed = unchecked(uniqueId.GetHashCode() ^ joinUtc.Ticks.GetHashCode());
            var rng = new System.Random(seed);
            float lookbackMinutes = config != null ? Mathf.Max(5f, config.LateJoinLookbackMinutes) : 30f;
            int minRewardScore = config != null ? Mathf.Max(1, config.MinArrowsForRewardedPlaces) : 71;
            int rewardedPlaces = config != null ? Mathf.Max(1, config.CountRewardedPlaces()) : 5;

            var names = TournamentBotNames.PickUnique(BotCount, seed ^ 9176);
            var bots = new List<TournamentBotData>(BotCount);

            for (int i = 0; i < BotCount; i++)
            {
                var archetype = (BotArchetype)(i % 8);
                double joinOffsetMin = rng.NextDouble() * lookbackMinutes;
                DateTime botJoin = joinUtc.AddMinutes(-joinOffsetMin);
                if (botJoin < tournamentStartUtc)
                    botJoin = tournamentStartUtc;

                var bot = new TournamentBotData
                {
                    Name = names[i],
                    Archetype = (int)archetype,
                    Seed = rng.Next(),
                    JoinUtcTicks = botJoin.Ticks
                };

                BuildSchedule(bot, archetype, config, tournamentStartUtc, tournamentEndUtc, botJoin, joinUtc, rng);
                bots.Add(bot);
            }

            EnsureRewardedFloor(bots, joinUtc, rewardedPlaces, minRewardScore, rng);
            return bots;
        }

        public static int GetBotScoreAt(TournamentBotData bot, DateTime utcNow)
        {
            if (bot?.Events == null) return 0;
            long ticks = utcNow.Ticks;
            int sum = 0;
            for (int i = 0; i < bot.Events.Count; i++)
            {
                if (bot.Events[i].UtcTicks <= ticks)
                    sum += bot.Events[i].Amount;
            }
            return sum;
        }

        private static void EnsureRewardedFloor(
            List<TournamentBotData> bots,
            DateTime joinUtc,
            int rewardedPlaces,
            int minScore,
            System.Random rng)
        {
            var indices = new List<int>(bots.Count);
            for (int i = 0; i < bots.Count; i++)
                indices.Add(i);
            for (int i = 0; i < rewardedPlaces && i < indices.Count; i++)
            {
                int j = rng.Next(i, indices.Count);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            for (int i = 0; i < rewardedPlaces && i < bots.Count; i++)
            {
                var bot = bots[indices[i]];
                int current = GetBotScoreAt(bot, joinUtc);
                if (current >= minScore)
                    continue;

                int needed = minScore + rng.Next(0, 25) - current;
                bot.Events.Add(new TournamentScoreEvent
                {
                    UtcTicks = joinUtc.AddMinutes(-rng.Next(1, 12)).Ticks,
                    Amount = Mathf.Max(1, needed)
                });
                bot.Events.Sort((a, b) => a.UtcTicks.CompareTo(b.UtcTicks));
            }
        }

        private static void BuildSchedule(
            TournamentBotData bot,
            BotArchetype archetype,
            TournamentConfigSO config,
            DateTime tournamentStart,
            DateTime tournamentEnd,
            DateTime botJoin,
            DateTime playerJoin,
            System.Random rng)
        {
            AddPastBatches(bot, archetype, botJoin, playerJoin, rng);
            AddFutureBatches(bot, archetype, config, playerJoin, tournamentEnd, rng);
            bot.Events.Sort((a, b) => a.UtcTicks.CompareTo(b.UtcTicks));
        }

        private static void AddPastBatches(
            TournamentBotData bot,
            BotArchetype archetype,
            DateTime botJoin,
            DateTime playerJoin,
            System.Random rng)
        {
            double minutes = Math.Max(1, (playerJoin - botJoin).TotalMinutes);
            int bursts = archetype == BotArchetype.Ghost ? rng.Next(0, 2) : rng.Next(1, 4);
            for (int i = 0; i < bursts; i++)
            {
                double t = rng.NextDouble() * minutes;
                int amount = PastAmount(archetype, rng);
                if (amount <= 0) continue;
                bot.Events.Add(new TournamentScoreEvent
                {
                    UtcTicks = botJoin.AddMinutes(t).Ticks,
                    Amount = amount
                });
            }
        }

        private static void AddFutureBatches(
            TournamentBotData bot,
            BotArchetype archetype,
            TournamentConfigSO config,
            DateTime from,
            DateTime end,
            System.Random rng)
        {
            double totalHours = Math.Max(0.25, (end - from).TotalHours);
            BotArchetypeGainRule rule = config != null
                ? config.GetBotGainRule(archetype)
                : TournamentConfigSO.CreateDefaultRule(archetype);

            switch (archetype)
            {
                case BotArchetype.SteadyGrinder:
                case BotArchetype.Casual:
                case BotArchetype.Ghost:
                    ScheduleUniform(bot, from, end,
                        rule.IntervalHours + rng.NextDouble(),
                        rule.AmountMin, rule.AmountMax, rng);
                    break;

                case BotArchetype.SleeperBurst:
                case BotArchetype.FrontRunner:
                case BotArchetype.ComebackKid:
                {
                    float split = Mathf.Clamp01(rule.PhaseSplit);
                    DateTime mid = from.AddHours(totalHours * split);
                    ScheduleUniform(bot, from, mid, rule.IntervalHours, rule.AmountMin, rule.AmountMax, rng);
                    ScheduleUniform(bot, mid, end, rule.Phase2IntervalHours, rule.Phase2AmountMin, rule.Phase2AmountMax, rng);
                    break;
                }

                case BotArchetype.DaySkipper:
                {
                    double skip = Math.Min(24, totalHours * Mathf.Clamp01(rule.PhaseSplit > 0 ? rule.PhaseSplit : 0.4f));
                    DateTime resume = from.AddHours(skip);
                    if (resume < end)
                        ScheduleUniform(bot, resume, end, rule.IntervalHours, rule.AmountMin, rule.AmountMax, rng);
                    break;
                }

                case BotArchetype.Spiky:
                {
                    int spikes = 3 + rng.Next(5);
                    for (int i = 0; i < spikes; i++)
                    {
                        double h = rng.NextDouble() * totalHours;
                        int amount = rule.AmountMax <= rule.AmountMin
                            ? rule.AmountMin
                            : rng.Next(rule.AmountMin, rule.AmountMax + 1);
                        if (amount <= 0) continue;
                        bot.Events.Add(new TournamentScoreEvent
                        {
                            UtcTicks = from.AddHours(h).Ticks,
                            Amount = amount
                        });
                    }
                    break;
                }
            }
        }

        private static void ScheduleUniform(
            TournamentBotData bot,
            DateTime from,
            DateTime end,
            double intervalHours,
            int amountMin,
            int amountMax,
            System.Random rng)
        {
            if (end <= from || intervalHours <= 0.05)
                return;

            DateTime t = from.AddHours(rng.NextDouble() * Math.Min(intervalHours, 2));
            while (t < end)
            {
                int amount = amountMax <= amountMin ? amountMin : rng.Next(amountMin, amountMax + 1);
                if (amount > 0)
                {
                    bot.Events.Add(new TournamentScoreEvent
                    {
                        UtcTicks = t.Ticks,
                        Amount = amount
                    });
                }
                t = t.AddHours(intervalHours * (0.75 + rng.NextDouble() * 0.5));
            }
        }

        private static int PastAmount(BotArchetype archetype, System.Random rng)
        {
            switch (archetype)
            {
                case BotArchetype.Ghost: return rng.Next(0, 4);
                case BotArchetype.Casual: return rng.Next(2, 12);
                case BotArchetype.FrontRunner: return rng.Next(10, 28);
                default: return rng.Next(3, 18);
            }
        }
    }
}
