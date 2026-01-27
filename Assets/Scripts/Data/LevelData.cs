using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Data
{
    [Serializable]
    public class LevelData
    {
        public Vector2IntData gridSize;
        public List<ArrowData> arrows;
        public int duration; // Optional: duration in seconds for time-based levels (0 = no time limit)
    }

    [Serializable]
    public class ArrowData
    {
        public int id;
        public string color;
        public List<Vector2IntData> path;
    }

    [Serializable]
    public class Vector2IntData
    {
        public int x;
        public int y;

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }
    }
}
