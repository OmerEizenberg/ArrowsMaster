import random
import os
import sys
import json
import argparse
import collections
import re
from PIL import Image

# Merge COMMON_CONFIG into DIFF_CONFIG so every function gets one complete config
DIFF_CONFIG = {
    "TARGET_DENSITY": 0.901,
    "MAX_RETRY_ATTEMPTS": 100,
    # Image parsing thresholds
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128,
    "BG_DISTANCE_THRESHOLD": 30
}

# Phase-based arrow length strategy: try Long first, then Mid, then Short.
# No probability rolls — each phase is exhausted before moving to the next.
LENGTH_PHASES = [
    (9, 25),  # Phase 1: Long
    (5, 12),  # Phase 2: Mid
    (2, 6),   # Phase 3: Short (gap filler)
]

DURATION_MULTIPLIER = 0.28

# --- FIXED LEVEL VALUES CONFIGURATION ---
FIXED_LEVEL_VALUES = {
    1: (31, 31), 2: (31, 31), 3: (34, 34), 4: (28, 28), 5: (31, 31), 6: (34, 34), 7: (31, 31), 8: (36, 36), 9: (28, 28), 10: (31, 31),
    11: (33, 33), 12: (33, 33), 13: (35, 35), 14: (29, 29), 15: (33, 33), 16: (35, 35), 17: (33, 33), 18: (38, 38), 19: (29, 29), 20: (33, 33),
    21: (34, 34), 22: (34, 34), 23: (38, 38), 24: (30, 30), 25: (34, 34), 26: (38, 38), 27: (34, 34), 28: (41, 41), 29: (30, 30), 30: (34, 34),
    31: (37, 37), 32: (37, 37), 33: (40, 40), 34: (32, 32), 35: (37, 37), 36: (40, 40), 37: (37, 37), 38: (44, 44), 39: (32, 32), 40: (37, 37),
    41: (38, 38), 42: (38, 38), 43: (42, 42), 44: (33, 33), 45: (38, 38), 46: (42, 42), 47: (38, 38), 48: (46, 46), 49: (33, 33), 50: (38, 38),
    51: (39, 40), 52: (40, 40), 53: (45, 45), 54: (34, 34), 55: (40, 40), 56: (45, 45), 57: (40, 40), 58: (49, 49), 59: (34, 34), 60: (40, 40),
    61: (40, 42), 62: (42, 42), 63: (47, 47), 64: (36, 36), 65: (42, 42), 66: (45, 47), 67: (42, 42), 68: (52, 52), 69: (36, 36), 70: (42, 42),
    71: (41, 44), 72: (44, 44), 73: (50, 50), 74: (37, 37), 75: (44, 44), 76: (49, 50), 77: (43, 44), 78: (55, 55), 79: (37, 37), 80: (44, 44),
    81: (42, 46), 82: (46, 46), 83: (51, 51), 84: (38, 38), 85: (46, 46), 86: (50, 51), 87: (44, 46), 88: (57, 57), 89: (38, 38), 90: (46, 46),
    91: (43, 48), 92: (48, 48), 93: (54, 54), 94: (40, 40), 95: (48, 48), 96: (51, 54), 97: (45, 48), 98: (60, 60), 99: (40, 40), 100: (48, 48),
}

DEFAULT_WIDTH_RANGE = (20, 45)

# --- HARDCODED PALETTE ---
# A broad set of distinct colors to quantize the source image into sections.
# Each pixel in the source image will be snapped to its closest color here.
PALETTE_HEXES = [
    "#000000",  # Black
    "#ffffff",  # White
    "#ff0000",  # Red
    "#00ff00",  # Green
    "#0000ff",  # Blue
    "#ffff00",  # Yellow
    "#ff6600",  # Orange
    "#ff00ff",  # Magenta
    "#00ffff",  # Cyan
    "#800000",  # Dark Red
    "#008000",  # Dark Green
    "#000080",  # Dark Blue (Navy)
    "#808000",  # Olive
    "#800080",  # Purple
    "#008080",  # Teal
    "#c0c0c0",  # Silver
    "#808080",  # Gray
    "#ffc0cb",  # Pink
    "#ffd700",  # Gold
    "#a52a2a",  # Brown
    "#40e0d0",  # Turquoise
    "#ee82ee",  # Violet
    "#f5deb3",  # Wheat
    "#4b0082",  # Indigo
    "#ff69b4",  # Hot Pink
    "#7fff00",  # Chartreuse
    "#dc143c",  # Crimson
    "#1e90ff",  # Dodger Blue
    "#228b22",  # Forest Green
    "#ff8c00",  # Dark Orange
]

# Convert to RGB tuples once at module load
PALETTE_RGBS = []
for _h in PALETTE_HEXES:
    _h = _h.lstrip('#')
    PALETTE_RGBS.append(tuple(int(_h[i:i+2], 16) for i in (0, 2, 4)))

# --- UTILS ---
def hex_to_rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i+2], 16) for i in (0, 2, 4))

def rgb_to_hex(rgb):
    """Always outputs a fully-opaque 6-digit hex color string."""
    return '#{:02x}{:02x}{:02x}'.format(int(rgb[0]), int(rgb[1]), int(rgb[2]))

def get_nearest_color(color_rgb, palette_rgbs):
    min_dist = float('inf')
    best_color = palette_rgbs[0]
    for p_rgb in palette_rgbs:
        dist = sum((color_rgb[i] - p_rgb[i])**2 for i in range(3))
        if dist < min_dist:
            min_dist = dist
            best_color = p_rgb
    return best_color

def get_image_data_quantized(img, config, palette_rgbs):
    gw, gh = img.size
    shape_mask = set()
    pixel_colors = {}
    
    edge_pixels = []
    for x in range(gw):
        edge_pixels.append(img.getpixel((x, 0)))
        edge_pixels.append(img.getpixel((x, gh - 1)))
    for y in range(gh):
        edge_pixels.append(img.getpixel((0, y)))
        edge_pixels.append(img.getpixel((gw - 1, y)))
    
    opaque_edge_colors = [p[:3] for p in edge_pixels if p[3] >= config["ALPHA_THRESHOLD"]]
    main_bg_color = None
    if opaque_edge_colors:
        main_bg_color = collections.Counter(opaque_edge_colors).most_common(1)[0][0]
    
    bg_dist = config.get("BG_DISTANCE_THRESHOLD", 30)
    main_bg_is_white = False
    if main_bg_color:
        main_bg_is_white = all(c > config["WHITE_THRESHOLD"] for c in main_bg_color)
    
    for y in range(gh):
        for x in range(gw):
            color = img.getpixel((x, y))
            r, g, b, a = color
            # Quantize color to palette — store ONLY RGB (no alpha), always fully opaque
            nearest_rgb = get_nearest_color((r, g, b), palette_rgbs)
            pixel_colors[(x, y)] = nearest_rgb  # Pure (R, G, B) tuple — no alpha
            
            if a < config["ALPHA_THRESHOLD"]: continue
            is_bg = False
            if main_bg_color:
                dist = sum(abs(color[i] - main_bg_color[i]) for i in range(3))
                if dist < bg_dist: is_bg = True
            if not is_bg and (not main_bg_color or main_bg_is_white):
                if r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]:
                    is_bg = True
            if not is_bg:
                shape_mask.add((x, y))
                
    return shape_mask, pixel_colors

def is_level_solvable(level_data, pre_occupied=None):
    grid_size = level_data["gridSize"]
    gw, gh = grid_size["x"], grid_size["y"]
    id_to_arrow = {a["id"]: a for a in level_data["arrows"]}
    
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
            if (cx, cy) in occupied: return False
            cx += dx
            cy += dy
        return True

    ready = {aid for aid in id_to_arrow if check_arrow(aid)}
    removed_count = 0
    total = len(id_to_arrow)
    handled = set()
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
        points = id_to_arrow[aid]["path"]
        affected_rows = set()
        affected_cols = set()
        for p in points:
            px, py = p["x"], p["y"]
            occupied.pop((px, py), None)
            affected_rows.add(py)
            affected_cols.add(px)
        for r in affected_rows:
            for rid in row_map[r]:
                if rid not in handled and rid not in ready:
                    if check_arrow(rid): ready.add(rid)
        for c in affected_cols:
            for rid in col_map[c]:
                if rid not in handled and rid not in ready:
                    if check_arrow(rid): ready.add(rid)
            
    return removed_count == total

def section_coverage_score(candidate_pos, shape_mask, occupied_set, pixel_colors, section_color, temp_path_set):
    """
    Greedy coverage heuristic: count free same-section neighbors of a candidate position.
    A higher score means this direction opens up more of the section for future growth.
    """
    cx, cy = candidate_pos
    score = 0
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        nx, ny = cx + dx, cy + dy
        if ((nx, ny) in shape_mask
                and (nx, ny) not in occupied_set
                and (nx, ny) not in temp_path_set
                and pixel_colors.get((nx, ny)) == section_color):
            score += 1
    return score

def run_reverse_generator_with_sections(image_path, grid_width, grid_height, config, palette_rgbs):
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    shape_mask, pixel_colors = get_image_data_quantized(img, config, palette_rgbs)

    if not shape_mask: return None

    occupied = set()
    occupied_with_ids = {}
    escape_routes = {}
    arrows = []
    arrow_id = 1
    total_shape_points = len(shape_mask)
    target_points = int(total_shape_points * config["TARGET_DENSITY"])
    free_points = set(shape_mask)

    dist_to_bounds = {}
    for p in shape_mask:
        px, py = p
        dist_to_bounds[p] = {
            (1, 0): grid_width - 1 - px, (-1, 0): px,
            (0, 1): grid_height - 1 - py, (0, -1): py
        }

    heads_by_row = collections.defaultdict(list)
    heads_by_col = collections.defaultdict(list)

    def update_head_maps(arrow):
        aid = arrow["id"]
        h = arrow["path"][-1]
        ldir = arrow["lookDirection"]
        heads_by_row[h["y"]].append((h["x"], aid, ldir))
        heads_by_col[h["x"]].append((h["y"], aid, ldir))

    inv_map = {"right": (1, 0), "left": (-1, 0), "up": (0, 1), "down": (0, -1)}
    dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}

    def build_candidates():
        """Build list of (head_node, escape_dir, priority) for all valid free points."""
        cands = []
        for p in free_points:
            px, py = p
            row_heads = heads_by_row.get(py, [])
            col_heads = heads_by_col.get(px, [])
            for edir, dist_to_boundary in dist_to_bounds[p].items():
                is_mutual = False
                for hx, _, ldir in row_heads:
                    odx, ody = inv_map[ldir]
                    if odx == 0: continue
                    if (px - hx) * odx > 0 and edir[0] != 0 and (hx - px) * edir[0] > 0:
                        is_mutual = True; break
                if is_mutual: continue
                for hy, _, ldir in col_heads:
                    odx, ody = inv_map[ldir]
                    if ody == 0: continue
                    if (py - hy) * ody > 0 and edir[1] != 0 and (hy - py) * edir[1] > 0:
                        is_mutual = True; break
                if is_mutual: continue
                priority = dist_to_boundary + len(escape_routes.get(p, set())) * 40
                cands.append((p, edir, priority))
        return cands

    def try_place_arrow(head_node, escape_dir, phase_min, phase_max):
        """Try to grow and place an arrow from head_node with length in [phase_min, phase_max].
        Uses greedy coverage direction selection (no turn probability).
        Returns True if an arrow was successfully placed."""
        nonlocal arrow_id
        section_color_rgb = pixel_colors[head_node]

        for target_len in range(phase_max, phase_min - 1, -1):  # try longest first
            path = [head_node]
            temp_path_occupied = {head_node}
            curr_dir = (-escape_dir[0], -escape_dir[1])

            for i in range(target_len - 1):
                last_x, last_y = path[-1]
                # Collect valid same-section neighbors
                valid_dirs = []
                for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                    nx, ny = last_x + dx, last_y + dy
                    if ((nx, ny) in shape_mask
                            and (nx, ny) not in occupied
                            and (nx, ny) not in temp_path_occupied
                            and pixel_colors.get((nx, ny)) == section_color_rgb):
                        if i == 0 and (dx, dy) == escape_dir: continue  # can't go back toward escape
                        valid_dirs.append((dx, dy))
                if not valid_dirs: break

                # --- GREEDY COVERAGE DIRECTION SELECTION ---
                # Score each direction by how many free same-section cells its
                # destination has adjacent to it. Prefer the direction that
                # keeps the most space open (maximises future coverage).
                scored = []
                for d in valid_dirs:
                    nx, ny = last_x + d[0], last_y + d[1]
                    score = section_coverage_score(
                        (nx, ny), shape_mask, occupied, pixel_colors,
                        section_color_rgb, temp_path_occupied
                    )
                    scored.append((d, score))
                scored.sort(key=lambda x: x[1], reverse=True)
                best_score = scored[0][1]
                top_dirs = [d for d, s in scored if s == best_score]
                chosen_dir = random.choice(top_dirs)  # random tiebreak only
                curr_dir = chosen_dir
                path.append((last_x + curr_dir[0], last_y + curr_dir[1]))
                temp_path_occupied.add(path[-1])

            if len(path) < 2: continue

            # Self-aim check
            aim_x, aim_y = path[0][0] + escape_dir[0], path[0][1] + escape_dir[1]
            is_self_aiming = False
            while 0 <= aim_x < grid_width and 0 <= aim_y < grid_height:
                if (aim_x, aim_y) in temp_path_occupied: is_self_aiming = True; break
                aim_x += escape_dir[0]; aim_y += escape_dir[1]
            if is_self_aiming: continue

            path_rev = list(reversed(path))
            if len(path_rev) >= 2:
                last_p, prev_p = path_rev[-1], path_rev[-2]
                look_dir = dir_map.get((last_p[0]-prev_p[0], last_p[1]-prev_p[1]), dir_map[escape_dir])
            else:
                look_dir = dir_map[escape_dir]

            new_arrow = {
                "id": arrow_id, "color": rgb_to_hex(section_color_rgb),
                "path": [{"x": p[0], "y": p[1]} for p in path_rev],
                "lookDirection": look_dir
            }
            test_level = {"gridSize": {"x": grid_width, "y": grid_height}, "arrows": arrows + [new_arrow]}
            test_occupied = occupied_with_ids.copy()
            for p in path: test_occupied[p] = arrow_id

            if is_level_solvable(test_level, pre_occupied=test_occupied):
                arrows.append(new_arrow)
                update_head_maps(new_arrow)
                for p in path:
                    occupied.add(p); occupied_with_ids[p] = arrow_id
                    if p in free_points: free_points.remove(p)
                ex, ey = head_node[0]+escape_dir[0], head_node[1]+escape_dir[1]
                while 0 <= ex < grid_width and 0 <= ey < grid_height:
                    if (ex, ey) not in escape_routes: escape_routes[(ex, ey)] = set()
                    escape_routes[(ex, ey)].add(arrow_id)
                    ex += escape_dir[0]; ey += escape_dir[1]
                arrow_id += 1
                return True
        return False

    # ---- PHASE-BASED MAIN LOOP ----
    # Phase 1: Long  (9-22)  → Phase 2: Mid (5-12)  → Phase 3: Short (2-6)
    # A phase ends when a full pass over all candidates yields zero successful placements.
    for phase_idx, (phase_min, phase_max) in enumerate(LENGTH_PHASES):
        phase_names = ["Long", "Mid", "Short"]
        print(f"    Phase {phase_idx+1} ({phase_names[phase_idx]}, len {phase_min}-{phase_max})...")
        while len(occupied) < target_points:
            candidates = build_candidates()
            if not candidates: break
            candidates.sort(key=lambda x: x[2], reverse=True)
            # Try the top pool of candidates
            top_pool = candidates[:max(1, len(candidates) // 5)]
            random.shuffle(top_pool)
            placed_this_pass = False
            for head_node, escape_dir, _ in top_pool:
                if try_place_arrow(head_node, escape_dir, phase_min, phase_max):
                    placed_this_pass = True
                    break  # rebuild candidates fresh after each placement
            if not placed_this_pass:
                break  # this phase is exhausted — move to next

    return {
        "gridSize": {"x": grid_width, "y": grid_height}, "arrows": arrows,
        "total_shape_points": total_shape_points, "pixel_colors": pixel_colors, "shape_mask": shape_mask
    }

def post_process_fill_gaps_sections(level_data, config):
    pixel_colors = level_data["pixel_colors"]
    shape_mask = level_data["shape_mask"]
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    occupied = {(p["x"], p["y"]): a["id"] for a in level_data["arrows"] for p in a["path"]}
    free_points = [p for p in shape_mask if p not in occupied]
    random.shuffle(free_points)
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}
    
    for start_node in free_points:
        if start_node in occupied: continue
        # pixel_colors stores pure (R,G,B); no [:3] slice needed
        section_rgb = pixel_colors[start_node]
        for _t in range(5):
            target_length = random.randint(2, 6)
            path, temp_path_set, curr_dir = [start_node], {start_node}, None
            for i in range(target_length - 1):
                last_x, last_y = path[-1]
                valid_dirs = []
                for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                    nx, ny = last_x + dx, last_y + dy
                    if (nx, ny) in shape_mask and (nx, ny) not in occupied and (nx, ny) not in temp_path_set:
                        if pixel_colors[(nx, ny)] == section_rgb:
                            valid_dirs.append((dx, dy))
                if not valid_dirs: break
                chosen_dir = curr_dir if curr_dir in valid_dirs and random.random() > 0.2 else random.choice(valid_dirs)
                path.append((last_x + chosen_dir[0], last_y + chosen_dir[1]))
                temp_path_set.add(path[-1]); curr_dir = chosen_dir
            if len(path) < 2: continue
            head, prev = path[-1], path[-2]
            look_dx, look_dy = head[0] - prev[0], head[1] - prev[1]
            aim_x, aim_y = head[0] + look_dx, head[1] + look_dy
            is_self_aiming = False
            while 0 <= aim_x < gw and 0 <= aim_y < gh:
                if (aim_x, aim_y) in temp_path_set: is_self_aiming = True; break
                aim_x += look_dx; aim_y += look_dy
            if is_self_aiming: continue
            new_arrow = {
                "id": next_id, "color": rgb_to_hex(section_rgb),
                "path": [{"x": p[0], "y": p[1]} for p in path],
                "lookDirection": dir_map[(look_dx, look_dy)]
            }
            test_occupied = occupied.copy()
            for p in path: test_occupied[p] = next_id
            if is_level_solvable({"gridSize": level_data["gridSize"], "arrows": level_data["arrows"] + [new_arrow]}, pre_occupied=test_occupied):
                level_data["arrows"].append(new_arrow)
                for p in path: occupied[p] = next_id
                next_id += 1; break
    return level_data

def merge_stuck_arrows_sections(level_data):
    arrows = level_data.get("arrows", [])
    dir_vecs = {"up": (0, 1), "down": (0, -1), "left": (-1, 0), "right": (1, 0)}
    changed = True
    while changed:
        changed = False
        point_to_arrow_idx = {(p["x"], p["y"]): idx for idx, a in enumerate(arrows) for p in a["path"]}
        merged_any, to_remove = False, set()
        for i in range(len(arrows)):
            if i in to_remove: continue
            a = arrows[i]
            if len(a["path"]) >= 4: continue
            head, ldir = a["path"][-1], a["lookDirection"]
            dx, dy = dir_vecs[ldir]
            tx, ty = head["x"] + dx, head["y"] + dy
            if (tx, ty) in point_to_arrow_idx:
                target_idx = point_to_arrow_idx[(tx, ty)]
                if target_idx != i and target_idx not in to_remove:
                    b = arrows[target_idx]
                    # Merging Constraint: Must be SAME color (section)
                    if a["color"] == b["color"]:
                        tail_b = b["path"][0]
                        if tail_b["x"] == tx and tail_b["y"] == ty and len(b["path"]) < 4:
                            b["path"] = a["path"] + b["path"]
                            to_remove.add(i); merged_any = True; changed = True; break
        if merged_any:
            arrows = [a for idx, a in enumerate(arrows) if idx not in to_remove]
            level_data["arrows"] = arrows
    return level_data

def generate_palette_level(image_path, grid_width, grid_height):
    """
    Generates a palette-section level using the hardcoded PALETTE_RGBS.
    Mirrors the Difficulty 3 approach from batch_gen_fixed_values:
      - Up to 50 attempts per image.
      - Each attempt: generate → filter short arrows → fill gaps (section-aware)
        → merge (section-aware, same-color only).
      - Accepts only if density >= TARGET_DENSITY AND level is fully solvable.
      - Outputs a clean JSON matching the standard format:
        { "gridSize": {...}, "arrows": [{id, color, path, lookDirection}, ...], "duration": float }
    """
    for attempt in range(50):
        level_data = run_reverse_generator_with_sections(
            image_path, grid_width, grid_height, DIFF_CONFIG, PALETTE_RGBS
        )
        if not level_data:
            print(f"  Attempt {attempt+1}: Generator returned nothing. Retrying...")
            continue

        # Step 1: Remove arrows with fewer than 2 path points
        level_data["arrows"] = [a for a in level_data["arrows"] if len(a["path"]) >= 2]

        # Step 2: Fill remaining empty shape areas (section-aware, section-colored)
        level_data = post_process_fill_gaps_sections(level_data, DIFF_CONFIG)

        # Step 3: Merge stuck small arrows (only within same-color section)
        level_data = merge_stuck_arrows_sections(level_data)

        # Step 4: Verify density and full solvability
        occupied_points = sum(len(a["path"]) for a in level_data["arrows"])
        total_shape_points = level_data.get("total_shape_points", 1)
        final_density = occupied_points / total_shape_points

        # Build a clean copy for the solver check (no internal keys)
        clean_for_solver = {
            "gridSize": level_data["gridSize"],
            "arrows": level_data["arrows"]
        }

        if final_density >= DIFF_CONFIG["TARGET_DENSITY"] and is_level_solvable(clean_for_solver):
            print(f"  Attempt {attempt+1}: PASSED. Density={final_density:.1%}. Saving.")
            level_data["duration"] = max(30, occupied_points * DURATION_MULTIPLIER)
            # Produce clean output JSON — only standard fields
            return {
                "gridSize": level_data["gridSize"],
                "arrows": level_data["arrows"],
                "duration": level_data["duration"]
            }
        else:
            reason = "SOLVER FAILED" if final_density >= DIFF_CONFIG["TARGET_DENSITY"] else "DENSITY LOW"
            print(f"  Attempt {attempt+1}: {reason} ({final_density:.1%}). Retrying...")

    print(f"  All 50 attempts failed for {image_path}.")
    return None

def get_width_range_for_level(filename):
    match = re.search(r'\d+', filename)
    if not match: return DEFAULT_WIDTH_RANGE
    return FIXED_LEVEL_VALUES.get(int(match.group()), DEFAULT_WIDTH_RANGE)

def main():
    parser = argparse.ArgumentParser(description="Section-based level generator using color quantization with hardcoded palette.")
    parser.add_argument("input_path", help="Source images folder/file")
    
    args = parser.parse_args()
    print(f"Using hardcoded palette with {len(PALETTE_RGBS)} colors.")
    input_path = os.path.abspath(args.input_path)
    
    image_files = []
    if os.path.isfile(input_path):
        source_folder = os.path.dirname(input_path)
        image_files = [os.path.basename(input_path)]
    else:
        source_folder = input_path
        image_files = [f for f in os.listdir(source_folder) if f.lower().endswith(('.png', '.jpg', '.jpeg'))]

    output_folder = os.path.join(os.path.dirname(source_folder), "GeneratedLevelsFixed")
    if not os.path.exists(output_folder): os.makedirs(output_folder)
    
    for img_name in image_files:
        img_path = os.path.join(source_folder, img_name)
        try:
            with Image.open(img_path) as img: orig_w, orig_h = img.size
            min_w, max_w = get_width_range_for_level(img_name)
            target = random.randint(min_w, max_w)
            if orig_w >= orig_h: grid_w = target; grid_h = int(grid_w * (orig_h / orig_w))
            else: grid_h = target; grid_w = int(grid_h * (orig_w / orig_h))
            
            print(f"Generating section-level for {img_name} ({grid_w}x{grid_h})...")
            level_data = generate_palette_level(img_path, max(grid_w, 5), max(grid_h, 5))
            if level_data:
                output_file = os.path.join(output_folder, f"{os.path.splitext(img_name)[0]}.json")
                with open(output_file, 'w') as f: json.dump(level_data, f, indent=2)
                print(f"Saved: {output_file}")
        except Exception as e: print(f"Error {img_name}: {e}")

if __name__ == "__main__":
    main()
