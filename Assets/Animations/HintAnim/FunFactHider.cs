using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FunFactHider : MonoBehaviour
{
    public void HideMe()
    {
        gameObject.SetActive(false);
    }
    

    // Update is called once per frame
    void Update()
    {
        if(Input.anyKeyDown)
        {
            HideMe();
        }
    }
}
