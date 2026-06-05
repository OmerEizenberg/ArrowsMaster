using Assets.Scripts.Core;
using UnityEngine;

public class ClickManager : MonoBehaviour
{
    public void DestroyMe()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnEffect(gameObject);
            return;
        }

        Destroy(gameObject);
    }
}
