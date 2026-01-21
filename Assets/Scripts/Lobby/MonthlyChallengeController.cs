using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;


public class MonthlyChallengeController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Assign all 35 day images here (Row 1 Col 1 to Row 5 Col 7)")]
    [SerializeField] private Image[] dayImages;
    
    [Tooltip("Assign all 35 day text components here")]
    [SerializeField] private TextMeshProUGUI[] dayTexts;

    private void OnEnable()
    {
        // Automatically initialize to the current month and year
        Init(DateTime.Now.Month, DateTime.Now.Year);
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
            }
            else
            {
                // Hide day (before start or after end of month)
                dayImages[i].gameObject.SetActive(false);
            }
        }
    }
}