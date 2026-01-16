using System.Collections.Generic;
using UnityEngine;
using Assets.Scripts.Data;

namespace Assets.Scripts.Core
{
    public class LevelManager : MonoBehaviour
    {
        public TextAsset levelJsonFile; // Drag JSON here
        public ArrowController arrowPrefab; // Drag Prefab here
        public GameObject wallPrefab; // Drag Wall Prefab here

        void Start()
        {
            if (levelJsonFile != null)
            {
                LoadLevel(levelJsonFile.text);
            }
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
            }

            GenerateWalls(data.gridSize.ToVector2Int());
        }

        private void GenerateWalls(Vector2Int size)
        {
            if (wallPrefab == null) return;

            float step = ArrowController.CellSize;

            // Generate walls around the grid: x from -1 to width, y from -1 to height
            // Top and Bottom rows
            for (int x = -1; x <= size.x; x++)
            {
                Instantiate(wallPrefab, new Vector3(x * step, -1 * step, 0), Quaternion.identity, transform); // Bottom
                Instantiate(wallPrefab, new Vector3(x * step, size.y * step, 0), Quaternion.identity, transform); // Top
            }

            // Left and Right columns (avoid corners already placed)
            for (int y = 0; y < size.y; y++)
            {
                Instantiate(wallPrefab, new Vector3(-1 * step, y * step, 0), Quaternion.identity, transform); // Left
                Instantiate(wallPrefab, new Vector3(size.x * step, y * step, 0), Quaternion.identity, transform); // Right
            }
        }
    }
}
