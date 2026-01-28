using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LikeBehaviour : MonoBehaviour
{
    // Start is called before the first frame update
    void OnEnable()
    {
        gameObject.GetComponent<Animator>().SetTrigger("StartAnim");
    }
    public void DESTROYME()
    {
        Destroy(gameObject);
    }
}
