import json
import random
import argparse
import sys
import os
from PIL import Image

# --- CONFIGURATION PARAMETERS ---
# Edit these values to change the level generation behavior
GENERATOR_CONFIG = {
    "SHORT_PATH_PROBABILITY": 0.3,      # Lower means more long paths
    "SHORT_PATH_RANGE": (2, 4),        # Min and max length for short paths
    "LONG_PATH_RANGE": (5, 13),        # Min and max length for long paths
    "TURN_PROBABILITY": 0.6,           # Likelihood of changing direction (0-1)
    "COLOR_SIMILARITY_WEIGHT": 0.8,    # 0 to 1. Higher means arrows stay strictly within same color
    "MAX_ARROW_ATTEMPTS": 300,         # More attempts to find paths
    "BACKTRACK_DEPTH": 12,             # Deeper backtracks
    "MAX_BACKTRACK_RETRIES": 300,      # More room to escape dead ends
    "MAX_LEVEL_RESETS": 5,             # Reset if completely stuck
    "WHITE_THRESHOLD": 245,           # RGB value above which a pixel is considered white (background)
    "ALPHA_THRESHOLD": 128             # Alpha value below which a pixel is considered transparent (background)
}
# --------------------------------

def rgb_to_hex(rgb):
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def color_distance(c1, c2):
    """Simple Euclidean distance between two colors."""
    return ((c1[0]-c2[0])**2 + (c1[1]-c2[1])**2 + (c1[2]-c2[2])**2)**0.5

def get_escape_options(p, grid_width, grid_height, occupied):
    """Counts how many straight-line exit directions are currently possible from point p."""
    options = 0
    for dx, dy in [(1,0), (-1,0), (0,1), (0,-1)]:
        reachable = True
        curr_x, curr_y = p[0] + dx, p[1] + dy
        while 0 <= curr_x < grid_width and 0 <= curr_y < grid_height:
            if (curr_x, curr_y) in occupied:
                reachable = False
                break
            curr_x += dx
            curr_y += dy
        if reachable:
            options += 1
    return options

def get_neighbor_count(p, remaining):
    count = 0
    for dx, dy in [(1,0), (-1,0), (0,1), (0,-1)]:
        if (p[0] + dx, p[1] + dy) in remaining:
            count += 1
    return count

def generate_level_json(image_path, grid_width, grid_height, config=None):
    if config is None:
        config = GENERATOR_CONFIG

    # 1. Image Processing
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    # Map shape and color
    shape_mask = []
    pixel_colors = {}
    for y in range(grid_height):
        for x in range(grid_width):
            color = img.getpixel((x, y))
            r, g, b, a = color
            pixel_colors[(x, y)] = color
            if a >= config["ALPHA_THRESHOLD"] and not (r > config["WHITE_THRESHOLD"] and g > config["WHITE_THRESHOLD"] and b > config["WHITE_THRESHOLD"]):
                shape_mask.append((x, y))

    if not shape_mask:
        print("No shape detected in image.")
        return None

    shape_set = set(shape_mask)
    
    # Precompute "depth" (distance to boundary) for each pixel
    pixel_depth = {}
    for p in shape_mask:
        search_limit = min(p[0] + 1, grid_width - p[0], p[1] + 1, grid_height - p[1])
        min_dist = float(search_limit)
        for dx in range(-search_limit, search_limit + 1):
            for dy in range(-search_limit, search_limit + 1):
                nx, ny = p[0] + dx, p[1] + dy
                if not (0 <= nx < grid_width and 0 <= ny < grid_height) or (nx, ny) not in shape_set:
                    dist = (dx*dx + dy*dy)**0.5
                    if dist < min_dist:
                        min_dist = dist
        pixel_depth[p] = min_dist

    # 2. Level Generation Loop
    for reset_count in range(config["MAX_LEVEL_RESETS"]):
        print(f"Generation attempt {reset_count + 1}...")
        occupied = set()
        arrows = []
        remaining_points = set(shape_mask)
        backtrack_count = 0
        
        while remaining_points:
            # HEURISTIC: 
            # 1. Prioritize pixels with 0 or 1 neighbors in remaining_points (Critical/Isolated)
            # 2. Then prioritize pixels with high depth (Inside-Out)
            # 3. Then few escape options
            def selection_priority(p):
                # 1. Prioritize bottlenecks (0 or 1 neighbors left)
                # 2. Fewest escape options
                # 3. Deepest points (Tie breaker)
                nb_count = get_neighbor_count(p, remaining_points)
                esc = get_escape_options(p, grid_width, grid_height, occupied)
                return (nb_count, esc, -pixel_depth[p])

            sorted_remaining = sorted(list(remaining_points), key=selection_priority)
            start_node = sorted_remaining[0]
            
            # Check if start_node is isolated (0 neighbors)
            if get_neighbor_count(start_node, remaining_points) == 0:
                print(f"Terminal failure: Isolated pixel found at {start_node}. Triggering backtrack.")
                found_arrow = False
            else:
                start_color = pixel_colors[start_node][:3]
                found_arrow = False
                
                for _ in range(config["MAX_ARROW_ATTEMPTS"]):
                    if random.random() < config["SHORT_PATH_PROBABILITY"]:
                        base_length = random.randint(*config["SHORT_PATH_RANGE"])
                    else:
                        base_length = random.randint(*config["LONG_PATH_RANGE"])
                    
                    for target_length in range(base_length, 1, -1):
                        path = [start_node]
                        temp_occupied = {start_node}
                        current_dir = None
                        
                        for _ in range(target_length - 1):
                            last_x, last_y = path[-1]
                            neighbors = []
                            must_pick = None
                            
                            for dx, dy in [(1,0), (-1,0), (0,1), (0,-1)]:
                                neighbor = (last_x + dx, last_y + dy)
                                if neighbor in remaining_points and neighbor not in temp_occupied:
                                    # NEIGHBOR ANALYSIS
                                    remaining_after = remaining_points - temp_occupied - {neighbor}
                                    nb_count = get_neighbor_count(neighbor, remaining_after)
                                    
                                    # MUST-PICK: If this neighbor has NO other way out, we must take it
                                    if nb_count == 0:
                                        must_pick = (neighbor, (dx, dy))
                                        break
                                    
                                    dist = color_distance(start_color, pixel_colors[neighbor][:3])
                                    color_score = max(0, 1 - (dist / 441.0))
                                    dir_score = 1.0
                                    if current_dir:
                                        dir_score = 1.0 if (dx, dy) == current_dir else config["TURN_PROBABILITY"]
                                    
                                    # ORPHAN PREVENTION: Boost neighbors that have few remaining neighbors (bottlenecks)
                                    orphan_score = (4 - nb_count) * 0.5
                                    
                                    noise = random.uniform(0, 0.1)
                                    score = (color_score * config["COLOR_SIMILARITY_WEIGHT"] + 
                                             dir_score * (0.2 * (1 - config["COLOR_SIMILARITY_WEIGHT"])) + 
                                             orphan_score * 0.8) + noise
                                    neighbors.append((neighbor, score, (dx, dy)))
                            
                            if must_pick:
                                next_node, chosen_dir = must_pick
                            elif neighbors:
                                neighbors.sort(key=lambda x: x[1], reverse=True)
                                best_score = neighbors[0][1]
                                top = [n for n in neighbors if n[1] >= best_score * 0.8]
                                next_node, _, chosen_dir = random.choice(top)
                            else:
                                break
                            
                            path.append(next_node)
                            temp_occupied.add(next_node)
                            current_dir = chosen_dir
                        
                        if len(path) < 2: continue
                        
                        # VALIDATE AND COMMIT
                        head_x, head_y = path[-1]
                        prev_x, prev_y = path[-2]
                        dx, dy = head_x - prev_x, head_y - prev_y
                        
                        escapable = True
                        check_x, check_y = head_x + dx, head_y + dy
                        while 0 <= check_x < grid_width and 0 <= check_y < grid_height:
                            if (check_x, check_y) in occupied:
                                escapable = False
                                break
                            check_x += dx
                            check_y += dy
                        
                        if escapable:
                            avg_color = [sum(pixel_colors[p][i] for p in path) // len(path) for i in range(3)]
                            arrows.append({
                                "id": len(arrows) + 1,
                                "color": rgb_to_hex(avg_color),
                                "path": [{"x": p[0], "y": p[1]} for p in path]
                            })
                            for p in path:
                                occupied.add(p)
                                remaining_points.remove(p)
                            
                            if len(arrows) % 20 == 0:
                                print(f"Progress: {len(arrows)} arrows, {len(remaining_points)} points left.")
                                
                            found_arrow = True
                            break # Exit target_length loop
                    
                    if found_arrow: break # Exit MAX_ARROW_ATTEMPTS loop
            
            if not found_arrow:
                backtrack_count += 1
                if arrows and backtrack_count < config["MAX_BACKTRACK_RETRIES"]:
                    # Dynamic backtracking depth: scales deeper but more slowly
                    depth = min(len(arrows), config["BACKTRACK_DEPTH"] + (backtrack_count // 10))
                    print(f"Stuck at {start_node} (nb={get_neighbor_count(start_node, remaining_points)}, esc={get_escape_options(start_node, grid_width, grid_height, occupied)}). Backtracking {depth} (Total: {backtrack_count})...")
                    for _ in range(depth):
                        removed_arrow = arrows.pop()
                        for p_dict in removed_arrow["path"]:
                            p = (p_dict["x"], p_dict["y"])
                            occupied.remove(p)
                            remaining_points.add(p)
                else: break
        
        if not remaining_points:
            print(f"Successfully generated level with {len(arrows)} arrows!")
            arrows.reverse()
            return {
                "gridSize": {"x": grid_width, "y": grid_height},
                "arrows": arrows
            }
        else:
            print(f"Failure in attempt {reset_count+1}: {len(remaining_points)} points left uncovered.")

    print("Failed to achieve 100% coverage after maximum resets.")
    return None

def get_image_path():
    """Fallback to GUI file picker if tkinter is available."""
    try:
        import tkinter as tk
        from tkinter import filedialog
        root = tk.Tk()
        root.withdraw()
        file_path = filedialog.askopenfilename(
            title="Select Image File",
            filetypes=[("Image files", "*.png *.jpg *.jpeg *.bmp *.gif")]
        )
        root.destroy()
        return file_path
    except Exception:
        return None

def main():
    parser = argparse.ArgumentParser(description="Generate AlgoArrows level JSON from an image.")
    parser.add_argument("--image", help="Path to the input image file")
    parser.add_argument("--width", type=int, default=15, help="Grid width (default: 15)")
    parser.add_argument("--height", type=int, default=15, help="Grid height (default: 15)")
    parser.add_argument("--prob", type=float, help="Probability of short arrows (overrides config)")
    parser.add_argument("--output", default="level_output.json", help="Output JSON file name (default: level_output.json)")

    args = parser.parse_args()

    # Create a local config copy to allow command line overrides
    local_config = GENERATOR_CONFIG.copy()
    if args.prob is not None:
        local_config["SHORT_PATH_PROBABILITY"] = args.prob

    image_path = args.image
    if not image_path:
        print("No image path provided. Opening file picker...")
        image_path = get_image_path()
        if not image_path:
            print("No image selected. Exiting.")
            sys.exit(1)

    if not os.path.exists(image_path):
        print(f"Error: Image file '{image_path}' not found.")
        sys.exit(1)

    print(f"Processing image: {image_path}")
    output_data = generate_level_json(image_path, args.width, args.height, local_config)

    if output_data:
        with open(args.output, 'w') as f:
            json.dump(output_data, f, indent=2)
        print(f"Successfully generated level with {len(output_data['arrows'])} arrows.")
        print(f"Saved to: {args.output}")
    else:
        print("Failed to generate level.")

if __name__ == "__main__":
    main()

