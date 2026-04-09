"""
Post-Godot-import: relocate .ctex files from .godot/imported/ to <ModName>/_imported/
and update all .import files to reference the new path.  This avoids conflicting
with the host game's own .godot/ directory when the PCK is mounted.

Usage:
    python import_assets.py <pck_src_dir> [mod_name]

    mod_name defaults to "Watcher" if not specified.

Steps (run AFTER Godot headless --import):
1. Move .godot/imported/*.ctex → <ModName>/_imported/*.ctex
2. Rewrite every .import file: path=res://.godot/imported/... → path=res://<ModName>/_imported/...
3. Delete .godot/ and project.godot
"""

import os
import re
import shutil
import sys


def main():
    if len(sys.argv) < 2:
        print("Usage: python import_assets.py <pck_src_dir> [mod_name]")
        sys.exit(1)

    pck_src = os.path.abspath(sys.argv[1])
    mod_name = sys.argv[2] if len(sys.argv) >= 3 else "Watcher"

    godot_imported = os.path.join(pck_src, ".godot", "imported")
    new_imported = os.path.join(pck_src, mod_name, "_imported")

    if not os.path.isdir(godot_imported):
        print(f"ERROR: {godot_imported} does not exist. Run Godot --import first.")
        sys.exit(1)

    # Step 1: Move .ctex files
    os.makedirs(new_imported, exist_ok=True)
    moved = 0
    for fname in os.listdir(godot_imported):
        src = os.path.join(godot_imported, fname)
        dst = os.path.join(new_imported, fname)
        if os.path.isfile(src):
            shutil.move(src, dst)
            moved += 1
    print(f"Moved {moved} files to {mod_name}/_imported/")

    # Step 2: Rewrite .import files
    rewritten = 0
    for root, dirs, files in os.walk(pck_src):
        # Skip .godot directory
        if ".godot" in root.split(os.sep):
            continue
        for fname in files:
            if not fname.endswith(".import"):
                continue
            fpath = os.path.join(root, fname)
            with open(fpath, "r", encoding="utf-8") as f:
                content = f.read()

            new_content = content.replace(
                "res://.godot/imported/",
                f"res://{mod_name}/_imported/"
            )
            if new_content != content:
                with open(fpath, "w", encoding="utf-8") as f:
                    f.write(new_content)
                rewritten += 1
    print(f"Rewrote {rewritten} .import files")

    # Step 3: Remove .godot/ and project.godot
    godot_dir = os.path.join(pck_src, ".godot")
    if os.path.isdir(godot_dir):
        shutil.rmtree(godot_dir)
        print("Removed .godot/")

    project_godot = os.path.join(pck_src, "project.godot")
    if os.path.isfile(project_godot):
        os.remove(project_godot)
        print("Removed project.godot")

    # Verify: check for .import files that still reference .godot
    issues = 0
    for root, dirs, files in os.walk(pck_src):
        for fname in files:
            if not fname.endswith(".import"):
                continue
            fpath = os.path.join(root, fname)
            with open(fpath, "r", encoding="utf-8") as f:
                content = f.read()
            if ".godot/imported/" in content:
                print(f"WARNING: {fpath} still references .godot/imported/")
                issues += 1

            # Also check that the referenced .ctex exists
            match = re.search(
                rf'path="res://{re.escape(mod_name)}/_imported/([^"]+)"',
                content,
            )
            if match:
                ctex_name = match.group(1)
                ctex_path = os.path.join(new_imported, ctex_name)
                if not os.path.isfile(ctex_path):
                    print(f"WARNING: {fpath} references missing {ctex_name}")
                    issues += 1

    # Check for images without .import files
    missing_import = 0
    skip_dirs = {mod_name, ".godot"}
    for root, dirs, files in os.walk(pck_src):
        dirs[:] = [d for d in dirs if d not in skip_dirs]
        for fname in files:
            if fname.endswith((".png", ".jpg", ".jpeg", ".atlas", ".skel")):
                import_path = os.path.join(root, fname + ".import")
                if not os.path.isfile(import_path):
                    rel = os.path.relpath(os.path.join(root, fname), pck_src)
                    print(f"WARNING: No .import for {rel}")
                    missing_import += 1

    if missing_import:
        print(f"\n{missing_import} images lack .import files (Godot didn't import them)")
    if issues:
        print(f"\n{issues} issues found")
    else:
        print("\nAll good!")


if __name__ == "__main__":
    main()
