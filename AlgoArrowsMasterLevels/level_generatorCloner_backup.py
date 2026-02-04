import json
import random
import argparse
import sys
import os
from PIL import Image

# --- DIFFICULTY CONFIGURATIONS ---
# Difficulty ranges from 1 (Easy) to 4 (Hard)
DIFFICULTY_CONFIGS = {
    1: {  # Easy
        "WIDTH_RANGE": (15, 25),              # Smaller grids
        "SHORT_PATH_PROBABILITY": 0.7,        # More short arrows
        "SHORT_PATH_RANGE": (2, 4),
        "LONG_PATH_RANGE": (5, 8),
        "TURN_PROBABILITY": 0.3,              # Less turning, straighter paths
        "COLOR_SIMILARITY_WEIGHT": 0.3,       # Less strict on color matching
        "BLOCKING_ALLOWED": False,            # No blocking - all arrows free
        "START_FROM_EDGE": True,              # Start from edges (easier to see options)
        "ENCOURAGE_BLOCKING": False,          # No blocking encouraged
    },
    2: {  # Medium
        "WIDTH_RANGE": (20, 35),
        "SHORT_PATH_PROBABILITY": 0.5,
        "SHORT_PATH_RANGE": (3, 5),
        "LONG_PATH_RANGE": (6, 10),
        "TURN_PROBABILITY": 0.5,
        "COLOR_SIMILARITY_WEIGHT": 0.5,
        "BLOCKING_ALLOWED": True,
        "START_FROM_EDGE": False,
        "ENCOURAGE_BLOCKING": False,          # Some blocking but not encouraged
    },
    3: {  # Hard
        "WIDTH_RANGE": (30, 45),              # Larger grids
        "SHORT_PATH_PROBABILITY": 0.3,        # More long arrows
        "SHORT_PATH_RANGE": (3, 6),
        "LONG_PATH_RANGE": (7, 18),
        "TURN_PROBABILITY": 0.7,              # More turning, complex paths
        "COLOR_SIMILARITY_WEIGHT": 0.9,       # Very strict - follow structure closely
        "BLOCKING_ALLOWED": True,             # Allow blocking
        "START_FROM_EDGE": False,             # Start from center (harder beginning)
        "ENCOURAGE_BLOCKING": True,           # Actively try to block other arrows
    },
    4: {  # Very Hard
        "WIDTH_RANGE": (35, 50),              # Largest grids
        "SHORT_PATH_PROBABILITY": 0.2,        # Mostly long arrows
        "SHORT_PATH_RANGE": (4, 6),
        "LONG_PATH_RANGE": (8, 24),
        "TURN_PROBABILITY": 0.8,              # Very twisty
        "COLOR_SIMILARITY_WEIGHT": 0.95,      # Extremely strict - follow structure
        "BLOCKING_ALLOWED": True,
        "START_FROM_EDGE": False,             # Start from center (very constrained)
        "ENCOURAGE_BLOCKING": True,           # Actively try to block other arrows
        "PREFER_PERPENDICULAR_BLOCKING": True, # Prefer blocking arrows pointing different directions
        "MULTI_PASS_GENERATION": True,        # Generate in passes: center->middle->edges
    }
}

# Common configuration
COMMON_CONFIG = {
    "MAX_RETRY_ATTEMPTS": 100,
    "WHITE_THRESHOLD": 245,
    "ALPHA_THRESHOLD": 128,
}

def rgb_to_hex(rgb):
    """Convert RGB tuple to hex color string."""
    return '#{:02x}{:02x}{:02x}'.format(rgb[0], rgb[1], rgb[2])

def color_distance(c1, c2):
    """Calculate Euclidean distance between two RGB colors."""
    return ((c1[0]-c2[0])**2 + (c1[1]-c2[1])**2 + (c1[2]-c2[2])**2)**0.5

def check_arrow_escape(path, direction, grid_width, grid_height, occupied):
    """
    Verifies that an arrow can escape based on game logic.
    
    An arrow can escape if there's a clear ray from its head in the direction
    it's pointing until it goes out of bounds, without hitting:
    1. Other arrows (occupied cells)
    2. Its own body (self-blocking)
    """
    if len(path) < 2:
        return False
        
    head = path[-1]
    dx, dy = direction
    
    # Convert path to set for O(1) lookup
    path_set = set(path)
    
    # Check if there's a clear ray from head in the direction
    check_x, check_y = head[0] + dx, head[1] + dy
    
    while 0 <= check_x < grid_width and 0 <= check_y < grid_height:
        # Check both occupied cells AND the arrow's own body
        if (check_x, check_y) in occupied or (check_x, check_y) in path_set:
            return False
        check_x += dx
        check_y += dy
    
    # Ray is clear until out of bounds
    return True

def get_distance_from_center(point, center):
    """Calculate squared distance from center (for sorting)."""
    return (point[0] - center[0])**2 + (point[1] - center[1])**2

def get_distance_from_edge(point, grid_width, grid_height):
    """Calculate minimum distance from any edge."""
    return min(point[0], point[1], grid_width - 1 - point[0], grid_height - 1 - point[1])

def generate_level_json(image_path, difficulty=2):
    """
    Generate a solvable level from an image with specified difficulty.
    
    Args:
        image_path: Path to the source image
        difficulty: Integer from 1-4 (1=Easy, 4=Very Hard)
    
    Returns:
        Dictionary containing level data, or None if generation failed
    """
    # Get configuration for this difficulty
    config = COMMON_CONFIG.copy()
    diff_config = DIFFICULTY_CONFIGS.get(difficulty, DIFFICULTY_CONFIGS[2])
    config.update(diff_config)
    
    print(f"Generating Level - Difficulty: {difficulty}")
    
    # 1. Load and process image
    try:
        img = Image.open(image_path).convert('RGBA')
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None
    
    # Get original dimensions
    orig_width, orig_height = img.size
    
    # Calculate grid size based on difficulty
    width_min, width_max = config["WIDTH_RANGE"]
    grid_width = random.randint(width_min, width_max)
    
    # Calculate height maintaining aspect ratio
    grid_height = int(grid_width * (orig_height / orig_width))
    grid_height = max(10, grid_height)  # Ensure minimum height
    
    print(f"Grid Size: {grid_width}x{grid_height}")
    
    # Resize image to grid
    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    # 2. Extract shape mask and pixel colors
    shape_mask = []
    pixel_colors = {}
    
    for y in range(grid_height):
        for x in range(grid_width):
            color = img.getpixel((x, y))
            r, g, b, a = color
            pixel_colors[(x, y)] = color
            
            # Check if pixel is part of the shape (not transparent or white)
            is_transparent = a < config["ALPHA_THRESHOLD"]
            is_white = (r > config["WHITE_THRESHOLD"] and 
                       g > config["WHITE_THRESHOLD"] and 
                       b > config["WHITE_THRESHOLD"])
            
            if not is_transparent and not is_white:
                shape_mask.append((x, y))
    
    if not shape_mask:
        print("No valid shape detected in image")
        return None
    
    # 3. Calculate center of shape
    avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
    avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
    center = (avg_x, avg_y)
    
    print(f"Shape center: ({avg_x:.1f}, {avg_y:.1f}), Total pixels: {len(shape_mask)}")
    
    # 4. Initialize generation state
    occupied = set()
    escape_routes = {}  # Maps (x,y) -> (dx,dy) direction of arrows passing above
    arrows = []
    arrow_id = 1
    remaining_points = set(shape_mask)
    
    # 5. Generate arrows (with multi-pass for very hard difficulty)
    if config.get("MULTI_PASS_GENERATION", False):
        # Multi-pass generation: center -> middle -> edges
        # This ensures outer arrows block inner ones
        
        # Calculate distance from center for all points
        point_distances = {p: get_distance_from_center(p, center) for p in shape_mask}
        max_dist = max(point_distances.values()) if point_distances else 1
        
        # Define 3 passes: inner (0-33%), middle (33-66%), outer (66-100%)
        passes = [
            ("Inner", 0.0, 0.33),
            ("Middle", 0.33, 0.66),
            ("Outer", 0.66, 1.0)
        ]
        
        print(f"Multi-pass generation enabled (3 passes)")
        
        for pass_name, min_pct, max_pct in passes:
            # Filter points for this pass
            pass_points = {
                p for p in remaining_points 
                if min_pct * max_dist <= point_distances[p] < max_pct * max_dist
            }
            
            if not pass_points:
                continue
                
            print(f"  Pass '{pass_name}': {len(pass_points)} points to process")
            
            # Generate arrows only from points in this pass
            while pass_points and remaining_points:
                # Select starting point from this pass's points
                sorted_pass = sorted(
                    list(pass_points & remaining_points),
                    key=lambda p: get_distance_from_center(p, center)
                )
                
                if not sorted_pass:
                    break
                    
                start_node = sorted_pass[0]
                start_color = pixel_colors[start_node][:3]
                
                success = False
                
                # Try to create a valid arrow from this starting point
                success = try_generate_arrow(
                    start_node, start_color, remaining_points, occupied, 
                    escape_routes, arrows, pixel_colors, 
                    grid_width, grid_height, config
                )
                
                if not success:
                    # Couldn't create a valid arrow from this point, skip it
                    remaining_points.discard(start_node)
                    pass_points.discard(start_node)
    else:
        # Single-pass generation (original logic)
        while remaining_points:
            # Select starting point based on difficulty
            if config["START_FROM_EDGE"]:
                # Easy: Start from edges (more visible, easier to pick)
                sorted_remaining = sorted(
                    list(remaining_points),
                    key=lambda p: -get_distance_from_edge(p, grid_width, grid_height)
                )
            else:
                # Hard: Start from center (more constrained, harder beginning)
                sorted_remaining = sorted(
                    list(remaining_points),
                    key=lambda p: get_distance_from_center(p, center)
                )
            
            start_node = sorted_remaining[0]
            start_color = pixel_colors[start_node][:3]
            
            success = try_generate_arrow(
                start_node, start_color, remaining_points, occupied, 
                escape_routes, arrows, pixel_colors, 
                grid_width, grid_height, config
            )
            
            if not success:
                # Couldn't create a valid arrow from this point, skip it
                remaining_points.remove(start_node)

def try_generate_arrow(start_node, start_color, remaining_points, occupied, 
                       escape_routes, arrows, pixel_colors, 
                       grid_width, grid_height, config):
    """
    Try to generate a valid arrow starting from start_node.
    Returns True if successful, False otherwise.
    """
    success = False
        
        # Try to create a valid arrow from this starting point
        for attempt in range(config["MAX_RETRY_ATTEMPTS"]):
            # Decide arrow length
            if random.random() < config["SHORT_PATH_PROBABILITY"]:
                target_length = random.randint(*config["SHORT_PATH_RANGE"])
            else:
                target_length = random.randint(*config["LONG_PATH_RANGE"])
            
            # Build arrow path
            current_path = [start_node]
            temp_occupied = {start_node}
            current_direction = None
            
            # Grow the arrow
            for i in range(target_length - 1):
                last_x, last_y = current_path[-1]
                directions = [(1, 0), (-1, 0), (0, 1), (0, -1)]
                
                # Score each possible neighbor
                scored_neighbors = []
                for dx, dy in directions:
                    neighbor = (last_x + dx, last_y + dy)
                    
                    # Check if neighbor is valid
                    if neighbor in remaining_points and neighbor not in temp_occupied:
                        n_color = pixel_colors[neighbor][:3]
                        
                        # Color similarity score
                        dist = color_distance(start_color, n_color)
                        color_score = max(0, 1 - (dist / 441.0))
                        
                        # Direction continuity score
                        dir_score = 1.0
                        if current_direction:
                            if (dx, dy) == current_direction:
                                dir_score = 1.0  # Continue straight
                            else:
                                dir_score = config["TURN_PROBABILITY"]  # Turn
                        
                        # Combined score
                        total_score = (color_score * config["COLOR_SIMILARITY_WEIGHT"] + 
                                     dir_score * (1 - config["COLOR_SIMILARITY_WEIGHT"]))
                        
                        scored_neighbors.append((neighbor, total_score, (dx, dy)))
                
                if not scored_neighbors:
                    break  # Dead end
                
                # Select best neighbor (with some randomness)
                scored_neighbors.sort(key=lambda x: x[1], reverse=True)
                best_score = scored_neighbors[0][1]
                top_candidates = [n for n in scored_neighbors if n[1] >= best_score * 0.9]
                next_node, _, chosen_dir = random.choice(top_candidates)
                
                current_path.append(next_node)
                temp_occupied.add(next_node)
                current_direction = chosen_dir
            
            # Validate the generated path
            if len(current_path) < 2:
                continue  # Path too short
            
            # Get arrow direction (from second-to-last to last point)
            head_x, head_y = current_path[-1]
            prev_x, prev_y = current_path[-2]
            dx, dy = head_x - prev_x, head_y - prev_y
            
            # Check blocking behavior based on difficulty
            blocks_existing = False
            is_perpendicular_block = False
            
            if config["BLOCKING_ALLOWED"] and (head_x, head_y) in escape_routes:
                blocking_dir = escape_routes[(head_x, head_y)]
                if (dx, dy) == blocking_dir:
                    blocks_existing = True
                    
                    # Check if this is a perpendicular block (different direction)
                    # Perpendicular means: if blocked arrow points horizontally, blocker points vertically
                    blocked_is_horizontal = blocking_dir[0] != 0  # (1,0) or (-1,0)
                    blocker_is_horizontal = dx != 0  # (1,0) or (-1,0)
                    
                    if blocked_is_horizontal != blocker_is_horizontal:
                        is_perpendicular_block = True
            
            # For hard difficulties, PREFER arrows that block others
            # For difficulty 4, STRONGLY prefer perpendicular blocking
            # For easier difficulties, AVOID blocking
            if config.get("ENCOURAGE_BLOCKING", False):
                # Hard mode: Skip arrows that DON'T block (unless we're running out of options)
                if not blocks_existing and len(arrows) > 3 and attempt < config["MAX_RETRY_ATTEMPTS"] * 0.7:
                    continue  # Try to find a blocking arrow instead
                
                # Difficulty 4: Prefer perpendicular blocking (different directions)
                if config.get("PREFER_PERPENDICULAR_BLOCKING", False):
                    # If we found a blocking arrow but it's NOT perpendicular, keep trying
                    if blocks_existing and not is_perpendicular_block and attempt < config["MAX_RETRY_ATTEMPTS"] * 0.5:
                        continue  # Try to find a perpendicular blocking arrow instead
            else:
                # Easy/Medium mode: Skip arrows that DO block
                if blocks_existing:
                    continue
            
            # Check if arrow can escape
            escapable = check_arrow_escape(current_path, (dx, dy), grid_width, grid_height, occupied)
            
            if escapable:
                # Calculate average color
                avg_r = sum(pixel_colors[p][0] for p in current_path) // len(current_path)
                avg_g = sum(pixel_colors[p][1] for p in current_path) // len(current_path)
                avg_b = sum(pixel_colors[p][2] for p in current_path) // len(current_path)
                
                # Create arrow object
                arrow_obj = {
                    "id": arrow_id,
                    "color": rgb_to_hex((avg_r, avg_g, avg_b)),
                    "path": [{"x": p[0], "y": p[1]} for p in current_path]
                }
                arrows.append(arrow_obj)
                arrow_id += 1
                
                # Mark cells as occupied
                for p in current_path:
                    occupied.add(p)
                    remaining_points.remove(p)
                
                # Update escape routes (mark cells this arrow passes over)
                curr_ex, curr_ey = head_x + dx, head_y + dy
                while 0 <= curr_ex < grid_width and 0 <= curr_ey < grid_height:
                    escape_routes[(curr_ex, curr_ey)] = (dx, dy)
                    curr_ex += dx
                    curr_ey += dy
                
                success = True
                print(f"  Arrow {arrow_id-1}: length={len(current_path)}, color={arrow_obj['color']}")
                break
        
        if not success:
            # Couldn't create a valid arrow from this point, skip it
            remaining_points.remove(start_node)
            print(f"  Skipped isolated point at {start_node}")
    
    # 6. Create level JSON
    level_json = {
        "gridSize": {
            "x": grid_width,
            "y": grid_height
        },
        "arrows": arrows
    }
    
    print(f"Level generated successfully! Total arrows: {len(arrows)}")
    return level_json

def main():
    parser = argparse.ArgumentParser(
        description="Generate solvable AlgoArrows levels with difficulty settings"
    )
    parser.add_argument("--image", help="Input image path")
    parser.add_argument(
        "--difficulty", 
        type=int, 
        default=2, 
        choices=[1, 2, 3, 4],
        help="Difficulty level: 1=Easy, 2=Medium, 3=Hard, 4=Very Hard"
    )
    parser.add_argument("--output", default="level_output.json", help="Output JSON file path")
    
    args = parser.parse_args()
    
    # Get image path
    image_path = args.image
    if not image_path:
        try:
            import tkinter as tk
            from tkinter import filedialog
            root = tk.Tk()
            root.withdraw()
            image_path = filedialog.askopenfilename(
                title="Select an image file",
                filetypes=[
                    ("Image files", "*.png *.jpg *.jpeg *.bmp *.gif"),
                    ("All files", "*.*")
                ]
            )
            root.destroy()
        except:
            print("Error: Could not open file dialog. Please specify --image argument.")
            sys.exit(1)
    
    if not image_path or not os.path.exists(image_path):
        print(f"Error: Image file not found: {image_path}")
        sys.exit(1)
    
    # Generate level
    level_data = generate_level_json(image_path, args.difficulty)
    
    if level_data:
        # Save to file
        with open(args.output, 'w') as f:
            json.dump(level_data, f, indent=2)
        print(f"\nLevel saved to: {args.output}")
    else:
        print("\nFailed to generate level")
        sys.exit(1)

if __name__ == "__main__":
    main()
