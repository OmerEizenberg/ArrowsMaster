# Timer Color Change Feature

## Overview
The timer now automatically changes color when 30 seconds or less remain, providing a visual warning to the player.

## Configuration

In the **GameUIController** component (Unity Inspector), you'll find two new color parameters under the "Timer Colors" header:

### Parameters
1. **m_TimerDefaultColor** (Default: White)
   - The normal color of the timer
   - Used when more than 30 seconds remain
   
2. **m_TimerWarningColor** (Default: Red)
   - The warning color for urgency
   - Automatically applied when 30 seconds or less remain

## Behavior

### Timeline
```
Timer at 02:00 (120s) → Default Color (White)
Timer at 01:00 (60s)  → Default Color (White)
Timer at 00:31 (31s)  → Default Color (White)
Timer at 00:30 (30s)  → Warning Color (Red) ← Color changes here!
Timer at 00:15 (15s)  → Warning Color (Red)
Timer at 00:00 (0s)   → Warning Color (Red) → Game Over
```

### Color Transition
- The color change happens **instantly** when the timer reaches 30 seconds
- No gradual fade - immediate switch for maximum visibility
- Color resets to default when a new level starts

## Customization Examples

### Recommended Color Schemes

**Option 1: Classic Warning**
- Default: White `#FFFFFF`
- Warning: Red `#FF0000`

**Option 2: Subtle Warning**
- Default: Light Gray `#CCCCCC`
- Warning: Orange `#FF8800`

**Option 3: High Contrast**
- Default: Cyan `#00FFFF`
- Warning: Magenta `#FF00FF`

**Option 4: Game Theme**
- Default: Your game's primary color
- Warning: Complementary urgent color

## Implementation Details

The color update happens in `GameUIController.UpdateTimerUI()`:
```csharp
float remainingTime = GameManager.Instance.CurrentTime;
m_TimerText.color = remainingTime <= 30f ? m_TimerWarningColor : m_TimerDefaultColor;
```

This check runs every frame while the timer is active, ensuring smooth and accurate color transitions.

## Testing

1. Load a time-based level (e.g., `LevelTimedExample.json`)
2. Set the duration to 35 seconds for quick testing
3. Start the level and click an arrow
4. Watch the timer count down
5. At 00:30, the color should change to your warning color
6. Verify the color change is visible and attention-grabbing

## Notes

- The 30-second threshold is hardcoded but can be easily modified in `GameUIController.cs` line ~77
- To change the threshold, modify: `remainingTime <= 30f` to your desired value
- Color applies to the entire timer text (both minutes and seconds)
- Works seamlessly with PlayOn feature (color updates correctly when time is added)
