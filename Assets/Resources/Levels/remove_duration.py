import os
import json
import sys

def remove_duration_from_json(target_dir):
    """
    Goes through all .json files in the specified directory and removes
    the 'duration' parameter from the top-level object if it exists.
    """
    if not os.path.exists(target_dir):
        print(f"Error: Directory '{target_dir}' does not exist.")
        return

    # List all files in the directory
    json_files = [f for f in os.listdir(target_dir) if f.endswith('.json')]
    
    if not json_files:
        print(f"No JSON files found in {os.path.abspath(target_dir)}.")
        return

    updated_count = 0
    skipped_count = 0
    error_count = 0

    print(f"Processing JSON files in: {os.path.abspath(target_dir)}\n")

    for filename in json_files:
        file_path = os.path.join(target_dir, filename)
        try:
            # Read the JSON file
            with open(file_path, 'r', encoding='utf-8') as f:
                try:
                    data = json.load(f)
                except json.JSONDecodeError:
                    print(f"Error: {filename} is not a valid JSON file. Skipping.")
                    error_count += 1
                    continue
            
            # Check if 'duration' key exists
            if isinstance(data, dict) and 'duration' in data:
                # Remove the key
                del data['duration']
                
                # Write the updated JSON back to the file
                with open(file_path, 'w', encoding='utf-8') as f:
                    json.dump(data, f, indent=4)
                
                print(f"✅ Updated: {filename}")
                updated_count += 1
            else:
                skipped_count += 1
                
        except Exception as e:
            print(f"❌ Error processing {filename}: {e}")
            error_count += 1

    print("\n--- Summary ---")
    print(f"Files updated: {updated_count}")
    print(f"Files skipped: {skipped_count}")
    if error_count > 0:
        print(f"Errors encountered: {error_count}")

if __name__ == "__main__":
    # Use current directory if no argument is provided
    target = sys.argv[1] if len(sys.argv) > 1 else "."
    remove_duration_from_json(target)
