using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Developer cheat script for testing and debugging.
    /// Provides hacky ways to win levels and change player level without modifying core scripts.
    /// </summary>
    public class DeveloperCheats : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_InputField levelInputField;

        private void Start()
        {
            // Hook up the input field change event if it exists
            if (levelInputField != null)
            {
                levelInputField.onValueChanged.AddListener(OnLevelInputChanged);
            }
        }

        /// <summary>
        /// Cheat function to instantly win the level by destroying all arrows.
        /// This bypasses all game logic and directly triggers the win condition.
        /// </summary>
        public void WinLevel()
        {
            Debug.Log("[CHEAT] Attempting to instantly win level...");

            // Find all arrow controllers in the scene
            ArrowController[] arrows = FindObjectsOfType<ArrowController>();
            
            if (arrows == null || arrows.Length == 0)
            {
                Debug.LogWarning("[CHEAT] No arrows found in the scene. Cannot win level.");
                return;
            }

            Debug.Log($"[CHEAT] Found {arrows.Length} arrows. Destroying them all...");

            // Start the destruction coroutine
            StartCoroutine(DestroyAllArrows(arrows));
        }

        /// <summary>
        /// Hacky coroutine that destroys all arrows and manually triggers the win condition.
        /// This completely bypasses the normal game flow.
        /// </summary>
        private IEnumerator DestroyAllArrows(ArrowController[] arrows)
        {
            GameManager gameManager = GameManager.Instance;
            
            if (gameManager == null)
            {
                Debug.LogError("[CHEAT] GameManager not found!");
                yield break;
            }

            // Destroy each arrow and notify the game manager
            foreach (ArrowController arrow in arrows)
            {
                if (arrow != null && arrow.gameObject != null)
                {
                    Debug.Log($"[CHEAT] Destroying arrow at {arrow.GetHeadPosition()}");
                    
                    // Notify game manager that this arrow succeeded
                    // This will decrement the active arrow count
                    gameManager.NotifyArrowSuccess();
                    
                    // Destroy the arrow immediately
                    Destroy(arrow.gameObject);
                    
                    // Small delay to make it visible
                    yield return new WaitForSeconds(0.05f);
                }
            }

            Debug.Log("[CHEAT] All arrows destroyed. Win sequence should trigger automatically.");
        }

        /// <summary>
        /// Cheat function to change the player's current level.
        /// Called when the input field value changes.
        /// This is a hacky approach that directly modifies UserDataManager without editing it.
        /// </summary>
        /// <param name="levelString">The level number as a string from the input field</param>
        public void OnLevelInputChanged(string levelString)
        {
            // Try to parse the input as an integer
            if (int.TryParse(levelString, out int newLevel))
            {
                // Validate the level number (must be positive)
                if (newLevel > 0)
                {
                    Debug.Log($"[CHEAT] Changing player level to: {newLevel}");
                    
                    // Hacky way: Use reflection to access the UserDataManager singleton
                    // and call its SetLevel method
                    var userDataManager = UserDataManager.Instance;
                    
                    if (userDataManager != null)
                    {
                        // Call the public SetLevel method
                        userDataManager.SetLevel(newLevel);
                        
                        Debug.Log($"[CHEAT] Player level changed to: {userDataManager.CurrentLevel}");
                        
                        // Optional: Provide visual feedback
                        if (levelInputField != null)
                        {
                            // Change the input field color briefly to indicate success
                            StartCoroutine(FlashInputFieldSuccess());
                        }
                    }
                    else
                    {
                        Debug.LogError("[CHEAT] UserDataManager instance not found!");
                    }
                }
                else
                {
                    Debug.LogWarning($"[CHEAT] Invalid level number: {newLevel}. Must be greater than 0.");
                }
            }
            else
            {
                // Not a valid number, ignore
                if (!string.IsNullOrEmpty(levelString))
                {
                    Debug.LogWarning($"[CHEAT] Invalid input: '{levelString}'. Please enter a valid number.");
                }
            }
        }

        /// <summary>
        /// Visual feedback coroutine to flash the input field green when level is changed successfully.
        /// </summary>
        private IEnumerator FlashInputFieldSuccess()
        {
            if (levelInputField == null) yield break;

            // Store original color
            Color originalColor = Color.white;
            Image inputFieldImage = levelInputField.GetComponent<Image>();
            
            if (inputFieldImage != null)
            {
                originalColor = inputFieldImage.color;
                
                // Flash green
                inputFieldImage.color = new Color(0.5f, 1f, 0.5f, 1f);
                yield return new WaitForSeconds(0.2f);
                
                // Return to original color
                inputFieldImage.color = originalColor;
            }
        }

        private void OnDestroy()
        {
            // Clean up the input field listener
            if (levelInputField != null)
            {
                levelInputField.onValueChanged.RemoveListener(OnLevelInputChanged);
            }
        }

        #region Editor Buttons (for testing in Inspector)
        
        // These methods can be called from Unity's Inspector or custom editor buttons
        
        [ContextMenu("Cheat: Win Level")]
        private void CheatWinLevel()
        {
            WinLevel();
        }

        [ContextMenu("Cheat: Set Level to 10")]
        private void CheatSetLevel10()
        {
            OnLevelInputChanged("10");
        }

        [ContextMenu("Cheat: Set Level to 100")]
        private void CheatSetLevel100()
        {
            OnLevelInputChanged("100");
        }

        #endregion
    }
}
