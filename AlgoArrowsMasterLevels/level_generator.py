import json
import random
import argparse
import sys
import os
from PIL import Image

def generate_level_json(image_path, grid_width, grid_height, short_prob=0.7):
    # 1. עיבוד התמונה למפת גריד
    try:
        img = Image.open(image_path).convert('L')
        # fix the "upside down" issue
        img = img.transpose(Image.FLIP_TOP_BOTTOM)
    except Exception as e:
        print(f"Error opening image: {e}")
        return None

    img = img.resize((grid_width, grid_height), Image.NEAREST)
    
    # מיפוי התאים שמרכיבים את הצורה (שחור בתמונה המקורית)
    shape_mask = []
    for y in range(grid_height):
        for x in range(grid_width):
            if img.getpixel((x, y)) < 128:
                shape_mask.append((x, y))

    if not shape_mask:
        print("No shape detected in image.")
        return None

    # חישוב מרכז הצורה
    avg_x = sum(p[0] for p in shape_mask) / len(shape_mask)
    avg_y = sum(p[1] for p in shape_mask) / len(shape_mask)
    center = (avg_x, avg_y)

    occupied = set()
    arrows = []
    arrow_id = 1

    # רשימת נקודות פנויות ממוינות לפי מרחק מהמרכז (הקרוב ביותר ראשון)
    remaining_points = set(shape_mask)
    
    def get_dist(p):
        return (p[0] - center[0])**2 + (p[1] - center[1])**2

    # נמשיך לנסות למלא כל עוד יש נקודות פנויות
    while remaining_points:
        # בחר את הנקודה הפנויה הקרובה ביותר למרכז
        sorted_remaining = sorted(list(remaining_points), key=get_dist)
        start_node = sorted_remaining[0]
        
        success = False
        # ננסה מספר כיוונים ואורכים כדי למצוא מסלול תקין
        # נערבב כיוונים אפשריים בכל צעד
        for _ in range(20): # יותר נסיונות לכל נקודה
            if random.random() < short_prob:
                target_length = random.randint(2, 3)
            else:
                target_length = random.randint(4, 6)
                
            current_path = [start_node]
            temp_occupied = {start_node}
            
            # בניית מסלול
            for i in range(target_length - 1):
                last_x, last_y = current_path[-1]
                neighbors = [
                    (last_x + 1, last_y), (last_x - 1, last_y),
                    (last_x, last_y + 1), (last_x, last_y - 1)
                ]
                # סינון שכנים: בתוך ה-shape_mask, לא ב-occupied הכללי, לא ב-temp_occupied
                valid_neighbors = [
                    n for n in neighbors 
                    if n in remaining_points and n not in temp_occupied
                ]
                
                if not valid_neighbors:
                    break
                
                next_node = random.choice(valid_neighbors)
                current_path.append(next_node)
                temp_occupied.add(next_node)
            
            if len(current_path) < 2:
                continue

            # בדיקת יציאה (Escape check)
            head_x, head_y = current_path[-1]
            prev_x, prev_y = current_path[-2]
            dx = head_x - prev_x
            dy = head_y - prev_y
            
            escapable = True
            check_x, check_y = head_x + dx, head_y + dy
            while 0 <= check_x < grid_width and 0 <= check_y < grid_height:
                if (check_x, check_y) in occupied:
                    escapable = False
                    break
                check_x += dx
                check_y += dy
            
            if escapable:
                arrow_obj = {
                    "id": arrow_id,
                    "color": "#FFFFFF",
                    "path": [{"x": p[0], "y": p[1]} for p in current_path]
                }
                arrows.append(arrow_obj)
                arrow_id += 1
                for p in current_path:
                    occupied.add(p)
                    remaining_points.remove(p)
                success = True
                break
        
        if not success:
            # אם לא הצלחנו למצוא מסלול שיוצא מהנקודה הזו, נוריד אותה מהרשימה כדי לא להיתקע
            # אבל לא נוסיף אותה ל-occupied כדי שלא תחסום אחרים שלא לצורך
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
    parser.add_argument("--prob", type=float, default=0.6, help="Probability of short arrows (default: 0.6)")
    parser.add_argument("--output", default="level_output.json", help="Output JSON file name (default: level_output.json)")

    args = parser.parse_args()

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
    output_data = generate_level_json(image_path, args.width, args.height, args.prob)

    if output_data:
        with open(args.output, 'w') as f:
            json.dump(output_data, f, indent=2)
        print(f"Successfully generated level with {len(output_data['arrows'])} arrows.")
        print(f"Saved to: {args.output}")
    else:
        print("Failed to generate level.")

if __name__ == "__main__":
    main()
