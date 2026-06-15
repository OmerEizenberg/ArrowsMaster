using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.GAE
{
    /// <summary>
    /// Tracks spawned fly icons and destroys them after a timeout, even if the driving coroutine stops.
    /// </summary>
    public class GAEFlyEffectRunner : MonoBehaviour
    {
        private readonly List<GameObject> m_TrackedObjects = new List<GameObject>();
        private float m_DestroyAt;
        private bool m_IsCleaningUp;

        public static GAEFlyEffectRunner Create(Transform parent, float lifetimeSeconds)
        {
            GameObject host = new GameObject("GAEFlyEffectRunner");
            if (parent != null)
            {
                host.transform.SetParent(parent, false);
            }

            GAEFlyEffectRunner runner = host.AddComponent<GAEFlyEffectRunner>();
            runner.m_DestroyAt = Time.unscaledTime + Mathf.Max(0.5f, lifetimeSeconds);
            return runner;
        }

        public void Track(GameObject obj)
        {
            if (obj == null || m_IsCleaningUp)
            {
                return;
            }

            m_TrackedObjects.Add(obj);
        }

        private void Update()
        {
            if (!m_IsCleaningUp && Time.unscaledTime >= m_DestroyAt)
            {
                Cleanup();
            }
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            if (m_IsCleaningUp)
            {
                return;
            }

            m_IsCleaningUp = true;
            for (int i = 0; i < m_TrackedObjects.Count; i++)
            {
                if (m_TrackedObjects[i] != null)
                {
                    Destroy(m_TrackedObjects[i]);
                }
            }

            m_TrackedObjects.Clear();
            Destroy(gameObject);
        }
    }
}
