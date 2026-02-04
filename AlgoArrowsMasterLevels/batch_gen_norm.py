import random
import os
import sys
import json
import argparse
import collections
from PIL import Image

# --- DIFFICULTY CONFIGURATIONS ---
DIFFICULTY_CONFIGS = {
    1: { # Based on old "Difficulty 3" (Hard)
        "SHORT_PATH_PROBABILITY": 0.3,
        "SHORT_PATH_RANGE": (3, 7),
        "LONG_PATH_RANGE": (7, 22),
        "TURN_PROBABILITY": 0.7,
        "TARGET_BLOCKED_PROBABILITY": 0.5,
        "PERPENDICULAR_PREFERENCE": 0.0,
        "MAX_RETRY_ATTEMPTS": 100,
        "MIN_SEEDS": 1,
        "MAX_SEEDS": 3,
        "SORT_STRATEGY": "center"
    },
    2: { # Based on old "Difficulty 4" (Very Hard / Interlocking)
        "SHORT_PATH_PROBABILITY": 0.4,
        "SHORT_PATH_RANGE": (3, 7),
        "LONG_PATH_RANGE": (7, 22),
        "TURN_PROBABILITY": 0.7,
        "TARGET_BLOCKED_PROBABILITY": 0.8,
        "PERPENDICULAR_PREFERENCE": 0.8,
        "MAX_RETRY_ATTEMPTS": 100,
        "MIN_SEEDS": 1,
        "MAX_SEEDS": 5,
        "SORT_STRATEGY": "seeds"
    },
    3: { # New Reverse-Backtracking Logic
        "TARGET_DENSITY": 0.901,
        "TURN_CHANCE": 0.4,
        "LENGTH_TIERS": [
            (0.2, (2, 6)),   # Short: 20%
            (0.4, (5, 12)),  # Mid: 40%
            (0.4, (9, 22))   # Long: 40%
        ],
        "MAX_RETRY_ATTEMPTS": 100
    }
}

COMMON_CONFIG = {
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128
}

DURATION_MULTIPLIER = 0.121

def rgb_to_hex(rgb):
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def is_reachable(adj, start_node, target_nodes):
    if not target_nodes: return False
    stack = [start_node]
    visited = {start_node}
    while stack:
        curr = stack.pop()
        if curr in target_nodes:
            return True
        for neighbor in adj.get(curr, set()):
            if neighbor not in visited:
                visited.add(neighbor)
                stack.append(neighbor)
    return False

def is_level_solvable(level_data, pre_occupied=None):
    """Highly optimized axial-based iterative solver."""
    grid_size = level_data["gridSize"]
    gw, gh = grid_size["x"], grid_size["y"]
    id_to_arrow = {a["id"]: a for a in level_data["arrows"]} if isinstance(level_data["arrows"], list) else level_data["arrows"]
    
    if pre_occupied is not None:
        occupied = pre_occupied.copy()
    else:
        occupied = {}
        for aid, a in id_to_arrow.items():
            for p in a["path"]:
                occupied[(p["x"], p["y"])] = aid
            
    dir_vecs = {"up": (0, 1), "down": (0, -1), "left": (-1, 0), "right": (1, 0)}
    
    def check_arrow(aid):
        a = id_to_arrow[aid]
        h = a["path"][-1]
        dx, dy = dir_vecs[a["lookDirection"]]
        cx, cy = h["x"] + dx, h["y"] + dy
        while 0 <= cx < gw and 0 <= cy < gh:
            if (cx, cy) in occupied and occupied[(cx, cy)] != aid:
                return False
            cx += dx
            cy += dy
        return True

    results = {aid: check_arrow(aid) for aid in id_to_arrow}
    ready = {aid for aid, ok in results.items() if ok}
    removed_count = 0
    total = len(id_to_arrow)
    handled = set()
    
    # Axial indexing to find arrows affected by removal
    row_map = collections.defaultdict(set)
    col_map = collections.defaultdict(set)
    for aid, a in id_to_arrow.items():
        h = a["path"][-1]
        row_map[h["y"]].add(aid)
        col_map[h["x"]].add(aid)

    while ready:
        aid = ready.pop()
        handled.add(aid)
        removed_count += 1
        
        # When an arrow is removed, its points disappear
        points = id_to_arrow[aid]["path"]
        affected_rows = set()
        affected_cols = set()
        for p in points:
            px, py = p["x"], p["y"]
            occupied.pop((px, py), None)
            affected_rows.add(py)
            affected_cols.add(px)
        
        # Only re-check arrows on affected axes
        for r in affected_rows:
            for rid in row_map[r]:
                if rid not in handled and rid not in ready:
                    if check_arrow(rid): ready.add(rid)
        for c in affected_cols:
            for rid in col_map[c]:
                if rid not in handled and rid not in ready:
                    if check_arrow(rid): ready.add(rid)
            
    return removed_count == total

def generate_difficulty_3(image_path, grid_width, grid_height, config):
    # Try multiple times to find a level that both meets density and solvability
    for attempt in range(50):
        level_data = run_reverse_generator(image_path, grid_width, grid_height, config)
        if level_data:
            # 1. Post-process: Remove arrows with < 2 points
            level_data["arrows"] = [a for a in level_data["arrows"] if len(a["path"]) >= 2]
            
            # 2. Post-process: Fill remaining gaps with Difficulty 1 logic
            level_data = post_process_fill_gaps(level_data, image_path, config)
            
            # 3. Final Verification
            occupied_points = sum(len(a["path"]) for a in level_data["arrows"])
            # Re-read total shape points for density calculation
            total_shape_points = level_data.get("total_shape_points", 1)
            final_density = occupied_points / total_shape_points
            
            if final_density >= config["TARGET_DENSITY"] and is_level_solvable(level_data):
                print(f"  Level Attempt {attempt+1}: Solvability PASSED. Saving (Density: {final_density:.1%}).")
                level_data["duration"] = occupied_points * DURATION_MULTIPLIER
                return level_data
            else:
                reason = "SOLVER FAILED" if final_density >= config["TARGET_DENSITY"] else "DENSITY LOW"
                print(f"  Level Attempt {attempt+1}: {reason} ({final_density:.1%}). Retrying...")
    return None

def post_process_fill_gaps(level_data, image_path, config):
    """
    Fills empty areas with Difficulty 1 logic (Forward growth) while maintaining solvability.
    """
    # Load image data again to get shape_mask and colors
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
        img = img.resize((level_data["gridSize"]["x"], level_data["gridSize"]["y"]), Image.NEAREST)
        
        gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
        shape_mask = []
        pixel_colors = {}
        for y in range(gh):
            for x in range(gw):
                color = img.getpixel((x, y))
                r, g, b, a = color
                pixel_colors[(x, y)] = color
                if a >= config["ALPHA_THRESHOLD"] and not (r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]):
                    shape_mask.append((x, y))
    except:
        return level_data

    # Current occupation state
    occupied = {} # (x,y) -> arrow_id
    for a in level_data["arrows"]:
        for p in a["path"]:
            occupied[(p["x"], p["y"])] = a["id"]
    
    free_points = [p for p in shape_mask if p not in occupied]
    random.shuffle(free_points)
    
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}
    
    for start_node in free_points:
        if start_node in occupied: continue
        
        # Try to grow a Difficulty 1-style arrow from this point
        success = False
        # Multiple tries per start node
        for _t in range(5):
            target_length = random.randint(2, 6) # Moderate length for fillers
            path = [start_node]
            temp_path_set = {start_node}
            curr_dir = None
            
            for i in range(target_length - 1):
                last_x, last_y = path[-1]
                valid_dirs = []
                for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                    nx, ny = last_x + dx, last_y + dy
                    if (nx, ny) in shape_mask and (nx, ny) not in occupied and (nx, ny) not in temp_path_set:
                        valid_dirs.append((dx, dy))
                
                if not valid_dirs: break
                
                # Favor same direction, low turn probability
                if curr_dir in valid_dirs and random.random() > 0.2:
                    chosen_dir = curr_dir
                else:
                    chosen_dir = random.choice(valid_dirs)
                
                path.append((last_x + chosen_dir[0], last_y + chosen_dir[1]))
                temp_path_set.add(path[-1])
                curr_dir = chosen_dir
            
            if len(path) < 2: continue
            
            # Verify constraints
            head = path[-1]
            prev = path[-2]
            look_dx, look_dy = head[0] - prev[0], head[1] - prev[1]
            
            # Constraint: Cant aim into own segment
            aim_x, aim_y = head[0] + look_dx, head[1] + look_dy
            is_self_aiming = False
            while 0 <= aim_x < gw and 0 <= aim_y < gh:
                if (aim_x, aim_y) in temp_path_set:
                    is_self_aiming = True
                    break
                if (aim_x, aim_y) in occupied or (aim_x, aim_y) in shape_mask: # If it hits anything else, we stop checking for self-aiming
                    # Actually, self-aiming only occurs if it passes over its own path before hitting anything else or boundary
                    pass
                aim_x += look_dx
                aim_y += look_dy
            
            if is_self_aiming: continue
            
            # Solver check
            avg_c = [sum(pixel_colors.get(p, (0,0,0,0))[c] for p in path)//len(path) for c in range(3)]
            new_arrow = {
                "id": next_id,
                "color": rgb_to_hex(avg_c),
                "path": [{"x": p[0], "y": p[1]} for p in path],
                "lookDirection": dir_map[(look_dx, look_dy)]
            }
            
            test_level = {
                "gridSize": {"x": gw, "y": gh},
                "arrows": level_data["arrows"] + [new_arrow]
            }
            
            # Efficiently update occupation for solver
            test_occupied = occupied.copy()
            for p in path: test_occupied[p] = next_id
            
            if is_level_solvable(test_level, pre_occupied=test_occupied):
                level_data["arrows"].append(new_arrow)
                for p in path: occupied[p] = next_id
                next_id += 1
                success = True
                break
                
        if success: continue
        
    return level_data

def generate_difficulty_1(image_path, grid_width, grid_height, config):
    return run_core_generator(image_path, grid_width, grid_height, config)

def generate_difficulty_2(image_path, grid_width, grid_height, config):
    return run_core_generator(image_path, grid_width, grid_height, config)

def generate_level_json(image_path, grid_width, grid_height, difficulty=1):
    config = COMMON_CONFIG.copy()
    diff_config = DIFFICULTY_CONFIGS.get(difficulty, DIFFICULTY_CONFIGS[1])
    config.update(diff_config)
    
    if difficulty == 1:
        return generate_difficulty_1(image_path, grid_width, grid_height, config)
    elif difficulty == 2:
        return generate_difficulty_2(image_path, grid_width, grid_height, config)
    elif difficulty == 3:
        return generate_difficulty_3(image_path, grid_width, grid_height, config)
    else:
        return generate_difficulty_1(image_path, grid_width, grid_height, config)

def run_reverse_generator(image_path, grid_width, grid_height, config):
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    shape_mask = set()
    pixel_colors = {}
    for y in range(grid_height):
        for x in range(grid_width):
            color = img.getpixel((x, y))
            r, g, b, a = color
            pixel_colors[(x, y)] = color
            is_transparent = a < config["ALPHA_THRESHOLD"]
            is_white = r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]
            if not is_transparent and not is_white:
                shape_mask.add((x, y))

    if not shape_mask:
        return None

    occupied = set() # For quick membership check
    occupied_with_ids = {} # (x, y) -> arrow_id for solver consistency
    escape_routes = {} 
    arrows = []
    arrow_id = 1
    total_shape_points = len(shape_mask)
    target_points = int(total_shape_points * config["TARGET_DENSITY"])
    free_points = set(shape_mask) # Incremental tracking

    # Pre-calculate boundary distances for all points
    # dist_to_bounds[p] -> {dir: dist}
    dist_to_bounds = {}
    for p in shape_mask:
        px, py = p
        dist_to_bounds[p] = {
            (1, 0): grid_width - 1 - px,
            (-1, 0): px,
            (0, 1): grid_height - 1 - py,
            (0, -1): py
        }

    # Solver-Validated strategy with axial lookup optimization
    heads_by_row = collections.defaultdict(list) # y -> [(x, head_id, lookDirection)]
    heads_by_col = collections.defaultdict(list) # x -> [(y, head_id, lookDirection)]
    
    def update_head_maps(arrow):
        aid = arrow["id"]
        path = arrow["path"]
        h = path[-1] # Head is at end
        ldir = arrow["lookDirection"]
        heads_by_row[h["y"]].append((h["x"], aid, ldir))
        heads_by_col[h["x"]].append((h["y"], aid, ldir))

    iteration = 0
    max_iters = total_shape_points * 20 # More room for 60x60 final packing
    while len(occupied) < target_points and iteration < max_iters:
        iteration += 1
        candidates = []
        # Performance: Pre-fetch only necessary head data
        inv_map = {"right": (1, 0), "left": (-1, 0), "up": (0, 1), "down": (0, -1)}
        
        for p in free_points:
            px, py = p
            # Check mutual aiming using axial lookups instead of iterating all arrows
            # Rows (same y)
            row_heads = heads_by_row.get(py, [])
            # Columns (same x)
            col_heads = heads_by_col.get(px, [])
            
            for edir, dist_to_boundary in dist_to_bounds[p].items():
                is_mutual = False
                # 1. Check same row
                for hx, _, ldir in row_heads:
                    odx, ody = inv_map[ldir]
                    if odx == 0: continue # Only horizontal lookDirections can aim at p in same row
                    vx = px - hx
                    if vx * odx > 0: # Other head aims at p
                        wx = hx - px
                        if edir[0] != 0 and wx * edir[0] > 0: # p aims at other head
                            is_mutual = True; break
                if is_mutual: continue
                
                # 2. Check same column
                for hy, _, ldir in col_heads:
                    odx, ody = inv_map[ldir]
                    if ody == 0: continue # Only vertical lookDirections can aim at p in same col
                    vy = py - hy
                    if vy * ody > 0: # Other head aims at p
                        wy = hy - py
                        if edir[1] != 0 and wy * edir[1] > 0: # p aims at other head
                            is_mutual = True; break
                if is_mutual and current_density < 0.8:
                    if is_mutual: continue

                current_density = len(occupied) / total_shape_points
                priority = dist_to_boundary
                if p in escape_routes:
                    priority += len(escape_routes.get(p, set())) * 40
                candidates.append((p, edir, priority))
        
        if iteration % 200 == 0:
            print(f"      - Progress: {current_density:.1%}, Iter: {iteration}")
            
        if not candidates: break
            
        candidates.sort(key=lambda x: x[2], reverse=True)
        # Multi-Candidate Strategy: Try several top candidates until one fits
        if current_density > 0.8:
            top_candidates = candidates # Exhaustive search at high density
        else:
            top_candidates = candidates[:max(1, len(candidates) // 10)]
        random.shuffle(top_candidates)
        
        success_placement = False
        for head_node, escape_dir, _ in top_candidates[:30]: # Try more spots
            if success_placement: break
            
            for _growth_try in range(2): # Fewer growth tries per spot
                if success_placement: break
                current_density = len(occupied) / total_shape_points
                tier_roll = random.random()
                cumulative = 0
                target_len = 2
                length_tiers = config["LENGTH_TIERS"]
                if current_density > 0.88:
                    length_tiers = [(0.9, (1, 2)), (0.1, (2, 3))] # Allow length 1
                elif current_density > 0.75: # Earlier transition
                    length_tiers = [(0.7, (2, 3)), (0.3, (3, 5))]
                elif current_density > 0.6:
                    length_tiers = [(0.4, (2, 4)), (0.4, (4, 8)), (0.2, (6, 12))]

                for prob, (range_min, range_max) in length_tiers:
                    cumulative += prob
                    if tier_roll <= cumulative:
                        target_len = random.randint(range_min, range_max)
                        break
                
                should_turn = random.random() < config["TURN_CHANCE"]
                path = [head_node]
                temp_path_occupied = {head_node}
                curr_dir = (-escape_dir[0], -escape_dir[1])
                made_turn = False
                
                for i in range(target_len - 1):
                    last_x, last_y = path[-1]
                    valid_dirs = []
                    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                        nx, ny = last_x + dx, last_y + dy
                        if (nx, ny) in shape_mask and (nx, ny) not in occupied and (nx, ny) not in temp_path_occupied:
                            if i == 0 and (dx, dy) == escape_dir: continue
                            valid_dirs.append((dx, dy))
                    if not valid_dirs: break
                    chosen_dir = None
                    if should_turn and not made_turn and i > 0:
                        turn_dirs = [d for d in valid_dirs if d[0] != curr_dir[0] and d[1] != curr_dir[1]]
                        if turn_dirs:
                            chosen_dir = random.choice(turn_dirs)
                            made_turn = True
                        else: chosen_dir = random.choice(valid_dirs)
                    else:
                        if curr_dir in valid_dirs and random.random() > 0.4: chosen_dir = curr_dir
                        else:
                            chosen_dir = random.choice(valid_dirs)
                            if chosen_dir != curr_dir: made_turn = True
                    curr_dir = chosen_dir
                    path.append((last_x + curr_dir[0], last_y + curr_dir[1]))
                    temp_path_occupied.add(path[-1])

                if len(path) >= 1: # Allow length 1
                    # SOLVER-VERIFIED GATEKEEPER
                    path_rev = list(reversed(path))
                    dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}
                    if len(path_rev) >= 2:
                        last_p, prev_p = path_rev[-1], path_rev[-2]
                        look_dir = dir_map.get((last_p[0]-prev_p[0], last_p[1]-prev_p[1]), dir_map[escape_dir])
                    else:
                        # Length 1 arrow: lookDirection is just escape_dir
                        look_dir = dir_map[escape_dir]
                    
                    new_arrow = {
                        "id": arrow_id,
                        "path": [{"x": p[0], "y": p[1]} for p in path_rev],
                        "lookDirection": look_dir
                    }
                    test_level = {
                        "gridSize": {"x": grid_width, "y": grid_height},
                        "arrows": arrows + [new_arrow]
                    }
                    
                    # Amortized solver state
                    test_occupied = occupied_with_ids.copy()
                    for p in path: test_occupied[p] = arrow_id
                    
                    if is_level_solvable(test_level, pre_occupied=test_occupied):
                        # Accept placement
                        avg_c = [sum(pixel_colors.get(p, (0,0,0,0))[c] for p in path)//len(path) for c in range(3)]
                        new_arrow["color"] = rgb_to_hex(avg_c)
                        arrows.append(new_arrow)
                        update_head_maps(new_arrow) # Maintain axial index
                        for p in path: 
                            occupied.add(p)
                            occupied_with_ids[p] = arrow_id
                            if p in free_points: free_points.remove(p)
                        # Update escape routes
                        ex, ey = head_node[0]+escape_dir[0], head_node[1]+escape_dir[1]
                        while 0 <= ex < grid_width and 0 <= ey < grid_height:
                            if (ex, ey) not in escape_routes: escape_routes[(ex, ey)] = set()
                            escape_routes[(ex, ey)].add(arrow_id)
                            ex += escape_dir[0]
                            ey += escape_dir[1]
                        arrow_id += 1
                        success_placement = True
                        if arrow_id % 20 == 0:
                            print(f"      - Arrows: {arrow_id}, Density: {len(occupied)/total_shape_points:.1%}, Iter: {iteration}")
                        break
        
        if not success_placement: 
            # Check for soft break
            if iteration > max_iters // 2 and current_density > 0.85:
                print(f"      - [TIMEOUT] Saving current density {current_density:.1%}")
                break
            continue
        
        if iteration % 100 == 0:
            print(f"      - Progress: {len(occupied)/total_shape_points:.1%}")

    final_density = len(occupied) / total_shape_points
    return {
        "gridSize": {"x": grid_width, "y": grid_height},
        "arrows": arrows,
        "density_success": final_density >= 0.901, # Above 90%
        "actual_density": final_density,
        "total_shape_points": total_shape_points
    }

def run_core_generator(image_path, grid_width, grid_height, config):
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    shape_mask = []
    pixel_colors = {}
    
    for y in range(grid_height):
        for x in range(grid_width):
            color = img.getpixel((x, y))
            r, g, b, a = color
            pixel_colors[(x, y)] = color
            is_transparent = a < config["ALPHA_THRESHOLD"]
            is_white = r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]
            if not is_transparent and not is_white:
                shape_mask.append((x, y))

    if not shape_mask:
        return None

    # Selection strategy
    if config.get("SORT_STRATEGY") == "seeds":
        num_seeds = random.randint(config["MIN_SEEDS"], config["MAX_SEEDS"])
        seeds = random.sample(shape_mask, min(num_seeds, len(shape_mask)))
        def get_priority(p):
            return min((p[0] - s[0])**2 + (p[1] - s[1])**2 for s in seeds)
    else: # Default behavior: start from center
        avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
        avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
        center = (avg_x, avg_y)
        def get_priority(p):
            return (p[0] - center[0])**2 + (p[1] - center[1])**2

    occupied_info = {} # (x, y) -> (dx, dy) of the arrow occupying it
    pixel_to_id = {}   # (x, y) -> arrow_id
    escape_routes = {} # (x, y) -> set of arrow_ids whose escape path passes through here
    adj = {}           # arrow_id -> set of arrow_ids it depends on
    id_to_dir = {}     # arrow_id -> (dx, dy)
    arrows = []
    arrow_id = 1

    remaining_points = set(shape_mask)
    
    while remaining_points:
        sorted_remaining = sorted(list(remaining_points), key=get_priority)
        start_node = sorted_remaining[0]
        
        success = False
        for _ in range(config["MAX_RETRY_ATTEMPTS"]):
            if random.random() < config["SHORT_PATH_PROBABILITY"]:
                target_length = random.randint(*config["SHORT_PATH_RANGE"])
            else:
                target_length = random.randint(*config["LONG_PATH_RANGE"])
                
            current_path = [start_node]
            temp_occupied = {start_node}
            current_direction = None
            
            for i in range(target_length - 1):
                last_x, last_y = current_path[-1]
                directions = [(1, 0), (-1, 0), (0, 1), (0, -1)]
                scored_neighbors = []
                for dx, dy in directions:
                    neighbor = (last_x + dx, last_y + dy)
                    if neighbor in remaining_points and neighbor not in temp_occupied:
                        score = 1.0
                        if current_direction:
                            score = 1.0 if (dx, dy) == current_direction else config["TURN_PROBABILITY"]
                        scored_neighbors.append((neighbor, score, (dx, dy)))
                
                if not scored_neighbors:
                    break
                
                scored_neighbors.sort(key=lambda x: x[1], reverse=True)
                best_score = scored_neighbors[0][1]
                top_candidates = [n for n in scored_neighbors if n[1] >= best_score * 0.9]
                next_node, _, chosen_dir = random.choice(top_candidates)
                current_path.append(next_node)
                temp_occupied.add(next_node)
                current_direction = chosen_dir
            
            if len(current_path) < 2:
                continue

            head_x, head_y = current_path[-1]
            prev_x, prev_y = current_path[-2]
            dx, dy = head_x - prev_x, head_y - prev_y

            is_self_blocked = False
            is_blocked_by_other = False
            blocker_dir_first = None
            target_ids = set()
            
            check_x, check_y = head_x + dx, head_y + dy
            while 0 <= check_x < grid_width and 0 <= check_y < grid_height:
                if (check_x, check_y) in temp_occupied:
                    is_self_blocked = True
                    break
                if (check_x, check_y) in pixel_to_id:
                    tid = pixel_to_id[(check_x, check_y)]
                    target_ids.add(tid)
                    if not is_blocked_by_other:
                        is_blocked_by_other = True
                        blocker_dir_first = occupied_info[(check_x, check_y)]
                check_x += dx
                check_y += dy
            
            if is_self_blocked:
                continue

            obstructed_ids = set()
            for p in current_path:
                if p in escape_routes:
                    obstructed_ids.update(escape_routes[p])

            has_cycle = False
            for t in target_ids:
                if t in obstructed_ids: 
                    has_cycle = True
                    break
                t_dir = id_to_dir.get(t)
                if t_dir and (dx * t_dir[0] + dy * t_dir[1] < 0): 
                    has_cycle = True
                    break
                if is_reachable(adj, t, obstructed_ids): 
                    has_cycle = True
                    break
            
            if has_cycle:
                continue

            blocks_same_dir = False
            for oid in obstructed_ids:
                if (dx, dy) == id_to_dir.get(oid):
                    blocks_same_dir = True
                    break
            if blocks_same_dir and _ < config["MAX_RETRY_ATTEMPTS"] - 10:
                continue

            target_blocked = random.random() < config.get("TARGET_BLOCKED_PROBABILITY", 0.8)
            if is_blocked_by_other:
                is_perp = (dx * blocker_dir_first[0] + dy * blocker_dir_first[1] == 0)
                is_same = (dx == blocker_dir_first[0] and dy == blocker_dir_first[1])
                if is_same and _ < config["MAX_RETRY_ATTEMPTS"] - 10:
                    continue
                if not is_perp and random.random() < config.get("PERPENDICULAR_PREFERENCE", 0.8):
                    if _ < config["MAX_RETRY_ATTEMPTS"] - 20:
                        continue
            else:
                if target_blocked and _ < config["MAX_RETRY_ATTEMPTS"] - 20:
                    continue

            best_dir = (dx, dy)
            avg_r = sum(pixel_colors[p][0] for p in current_path) // len(current_path)
            avg_g = sum(pixel_colors[p][1] for p in current_path) // len(current_path)
            avg_b = sum(pixel_colors[p][2] for p in current_path) // len(current_path)
            dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}
            arrow_obj = {
                "id": arrow_id,
                "color": rgb_to_hex((avg_r, avg_g, avg_b)),
                "path": [{"x": p[0], "y": p[1]} for p in current_path],
                "lookDirection": dir_map[best_dir]
            }
            arrows.append(arrow_obj)
            adj[arrow_id] = target_ids
            id_to_dir[arrow_id] = (dx, dy)
            for o in obstructed_ids:
                if o not in adj: adj[o] = set()
                adj[o].add(arrow_id)
            for p in current_path:
                occupied_info[p] = (dx, dy)
                pixel_to_id[p] = arrow_id
                remaining_points.remove(p)
            curr_ex, curr_ey = head_x + dx, head_y + dy
            while 0 <= curr_ex < grid_width and 0 <= curr_ey < grid_height:
                if (curr_ex, curr_ey) not in escape_routes:
                    escape_routes[(curr_ex, curr_ey)] = set()
                escape_routes[(curr_ex, curr_ey)].add(arrow_id)
                curr_ex += dx
                curr_ey += dy
            arrow_id += 1
            success = True
            break
        
        if not success:
            remaining_points.remove(start_node)

    return {
        "gridSize": {"x": grid_width, "y": grid_height},
        "arrows": arrows,
        "duration": len(occupied_info) * DURATION_MULTIPLIER
    }

def main():
    parser = argparse.ArgumentParser(description="Bulk generate AlgoArrows levels from a folder of images.")
    parser.add_argument("folder", help="Path to the folder containing source images")
    parser.add_argument("--min_width", type=int, default=20, help="Minimum grid width (default: 20)")
    parser.add_argument("--max_width", type=int, default=45, help="Maximum grid width (default: 45)")
    parser.add_argument("--difficulty", type=int, default=1, choices=[1, 2, 3], help="Level difficulty (1, 2, or 3)")
    
    args = parser.parse_args()
    source_folder = os.path.abspath(args.folder)
    if not os.path.isdir(source_folder):
        print(f"Error: {source_folder} is not a directory.")
        sys.exit(1)
        
    parent_dir = os.path.dirname(source_folder)
    output_folder = os.path.join(parent_dir, "GeneratedLevels")
    if not os.path.exists(output_folder):
        os.makedirs(output_folder)
    
    valid_extensions = ('.png', '.jpg', '.jpeg', '.bmp', '.gif')
    image_files = [f for f in os.listdir(source_folder) if f.lower().endswith(valid_extensions)]
    if not image_files:
        sys.exit(0)
        
    for img_name in image_files:
        img_path = os.path.join(source_folder, img_name)
        try:
            with Image.open(img_path) as img:
                orig_w, orig_h = img.size
            grid_width = random.randint(args.min_width, args.max_width)
            grid_height = int(grid_width * (orig_h / orig_w))
            grid_height = max(grid_height, 5)
            level_data = generate_level_json(img_path, grid_width, grid_height, difficulty=args.difficulty)
            if level_data:
                base_name = os.path.splitext(img_name)[0]
                output_file = os.path.join(output_folder, f"{base_name}.json")
                with open(output_file, 'w') as f:
                    json.dump(level_data, f, indent=2)
                print(f"Successfully saved {img_name}")
        except Exception as e:
            print(f"Error processing {img_name}: {e}")

if __name__ == "__main__":
    main()
