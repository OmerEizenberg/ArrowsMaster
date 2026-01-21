using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEditor.UI;


public class MonthlyChallengeController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Assign all 35 day images here (Row 1 Col 1 to Row 5 Col 7)")]
    [SerializeField] private Image[] dayImages;
    [SerializeField] private Color m_NormalColor;
    [SerializeField] private Color m_PassedColor;
    [SerializeField] private Color m_TodayColor;

    [SerializeField] private Color m_NormalColorTxt;
    [SerializeField] private Color m_PassedColorTxt;
    [SerializeField] private Color m_TodayColorTxt;
    
    [Tooltip("Assign all 35 day text components here")]
    [SerializeField] private TextMeshProUGUI[] dayTexts;

    public Image m_SelectedDateBg;
    public TextMeshProUGUI m_SelectedDateTxt;

    private void OnEnable()
    {
        m_NormalColor.a = 256f;
        m_PassedColor.a = 256f;
        m_TodayColor.a = 256f;
        m_NormalColorTxt.a = 256f;
        m_PassedColorTxt.a = 256f;
        m_TodayColorTxt.a = 256f;

        // Automatically initialize to the current month and year
        Init(DateTime.Now.Month, DateTime.Now.Year);
        MarkAsPassed();
    }

    public void Init(int month, int year)
    {
        // 1. Get the first day of the month
        DateTime firstDayOfMonth = new DateTime(year, month, 1);
        
        // 2. Calculate the starting offset
        // DayOfWeek enum: Sunday = 0, Monday = 1 ... Saturday = 6
        // Your calendar starts on Monday, so we adjust the offset:
        int dayOffset = ((int)firstDayOfMonth.DayOfWeek - 1 + 7) % 7;

        // 3. Get total days in the month
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // 4. Loop through all 35 slots
        for (int i = 0; i < dayImages.Length; i++)
        {
            // Calculate the actual date number for this cell
            int dateNumber = i - dayOffset + 1;

            if (dateNumber >= 1 && dateNumber <= daysInMonth)
            {
                // Active day
                dayImages[i].gameObject.SetActive(true);
                dayTexts[i].text = dateNumber.ToString();
                dayImages[i].color = m_NormalColor;
                dayTexts[i].color = m_NormalColorTxt;
            }
            else
            {
                // Hide day (before start or after end of month)
                dayImages[i].gameObject.SetActive(false);
            }
        }
    }

    public void MarkAsSelected(Image i_bgDate)
    {
        m_SelectedDateBg = i_bgDate;
        m_SelectedDateTxt = m_SelectedDateBg.GetComponentInChildren<TextMeshProUGUI>();;
    MarkAsPassed();
    }

   public void MarkAsPassed()
    {
        m_SelectedDateBg.color = m_PassedColor;
        m_SelectedDateTxt.color = m_PassedColorTxt;
    }
}