"""Compatibility entry point for the renamed administrator guide builder."""
import runpy
from pathlib import Path

if __name__ == "__main__":
    runpy.run_path(str(Path(__file__).with_name("create_icp_admin_guide.py")), run_name="__main__")
