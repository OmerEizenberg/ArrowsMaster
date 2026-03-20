using UnityEngine;
using System;

namespace Assets.Scripts.GameUI
{
    [Serializable]
    public class MultiplierZone
    {
        public int multiplier;
        public float weight = 1.0f;
    }

    [CreateAssetMenu(fileName = "MultiplyRewardConfig", menuName = "Game/MultiplyRewardConfig")]
    public class MultiplyRewardConfig : ScriptableObject
    {
        public MultiplierZone[] zones;
        public float pointerSpeed = 2f;
        public float curveAmplitude = 50f;
    }
}
