using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Core
{
    public class LevelManager : MonoBehaviour
    {
        public TextAsset levelJsonFile; // Drag JSON here
        public ArrowController arrowPrefab; // Drag Prefab here

        private List<GameObject> currentLevelObjects = new List<GameObject>();

        // Start removed to prevent auto-loading. Level is loaded via GameManager.StartLevel.

        public void LoadLevelFromResources(string levelId)
        {
            TextAsset jsonFile = Resources.Load<TextAsset>($"Levels/{levelId}");
            if (jsonFile != null)
            {
                ClearLevel();
                LoadLevel(jsonFile.text);
            }
            else
            {
                Debug.LogError($"Level with ID {levelId} not found in Resources.");
            }
        }

        public void ClearLevel()
        {
            foreach (GameObject obj in currentLevelObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            currentLevelObjects.Clear();
            GridManager.Instance.InitializeGrid(Vector2Int.zero); // Reset with zero or just clear map
        }

        public void LoadLevel(string json)
        {
            LevelData data = JsonUtility.FromJson<LevelData>(json);
            
            // Initialize Grid
            GridManager.Instance.InitializeGrid(data.gridSize.ToVector2Int());
            
            // Create Arrows
            foreach (ArrowData arrowData in data.arrows)
            {
                ArrowController arrow = Instantiate(arrowPrefab, Vector3.zero, Quaternion.identity);
                arrow.Initialize(arrowData);
                currentLevelObjects.Add(arrow.gameObject);
            }


            // Set Camera Bounds
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetBounds(data.gridSize.ToVector2Int());
            }
        }

    }
}
