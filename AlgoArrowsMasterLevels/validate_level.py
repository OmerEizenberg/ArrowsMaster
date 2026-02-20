import json
import argparse
import os
import collections

def is_level_solvable_verbose(level_data):
    """
    Iteratively solves the level by removing unblocked arrows.
    Provides verbose output about the removal sequence and blockers.
    """
    grid_size = level_data.get("gridSize")
    if not grid_size:
        print("Error: JSON missing 'gridSize'")
        return False
    
    gw, gh = grid_size["x"], grid_size["y"]
    arrows_list = level_data.get("arrows", [])
    if not isinstance(arrows_list, list):
        print("Error: 'arrows' must be a list")
        return False
    
    id_to_arrow = {a["id"]: a for a in arrows_list}
    total_arrows = len(id_to_arrow)
    
    if total_arrows == 0:
        print("Level is empty (0 arrows). Technically solvable.")
        return True

    print(f"--- Starting Validation of Level ({total_arrows} arrows) ---")
    
    # Initialize occupation map
    occupied = {}
    for aid, a in id_to_arrow.items():
        for p in a["path"]:
            occupied[(p["x"], p["y"])] = aid
            
    dir_vecs = {"up": (0, 1), "down": (0, -1), "left": (-1, 0), "right": (1, 0)}
    
    def check_arrow_blocked(aid):
        """Returns the ID of the first arrow blocking aid, or None if unblocked."""
        a = id_to_arrow[aid]
        h = a["path"][-1]
        ldir = a["lookDirection"]
        if ldir not in dir_vecs:
            # Fallback for old formats or errors
            print(f"  [WARN] Arrow {aid} has invalid lookDirection: {ldir}")
            return None
        
        dx, dy = dir_vecs[ldir]
        cx, cy = h["x"] + dx, h["y"] + dy
        while 0 <= cx < gw and 0 <= cy < gh:
            if (cx, cy) in occupied:
                return occupied[(cx, cy)]
            cx += dx
            cy += dy
        return None

    removed_count = 0
    handled = set()
    
    # We use a loop to repeatedly find unblocked arrows.
    # To be efficient, we keep track of which arrows are currently "ready".
    
    while removed_count < total_arrows:
        ready_this_round = []
        for aid in id_to_arrow:
            if aid not in handled:
                blocker = check_arrow_blocked(aid)
                if blocker is None:
                    ready_this_round.append(aid)
        
        if not ready_this_round:
            # Stuck!
            remaining = total_arrows - removed_count
            print(f"\n[FAILED] Stuck with {remaining} arrows remaining.")
            print("Blocker analysis:")
            for aid in id_to_arrow:
                if aid not in handled:
                    blocker = check_arrow_blocked(aid)
                    print(f"  - Arrow {aid} (at {id_to_arrow[aid]['path'][-1]}) is blocked by Arrow {blocker}")
            return False
        
        # Remove arrows found this round
        for aid in ready_this_round:
            a = id_to_arrow[aid]
            print(f"  [{removed_count + 1}/{total_arrows}] Removing Arrow {aid} (Dir: {a['lookDirection']})")
            
            # Clear points from occupation map
            for p in a["path"]:
                occupied.pop((p["x"], p["y"]), None)
            
            handled.add(aid)
            removed_count += 1
            
    print(f"\n[SUCCESS] Level is solvable! All {total_arrows} arrows cleared.")
    return True

def main():
    parser = argparse.ArgumentParser(description="Validate the solvability of an ArrowsMaster level JSON.")
    parser.add_argument("json_path", help="Path to the level JSON file")
    
    args = parser.parse_args()
    
    if not os.path.exists(args.json_path):
        print(f"Error: File not found: {args.json_path}")
        return

    try:
        with open(args.json_path, 'r') as f:
            level_data = json.load(f)
    except Exception as e:
        print(f"Error parsing JSON: {e}")
        return

    solvable = is_level_solvable_verbose(level_data)
    
    if solvable:
        print("\nRESULT: SOLVABLE")
    else:
        print("\nRESULT: UNSOLVABLE")

if __name__ == "__main__":
    main()
