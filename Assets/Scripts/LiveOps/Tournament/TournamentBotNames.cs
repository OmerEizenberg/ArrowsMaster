using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.LiveOps.Tournament
{
    /// <summary>
    /// 500 display names: ~30% real first names, ~70% puzzle-game style handles.
    /// </summary>
    public static class TournamentBotNames
    {
        private static readonly string[] RealFirstNames =
        {
            "Robert","Omer","Leon","Emma","Noah","Olivia","Liam","Ava","Mason","Sophia",
            "Ethan","Isabella","Logan","Mia","Lucas","Charlotte","James","Amelia","Benjamin","Harper",
            "Henry","Evelyn","Alexander","Abigail","Michael","Emily","Daniel","Elizabeth","Jacob","Sofia",
            "Jackson","Avery","Sebastian","Ella","Jack","Scarlett","Aiden","Grace","Owen","Chloe",
            "Samuel","Victoria","Matthew","Riley","Joseph","Aria","Levi","Lily","Mateo","Aurora",
            "David","Zoey","John","Nora","Wyatt","Camila","Carter","Hannah","Julian","Addison",
            "Luke","Eleanor","Grayson","Natalie","Isaac","Luna","Jayden","Savannah","Gabriel","Brooklyn",
            "Anthony","Leah","Dylan","Zoe","Leo","Stella","Lincoln","Hazel","Jaxon","Ellie",
            "Asher","Paisley","Christopher","Audrey","Josiah","Skylar","Andrew","Violet","Thomas","Claire",
            "Joshua","Bella","Ezra","Lucy","Charles","Anna","Caleb","Caroline","Ryan","Genesis",
            "Nathan","Kennedy","Adrian","Kinsley","Nolan","Allison","Cameron","Maya","Aaron","Willow",
            "Eli","Naomi","Connor","Elena","Nicholas","Sarah","Jeremiah","Ariana","Colton","Allison",
            "Jordan","Alice","Ian","Julia","Adam","Madelyn","Jason","Ruby","Justin","Eva",
            "Kevin","Serenity","Brian","Autumn","Eric","Quinn","Tyler","Piper","Kyle","Sophie",
            "Patrick","Ivy","Sean","Clara","Marcus","Lydia","Victor","Jade","Oscar","Rose",
            "Felix","Iris","Hugo","Sadie","Arthur","Peyton","George","Rylee","Louis","Melanie"
        };

        private static readonly string[] TagRoots =
        {
            "Arrow","Puzzle","Slide","Swipe","Path","Grid","Dash","Bolt","Quest","Legend",
            "Master","Sharp","Swift","Pixel","Nova","Blitz","Echo","Frost","Spark","Orbit",
            "Turbo","Ninja","Tiger","Wolf","Falcon","Cobra","Shadow","Crystal","Rocket","Comet",
            "Lambda","Vertex","Prism","Cipher","Vector","Matrix","Logic","Brain","Mind","Focus",
            "Lucky","LuckyShot","Combo","Streak","Perfect","Ace","Pro","Elite","Champ","King",
            "Queen","Duke","Baron","Captain","Pilot","Ranger","Hunter","Seeker","Finder","Solver",
            "Toby","Maverick","Zen","Karma","Lotus","Bamboo","Coral","Maple","Cedar","Amber",
            "Neon","Aqua","Solar","Lunar","Storm","Thunder","Flame","Ember","Glitch","Byte"
        };

        private static readonly string[] TagSuffixes =
        {
            "007","99","42","21","77","88","123","x","XD","Pro",
            "GG","HQ","TV","HD","FX","Max","One","Two","Prime","Plus",
            "2024","2025","2026","_YT","_TT","Play","Game","Win","Rush","Lab"
        };

        private static List<string> cachedPool;

        public static IReadOnlyList<string> GetPool()
        {
            if (cachedPool != null)
                return cachedPool;

            cachedPool = new List<string>(500);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rng = new Random(7331);

            int realTarget = 150; // 30% of 500
            for (int i = 0; i < realTarget; i++)
            {
                string name = RealFirstNames[i % RealFirstNames.Length];
                if (i >= RealFirstNames.Length || !used.Add(name))
                {
                    name = $"{RealFirstNames[rng.Next(RealFirstNames.Length)]}{rng.Next(10, 99)}";
                    int guard = 0;
                    while (!used.Add(name) && guard++ < 20)
                        name = $"{RealFirstNames[rng.Next(RealFirstNames.Length)]}{rng.Next(10, 999)}";
                }
                cachedPool.Add(name);
            }

            while (cachedPool.Count < 500)
            {
                string tag = BuildGamertag(rng);
                if (used.Add(tag))
                    cachedPool.Add(tag);
            }

            return cachedPool;
        }

        public static List<string> PickUnique(int count, int seed)
        {
            var pool = GetPool();
            var rng = new Random(seed);
            var indices = new List<int>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
                indices.Add(i);

            // Fisher-Yates partial shuffle
            for (int i = 0; i < count; i++)
            {
                int j = rng.Next(i, indices.Count);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            var result = new List<string>(count);
            for (int i = 0; i < count; i++)
                result.Add(pool[indices[i]]);
            return result;
        }

        private static string BuildGamertag(Random rng)
        {
            var sb = new StringBuilder();
            string root = TagRoots[rng.Next(TagRoots.Length)];
            int style = rng.Next(5);
            switch (style)
            {
                case 0:
                    sb.Append(root).Append(TagSuffixes[rng.Next(TagSuffixes.Length)]);
                    break;
                case 1:
                    sb.Append("xX").Append(root).Append("Xx");
                    break;
                case 2:
                    sb.Append(root).Append(rng.Next(10, 9999));
                    break;
                case 3:
                    sb.Append(TagRoots[rng.Next(TagRoots.Length)])
                        .Append(TagRoots[rng.Next(TagRoots.Length)]);
                    break;
                default:
                    sb.Append(root).Append('_').Append(rng.Next(1, 999));
                    break;
            }
            return sb.ToString();
        }
    }
}
