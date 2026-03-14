using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OneTimeView : MonoBehaviour
{
    

    // Update is called once per frame
    void OnDisable()
    {
        Destroy(gameObject);
    }
}
