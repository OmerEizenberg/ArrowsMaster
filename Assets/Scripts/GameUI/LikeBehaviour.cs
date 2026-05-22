using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LikeBehaviour : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        var animator = GetComponent<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("StartAnim");
        }
    }
    public void DESTROYME()
    {
        Destroy(gameObject);
    }
}
