using Assets.Scripts.Core;
using UnityEngine;

public class LikeBehaviour : MonoBehaviour
{
    private const int LikeSortingOrder = 15;
    private static readonly Vector3 DefaultLocalScale = new Vector3(0.1f, 0.1f, 1f);

    private void OnEnable()
    {
        transform.localScale = DefaultLocalScale;

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = LikeSortingOrder;
        }

        Animator animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.Rebind();
            animator.Update(0f);
            animator.SetTrigger("StartAnim");
        }
    }

    public void DESTROYME()
    {
        GameObject root = transform.root.gameObject;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReturnEffect(root);
            return;
        }

        Destroy(root);
    }
}
