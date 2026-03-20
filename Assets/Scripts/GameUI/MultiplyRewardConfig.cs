using UnityEngine;
using System;

namespace Assets.Scripts.GameUI
{
    [Serializable]
    public class MultiplierZone
    {
        public float minX; // Normalized 0-1
        public float maxX; // Normalized 0-1
        public int multiplier;
        public Color color;
    }

    [CreateAssetMenu(fileName = "MultiplyRewardConfig", menuName = "Game/MultiplyRewardConfig")]
    public class MultiplyRewardConfig : ScriptableObject
    {
        public MultiplierZone[] zones;
        public float pointerSpeed = 2f;
        public float curveAmplitude = 50f;
    }
}
