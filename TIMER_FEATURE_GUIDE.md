# Countdown Timer Feature - Implementation Guide

## Overview
This feature adds countdown timer support for time-based levels in the Arrow Escape game. Levels can optionally include a `duration` parameter (in seconds) that triggers a countdown timer displayed in MM:SS format.

## Key Features

### 1. **Time-Based Levels**
- Levels with a `duration` parameter in their JSON become time-based
- Timer displays in MM:SS format (e.g., "02:30" for 2 minutes 30 seconds)
- Timer starts on first arrow interaction (first touch)
- When time runs out, the failure screen appears
- **Timer color changes** when 30 seconds or less remain (configurable warning color)

### 2. **Dynamic Failure Messages**
- **Time-Based Levels:**
  - Title: "Time's Up!"
  - Subtitle: "You ran out of time"
  
- **Normal Levels (Life-Based):**
  - Title: "Out of Lives!"
  - Subtitle: "Better luck next time"

### 3. **PlayOn Feature for Time-Based Levels**
When using PlayOn (rewarded ad) in time-based levels:
- Player receives **3 lives** (standard)
- Player receives **60 additional seconds** (new!)
- Timer resumes counting down from the new time

## Implementation Details

### Modified Files

#### 1. **LevelData.cs**
Added optional `duration` field:
```csharp
public int duration; // Optional: duration in seconds for time-based levels (0 = no time limit)
```

#### 2. **GameManager.cs**
Added comprehensive timer management:
- Timer state tracking (`currentTime`, `levelDuration`, `isTimerActive`, `isTimeUp`)
- Public properties: `IsTimedLevel`, `CurrentTime`, `LevelDuration`
- Event: `OnTimerUpdated` - fires with formatted time string (MM:SS)
- Methods:
  - `InitializeTimer(int durationInSeconds)` - Sets up timer for level
  - `StartTimer()` - Begins countdown (called on first touch)
  - `GetFailureTitle()` - Returns appropriate title based on level type
  - `GetFailureSubtitle()` - Returns appropriate subtitle based on level type
- PlayOn now adds 60 seconds for time-based levels

#### 3. **LevelManager.cs**
- Calls `GameManager.Instance.InitializeTimer(data.duration)` when loading levels with duration > 0

#### 4. **InputManager.cs**
- Starts timer on first arrow click via `GameManager.Instance.StartTimer()`

#### 5. **GameUIController.cs**
Added UI management:
- New serialized fields:
  - `m_TimerContainer` - Container GameObject for timer UI
  - `m_TimerText` - TextMeshProUGUI for displaying time
  - `m_FailureTitle` - TextMeshProUGUI for failure screen title
  - `m_FailureSubtitle` - TextMeshProUGUI for failure screen subtitle
- Subscribes to `OnTimerUpdated`, `OnLevelStarted`, `OnGameOver` events
- Updates timer visibility based on level type
- Updates failure screen text dynamically

## Level JSON Format

### Normal Level (No Timer)
```json
{
  "gridSize": {
    "x": 16,
    "y": 14
  },
  "arrows": [...]
}
```

### Time-Based Level (With Timer)
```json
{
  "gridSize": {
    "x": 16,
    "y": 14
  },
  "duration": 120,
  "arrows": [...]
}
```
**Note:** `duration` is in seconds. Example: 120 = 2 minutes

## UI Setup Instructions

To complete the implementation, you need to set up the UI in Unity:

### 1. **Timer UI**
Create a timer display in your Game UI:
1. Create a GameObject container (e.g., "TimerContainer")
2. Add a TextMeshProUGUI component for the timer text
3. Assign references in GameUIController:
   - `m_TimerContainer` → TimerContainer GameObject
   - `m_TimerText` → TextMeshProUGUI component
4. Configure timer colors in GameUIController:
   - `m_TimerDefaultColor` → Default color (e.g., White)
   - `m_TimerWarningColor` → Warning color for last 30 seconds (e.g., Red or Orange)

### 2. **Failure Screen**
Update your failure screen to support dynamic text:
1. Add TextMeshProUGUI for title (or find existing)
2. Add TextMeshProUGUI for subtitle (or find existing)
3. Assign references in GameUIController:
   - `m_FailureTitle` → Title TextMeshProUGUI
   - `m_FailureSubtitle` → Subtitle TextMeshProUGUI

## Testing

### Test Normal Level
1. Load any existing level without `duration` parameter
2. Verify timer UI is hidden
3. Lose all lives
4. Verify failure screen shows: "Out of Lives!" / "Better luck next time"

### Test Time-Based Level
1. Load `LevelTimedExample.json` (included)
2. Verify timer shows "02:00" and is visible
3. Click an arrow - timer should start counting down
4. Wait for timer to reach 00:00
5. Verify failure screen shows: "Time's Up!" / "You ran out of time"

### Test PlayOn (Time-Based)
1. Start a time-based level
2. Let timer run low or lose lives
3. Use PlayOn (rewarded ad)
4. Verify:
   - Lives reset to 3
   - Timer increases by 60 seconds (01:00)
   - Timer continues counting down

## Example Levels

A sample time-based level has been created at:
`Assets/Resources/Levels/LevelTimedExample.json`

This level has:
- 120 second (2 minute) duration
- Simple 3-arrow puzzle
- 10x10 grid

## Notes

- Timer only starts on **first touch** (not when level loads)
- Timer format is always **MM:SS** (e.g., "02:30", "00:45")
- If `duration` is 0 or not present, level is treated as normal (life-based)
- Timer stops when:
  - Time reaches 00:00 (triggers failure)
  - Player wins the level
  - Player is on failure screen
- PlayOn in time-based levels gives both lives AND time
