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
    "TARGET_DENSITY": 0.8401,
    "MAX_RETRY_ATTEMPTS": 100,
    # Image parsing thresholds
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128,
    "BG_DISTANCE_THRESHOLD": 30
}

# Phase-based arrow length strategy: try Long first, then Mid, then Short.
# No probability rolls — each phase is exhausted before moving to the next.
LENGTH_PHASES = [
    (12, 30),  # Phase 1: Long
    (4, 12),  # Phase 2: Mid
    (2, 4),   # Phase 3: Short (gap filler)
]

# Momentum bias: probability of continuing in the same direction during path growth.
# Higher = fewer turns, straighter arrows.
MOMENTUM_BIAS = 0.80

# How many consecutive empty passes before a phase is considered exhausted.
PHASE_PATIENCE = 10

DURATION_MULTIPLIER = 0.28

# --- ARROW WIDTH ---
# Set to a float (e.g. 0.3) to embed arrowWidth in every generated arrow's JSON.
# Set to None to omit the field and use the game's default width (0.2f).
ARROW_WIDTH = 0.32  # e.g. 0.3

def _arrow_width_field():
    """Returns a dict with the arrowWidth field if ARROW_WIDTH is set, else empty dict."""
    return {"arrowWidth": ARROW_WIDTH} if ARROW_WIDTH is not None else {}

# --- FIXED LEVEL VALUES CONFIGURATION ---
FIXED_LEVEL_VALUES = {
    1: (31, 31), 2: (31, 31), 3: (34, 34), 4: (28, 28), 5: (31, 31), 6: (34, 34), 7: (31, 31), 8: (28, 28), 9: (28, 28), 10: (28, 28),
    11: (28, 28), 12: (26, 26), 13: (35, 35), 14: (29, 29), 15: (33, 33), 16: (35, 35), 17: (33, 33), 18: (38, 38), 19: (29, 29), 20: (33, 33),
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
    "#1e272e",  # Black +
    "#fc5c65",  # Red +
    "#26de81",  # Green +
    "#4b7bec",  # Blue +
    "#fed330",  # Yellow +
    "#fa8231",  # Orange +
    "#eb3b5a",  # Dark Red +
    "#20bf6b",  # Dark Green +
    "#1B1464",  # Dark Blue (Navy) +
    "#A3CB38",  # Olive +
    "#c56cf0",  # Purple +
    "#4b4b4b",  # Black white+
    "#808e9b",  # Silver +
    "#a5b1c2",  # Gray +
    "#ef5777",  # Pink +
    "#f78fb3",  # Pink +
    "#f7b731",  # Gold +
    "#2bcbba",  # Turquoise +
    "#f5cd79",  # Wheat +
    "#a55eea",  # Indigo +
    "#4b6584",  #  +
    "#009432",  # Forest Green + 
    "#f0932b",  # Dark Orange +
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

def get_dir_name(dx, dy):
    if dx == 0 and dy > 0: return "up"
    if dx == 0 and dy < 0: return "down"
    if dx < 0 and dy == 0: return "left"
    if dx > 0 and dy == 0: return "right"
    # Fallback to horizontal or vertical for slight diagonals if they ever occur
    if abs(dx) > abs(dy):
        return "right" if dx > 0 else "left"
    else:
        return "up" if dy > 0 else "down"

def finalize_arrow_directions(arrows):
    for a in arrows:
        if len(a["path"]) >= 2:
            h = a["path"][-1]
            p = a["path"][-2]
            a["lookDirection"] = get_dir_name(h["x"] - p["x"], h["y"] - p["y"])

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
            
            # Rule 1: STRICT white detection (ignore 'total white' sections)
            if not is_bg and (r > 242 and g > 242 and b > 242):
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

def get_wall_count(pos, shape_mask, occupied_set, pixel_colors, section_color, temp_path_set=None):
    """
    Counts how many neighbors of 'pos' are 'walls'.
    A wall is a cell that is:
    - Outside of the shape_mask
    - A different color from 'section_color'
    - Already occupied or in the current temp_path
    Higher wall counts mean the cell is at an edge or corner.
    """
    cx, cy = pos
    wall_count = 0
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        nx, ny = cx + dx, cy + dy
        is_free_same_color = (
            (nx, ny) in shape_mask and 
            (nx, ny) not in occupied_set and 
            (temp_path_set is None or (nx, ny) not in temp_path_set) and 
            pixel_colors.get((nx, ny)) == section_color
        )
        if not is_free_same_color:
            wall_count += 1
    return wall_count

def get_doe(pos, gw, gh, occupied_set, extra_temp_set=None):
    """
    Calculates Degree of Escape: how many rays (Up, Down, Left, Right) 
    from 'pos' can exit the grid without hitting an occupied cell.
    """
    doe = 0
    dirs = [(0, 1), (0, -1), (1, 0), (-1, 0)]
    for dx, dy in dirs:
        cx, cy = pos[0]+dx, pos[1]+dy
        blocked = False
        while 0 <= cx < gw and 0 <= cy < gh:
            if (cx, cy) in occupied_set:
                blocked = True; break
            if extra_temp_set and (cx, cy) in extra_temp_set:
                blocked = True; break
            cx += dx; cy += dy
        if not blocked:
            doe += 1
    return doe

def section_wall_hugging_score(pos, shape_mask, occupied_set, pixel_colors, section_color, temp_path_set):
    """
    Score a candidate point by its 'wall' adjacency to encourage filling edges first.
    Includes a one-step lookahead: also considers the walliness of neighbors.
    """
    score = get_wall_count(pos, shape_mask, occupied_set, pixel_colors, section_color, temp_path_set) * 10
    
    # Lookahead: sum wall count of neighbors to further bias towards perimeter crawling
    for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        nx, ny = pos[0] + dx, pos[1] + dy
        if ((nx, ny) in shape_mask and 
            (nx, ny) not in occupied_set and 
            (nx, ny) not in temp_path_set and 
            pixel_colors.get((nx, ny)) == section_color):
            score += get_wall_count((nx, ny), shape_mask, occupied_set, pixel_colors, section_color, temp_path_set)
            
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

    # --- Rule 3: Merge small sections (< 7 points) ---
    while True:
        changed = False
        visited = set()
        for p in list(shape_mask):
            if p in visited: continue
            color = pixel_colors[p]
            comp = []
            q = collections.deque([p])
            visited.add(p)
            while q:
                curr = q.popleft()
                comp.append(curr)
                for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                    nx, ny = curr[0]+dx, curr[1]+dy
                    if (nx, ny) in shape_mask and (nx, ny) not in visited and pixel_colors.get((nx, ny)) == color:
                        visited.add((nx, ny))
                        q.append((nx, ny))
            if len(comp) < 7:
                # Find neighbors to absorb color from
                neighbor_colors = []
                for cp in comp:
                    for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                        nx, ny = cp[0]+dx, cp[1]+dy
                        if (nx, ny) in shape_mask and pixel_colors.get((nx, ny)) != color:
                            neighbor_colors.append(pixel_colors[(nx, ny)])
                if neighbor_colors:
                    best_color = collections.Counter(neighbor_colors).most_common(1)[0][0]
                    for cp in comp: pixel_colors[cp] = best_color
                    changed = True
                else:
                    # Isolated tiny island - remove it
                    for cp in comp: shape_mask.remove(cp)
                    changed = True
                if changed: break
        if not changed: break

    occupied = set()
    occupied_with_ids = {}
    escape_routes = {}
    arrows = []
    arrow_id = 1
    total_shape_points = len(shape_mask)
    target_points = int(total_shape_points * config["TARGET_DENSITY"])
    free_points = set(shape_mask)
    sweep_side = random.choice(["left", "right", "top", "bottom"])

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
                # --- SWEEP BIAS ---
                side_bias = 0
                if sweep_side == "left": side_bias = (grid_width - px) * 10
                elif sweep_side == "right": side_bias = px * 10
                elif sweep_side == "top": side_bias = py * 10
                elif sweep_side == "bottom": side_bias = (grid_height - py) * 10

                dir_bias = 0
                if sweep_side == "left" and edir == (-1, 0): dir_bias = 150
                elif sweep_side == "right" and edir == (1, 0): dir_bias = 150
                elif sweep_side == "top" and edir == (0, 1): dir_bias = 150
                elif sweep_side == "bottom" and edir == (0, -1): dir_bias = 150

                # --- DOE (Degree of Escape) ---
                # A cell with DoE=0 is already trapped.
                # A cell with DoE=1 is CRITICAL (must be filled in that one direction).
                doe = get_doe(p, grid_width, grid_height, occupied)
                if doe == 0: continue
                
                # --- SHADOW BIAS ---
                # Reward being adjacent to another same-colored arrow
                shadow_bias = 0
                for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                    nx, ny = p[0]+dx, p[1]+dy
                    if (nx, ny) in occupied_with_ids:
                        target_aid = occupied_with_ids[(nx, ny)]
                        # Look up color of existing arrow
                        for ex_arrow in arrows:
                            if ex_arrow["id"] == target_aid and ex_arrow["color"] == rgb_to_hex(pixel_colors[p]):
                                shadow_bias += 100; break
                # Priority: DoE ASCENDING (fill critical cells first), bias DESCENDING
                doe_score = (5 - doe) * 500  # huge boost for low DoE

                # --- WALL HUGGING BIAS ---
                wall_bias = get_wall_count(p, shape_mask, occupied, pixel_colors, pixel_colors[p]) * 50

                priority = dist_to_boundary + len(escape_routes.get(p, set())) * 40 + side_bias + dir_bias + wall_bias + doe_score + shadow_bias
                cands.append((p, edir, priority))
        return cands

    def try_place_arrow(head_node, escape_dir, phase_min, phase_max):
        """Try to grow and place an arrow from head_node with length in [phase_min, phase_max].
        Uses 2-depth greedy coverage + momentum bias to produce fewer turns.
        Returns True if an arrow was successfully placed."""
        nonlocal arrow_id
        section_color_rgb = pixel_colors[head_node]

        for target_len in range(phase_max, phase_min - 1, -1):  # try longest first
            path = [head_node]
            temp_path_occupied = {head_node}
            curr_dir = (-escape_dir[0], -escape_dir[1])  # initial momentum away from escape

            for i in range(target_len - 1):
                last_x, last_y = path[-1]
                valid_dirs = []
                for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
                    nx, ny = last_x + dx, last_y + dy
                    if ((nx, ny) in shape_mask
                            and (nx, ny) not in occupied
                            and (nx, ny) not in temp_path_occupied
                            and pixel_colors.get((nx, ny)) == section_color_rgb):
                        if i == 0 and (dx, dy) == escape_dir: continue
                        valid_dirs.append((dx, dy))
                if not valid_dirs: break

                # --- MOMENTUM BIAS: strongly prefer continuing in curr_dir ---
                # This reduces the number of turns in the generated arrows.
                if curr_dir in valid_dirs and random.random() < MOMENTUM_BIAS:
                    chosen_dir = curr_dir
                else:
                    # Fall back to wall-hugging + trap-prevention greedy
                    scored = []
                    for d in valid_dirs:
                        nx, ny = last_x + d[0], last_y + d[1]
                        
                        # Wall score
                        w_score = section_wall_hugging_score(
                            (nx, ny), shape_mask, occupied, pixel_colors,
                            section_color_rgb, temp_path_occupied
                        )
                        
                        # Trap prevention: count neighbors that become trapped if we move here
                        trapped_penalty = 0
                        for tdx, tdy in [(1,0),(-1,0),(0,1),(0,-1)]:
                            nnx, nny = nx+tdx, ny+tdy
                            if ((nnx, nny) in shape_mask and (nnx, nny) not in occupied and (nnx, nny) not in temp_path_occupied and (nnx, nny) != (nx, ny)):
                                if get_doe((nnx, nny), grid_width, grid_height, occupied, extra_temp_set=temp_path_occupied.union({(nx, ny)})) == 0:
                                    trapped_penalty += 200 # Heavy penalty for creating holes
                                    
                        # Shadow score (parallelism)
                        s_score = 0
                        for sdx, sdy in [(1,0),(-1,0),(0,1),(0,-1)]:
                            snx, sny = nx+sdx, ny+sdy
                            if (snx, sny) in occupied_with_ids:
                                target_aid = occupied_with_ids[(snx, sny)]
                                for ex_arrow in arrows:
                                    if ex_arrow["id"] == target_aid and ex_arrow["color"] == rgb_to_hex(section_color_rgb):
                                        s_score += 50; break

                        scored.append((d, w_score - trapped_penalty + s_score))
                    
                    scored.sort(key=lambda x: x[1], reverse=True)
                    best_score = scored[0][1]
                    top_dirs = [d for d, s in scored if s == best_score]
                    chosen_dir = random.choice(top_dirs)

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
                look_dir = get_dir_name(last_p[0]-prev_p[0], last_p[1]-prev_p[1])
            else:
                look_dir = get_dir_name(escape_dir[0], escape_dir[1])

            new_arrow = {
                "id": arrow_id, "color": rgb_to_hex(section_color_rgb),
                **_arrow_width_field(),
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

    # Phase 1: Long → Phase 2: Mid → Phase 3: Short
    # Dynamic lengths relative to grid size — Rule 2: min length 3
    grid_diagonal = int((grid_width**2 + grid_height**2)**0.5)
    L1 = max(10, int(grid_diagonal * 0.4))
    L2 = max(6, int(grid_diagonal * 0.2))
    
    DYNAMIC_PHASES = [
        (L1, L1 * 2),
        (L2, L1),
        (3, L2), # Start at 3 points
    ]

    for phase_idx, (phase_min, phase_max) in enumerate(DYNAMIC_PHASES):
        phase_names = ["Long", "Mid", "Short"]
        print(f"    Phase {phase_idx+1} ({phase_names[phase_idx]}, len {phase_min}-{phase_max})...")
        consecutive_empty = 0
        while len(occupied) < target_points:
            candidates = build_candidates()
            if not candidates: break
            # Sort by priority (high = better) then shuffle same-priority clusters
            candidates.sort(key=lambda x: x[2], reverse=True)
            placed_this_pass = False
            for head_node, escape_dir, _ in candidates:
                if head_node not in free_points: continue  # may have been filled already
                if try_place_arrow(head_node, escape_dir, phase_min, phase_max):
                    placed_this_pass = True
                    break  # rebuild candidates fresh after each placement
            if placed_this_pass:
                consecutive_empty = 0
            else:
                consecutive_empty += 1
                if consecutive_empty >= PHASE_PATIENCE:
                    break  # phase is truly exhausted

    return {
        "gridSize": {"x": grid_width, "y": grid_height}, "arrows": arrows,
        "total_shape_points": total_shape_points, "pixel_colors": pixel_colors, "shape_mask": shape_mask
    }

def escape_is_clear(head_pos, escape_dir, path_set, occupied, gw, gh):
    """Returns True if the escape lane from head_pos is free of occupied cells and path cells."""
    ax, ay = head_pos[0] + escape_dir[0], head_pos[1] + escape_dir[1]
    while 0 <= ax < gw and 0 <= ay < gh:
        if (ax, ay) in occupied or (ax, ay) in path_set:
            return False
        ax += escape_dir[0]; ay += escape_dir[1]
    return True

def post_process_fill_gaps_sections(level_data, config):
    """
    Exhaustively fill remaining free shape cells with small arrows.

    Improvements:
    - Side-aware filling: processes cells in sweeping order (left->right, etc.)
    - Tries ALL 4 escape directions per free point.
    - Iterates until no more progress can be made.
    - Correct self-aim check uses occupied dict.
    - Momentum bias during path growth reduces turns.
    """
    pixel_colors = level_data["pixel_colors"]
    shape_mask = level_data["shape_mask"]
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    occupied = {(p["x"], p["y"]): a["id"] for a in level_data["arrows"] for p in a["path"]}
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    all_escape_dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)]

    def try_fill_from(start_node, escape_dir, min_len, max_len):
        """Attempt to place a short arrow starting at start_node with the given escape direction."""
        nonlocal next_id
        if start_node in occupied:
            return False
        section_rgb = pixel_colors[start_node]
        body_dir = (-escape_dir[0], -escape_dir[1])

        for target_len in range(max_len, min_len - 1, -1):
            path = [start_node]
            temp_set = {start_node}
            curr_dir = body_dir

            for i in range(target_len - 1):
                lx, ly = path[-1]
                valid = []
                for dx, dy in all_escape_dirs:
                    nx, ny = lx + dx, ly + dy
                    if ((nx, ny) in shape_mask
                            and (nx, ny) not in occupied
                            and (nx, ny) not in temp_set
                            and pixel_colors.get((nx, ny)) == section_rgb):
                        if i == 0 and (dx, dy) == escape_dir: continue
                        valid.append((dx, dy))
                if not valid: break
                # Wall-hugging growth for gap fill
                scored = []
                for dx, dy in all_escape_dirs:
                    nx, ny = lx + dx, ly + dy
                    if ((nx, ny) in shape_mask and (nx, ny) not in occupied and (nx, ny) not in temp_set and pixel_colors.get((nx, ny)) == section_rgb):
                        if i == 0 and (dx, dy) == escape_dir: continue
                        score = get_wall_count((nx, ny), shape_mask, occupied, pixel_colors, section_rgb, temp_set)
                        scored.append(((dx, dy), score))
                
                if not scored: break
                
                # Biased random: strongly prefer high wall count
                scored.sort(key=lambda x: x[1], reverse=True)
                top_score = scored[0][1]
                best_dirs = [d for d, s in scored if s == top_score]
                
                # Lower momentum bias during gap filling for more flexibility
                GAP_MOMENTUM_BIAS = 0.4
                if curr_dir in best_dirs and random.random() < GAP_MOMENTUM_BIAS:
                    chosen = curr_dir
                else:
                    chosen = random.choice(best_dirs)
                curr_dir = chosen
                path.append((lx + curr_dir[0], ly + curr_dir[1]))
                temp_set.add(path[-1])

            if len(path) < 3: continue
            if not escape_is_clear(start_node, escape_dir, temp_set, occupied, gw, gh):
                continue

            path_rev = list(reversed(path))
            if len(path_rev) >= 3:
                look_dir = get_dir_name(path_rev[-1][0]-path_rev[-2][0], path_rev[-1][1]-path_rev[-2][1])
            else:
                look_dir = get_dir_name(escape_dir[0], escape_dir[1])

            new_arrow = {
                "id": next_id,
                "color": rgb_to_hex(section_rgb),
                **_arrow_width_field(),
                "path": [{"x": p[0], "y": p[1]} for p in path_rev],
                "lookDirection": look_dir
            }
            test_occupied = dict(occupied)
            for p in path: test_occupied[p] = next_id
            if is_level_solvable(
                {"gridSize": level_data["gridSize"], "arrows": level_data["arrows"] + [new_arrow]},
                pre_occupied=test_occupied
            ):
                level_data["arrows"].append(new_arrow)
                for p in path: occupied[p] = next_id
                next_id += 1
                return True
        return False

    # ---- Multi-sweep gap fill ----
    # 1. Try side-aware sweeps (left->right, right->left, top->bottom, bottom->top)
    # This helps fill from edges inward systematically.
    sweep_configs = [
        ("left-to-right", lambda p: p[0]),
        ("right-to-left", lambda p: -p[0]),
        ("top-to-bottom", lambda p: p[1]),
        ("bottom-to-top", lambda p: -p[1]),
    ]
    
    while True:
        total_placed_this_round = 0
        for sweep_name, sort_key in sweep_configs:
            # When sweeping, also prioritize higher wall count within that sweep
            free_points = sorted(
                [p for p in shape_mask if p not in occupied], 
                key=lambda p: (sort_key(p), -get_wall_count(p, shape_mask, occupied, pixel_colors, pixel_colors[p]))
            )
            placed_this_sweep = 0
            for start_node in free_points:
                if start_node in occupied: continue
                dirs = list(all_escape_dirs)
                random.shuffle(dirs)
                for edir in dirs:
                    ex, ey = start_node[0] + edir[0], start_node[1] + edir[1]
                    lane_usable = not (0 <= ex < gw and 0 <= ey < gh and (ex, ey) in occupied)
                    if not lane_usable: continue
                    if try_fill_from(start_node, edir, 3, 5):
                        placed_this_sweep += 1
                        break
            if placed_this_sweep > 0:
                total_placed_this_round += placed_this_sweep
                print(f"    Side-aware sweep ({sweep_name}): placed {placed_this_sweep} arrows.")
        if total_placed_this_round == 0:
            break

    # 2. Final random exhaustion sweep
    while True:
        free_points = [p for p in shape_mask if p not in occupied]
        if not free_points: break
        random.shuffle(free_points)
        placed_this_round = 0
        for start_node in free_points:
            if start_node in occupied: continue
            dirs = list(all_escape_dirs)
            random.shuffle(dirs)
            for edir in dirs:
                ex, ey = start_node[0] + edir[0], start_node[1] + edir[1]
                lane_usable = not (0 <= ex < gw and 0 <= ey < gh and (ex, ey) in occupied)
                if not lane_usable: continue
                if try_fill_from(start_node, edir, 3, 5):
                    placed_this_round += 1
                    break
        if placed_this_round == 0: break
        print(f"    Random exhaustion round: placed {placed_this_round} arrows.")

    return level_data

def fill_large_voids(level_data):
    """
    Identifies contiguous empty regions of 5+ points and attempts to fill them 
    with a single long, deterministic arrow that hugs the wall.
    """
    pixel_colors = level_data["pixel_colors"]
    shape_mask = level_data["shape_mask"]
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    occupied = {(p["x"], p["y"]): a["id"] for a in level_data["arrows"] for p in a["path"]}
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    
    # Keep repeating the process until no more long arrows can be placed anywhere
    while True:
        placed_any_in_round = False
        
        # 1. Group empty points into connected components of the same color
        free_points = [p for p in shape_mask if p not in occupied]
        if not free_points: break
        
        components = []
        visited = set()
        for p in free_points:
            if p in visited: continue
            comp = []
            q = collections.deque([p])
            visited.add(p)
            color = pixel_colors[p]
            while q:
                curr = q.popleft()
                comp.append(curr)
                for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                    nx, ny = curr[0]+dx, curr[1]+dy
                    if (nx, ny) in shape_mask and (nx, ny) not in occupied and (nx, ny) not in visited and pixel_colors.get((nx, ny)) == color:
                        visited.add((nx, ny))
                        q.append((nx, ny))
            if len(comp) >= 2: # Reduce to 2 to catch even smaller gaps
                components.append(comp)
        
        if not components: break

        placed_this_round = 0
        for comp in components:
            comp_set = set(comp)
            color = pixel_colors[comp[0]]
            
            # Find the best possible arrow within this component
            best_path = []
            best_edir = None
            
            # Only try the most 'constrained' points as seeds to save time
            seeds = sorted(comp, key=lambda p: get_doe(p, gw, gh, occupied))[:10]
            
            for start_node in seeds:
                # Try growing a path from this seed
                path = [start_node]
                p_set = {start_node}
                while True:
                    curr = path[-1]
                    scored = []
                    for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                        nx, ny = curr[0]+dx, curr[1]+dy
                        if (nx, ny) in comp_set and (nx, ny) not in p_set:
                            # Use existing high-quality scoring
                            score = get_wall_count((nx, ny), shape_mask, occupied, pixel_colors, color, p_set)
                            scored.append(((dx,dy), (nx, ny), score))
                    if not scored: break
                    scored.sort(key=lambda x: (x[2], 1 if len(path) >= 2 and x[0] == (path[-1][0]-path[-2][0], path[-1][1]-path[-2][1]) else 0), reverse=True)
                    path.append(scored[0][1])
                    p_set.add(path[-1])
                
                if len(path) < 3: continue
                
                # Check all 8 possible head/direction combos for this path
                for head_candidate in [path[0], path[-1]]:
                    ordered_path = list(reversed(path)) if head_candidate == path[0] else list(path)
                    head_pos = ordered_path[-1]
                    for edir in [(1,0),(-1,0),(0,1),(0,-1)]:
                        if not escape_is_clear(head_pos, edir, set(ordered_path), occupied, gw, gh): continue
                        
                        if len(ordered_path) > len(best_path):
                            best_path = ordered_path
                            best_edir = edir
            
            if best_path and best_edir:
                new_arrow = {
                    "id": next_id,
                    "color": rgb_to_hex(color),
                    **_arrow_width_field(),
                    "path": [{"x": p[0], "y": p[1]} for p in best_path],
                    "lookDirection": get_dir_name(best_edir[0], best_edir[1])
                }
                
                test_level = {"gridSize": level_data["gridSize"], "arrows": level_data["arrows"] + [new_arrow]}
                test_occ = dict(occupied)
                for p in best_path: test_occ[p] = next_id
                
                if is_level_solvable(test_level, pre_occupied=test_occ):
                    level_data["arrows"].append(new_arrow)
                    for p in best_path: occupied[p] = next_id
                    next_id += 1
                    placed_any_in_round = True
                    placed_this_round += 1
                    # Break to re-calculate components as this arrow might have split them
                    break 
            if placed_this_round > 0: break
            
        if not placed_any_in_round: break
        
    return level_data

def aggressive_tile_fill(level_data):
    """
    Exhaustively attempts to place 2-point arrows in every possible alignment
    in remaining empty areas. This is the 'brute force' final pass.
    """
    pixel_colors = level_data["pixel_colors"]
    shape_mask = level_data["shape_mask"]
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    occupied = {(p["x"], p["y"]): a["id"] for a in level_data["arrows"] for p in a["path"]}
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    
    free_points = [p for p in shape_mask if p not in occupied]
    random.shuffle(free_points)
    
    placed_count = 0
    for p in free_points:
        if p in occupied: continue
        color = pixel_colors[p]
        # Try all 4 neighbors for a 2-point arrow
        neighbors = [(p[0]+dx, p[1]+dy) for dx, dy in [(1,0),(-1,0),(0,1),(0,-1)]]
        random.shuffle(neighbors)
        
        for nb in neighbors:
            if nb in shape_mask and nb not in occupied and pixel_colors.get(nb) == color:
                # Potential 2-point path: (p, nb)
                # Try both orientations: p->nb (escape from nb) or nb->p (escape from p)
                combos = [
                    (p, nb, (nb[0]-p[0], nb[1]-p[1])), # path p,nb; head nb; escape in edir
                    (nb, p, (p[0]-nb[0], p[1]-nb[1]))  # path nb,p; head p; escape in edir
                ]
                random.shuffle(combos)
                
                for tail, head, edir in combos:
                    # edir is path direction, we need to pick an ESCAPE direction
                    esc_dirs = [(1,0),(-1,0),(0,1),(0,-1)]
                    random.shuffle(esc_dirs)
                    placed_local = False
                    for esc_v in esc_dirs:
                        if not escape_is_clear(head, esc_v, {tail, head}, occupied, gw, gh): continue
                        
                        new_arrow = {
                            "id": next_id,
                            "color": rgb_to_hex(color),
                            **_arrow_width_field(),
                            "path": [{"x": tail[0], "y": tail[1]}, {"x": head[0], "y": head[1]}],
                            "lookDirection": get_dir_name(esc_v[0], esc_v[1])
                        }
                        
                        test_occ = dict(occupied)
                        test_occ[tail] = next_id
                        test_occ[head] = next_id
                        
                        if is_level_solvable({"gridSize": level_data["gridSize"], "arrows": level_data["arrows"] + [new_arrow]}, pre_occupied=test_occ):
                            level_data["arrows"].append(new_arrow)
                            occupied[tail] = next_id
                            occupied[head] = next_id
                            next_id += 1
                            placed_count += 1
                            placed_local = True
                            break
                    if placed_local: break
    if placed_count > 0:
        print(f"    Aggressive Tile Fill: placed {placed_count} arrows.")
    return level_data

def single_pixel_desperation_fill(level_data):
    """
    Absolute last resort: fills remaining single pixels with 1-point arrows
    if it's solvable.
    """
    pixel_colors = level_data["pixel_colors"]
    shape_mask = level_data["shape_mask"]
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    occupied = {(p["x"], p["y"]): a["id"] for a in level_data["arrows"] for p in a["path"]}
    next_id = max([a["id"] for a in level_data["arrows"]] + [0]) + 1
    
    free_points = [p for p in shape_mask if p not in occupied]
    random.shuffle(free_points)
    
    placed_count = 0
    for p in free_points:
        if p in occupied: continue
        color = pixel_colors[p]
        
        esc_dirs = [(1,0),(-1,0),(0,1),(0,-1)]
        random.shuffle(esc_dirs)
        for edir in esc_dirs:
            if not escape_is_clear(p, edir, {p}, occupied, gw, gh): continue
            
            new_arrow = {
                "id": next_id,
                "color": rgb_to_hex(color),
                **_arrow_width_field(),
                "path": [{"x": p[0], "y": p[1]}],
                "lookDirection": get_dir_name(edir[0], edir[1])
            }
            
            test_occ = dict(occupied)
            test_occ[p] = next_id
            
            if is_level_solvable({"gridSize": level_data["gridSize"], "arrows": level_data["arrows"] + [new_arrow]}, pre_occupied=test_occ):
                level_data["arrows"].append(new_arrow)
                occupied[p] = next_id
                next_id += 1
                placed_count += 1
                break
    if placed_count > 0:
        print(f"    Single Pixel Desperation Fill: placed {placed_count} arrows.")
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
    for attempt in range(100):
        level_data = run_reverse_generator_with_sections(
            image_path, grid_width, grid_height, DIFF_CONFIG, PALETTE_RGBS
        )
        if not level_data:
            print(f"  Attempt {attempt+1}: Generator returned nothing. Retrying...")
            continue

        # Step 1: Remove arrows with fewer than 1 path points (none should exist, but safety first)
        level_data["arrows"] = [a for a in level_data["arrows"] if len(a["path"]) >= 1]

        # Step 2: Fill remaining empty shape areas (section-aware, section-colored)
        level_data = post_process_fill_gaps_sections(level_data, DIFF_CONFIG)

        # Step 3: Merge stuck small arrows (only within same-color section)
        level_data = merge_stuck_arrows_sections(level_data)

        # Step 4: Final Long Fill Pass for any remaining large unfillable blocks
        level_data = fill_large_voids(level_data)

        # Ensure ALL arrows look the right way before solving/saving
        finalize_arrow_directions(level_data["arrows"])

        # Step 5: Verify density and full solvability
        occupied_points = sum(len(a["path"]) for a in level_data["arrows"])
        total_shape_points = level_data.get("total_shape_points", 1)
        final_density = occupied_points / total_shape_points

        # Build a clean copy for the solver check (no internal keys)
        clean_for_solver = {
            "gridSize": level_data["gridSize"],
            "arrows": level_data["arrows"]
        }

        # Acceptance density degrades over attempts to ensure we eventually output SOMETHING,
        # but the new desperation passes should keep density high.
        if attempt > 75: ACCEPTANCE_DENSITY = 0.70
        elif attempt > 50: ACCEPTANCE_DENSITY = 0.75
        elif attempt > 25: ACCEPTANCE_DENSITY = 0.80
        else: ACCEPTANCE_DENSITY = DIFF_CONFIG["TARGET_DENSITY"]

        if final_density >= ACCEPTANCE_DENSITY and is_level_solvable(clean_for_solver):
            print(f"  Attempt {attempt+1}: PASSED. Density={final_density:.1%}. Saving.")
            level_data["duration"] = max(30, occupied_points * DURATION_MULTIPLIER)
            # Produce clean output JSON — only standard fields
            return {
                "gridSize": level_data["gridSize"],
                "arrows": level_data["arrows"],
               # "duration": level_data["duration"]
            }
        else:
            reason = "SOLVER FAILED" if final_density >= ACCEPTANCE_DENSITY else "DENSITY LOW"
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
