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
    "ENFORCE_DIFFICULTY": True,        # If True, blocking arrows must face different directions
    "MAX_RETRY_ATTEMPTS": 50,          # More attempts to satisfy difficulty constraints
    "WHITE_THRESHOLD": 245,           # RGB value above which a pixel is considered white (background)
    "ALPHA_THRESHOLD": 128             # Alpha value below which a pixel is considered transparent (background)
}
# --------------------------------

def rgb_to_hex(rgb):
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def color_distance(c1, c2):
    """Simple Euclidean distance between two colors."""
    return ((c1[0]-c2[0])**2 + (c1[1]-c2[1])**2 + (c1[2]-c2[2])**2)**0.5

def generate_level_json(image_path, grid_width, grid_height, config=None):
    if config is None:
        config = GENERATOR_CONFIG

    # 1. עיבוד התמונה למפת גריד
    try:
        # Convert to RGBA to ensure we can check both color and transparency
        img = Image.open(image_path).convert('RGBA')
        # fix the "upside down" issue
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    # מיפוי התאים שמרכיבים את הצורה ומידע צבע
    shape_mask = []
    pixel_colors = {} # (x,y) -> (r,g,b,a)
    
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
        print("No shape detected in image based on transparency/color filters.")
        return None

    # חישוב מרכז הצורה
    avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
    avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
    center = (avg_x, avg_y)

    occupied = set()
    escape_routes = {} # (x,y) -> (dx, dy) of the arrow that passes through this cell
    arrows = []
    arrow_id = 1

    # רשימת נקודות פנויות ממוינות לפי מרחק מהמרכז (הקרוב ביותר ראשון)
    remaining_points = set(shape_mask)
    
    def get_dist(p):
        return (p[0] - center[0])**2 + (p[1] - center[1])**2

    # נמשיך לנסות למלא כל עוד יש נקודות פנויות
    while remaining_points:
        # בחר את הנקודה הפנויה הקרובה ביותר לממרכז
        sorted_remaining = sorted(list(remaining_points), key=get_dist)
        start_node = sorted_remaining[0]
        start_color = pixel_colors[start_node][:3]
        
        success = False
        # ננסה מספר כיוונים ואורכים כדי למצוא מסלול תקין
        for _ in range(config["MAX_RETRY_ATTEMPTS"]):
            if random.random() < config["SHORT_PATH_PROBABILITY"]:
                target_length = random.randint(*config["SHORT_PATH_RANGE"])
            else:
                target_length = random.randint(*config["LONG_PATH_RANGE"])
                
            current_path = [start_node]
            temp_occupied = {start_node}
            current_direction = None # (dx, dy)
            
            # בניית מסלול
            for i in range(target_length - 1):
                last_x, last_y = current_path[-1]
                directions = [(1, 0), (-1, 0), (0, 1), (0, -1)]
                
                # Check neighbors and their color scores
                scored_neighbors = []
                for dx, dy in directions:
                    neighbor = (last_x + dx, last_y + dy)
                    if neighbor in remaining_points and neighbor not in temp_occupied:
                        n_color = pixel_colors[neighbor][:3]
                        dist = color_distance(start_color, n_color)
                        color_score = max(0, 1 - (dist / 441.0))
                        
                        dir_score = 1.0
                        if current_direction:
                            if (dx, dy) == current_direction:
                                dir_score = 1.0
                            else:
                                dir_score = config["TURN_PROBABILITY"]
                        
                        total_score = (color_score * config["COLOR_SIMILARITY_WEIGHT"] + 
                                       dir_score * (1 - config["COLOR_SIMILARITY_WEIGHT"]))
                        scored_neighbors.append((neighbor, total_score, (dx, dy)))
                
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

            # בדיקת יציאה (Escape check)
            head_x, head_y = current_path[-1]
            prev_x, prev_y = current_path[-2]
            dx = head_x - prev_x
            dy = head_y - prev_y
            
            # Cross-blocking check: If we are blocking another arrow, point in a different direction
            if config["ENFORCE_DIFFICULTY"] and (head_x, head_y) in escape_routes:
                blocking_dir = escape_routes[(head_x, head_y)]
                if (dx, dy) == blocking_dir:
                    # Pointing in same direction as the arrow we block. Harder if different.
                    continue

            escapable = True
            check_x, check_y = head_x + dx, head_y + dy
            while 0 <= check_x < grid_width and 0 <= check_y < grid_height:
                if (check_x, check_y) in occupied:
                    escapable = False
                    break
                check_x += dx
                check_y += dy
            
            if escapable:
                # Sample color
                avg_r = sum(pixel_colors[p][0] for p in current_path) // len(current_path)
                avg_g = sum(pixel_colors[p][1] for p in current_path) // len(current_path)
                avg_b = sum(pixel_colors[p][2] for p in current_path) // len(current_path)
                
                arrow_obj = {
                    "id": arrow_id,
                    "color": rgb_to_hex((avg_r, avg_g, avg_b)),
                    "path": [{"x": p[0], "y": p[1]} for p in current_path]
                }
                arrows.append(arrow_obj)
                arrow_id += 1
                
                for p in current_path:
                    occupied.add(p)
                    remaining_points.remove(p)
                
                # Add this arrow's escape route to the global map
                curr_ex, curr_ey = head_x + dx, head_y + dy
                while 0 <= curr_ex < grid_width and 0 <= curr_ey < grid_height:
                    escape_routes[(curr_ex, curr_ey)] = (dx, dy)
                    curr_ex += dx
                    curr_ey += dy

                success = True
                break
        
        if not success:
            # אם לא הצלחנו למצוא מסלול שיוצא מהנקודה הזו, נוריד אותה מהרשימה כדי לא להיתקע
            remaining_points.remove(start_node)

    # בניית האובייקט הסופי
    level_json = {
        "gridSize": {
            "x": grid_width,
            "y": grid_height
        },
        "arrows": arrows
    }
    
    return level_json

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

