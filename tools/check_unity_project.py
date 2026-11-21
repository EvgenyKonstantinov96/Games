#!/usr/bin/env python3
"""Read-only Unity project preflight. Does not compile or run Unity."""
from __future__ import annotations
import argparse
import json
from pathlib import Path
import sys

def inspect(root: Path) -> dict:
    checks = {}
    assets = root / "Assets"
    checks["assets_directory"] = assets.is_dir()
    checks["packages_manifest"] = (root / "Packages" / "manifest.json").is_file()
    checks["project_version"] = (root / "ProjectSettings" / "ProjectVersion.txt").is_file()
    checks["project_settings"] = (root / "ProjectSettings" / "ProjectSettings.asset").is_file()
    checks["approved_concept"] = (root / "docs" / "design" / "approved_concept.png").is_file()
    if assets.is_dir():
        checks["csharp_scripts"] = any(assets.rglob("*.cs"))
        checks["unity_scenes"] = any(assets.rglob("*.unity"))
        checks["prefabs"] = any(assets.rglob("*.prefab"))
        checks["asset_meta_files"] = any(assets.rglob("*.meta"))
    else:
        for key in ("csharp_scripts", "unity_scenes", "prefabs", "asset_meta_files"):
            checks[key] = False
    checks["package_json_valid"] = False
    if checks["packages_manifest"]:
        try:
            with (root / "Packages" / "manifest.json").open(encoding="utf-8") as stream:
                manifest = json.load(stream)
            checks["package_json_valid"] = isinstance(manifest.get("dependencies"), dict)
        except (OSError, json.JSONDecodeError, AttributeError):
            pass
    blocked = [key for key, ok in checks.items() if not ok]
    return {
        "status": "READY_FOR_UNITY_AUDIT" if not blocked else "BLOCKED",
        "checks": checks,
        "missing_or_invalid": blocked,
        "note": "Preflight only; no Unity compilation, gameplay or 100-test PASS claims.",
    }

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--json", action="store_true", help="Print machine-readable report")
    args = parser.parse_args()
    result = inspect(args.root.resolve())
    if args.json:
        print(json.dumps(result, ensure_ascii=False, indent=2))
    else:
        print("Unity source preflight:", result["status"])
        for name, passed in result["checks"].items():
            print(f"  {'OK' if passed else 'BLOCKED'}  {name}")
        print(result["note"])
    return 0 if result["status"] == "READY_FOR_UNITY_AUDIT" else 2

if __name__ == "__main__":
    sys.exit(main())
