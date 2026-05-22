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
    [Header("Special Day Colors")]
    [SerializeField] private Color m_FutureDayColor;
    [SerializeField] private Color m_FutureDayColorTxt;
    [SerializeField] private Color m_PaddingDayColor;
    [SerializeField] private Color m_PaddingDayColorTxt;
    
    [Tooltip("Assign all 35 day text components here")]
    [SerializeField] private TextMeshProUGUI[] m_dayTexts;
    [SerializeField] private TextMeshProUGUI m_challengeMonthTitle;
    [SerializeField] private Button m_NextMonthBtn;
    [SerializeField] private Button m_PrevMonthBtn;

    [SerializeField] private HomeContoller m_HomeController;
    [SerializeField] private Slider m_monthlyProgress;
    [SerializeField] private TextMeshProUGUI m_monthlyProgressGoalTxt;
    [SerializeField] private TextMeshProUGUI m_monthlyActualProgressText;
    [SerializeField] private GameObject m_CompletedIndication;
    [SerializeField] private GameObject m_PlayButton;
    public Image m_SelectedDateBg;
    public TextMeshProUGUI m_SelectedDateTxt;

    public int p_CurrentDay = 1;
    public int p_CurrentMonth = 1;
    public int p_CurrentYear = 2026;

    private void OnEnable()
    {
        // Return to the last viewed month to assist with completed "missions"
        p_CurrentMonth = UserDataManager.Instance.LastViewedChallengeMonth;
        p_CurrentYear = UserDataManager.Instance.LastViewedChallengeYear;
        p_CurrentDay = DateTime.Now.Day;
        
        Init(p_CurrentMonth, p_CurrentYear);

        if (UserDataManager.Instance != null)
        {
            UserDataManager.Instance.OnMonthlyProgressChanged += HandleMonthlyProgressChanged;
        }
    }

    private void Start()
    {
        // Double check initialization after all objects are ready
        Init(p_CurrentMonth, p_CurrentYear);
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
        m_challengeMonthTitle.isRightToLeftText = false;
        string titleText = firstDayOfMonth.ToString("Y", CultureInfo.InvariantCulture);
        m_challengeMonthTitle.text = titleText;
        p_CurrentYear = year;
        p_CurrentMonth = month;
        
        // Persist the last viewed month
        UserDataManager.Instance.SetLastViewedChallengeMonth(year, month);
        
        // 2. Calculate the starting offset
        // DayOfWeek enum: Sunday = 0, Monday = 1 ... Saturday = 6
        // Your calendar starts on Monday, so we adjust the offset:
        int dayOffset = ((int)firstDayOfMonth.DayOfWeek - 1 + 7) % 7;

        // 3. Get total days in the month
        int daysInMonth = DateTime.DaysInMonth(year, month);

        // 35 UI slots cannot fit every month when day 1 is late in the week (e.g. Mar 2026).
        // Shift the grid start so all in-month days remain playable; weekday labels may shift slightly.
        int requiredSlots = dayOffset + daysInMonth;
        if (dayImages != null && requiredSlots > dayImages.Length)
        {
            dayOffset = Mathf.Max(0, dayImages.Length - daysInMonth);
        }

        // Load progress
        int passedCount = 0;

        // 4. Loop through all 35 slots
        if (m_dayTexts == null || m_dayTexts.Length < dayImages.Length)
        {
            var oldTexts = m_dayTexts;
            m_dayTexts = new TextMeshProUGUI[dayImages.Length];
            if (oldTexts != null) Array.Copy(oldTexts, m_dayTexts, oldTexts.Length);
        }

        for (int i = 0; i < dayImages.Length; i++)
        {
            // Calculate the actual date number for this cell
            int dateNumber = i - dayOffset + 1;
            DateTime cellDate = firstDayOfMonth.AddDays(dateNumber - 1);
            int displayDay = cellDate.Day;

            dayImages[i].gameObject.SetActive(true);
            if (m_dayTexts[i] == null) m_dayTexts[i] = dayImages[i].GetComponentInChildren<TextMeshProUGUI>(true);

            if (dateNumber >= 1 && dateNumber <= daysInMonth)
            {
                bool isFuture = (year == DateTime.Now.Year && month == DateTime.Now.Month && dateNumber > DateTime.Now.Day);

                if (isFuture)
                {
                    // Visible but non-interactable and in future day color
                    dayImages[i].GetComponent<Button>().interactable = false;
                    dayImages[i].color = m_FutureDayColor;
                    if (i < m_dayTexts.Length)
                    {
                        m_dayTexts[i].text = displayDay.ToString();
                        m_dayTexts[i].color = m_FutureDayColorTxt;
                    }
                }
                else
                {
                    // Active day
                    dayImages[i].GetComponent<Button>().interactable = true;
                    if (i < m_dayTexts.Length)
                    {
                        m_dayTexts[i].text = displayDay.ToString();
                        m_dayTexts[i].color = m_NormalColorTxt;
                    }
                    
                    dayImages[i].color = m_NormalColor;

                    if (UserDataManager.Instance.IsDayCompleted(year, month, dateNumber))
                    {
                        MarkAsPassed(dayImages[i]);
                        passedCount++;
                    }
                }
            }
            else
            {
                // Day from last month or next month (padding)
                dayImages[i].GetComponent<Button>().interactable = false;
                dayImages[i].color = m_PaddingDayColor;
                if (i < m_dayTexts.Length)
                {
                    m_dayTexts[i].text = displayDay.ToString();
                    m_dayTexts[i].color = m_PaddingDayColorTxt;
                }
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

        if (m_monthlyActualProgressText != null) m_monthlyActualProgressText.gameObject.SetActive(passedCount < daysInMonth);
        if (m_CompletedIndication != null) m_CompletedIndication.SetActive(passedCount >= daysInMonth);
        
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
                int potentialIndex = d + dayOffset - 1;
                if (potentialIndex >= 0 && potentialIndex < dayImages.Length)
                {
                    selectedDayIndex = potentialIndex;
                    break;
                }
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

        if (m_PlayButton != null) m_PlayButton.SetActive(selectedDayIndex != -1);
        
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
        if(m_NextMonthBtn.gameObject.activeSelf == false) return;
        
        DateTime next = new DateTime(p_CurrentYear, p_CurrentMonth, 1).AddMonths(1);
        p_CurrentMonth = next.Month;
        p_CurrentYear = next.Year;
        Init(p_CurrentMonth, p_CurrentYear);
    }

    public void PrevMonth()
    {
        if(m_PrevMonthBtn.gameObject.activeSelf == false) return;
        
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
        if (dateBg == null) return;
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