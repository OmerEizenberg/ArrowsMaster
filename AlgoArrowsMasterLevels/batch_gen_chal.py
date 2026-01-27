import json
import random
import argparse
import sys
import os
from PIL import Image

# --- CONFIGURATION PARAMETERS ---
GENERATOR_CONFIG = {
    "SHORT_PATH_PROBABILITY": 0.3,
    "SHORT_PATH_RANGE": (3, 7),
    "LONG_PATH_RANGE": (7, 22),
    "TURN_PROBABILITY": 0.6,
    "COLOR_SIMILARITY_WEIGHT": 0.8,
    "ENFORCE_DIFFICULTY": True,
    "MAX_RETRY_ATTEMPTS": 50,
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128
}

DURATION_MULTIPLIER = 1

def rgb_to_hex(rgb):
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def color_distance(c1, c2):
    return ((c1[0]-c2[0])**2 + (c1[1]-c2[1])**2 + (c1[2]-c2[2])**2)**0.5

def generate_level_json(image_path, grid_width, grid_height, config=None):
    if config is None:
        config = GENERATOR_CONFIG

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

    avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
    avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
    center = (avg_x, avg_y)

    occupied = set()
    escape_routes = {} 
    arrows = []
    arrow_id = 1

    remaining_points = set(shape_mask)
    
    def get_dist(p):
        return (p[0] - center[0])**2 + (p[1] - center[1])**2

    while remaining_points:
        sorted_remaining = sorted(list(remaining_points), key=get_dist)
        start_node = sorted_remaining[0]
        start_color = pixel_colors[start_node][:3]
        
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
                        n_color = pixel_colors[neighbor][:3]
                        dist = color_distance(start_color, n_color)
                        color_score = max(0, 1 - (dist / 441.0))
                        
                        dir_score = 1.0
                        if current_direction:
                            dir_score = 1.0 if (dx, dy) == current_direction else config["TURN_PROBABILITY"]
                        
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

            head_x, head_y = current_path[-1]
            prev_x, prev_y = current_path[-2]
            dx, dy = head_x - prev_x, head_y - prev_y
            
            if config["ENFORCE_DIFFICULTY"] and (head_x, head_y) in escape_routes:
                blocking_dir = escape_routes[(head_x, head_y)]
                if (dx, dy) == blocking_dir:
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
                
                curr_ex, curr_ey = head_x + dx, head_y + dy
                while 0 <= curr_ex < grid_width and 0 <= curr_ey < grid_height:
                    escape_routes[(curr_ex, curr_ey)] = (dx, dy)
                    curr_ex += dx
                    curr_ey += dy

                success = True
                break
        
        if not success:
            remaining_points.remove(start_node)

    level_json = {
        "gridSize": {
            "x": grid_width,
            "y": grid_height
        },
        "arrows": arrows,
        "duration": len(occupied) * DURATION_MULTIPLIER
    }
    
    return level_json

def main():
    parser = argparse.ArgumentParser(description="Bulk generate AlgoArrows levels from a folder of images.")
    parser.add_argument("folder", help="Path to the folder containing source images")
    parser.add_argument("--min_width", type=int, default=30, help="Minimum grid width (default: 15)")
    parser.add_argument("--max_width", type=int, default=60, help="Maximum grid width (default: 50)")
    
    args = parser.parse_args()
    
    source_folder = os.path.abspath(args.folder)
    if not os.path.isdir(source_folder):
        print(f"Error: {source_folder} is not a directory.")
        sys.exit(1)
        
    parent_dir = os.path.dirname(source_folder)
    output_folder = os.path.join(parent_dir, "GeneratedLevels")
    
    if not os.path.exists(output_folder):
        os.makedirs(output_folder)
        print(f"Created output folder: {output_folder}")
    
    valid_extensions = ('.png', '.jpg', '.jpeg', '.bmp', '.gif')
    image_files = [f for f in os.listdir(source_folder) if f.lower().endswith(valid_extensions)]
    
    if not image_files:
        print(f"No valid image files found in {source_folder}")
        sys.exit(0)
        
    print(f"Found {len(image_files)} images. Starting batch generation...")
    
    for img_name in image_files:
        img_path = os.path.join(source_folder, img_name)
        
        try:
            with Image.open(img_path) as img:
                orig_w, orig_h = img.size
                
            # Random width between 15 and 50
            grid_width = random.randint(args.min_width, args.max_width)
            # Maintain aspect ratio for height
            grid_height = int(grid_width * (orig_h / orig_w))
            
            # Ensure height is at least 5 for playability
            grid_height = max(grid_height, 5)
            
            print(f"Processing {img_name} -> Grid Size: {grid_width}x{grid_height}")
            
            level_data = generate_level_json(img_path, grid_width, grid_height)
            
            if level_data:
                # Save to GeneratedLevels folder
                base_name = os.path.splitext(img_name)[0]
                output_file = os.path.join(output_folder, f"{base_name}.json")
                
                with open(output_file, 'w') as f:
                    json.dump(level_data, f, indent=2)
                print(f"  Successfully saved to {output_file}")
            else:
                print(f"  Failed to generate level for {img_name}")
                
        except Exception as e:
            print(f"  Error processing {img_name}: {e}")

    print("\nBatch generation complete!")

if __name__ == "__main__":
    main()
