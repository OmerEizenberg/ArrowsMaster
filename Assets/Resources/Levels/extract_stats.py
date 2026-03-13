import os
import json
import csv
import re

def numerical_sort(value):
    numbers = re.findall(r'\d+', value)
    return int(numbers[0]) if numbers else value

def extract_level_stats():
    # Use current directory
    folder_path = os.path.dirname(os.path.abspath(__file__))
    output_csv = os.path.join(folder_path, 'level_stats.csv')
    
    results = []
    
    # Get all .json files in the same folder as the script
    files = [f for f in os.listdir(folder_path) if f.endswith('.json')]
    files.sort(key=numerical_sort)
    
    for filename in files:
        file_path = os.path.join(folder_path, filename)
        try:
            with open(file_path, 'r') as f:
                data = json.load(f)
                # Check if it's actually a level file by looking for 'arrows' key
                if 'arrows' in data:
                    arrow_count = len(data['arrows'])
                    results.append({
                        'Level Name': filename,
                        'Arrow Count': arrow_count
                    })
        except Exception as e:
            print(f"Error processing {filename}: {e}")

    if not results:
        print("No valid level JSON files found in this folder.")
        return

    with open(output_csv, 'w', newline='') as csvfile:
        fieldnames = ['Level Name', 'Arrow Count']
        writer = csv.DictWriter(csvfile, fieldnames=fieldnames)
        writer.writeheader()
        for row in results:
            writer.writerow(row)
    
    print(f"Successfully extracted stats for {len(results)} levels to {output_csv}")

if __name__ == "__main__":
    extract_level_stats()
