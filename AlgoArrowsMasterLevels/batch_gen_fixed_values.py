import random
import os
import sys
import json
import argparse
import collections
import re
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
        "SHORT_PATH_PROBABILITY": 0.2,
        "SHORT_PATH_RANGE": (3, 10),
        "LONG_PATH_RANGE": (9, 30),
        "TURN_PROBABILITY": 0.7,
        "TARGET_BLOCKED_PROBABILITY": 0.8,
        "PERPENDICULAR_PREFERENCE": 0.8,
        "MAX_RETRY_ATTEMPTS": 100,
        "MIN_SEEDS": 1,
        "MAX_SEEDS": 5,
        "SORT_STRATEGY": "seeds"
    },
    3: { # New Reverse-Backtracking Logic
        "TARGET_DENSITY": 0.8651,
        "TURN_CHANCE": 0.03,
        "LENGTH_TIERS": [
            (0.01, (2, 6)),  # Short: 1%
            (0.04, (5, 12)), # Mid: 4%
            (0.95, (13, 47))  # Long: 95%
        ],
        "MAX_RETRY_ATTEMPTS": 100
    }
}

# --- FIXED LEVEL VALUES CONFIGURATION ---
# Format: { level_number: (min_width, max_width) }
FIXED_LEVEL_VALUES = {
    # Block 1 (E:28, SH:36, Spread:8)
    1: (10, 10), 2: (13, 13), 3: (14, 14), 4: (18, 18), 5: (21, 21), 6: (24, 24), 7: (45, 45), 8: (26, 26), 9: (28, 28), 10: (35, 35),
    # Block 2 (E:29, SH:38, Spread:9)
    11: (33, 33), 12: (33, 33), 13: (32, 32), 14: (29, 29), 15: (46, 47), 16: (39, 40), 17: (33, 50), 18: (38, 38), 19: (29, 29), 20: (49, 49),
    # Block 3 (E:30, SH:41, Spread:11)
    21: (34, 34), 22: (37, 37), 23: (49, 49), 24: (44, 48), 25: (37, 41), 26: (38, 38), 27: (36, 36), 28: (41, 41), 29: (37, 37), 30: (34, 34),
    # Block 4 (E:32, SH:44, Spread:12)
    31: (37, 37), 32: (42, 42), 33: (40, 40), 34: (39, 40), 35: (37, 37), 36: (40, 40), 37: (37, 37), 38: (44, 44), 39: (39, 39), 40: (37, 37),
    # Block 5 (E:33, SH:46, Spread:13)
    41: (39, 39), 42: (38, 38), 43: (42, 42), 44: (40, 40), 45: (38, 38), 46: (42, 42), 47: (40, 42), 48: (46, 46), 49: (37, 37), 50: (45, 45),
    # Block 6 (E:34, SH:49, Spread:15)
    51: (37, 37), 52: (40, 40), 53: (45, 45), 54: (34, 37), 55: (40, 40), 56: (45, 45), 57: (40, 40), 58: (49, 49), 59: (45, 45), 60: (47,47),
    # Block 7 (E:36, SH:52, Spread:16)
    61: (34, 34), 62: (42, 42), 63: (47, 47), 64: (36, 36), 65: (42, 42), 66: (45, 45), 67: (47, 47), 68: (65, 65), 69: (44, 44), 70: (42, 42),
    # Block 8 (E:37, SH:55, Spread:18)
    71: (42, 42), 72: (55, 55), 73: (50, 50), 74: (37, 37), 75: (44, 44), 76: (49, 49), 77: (43, 43), 78: (55, 55), 79: (37, 37), 80: (44, 44),
    # Block 9 (E:38, SH:57, Spread:19)
    81: (38, 38), 82: (46, 46), 83: (51, 51), 84: (41, 43), 85: (46, 46), 86: (46, 46), 87: (44, 44), 88: (52, 52), 89: (38, 38), 90: (46, 46),
    # Block 10 (E:40, SH:60, Spread:20)
    91: (38, 38), 92: (48, 48), 93: (54, 54), 94: (40, 40), 95: (48, 48), 96: (51, 51), 97: (45, 45), 98: (54, 54), 99: (56, 58), 100: (58, 58),
    # Block 11 (E:41, SH:63, Spread:22)
    101: (41, 41), 102: (50, 50), 103: (56, 56), 104: (41, 41), 105: (55, 55), 106: (54, 54), 107: (47, 47), 108: (62, 62), 109: (62, 62), 110: (53, 53),
    # Block 12 (E:42, SH:66, Spread:24)
    111: (42, 42), 112: (52, 52), 113: (65, 65), 114: (42, 42), 115: (52, 52), 116: (56, 56), 117: (49, 49), 118: (62, 62), 119: (40, 40), 120: (52, 52),
    # Block 13 (E:44, SH:69, Spread:25)
    121: (43, 43), 122: (46,46), 123: (62, 62), 124: (44, 44), 125: (54, 54), 126: (59, 59), 127: (50, 50), 128: (74, 74), 129: (44, 44), 130: (54, 54),
    # Block 14 (E:45, SH:72, Spread:27)
    131: (44, 44), 132: (45, 45), 133: (51, 51), 134: (53, 53), 135: (56, 56), 136: (62, 62), 137: (51, 51), 138: (58, 58), 139: (45, 45), 140: (45, 45),
    # Block 15 (E:46, SH:74, Spread:28)
    141: (45, 45), 142: (57, 57), 143: (62, 62), 144: (55, 55), 145: (57, 57), 146: (64, 64), 147: (52, 52), 148: (70, 70), 149: (62, 62), 150: (55, 55),
    # Block 16 (E:48, SH:77, Spread:29)
    151: (46, 46), 152: (60, 60), 153: (60, 60), 154: (48, 48), 155: (60, 60), 156: (66, 66), 157: (54, 54), 158: (65, 65), 159: (57, 57), 160: (56, 56),
    # Block 17 (E:49, SH:80, Spread:31)
    161: (47, 47), 162: (61, 61), 163: (71, 71), 164: (52, 52), 165: (61, 61), 166: (68, 68), 167: (55, 55), 168: (66, 66), 169: (49, 49), 170: (57, 57),
    # Block 18 (E:50, SH:83, Spread:33)
    171: (48, 48), 172: (60, 60), 173: (71, 71), 174: (51, 51), 175: (63, 63), 176: (71, 71), 177: (58, 58), 178: (67, 67), 179: (54, 54), 180: (58, 58),
    # Block 19 (E:52, SH:86, Spread:34)
    181: (50, 50), 182: (66, 66), 183: (71, 71), 184: (52, 52), 185: (66, 66), 186: (55, 55), 187: (60, 60), 188: (70, 70), 189: (52, 52), 190: (59, 59),
    # Block 20 (E:53, SH:88, Spread:35)
    191: (53, 53), 192: (67, 67), 193: (52, 52), 194: (53, 53), 195: (57, 57), 196: (64, 64), 197: (62, 62), 198: (60, 60), 199: (53, 53), 200: (60, 60),
    # Block 21 (E:54, SH:91, Spread:37)
    201: (54, 54), 202: (69, 69), 203: (70, 70), 204: (54, 54), 205: (69, 69), 206: (70, 70), 207: (65, 65), 208: (70, 70), 209: (54, 54), 210: (61, 61),
    # Block 22 (E:56, SH:94, Spread:38)
    211: (62, 62), 212: (71, 71), 213: (70, 70), 214: (56, 56), 215: (70, 70), 216: (80, 80), 217: (65, 65), 218: (70, 70), 219: (56, 56), 220: (65, 65),
    # Block 23 (E:57, SH:96, Spread:39)
    221: (67, 67), 222: (73, 73), 223: (70, 70), 224: (57, 57), 225: (70, 70), 226: (81, 81), 227: (65, 65), 228: (70, 70), 229: (63, 63), 230: (67, 67),
    # Block 24 (E:58, SH:98, Spread:40)
    231: (63, 63), 232: (74, 74), 233: (70, 70), 234: (66, 66), 235: (70, 70), 236: (78, 78), 237: (74, 74), 238: (70,70), 239: (58, 58), 240: (69, 69),
    # Block 25 (E:60, SH:100, Spread:40)
    241: (63, 63), 242: (71, 71), 243: (70, 70), 244: (65, 65), 245: (70, 70), 246: (58, 58), 247: (60, 60), 248: (70, 70), 249: (60, 60), 250: (75, 75),
    # Block 26 (E:61, SH:102, Spread:41)
    251: (64, 64), 252: (71, 71), 253: (70, 70), 254: (61, 61), 255: (70, 70), 256: (59, 59), 257: (63, 64), 258: (70, 70), 259: (61, 61), 260: (76, 76),
    # Block 27 (E:62, SH:105, Spread:43)
    261: (65, 65), 262: (72, 72), 263: (70, 70), 264: (62, 62), 265: (70, 70), 266: (60, 60), 267: (62, 62), 268: (70, 70), 269: (62, 62), 270: (77, 77),
    # Block 28 (E:64, SH:108, Spread:44)
    271: (67, 67), 272: (72, 72), 273: (70, 70), 274: (64, 64), 275: (70, 70), 276: (62, 62), 277: (64, 64), 278: (70, 70), 279: (72, 72), 280: (86, 86),
    # Block 29 (E:65, SH:110, Spread:45)
    281: (68, 68), 282: (72, 72), 283: (70, 70), 284: (65, 65), 285: (70, 70), 286: (79, 79), 287: (65, 65), 288: (70, 70), 289: (65, 65), 290: (80, 80),
    # Block 30 (E:66, SH:113, Spread:47)
    291: (69, 69), 292: (72, 72), 293: (70, 70), 294: (66, 66), 295: (61, 61), 296: (70, 70), 297: (66, 66), 298: (70, 70), 299: (66, 66), 300: (81, 81),
    # Block 31 (E:68, SH:116, Spread:48(96, 108)
    301: (71, 71), 302: (72, 72), 303: (70, 70), 304: (68, 68), 305: (70, 70), 306: (67, 67), 307: (68, 68), 308: (70, 70), 309: (68, 68), 310: (83, 83),
    # Block 32 (E:69, SH:118, Spread:49(96, 108)
    311: (72, 72), 312: (73, 73), 313: (70, 70), 314: (69, 69), 315: (70, 70), 316: (67, 67), 317: (69, 69), 318: (70, 70), 319: (69, 69), 320: (84, 84),
    # Block 33 (E:70, SH:121, Spread:51(96, 108)
    321: (73, 73), 322: (73, 73), 323: (70, 70), 324: (90, 90), 325: (75, 75), 326: (72, 72), 327: (79, 79), 328: (70, 70), 329: (70, 70), 330: (70, 70),
    # Block 34 (E:72, SH:124, Spread:52(96, 108)
    331: (88, 88), 332: (70, 70), 333: (71, 71), 334: (72, 72), 335: (75, 75), 336: (70, 70), 337: (76, 76), 338: (70, 70), 339: (70, 70), 340: (71, 71),
    # Block 35 (E:73, SH:126, Spread:53(96, 108)
    341: (76, 76), 342: (74, 74), 343: (72, 72), 344: (73, 73), 345: (66, 66), 346: (71, 71), 347: (67, 67), 348: (68, 68), 349: (66, 66), 350: (55, 55),
    # Block 36 (E:74, SH:129, Spread:55(96, 108)
    351: (72, 72), 352: (74, 74), 353: (75, 75), 354: (74, 74), 355: (74, 74), 356: (60, 60), 357: (74, 74), 358: (71, 71), 359: (70, 70), 360: (60, 60),
    # Block 37 (E:76, SH:132, Spread:56(96, 108)
    361: (70, 70), 362: (75, 75), 363: (65, 65), 364: (92, 92), 365: (58, 58), 366: (57, 57), 367: (56, 56), 368: (57, 57), 369: (66, 66), 370: (57, 57),
    # Block 38 (E:77, SH:134, Spread:57(96, 108)
    371: (73, 73), 372: (68, 68), 373: (56, 56), 374: (77, 77), 375: (71, 71), 376: (75, 75), 377: (77, 77), 378: (73, 73), 379: (70, 70), 380: (70, 70),
    # Block 39 (E:78, SH:137, Spread:59(96, 108)
    381: (70, 70), 382: (63, 63), 383: (73, 73), 384: (69, 69), 385: (65, 65), 386: (65, 65), 387: (70, 70), 388: (74, 74), 389: (70, 70), 390: (70, 70),
    # Block 40 (E:80, SH:140, Spread:60(96, 108)
    391: (73, 73), 392: (75, 75), 393: (75, 75), 394: (75, 75), 395: (75, 75), 396: (70, 70), 397: (70, 70), 398: (75, 75), 399: (70, 70), 400: (71,71),
}



# Default range if level number is not found in FIXED_LEVEL_VALUES
DEFAULT_WIDTH_RANGE = (20, 45)

COMMON_CONFIG = {
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128,
    "BG_DISTANCE_THRESHOLD": 30
}

DURATION_MULTIPLIER = 0.28

def rgb_to_hex(rgb):
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def get_image_data(img, config):
    """
    Analyzes the image to detect the shape mask and background color based on edge sampling.
    Returns (shape_mask_set, pixel_colors_dict).
    """
    gw, gh = img.size
    shape_mask = set()
    pixel_colors = {}
    
    # 1. Sample edge pixels to identify potential background colors
    edge_pixels = []
    for x in range(gw):
        edge_pixels.append(img.getpixel((x, 0)))
        edge_pixels.append(img.getpixel((x, gh - 1)))
    for y in range(gh):
        edge_pixels.append(img.getpixel((0, y)))
        edge_pixels.append(img.getpixel((gw - 1, y)))
    
    # Identify the most common opaque color on the edges
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
            pixel_colors[(x, y)] = color
            
            # Rule 1: Transparency is always background
            if a < config["ALPHA_THRESHOLD"]:
                continue
                
            is_bg = False
            # Rule 2: Most common edge color is background
            if main_bg_color:
                dist = sum(abs(color[i] - main_bg_color[i]) for i in range(3))
                if dist < bg_dist:
                    is_bg = True
            
            # Rule 3: Fallback to white check (legacy) if no main_bg found or if main_bg IS white
            if not is_bg and (not main_bg_color or main_bg_is_white):
                if r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]:
                    is_bg = True
            
            if not is_bg:
                shape_mask.add((x, y))
                
    return shape_mask, pixel_colors

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
            if (cx, cy) in occupied:
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
                level_data["duration"] = max(30, occupied_points * DURATION_MULTIPLIER)
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
        shape_mask, pixel_colors = get_image_data(img, config)
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
                aim_x += look_dx
                aim_y += look_dy
            
            if is_self_aiming: continue
            
            # Solver check
            avg_c = [sum(pixel_colors.get(p, (0,0,0,0))[c] for p in path)//len(path) for c in range(3)]
            new_arrow = {
                "id": next_id,
                "color": "#000000",
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

def merge_stuck_arrows(level_data, shape_mask=None):
    """
    Merges arrows that are stuck head-to-tail with no gap OR a 1-point empty gap,
    provided both are < 4 points and the gap (if any) is within the shape_mask.
    """
    arrows = level_data.get("arrows", [])
    if not isinstance(arrows, list):
        return level_data
    
    dir_vecs = {"up": (0, 1), "down": (0, -1), "left": (-1, 0), "right": (1, 0)}
    gw, gh = level_data["gridSize"]["x"], level_data["gridSize"]["y"]
    
    changed = True
    while changed:
        changed = False
        # Re-build point map every time because paths change
        point_to_arrow_idx = {}
        for idx, a in enumerate(arrows):
            for p in a["path"]:
                point_to_arrow_idx[(p["x"], p["y"])] = idx
        
        merged_any = False
        to_remove = set()
        
        for i in range(len(arrows)):
            if i in to_remove: continue
            
            a = arrows[i]
            if len(a["path"]) >= 4: continue
            
            head = a["path"][-1]
            ldir = a["lookDirection"]
            dx, dy = dir_vecs[ldir]
            
            # Case 1: No gap
            tx, ty = head["x"] + dx, head["y"] + dy
            
            # Try Case 1
            if (tx, ty) in point_to_arrow_idx:
                target_idx = point_to_arrow_idx[(tx, ty)]
                if target_idx != i and target_idx not in to_remove:
                    b = arrows[target_idx]
                    tail_b = b["path"][0]
                    if tail_b["x"] == tx and tail_b["y"] == ty and len(b["path"]) < 4:
                        print(f"  [MERGE] Merging arrow {a['id']} (len {len(a['path'])}) into arrow {b['id']} (len {len(b['path'])}) [No Gap]")
                        # Merge paths
                        b["path"] = a["path"] + b["path"]
                        to_remove.add(i)
                        merged_any = True
                        changed = True
                        break # Start over to re-map points
        
        if merged_any:
            arrows = [a for idx, a in enumerate(arrows) if idx not in to_remove]
            level_data["arrows"] = arrows
            
    return level_data

def generate_level_json(image_path, grid_width, grid_height, difficulty=1):
    config = COMMON_CONFIG.copy()
    diff_config = DIFFICULTY_CONFIGS.get(difficulty, DIFFICULTY_CONFIGS[1])
    config.update(diff_config)
    
    # Pre-extract shape mask for merge logic
    shape_mask = set()
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
        img = img.resize((grid_width, grid_height), Image.NEAREST)
        shape_mask, _ = get_image_data(img, config)
    except:
        shape_mask = None

    if difficulty == 1:
        level_data = generate_difficulty_1(image_path, grid_width, grid_height, config)
    elif difficulty == 2:
        level_data = generate_difficulty_2(image_path, grid_width, grid_height, config)
    elif difficulty == 3:
        level_data = generate_difficulty_3(image_path, grid_width, grid_height, config)
    else:
        level_data = generate_difficulty_1(image_path, grid_width, grid_height, config)
        
    if level_data:
        level_data = merge_stuck_arrows(level_data, shape_mask=shape_mask)
        # Re-calculate duration if it was present
        if "duration" in level_data:
            occupied_points = sum(len(a["path"]) for a in level_data["arrows"])
            level_data["duration"] = occupied_points * DURATION_MULTIPLIER
            
    return level_data

def run_reverse_generator(image_path, grid_width, grid_height, config):
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    shape_mask, pixel_colors = get_image_data(img, config)

    if not shape_mask:
        return None

    occupied = set() # For quick membership check
    occupied_with_ids = {} # (x, y) -> arrow_id for solver consistency
    escape_routes = {} 
    arrow_depths = {} # aid -> depth (for Chain Depth logic)
    arrows = []
    arrow_id = 1
    total_shape_points = len(shape_mask)
    
    # Calculate center and bounds for Zig-Zag biased turning
    avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
    avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
    center = (avg_x, avg_y)
    max_dist = max(((p[0]-avg_x)**2 + (p[1]-avg_y)**2)**0.5 for p in shape_mask) or 1
    target_points = int(total_shape_points * config["TARGET_DENSITY"])
    free_points = set(shape_mask) # Incremental tracking

    # Pre-calculate boundary distances for all points
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
        current_density = len(occupied) / total_shape_points
        candidates = []
        inv_map = {"right": (1, 0), "left": (-1, 0), "up": (0, 1), "down": (0, -1)}
        
        for p in free_points:
            px, py = p
            row_heads = heads_by_row.get(py, [])
            col_heads = heads_by_col.get(px, [])
            
            for edir, dist_to_boundary in dist_to_bounds[p].items():
                is_mutual = False
                for hx, _, ldir in row_heads:
                    odx, ody = inv_map[ldir]
                    if odx == 0: continue
                    vx = px - hx
                    if vx * odx > 0:
                        wx = hx - px
                        if edir[0] != 0 and wx * edir[0] > 0:
                            is_mutual = True; break
                if is_mutual: continue
                
                for hy, _, ldir in col_heads:
                    odx, ody = inv_map[ldir]
                    if ody == 0: continue
                    vy = py - hy
                    if vy * ody > 0:
                        wy = hy - py
                        if edir[1] != 0 and wy * edir[1] > 0:
                            is_mutual = True; break
                if is_mutual: continue

                priority = dist_to_boundary
                if p in escape_routes:
                    # 1. Chain Depth: sum of depths of blocked arrows * weight
                    # This ensures we prioritize blocking arrows that are already blocking others
                    blocking_aids = escape_routes.get(p, set())
                    depth_bonus = sum(arrow_depths.get(aid, 1) for aid in blocking_aids) * 60
                    priority += depth_bonus
                candidates.append((p, edir, priority))
        
        if iteration % 200 == 0:
            print(f"      - Progress: {current_density:.1%}, Iter: {iteration}")
            
        if not candidates: break
            
        candidates.sort(key=lambda x: x[2], reverse=True)
        if current_density > 0.8:
            top_candidates = candidates 
        else:
            top_candidates = candidates[:max(1, len(candidates) // 10)]
        random.shuffle(top_candidates)
        
        success_placement = False
        for head_node, escape_dir, _ in top_candidates[:30]:
            if success_placement: break
            
            for _growth_try in range(2):
                if success_placement: break
                current_density = len(occupied) / total_shape_points
                tier_roll = random.random()
                cumulative = 0
                target_len = 2
                # Use long tiers up until very high density to ensure the "core" is made of big arrows
                length_tiers = config["LENGTH_TIERS"]
                if current_density > 0.92:
                    length_tiers = [(0.5, (2, 2)), (0.5, (2, 3))] # Final gaps: Tiny
                elif current_density > 0.85:
                    length_tiers = [(0.4, (3, 6)), (0.6, (6, 15))] # High density: Medium
                
                for prob, (range_min, range_max) in length_tiers:
                    cumulative += prob
                    if tier_roll <= cumulative:
                        target_len = random.randint(range_min, range_max)
                        break

                # 2. Zig-Zag Pathing: Center-biased and length-dependent turning
                dist_from_center = ((head_node[0]-center[0])**2 + (head_node[1]-center[1])**2)**0.5
                center_factor = max(0, 1 - (dist_from_center / max_dist))
                
                # Base chance increases for extremely long arrows or center proximity
                base_turn_chance = config["TURN_CHANCE"]
                if target_len > 25: base_turn_chance = max(base_turn_chance, 0.3)
                local_turn_chance = base_turn_chance + (center_factor * 0.45)

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
                    
                    # Force a turn if we are long and haven't zig-zagged yet
                    final_turn_roll = local_turn_chance
                    if not made_turn and target_len > 10 and i > target_len // 2:
                        final_turn_roll = 0.7 
                        
                    chosen_dir = None
                    if random.random() < final_turn_roll and i > 0:
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

                if len(path) >= 1:
                    aim_x, aim_y = path[0][0] + escape_dir[0], path[0][1] + escape_dir[1]
                    is_self_aiming = False
                    while 0 <= aim_x < grid_width and 0 <= aim_y < grid_height:
                        if (aim_x, aim_y) in temp_path_occupied:
                            is_self_aiming = True
                            break
                        aim_x += escape_dir[0]
                        aim_y += escape_dir[1]
                    
                    if is_self_aiming:
                        continue
                    path_rev = list(reversed(path))
                    dir_map = {(1, 0): "right", (-1, 0): "left", (0, 1): "up", (0, -1): "down"}
                    if len(path_rev) >= 2:
                        last_p, prev_p = path_rev[-1], path_rev[-2]
                        look_dir = dir_map.get((last_p[0]-prev_p[0], last_p[1]-prev_p[1]), dir_map[escape_dir])
                    else:
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
                    
                    test_occupied = occupied_with_ids.copy()
                    for p in path: test_occupied[p] = arrow_id
                    
                    if is_level_solvable(test_level, pre_occupied=test_occupied):
                        new_arrow["color"] = "#000000"
                        arrows.append(new_arrow)
                        update_head_maps(new_arrow)
                        for p in path: 
                            occupied.add(p)
                            occupied_with_ids[p] = arrow_id
                            if p in free_points: free_points.remove(p)
                        # Update chain depth
                        blocked_aids = escape_routes.get(head_node, set())
                        if blocked_aids:
                            arrow_depths[arrow_id] = 1 + max(arrow_depths.get(aid, 1) for aid in blocked_aids)
                        else:
                            arrow_depths[arrow_id] = 1

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
        "density_success": final_density >= 0.901,
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
    
    shape_mask_set, pixel_colors = get_image_data(img, config)
    shape_mask = list(shape_mask_set) # Convert to list for sampling logic below

    if not shape_mask:
        return None

    if config.get("SORT_STRATEGY") == "seeds":
        num_seeds = random.randint(config["MIN_SEEDS"], config["MAX_SEEDS"])
        seeds = random.sample(shape_mask, min(num_seeds, len(shape_mask)))
        def get_priority(p):
            return min((p[0] - s[0])**2 + (p[1] - s[1])**2 for s in seeds)
    else:
        avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
        avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
        center = (avg_x, avg_y)
        def get_priority(p):
            return (p[0] - center[0])**2 + (p[1] - center[1])**2

    occupied_info = {}
    pixel_to_id = {}
    escape_routes = {}
    adj = {}
    id_to_dir = {}
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
                "color": "#000000",
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

def get_width_range_for_level(filename):
    """Extracts level number from filename and returns (min, max) width range."""
    # Find the first number in the filename
    match = re.search(r'\d+', filename)
    if not match:
        return DEFAULT_WIDTH_RANGE
    
    level_num = int(match.group())
    
    # Check if level_num exists in our fixed configuration
    return FIXED_LEVEL_VALUES.get(level_num, DEFAULT_WIDTH_RANGE)

def main():
    parser = argparse.ArgumentParser(description="Bulk generate AlgoArrows levels with fixed width based on level number in filename.")
    parser.add_argument("input_path", help="Path to the folder containing source images or a single image file")
    parser.add_argument("--difficulty", type=int, default=1, choices=[1, 2, 3], help="Level difficulty (1, 2, or 3)")
    parser.add_argument("--time", type=str, default="true", choices=["true", "false"], help="Include duration in level data (true/false)")
    
    args = parser.parse_args()
    input_path = os.path.abspath(args.input_path)
    
    source_folder = ""
    image_files = []
    output_folder = ""

    if os.path.isfile(input_path):
        source_folder = os.path.dirname(input_path)
        image_files = [os.path.basename(input_path)]
        parent_dir = os.path.dirname(source_folder)
        output_folder = os.path.join(parent_dir, "GeneratedLevelsFixed")
    elif os.path.isdir(input_path):
        source_folder = input_path
        parent_dir = os.path.dirname(source_folder)
        output_folder = os.path.join(parent_dir, "GeneratedLevelsFixed")
        valid_extensions = ('.png', '.jpg', '.jpeg', '.bmp', '.gif')
        image_files = [f for f in os.listdir(source_folder) if f.lower().endswith(valid_extensions)]
    else:
        print(f"Error: {input_path} is not a valid file or directory.")
        sys.exit(1)

    if not os.path.exists(output_folder):
        os.makedirs(output_folder)
    
    if not image_files:
        print("No image files found.")
        sys.exit(0)
        
    for img_name in image_files:
        img_path = os.path.join(source_folder, img_name)
        try:
            with Image.open(img_path) as img:
                orig_w, orig_h = img.size
            
            min_width, max_width = get_width_range_for_level(img_name)
            target_val = random.randint(min_width, max_width)
            
            if orig_w >= orig_h:
                grid_width = target_val
                grid_height = int(grid_width * (orig_h / orig_w))
            else:
                grid_height = target_val
                grid_width = int(grid_height * (orig_w / orig_h))
            
            grid_width = max(grid_width, 5)
            grid_height = max(grid_height, 5)
            
            print(f"Processing {img_name}: Grid {grid_width}x{grid_height} (Target {target_val} from Range {min_width}-{max_width})")
            
            level_data = generate_level_json(img_path, grid_width, grid_height, difficulty=args.difficulty)
            if level_data:
                if args.time.lower() == "false":
                    level_data.pop("duration", None)
                
                base_name = os.path.splitext(img_name)[0]
                output_file = os.path.join(output_folder, f"{base_name}.json")
                with open(output_file, 'w') as f:
                    json.dump(level_data, f, indent=2)
                print(f"Successfully saved {img_name}")
        except Exception as e:
            print(f"Error processing {img_name}: {e}")

if __name__ == "__main__":
    main()
