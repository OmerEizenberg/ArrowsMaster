using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using Assets.Scripts.Core;

namespace Assets.Scripts.Lobby
{



public class MonthlyChallengeController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Assign all 35 day images here (Row 1 Col 1 to Row 5 Col 7)")]
    [SerializeField] private Image[] dayImages;
    [SerializeField] private Color m_NormalColor;
    [SerializeField] private Color m_PassedColor;
    [SerializeField] private Color m_SelectedDateColor;

    [SerializeField] private Color m_NormalColorTxt;
    [SerializeField] private Color m_PassedColorTxt;
    [SerializeField] private Color m_SelectedDateTxtColor;
    
    [Tooltip("Assign all 35 day text components here")]
    [SerializeField] private TextMeshProUGUI[] m_dayTexts;
    [SerializeField] private TextMeshProUGUI m_challengeMonthTitle;
    [SerializeField] private Button m_NextMonthBtn;
    [SerializeField] private Button m_PrevMonthBtn;

    [SerializeField] private HomeContoller m_HomeController;
    [SerializeField] private Slider m_monthlyProgress;
    [SerializeField] private TextMeshProUGUI m_monthlyProgressGoalTxt;
    [SerializeField] private TextMeshProUGUI m_monthlyActualProgressText;

    public Image m_SelectedDateBg;
    public TextMeshProUGUI m_SelectedDateTxt;

    public int p_CurrentDay = 1;
    public int p_CurrentMonth = 1;
    public int p_CurrentYear = 2026;

    private void OnEnable()
    {
        m_NormalColor.a = 1f;
        m_PassedColor.a = 1f;
        m_SelectedDateColor.a = 1f;
        m_NormalColorTxt.a = 1f;
        m_PassedColorTxt.a = 1f;
        m_SelectedDateTxtColor.a = 1f;

        // Automatically initialize to the current month and year
        p_CurrentMonth = DateTime.Now.Month;
        p_CurrentYear = DateTime.Now.Year;
        p_CurrentDay = DateTime.Now.Day;
        
        Init(p_CurrentMonth, p_CurrentYear);

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnMonthlyProgressChanged += HandleMonthlyProgressChanged;
        }
    }

    private void OnDisable()
    {
        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnMonthlyProgressChanged -= HandleMonthlyProgressChanged;
        }
    }

    private void HandleMonthlyProgressChanged()
    {
        // Re-initialize with current view parameters to show updated status
        Init(p_CurrentMonth, p_CurrentYear);
    }

    public void Init(int month, int year)
    {
        // 1. Get the first day of the month
        DateTime firstDayOfMonth = new DateTime(year, month, 1);
        m_challengeMonthTitle.isRightToLeftText = CultureInfo.CurrentCulture.TextInfo.IsRightToLeft;
        string titleText = firstDayOfMonth.ToString("Y", CultureInfo.CurrentCulture);
        if (m_challengeMonthTitle.isRightToLeftText)
        {
            titleText = ReverseDigits(titleText);
        }
        m_challengeMonthTitle.text = titleText;
        p_CurrentYear = year;
        p_CurrentMonth = month;
        // 2. Calculate the starting offset
        // DayOfWeek enum: Sunday = 0, Monday = 1 ... Saturday = 6
        // Your calendar starts on Monday, so we adjust the offset:
        int dayOffset = ((int)firstDayOfMonth.DayOfWeek - 1 + 7) % 7;

        // 3. Get total days in the month
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // Load progress
        int passedCount = 0;

        // 4. Loop through all 35 slots
        for (int i = 0; i < dayImages.Length; i++)
        {
            // Calculate the actual date number for this cell
            int dateNumber = i - dayOffset + 1;

            if (dateNumber >= 1 && dateNumber <= daysInMonth)
            {
                if(year == DateTime.Now.Year && month == DateTime.Now.Month && DateTime.Now.Day < dateNumber)
                {
                    dayImages[i].gameObject.SetActive(false);
                }else{
                     // Active day
                    dayImages[i].GetComponent<Button>().interactable = true;
                    dayImages[i].gameObject.SetActive(true);
                    if(m_dayTexts[i] == null) m_dayTexts[i] = dayImages[i].transform.GetChild(0).GetComponent<TextMeshProUGUI>();
                    m_dayTexts[i].text = dateNumber.ToString();
                    dayImages[i].color = m_NormalColor;
                    m_dayTexts[i].color = m_NormalColorTxt;

                    if (UserDataManager.Instance.IsDayCompleted(year, month, dateNumber))
                    {
                        MarkAsPassed(dayImages[i]);
                        passedCount++;
                    }
                }
            }
            else
            {
                // Hide day (before start or after end of month)
                dayImages[i].gameObject.SetActive(false);
            }
        }

        if (m_monthlyActualProgressText != null)
        {
            m_monthlyActualProgressText.text = passedCount.ToString();
        }

        if (m_monthlyProgressGoalTxt != null)
        {
            m_monthlyProgressGoalTxt.text = daysInMonth.ToString();
            m_monthlyProgress.maxValue = daysInMonth;
        }

        if (m_monthlyActualProgressText != null)
        {
            // Update textual progress
            m_monthlyActualProgressText.text = ""+passedCount;
            m_monthlyProgress.value = passedCount;
        }
        
        // Auto-select latest available day that is NOT yet passed
        int latestDay = daysInMonth;
        if (month == DateTime.Now.Month && year == DateTime.Now.Year)
        {
            latestDay = DateTime.Now.Day;
        }

        int selectedDayIndex = -1;
        for (int d = latestDay; d >= 1; d--)
        {
            if (!UserDataManager.Instance.IsDayCompleted(year, month, d))
            {
                selectedDayIndex = d + dayOffset - 1;
                break;
            }
        }
        
        if (selectedDayIndex != -1)
        {
            MarkAsSelected(dayImages[selectedDayIndex]);
        }
        else
        {
            // All available days are completed
            ClearSelection();
        }
        
        UpdateNavButtons();
    }

    private void UpdateNavButtons()
    {
        DateTime currentDisplayed = new DateTime(p_CurrentYear, p_CurrentMonth, 1);
        DateTime now = DateTime.Now;
        DateTime firstAllowed = new DateTime(UserDataManager.Instance.InstallDate.Year, UserDataManager.Instance.InstallDate.Month, 1).AddMonths(-1);

        // Hide Next button if show current month (or future, though shouldn't happen)
        m_NextMonthBtn.gameObject.SetActive(currentDisplayed.Year < now.Year || (currentDisplayed.Year == now.Year && currentDisplayed.Month < now.Month));
        
        // Hide Previous button if reached the limit (1 month before install date)
        m_PrevMonthBtn.gameObject.SetActive(currentDisplayed > firstAllowed);
    }

    public void NextMonth()
    {
        DateTime next = new DateTime(p_CurrentYear, p_CurrentMonth, 1).AddMonths(1);
        p_CurrentMonth = next.Month;
        p_CurrentYear = next.Year;
        Init(p_CurrentMonth, p_CurrentYear);
    }

    public void PrevMonth()
    {
        DateTime prev = new DateTime(p_CurrentYear, p_CurrentMonth, 1).AddMonths(-1);
        p_CurrentMonth = prev.Month;
        p_CurrentYear = prev.Year;
        Init(p_CurrentMonth, p_CurrentYear);
    }

    public void MarkAsSelected(Image i_bgDate)
    {
        ClearSelection();

        if (i_bgDate == null) return;

        m_SelectedDateBg = i_bgDate;
        m_SelectedDateTxt = m_SelectedDateBg.GetComponentInChildren<TextMeshProUGUI>();
        
        if (m_SelectedDateTxt != null && int.TryParse(m_SelectedDateTxt.text, out int selectedDay))
        {
            p_CurrentDay = selectedDay;
            if (m_HomeController != null) m_HomeController.RefreshLobbyUI();
        }

        MarkAsSelected();
    }

    private void ClearSelection()
    {
        if (m_SelectedDateBg != null)
        {
            // Determine if the day we're unselecting was already passed
            bool wasPassed = false;
            if (m_SelectedDateTxt != null && int.TryParse(m_SelectedDateTxt.text, out int dayNum))
            {
                wasPassed = UserDataManager.Instance.IsDayCompleted(p_CurrentYear, p_CurrentMonth, dayNum);
            }

            if (wasPassed)
            {
                m_SelectedDateBg.color = m_PassedColor;
                if (m_SelectedDateTxt != null) m_SelectedDateTxt.color = m_PassedColorTxt;
                m_SelectedDateBg.GetComponent<Button>().interactable = false;
            }
            else
            {
                m_SelectedDateBg.color = m_NormalColor;
                if (m_SelectedDateTxt != null) m_SelectedDateTxt.color = m_NormalColorTxt;
                m_SelectedDateBg.GetComponent<Button>().interactable = true;
            }
        }
        m_SelectedDateBg = null;
        m_SelectedDateTxt = null;
    }

    public void MarkAsSelected()
    {
        if (m_SelectedDateBg != null)
        {
            m_SelectedDateBg.color = m_SelectedDateColor;
            if (m_SelectedDateTxt != null) m_SelectedDateTxt.color = m_SelectedDateTxtColor;
        }
    }

   public void MarkAsPassed()
    {
        MarkAsPassed(m_SelectedDateBg);
    }

    public void MarkAsPassed(Image dateBg)
    {
        dateBg.color = m_PassedColor;
        TextMeshProUGUI txt = dateBg.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null)
        {
            txt.color = m_PassedColorTxt;
        }
        dateBg.GetComponent<Button>().interactable = false;
    }
    private string ReverseDigits(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        char[] chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (char.IsDigit(chars[i]))
            {
                int start = i;
                while (i < chars.Length && char.IsDigit(chars[i]))
                {
                    i++;
                }
                int end = i - 1;
                while (start < end)
                {
                    char temp = chars[start];
                    chars[start] = chars[end];
                    chars[end] = temp;
                    start++;
                    end--;
                }
            }
        }
        return new string(chars);
    }
}
}