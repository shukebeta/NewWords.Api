import os
import sys
import re

# --- Configuration ---
INPUT_DIR = "documents/word-collection"
OUTPUT_SQL_FILE = "documents/import_words.sql"
BATCH_SIZE = 500
TARGET_TABLE = "WordCollection"
LANGUAGE = "english"
# --- End Configuration ---

def escape_sql_string(value):
    """Escapes single quotes for SQL insertion."""
    if value is None:
        return "NULL"
    return value.replace("'", "''")

def is_valid_english_word_char(char):
    """Checks if a character is a valid part of an English word (letter, hyphen, or internal apostrophe)."""
    return char.isalpha() or char == '-' or char == "'"

def clean_and_validate_word(raw_word):
    """Cleans and validates the extracted word."""
    if not raw_word:
        return None

    # 1. Trim whitespace AND leading/trailing single quotes aggressively
    cleaned = raw_word.strip().strip("'")

    # 2. Normalize consecutive internal quotes (e.g., O''Malley -> O'Malley)
    cleaned = re.sub(r"'{2,}", "'", cleaned)

    # 3. Validate characters: only letters, hyphens, internal apostrophes
    #    Ensure it's not empty after cleaning and doesn't start/end with hyphen/apostrophe
    if not cleaned or \
       not all(is_valid_english_word_char(c) for c in cleaned) or \
       cleaned.startswith('-') or cleaned.endswith('-') or \
       cleaned.startswith("'") or cleaned.endswith("'"): # Re-check after cleaning
           # print(f"Debug: Failed validation: '{raw_word}' -> '{cleaned}'")
           return None

    # 4. Length check
    if len(cleaned) > 255:
        # print(f"Debug: Failed length check: '{cleaned[:50]}...'")
        return None

    return cleaned


def main():
    """Reads word files, extracts unique words, and generates SQL."""
    # Assume script is run from workspace root or use absolute paths if needed
    input_path = INPUT_DIR
    output_sql_path = OUTPUT_SQL_FILE

    print(f"Input directory: {input_path}")
    print(f"Output SQL file: {output_sql_path}")

    if not os.path.isdir(input_path):
        print(f"Error: Input directory not found: {input_path}", file=sys.stderr)
        sys.exit(1)

    unique_words = set()
    file_count = 0
    processed_lines = 0
    skipped_lines = 0

    try:
        print("Scanning for .txt files...")
        for filename in os.listdir(input_path):
            if filename.lower().endswith(".txt"):
                file_path = os.path.join(input_path, filename)
                file_count += 1
                print(f"  Processing file: {filename}")
                try:
                    with open(file_path, 'r', encoding='utf-8') as f:
                        for line in f:
                            processed_lines += 1
                            stripped_line = line.strip()
                            if stripped_line:
                                parts = stripped_line.split(maxsplit=1)
                                if parts:
                                    raw_word = parts[0]
                                    # --- Use new cleaning/validation ---
                                    valid_word = clean_and_validate_word(raw_word)
                                    if valid_word:
                                        unique_words.add(valid_word)
                                    else:
                                        skipped_lines += 1
                                else:
                                    skipped_lines += 1 # Line had content but split failed
                            # else: ignore empty lines (already handled by strip)

                except Exception as e:
                    print(f"    Warning: Could not process file {filename} fully. Error: {e}", file=sys.stderr)

        if file_count == 0:
             print(f"Warning: No .txt files found in {input_path}", file=sys.stderr)
        else:
             print(f"Processed {file_count} files and {processed_lines} lines.")

        print(f"Found {len(unique_words)} unique valid words.")
        if skipped_lines > 0:
            print(f"Skipped {skipped_lines} lines/words due to invalid format or content.")


        if not unique_words:
            print("No unique valid words found to insert.")
            with open(output_sql_path, 'w', encoding='utf-8') as outfile:
                 outfile.write("-- No valid words found to import.\n")
            print(f"Empty SQL file created: {output_sql_path}")
            return

        sorted_words = sorted(list(unique_words))

        print(f"Generating SQL file with batch size {BATCH_SIZE}...")
        with open(output_sql_path, 'w', encoding='utf-8') as outfile:
            outfile.write(f"-- SQL import script generated for {TARGET_TABLE}\n")
            outfile.write(f"-- Found {len(sorted_words)} unique valid words from {INPUT_DIR}\n\n")

            for i in range(0, len(sorted_words), BATCH_SIZE):
                batch = sorted_words[i:i + BATCH_SIZE]
                if not batch:
                    continue

                sql = f"INSERT IGNORE INTO {TARGET_TABLE} (WordText, Language, QueryCount, CreatedAt) VALUES\n"
                values = []
                for word in batch:
                    escaped_word = escape_sql_string(word) # Escape the final cleaned word
                    values.append(f"  ('{escaped_word}', '{LANGUAGE}', 0, unix_timestamp())")

                sql += ",\n".join(values)
                sql += ";\n\n"
                outfile.write(sql)

        print(f"Successfully generated SQL file: {output_sql_path}")

    except Exception as e:
        print(f"An unexpected error occurred: {e}", file=sys.stderr)
        sys.exit(1)

if __name__ == "__main__":
    main()