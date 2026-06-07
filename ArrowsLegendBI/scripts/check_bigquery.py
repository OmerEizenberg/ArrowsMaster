#!/usr/bin/env python3
"""Print BigQuery auth status and run a quick GA4 export sanity query."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

from server.main import auth_setup_steps, auth_status, health  # noqa: E402


def main() -> int:
    print("=== ArrowsLegendBI — BigQuery check ===\n")
    h = health()
    print(json.dumps({k: v for k, v in h.items() if k != "setup_steps"}, indent=2))
    steps = h.get("setup_steps") or []
    if steps:
        print("\nSetup steps:")
        for i, step in enumerate(steps, 1):
            print(f"  {i}. {step}")
        return 1
    print("\nAuth OK. Open dashboard: npm run dev → http://localhost:5173")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
