import batch_gen_custom
import batch_gen_norm

def run_tests(module):
    print(f"\n--- Testing {module.__name__} ---")
    merge_stuck_arrows = module.merge_stuck_arrows
    test_basic_merge(merge_stuck_arrows)
    test_too_long_no_merge(merge_stuck_arrows)
    test_gap_no_merge(merge_stuck_arrows)
    test_gap_1_point_merge(merge_stuck_arrows)
    test_wrong_direction_no_merge(merge_stuck_arrows)

def test_basic_merge(merge_stuck_arrows):
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": [
            {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}],
                "color": "#ff0000"
            },
            {
                "id": 2,
                "lookDirection": "right",
                "path": [{"x": 2, "y": 0}, {"x": 3, "y": 0}],
                "color": "#00ff00"
            }
        ]
    }
    
    merged_data = merge_stuck_arrows(level_data)
    arrows = merged_data["arrows"]
    
    print(f"Test Basic Merge: {len(arrows)} arrows remaining")
    assert len(arrows) == 1
    assert len(arrows[0]["path"]) == 4
    assert arrows[0]["id"] == 2 # Merged into ID 2
    print("Test Basic Merge Passed!")

def test_too_long_no_merge(merge_stuck_arrows):
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": [
            {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}, {"x": 2, "y": 0}, {"x": 3, "y": 0}],
                "color": "#ff0000"
            },
            {
                "id": 2,
                "lookDirection": "right",
                "path": [{"x": 4, "y": 0}, {"x": 5, "y": 0}],
                "color": "#00ff00"
            }
        ]
    }
    
    merged_data = merge_stuck_arrows(level_data)
    arrows = merged_data["arrows"]
    
    print(f"Test Too Long No Merge: {len(arrows)} arrows remaining")
    assert len(arrows) == 2
    print("Test Too Long No Merge Passed!")

def test_gap_no_merge(merge_stuck_arrows):
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": [
            {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}],
                "color": "#ff0000"
            },
            {
                "id": 2,
                "lookDirection": "right",
                "path": [{"x": 4, "y": 0}, {"x": 5, "y": 0}], # Gap at (2,0) and (3,0)
                "color": "#00ff00"
            }
        ]
    }
    
    merged_data = merge_stuck_arrows(level_data)
    arrows = merged_data["arrows"]
    
    print(f"Test Gap No Merge: {len(arrows)} arrows remaining")
    assert len(arrows) == 2
    print("Test Gap No Merge Passed!")

def test_gap_1_point_merge(merge_stuck_arrows):
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": [
            {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}],
                "color": "#ff0000"
            },
            {
                "id": 2,
                "lookDirection": "right",
                "path": [{"x": 3, "y": 0}, {"x": 4, "y": 0}], # Gap at (2,0)
                "color": "#00ff00"
            }
        ]
    }
    
    # Test with no shape mask (should merge by default)
    merged_data = merge_stuck_arrows(level_data)
    arrows = merged_data["arrows"]
    print(f"Test 1-Point Gap Merge (No Mask): {len(arrows)} arrows remaining")
    assert len(arrows) == 1
    assert len(arrows[0]["path"]) == 5 # 2 + 1 (gap) + 2
    assert {"x": 2, "y": 0} in arrows[0]["path"]
    
    # Test with shape mask containing the gap
    shape_mask = {(0,0), (1,0), (2,0), (3,0), (4,0)}
    level_data["arrows"] = [
        {"id": 1, "lookDirection": "right", "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}], "color": "#ff0000"},
        {"id": 2, "lookDirection": "right", "path": [{"x": 3, "y": 0}, {"x": 4, "y": 0}], "color": "#00ff00"}
    ]
    merged_data = merge_stuck_arrows(level_data, shape_mask=shape_mask)
    assert len(merged_data["arrows"]) == 1
    print("Test 1-Point Gap Merge (With Mask) Passed!")

    # Test with shape mask EXCLUDING the gap
    shape_mask = {(0,0), (1,0), (3,0), (4,0)} # (2,0) missing
    level_data["arrows"] = [
        {"id": 1, "lookDirection": "right", "path": [{"x": 0, "y": 0}, {"x": 1, "y": 0}], "color": "#ff0000"},
        {"id": 2, "lookDirection": "right", "path": [{"x": 3, "y": 0}, {"x": 4, "y": 0}], "color": "#00ff00"}
    ]
    merged_data = merge_stuck_arrows(level_data, shape_mask=shape_mask)
    assert len(merged_data["arrows"]) == 2
    print("Test 1-Point Gap Merge (Mask mismatch) Passed!")

def test_wrong_direction_no_merge(merge_stuck_arrows):
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": [
            {
                "id": 1,
                "lookDirection": "down", # Points to (1, -1)
                "path": [{"x": 1, "y": 1}, {"x": 1, "y": 0}],
                "color": "#ff0000"
            },
            {
                "id": 2,
                "lookDirection": "right",
                "path": [{"x": 2, "y": 0}, {"x": 3, "y": 0}], # At (2,0)
                "color": "#00ff00"
            }
        ]
    }
    
    merged_data = merge_stuck_arrows(level_data)
    arrows = merged_data["arrows"]
    
    print(f"Test Wrong Direction No Merge: {len(arrows)} arrows remaining")
    assert len(arrows) == 2
    print("Test Wrong Direction No Merge Passed!")

if __name__ == "__main__":
    run_tests(batch_gen_custom)
    run_tests(batch_gen_norm)
    print("\nAll tests for all modules passed!")
