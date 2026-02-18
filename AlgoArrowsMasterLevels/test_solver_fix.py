import json
from batch_gen_norm import is_level_solvable

def test_self_aiming():
    # Grid 5x5
    # Arrow 1: Head at (2, 2), points Right. Tail at (4, 2).
    # This arrow aims at its own tail.
    level_data = {
        "gridSize": {"x": 5, "y": 5},
        "arrows": {
            1: {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 4, "y": 2}, {"x": 3, "y": 2}, {"x": 2, "y": 2}] # Head is at (2,2)
            }
        }
    }
    
    # In the old solver, this would return True.
    # In the new solver, it should return False.
    solvable = is_level_solvable(level_data)
    print(f"Self-aiming arrow (direct hit): Solvable = {solvable}")
    assert solvable == False, "Direct self-aiming arrow should NOT be solvable"

def test_self_aiming_blocked():
    # Grid 10x10
    # Arrow 1: Head at (2, 2), points Right. Tail at (6, 2).
    # Arrow 2: Block at (4, 2).
    # Arrow 1 aims at Arrow 2, and BEHIND Arrow 2 is Arrow 1's tail.
    level_data = {
        "gridSize": {"x": 10, "y": 10},
        "arrows": {
            1: {
                "id": 1,
                "lookDirection": "right",
                "path": [{"x": 6, "y": 2}, {"x": 5, "y": 2}, {"x": 2, "y": 2}] # Head is at (2,2)
            },
            2: {
                "id": 2,
                "lookDirection": "up",
                "path": [{"x": 4, "y": 2}, {"x": 4, "y": 3}]
            }
        }
    }
    
    # Arrow 2 is solvable (it aims up, path clear)
    # Once Arrow 2 is removed, Arrow 1 aims at its own tail.
    # So the level should NOT be solvable.
    solvable = is_level_solvable(level_data)
    print(f"Self-aiming arrow (behind blocker): Solvable = {solvable}")
    assert solvable == False, "Level with latent self-aiming should NOT be solvable"

if __name__ == "__main__":
    try:
        test_self_aiming()
        test_self_aiming_blocked()
        print("All tests passed!")
    except AssertionError as e:
        print(f"Test FAILED: {e}")
    except Exception as e:
        print(f"An error occurred: {e}")
