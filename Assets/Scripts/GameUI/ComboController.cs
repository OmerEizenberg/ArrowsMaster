using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ComboController : MonoBehaviour
{
    [SerializeField]
    TextMeshProUGUI m_comboNum;

    private int m_upComingNum=1;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    public void UpdateComboNumber()
    {
        m_comboNum.text = ""+m_upComingNum;
    }
    public void UpdateUpComingComboNumber(int i_num)
    {
        m_upComingNum = i_num;
    }
    public void DestroyME()
    {
        Destroy(gameObject);
    }
}
