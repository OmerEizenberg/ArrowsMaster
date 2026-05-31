using UnityEngine;

namespace LiftEngine
{
    [DefaultExecutionOrder(-500)]
    internal sealed class LiftEngineHost : MonoBehaviour
    {
        private static LiftEngineHost _instance;

        public static LiftEngineHost Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var go = new GameObject("LiftEngineHost");
                _instance = go.AddComponent<LiftEngineHost>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public LiftEngineController Controller { get; private set; }

        public void AttachController(LiftEngineController controller)
        {
            Controller = controller;
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }
    }
}
