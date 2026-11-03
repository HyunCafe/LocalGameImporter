#!/usr/bin/env python3
"""Refresh SHA256SUMS.txt from already-staged Git files; never include untracked local data."""
from pathlib import Path
from hashlib import sha256
import subprocess

root = Path(__file__).resolve().parents[1]
index = subprocess.run(["git", "-C", str(root), "ls-files", "-z"],
                       capture_output=True, check=True).stdout
names = sorted({n.decode("utf-8") for n in index.split(b"\0") if n})
if not names or "Source/Host.cs" not in names or "extension.yaml" not in names:
    raise SystemExit("BLOCKED: Expected staged source files absent")
lines = []
for name in names:
    if name == "SHA256SUMS.txt":
        continue
    file = root / name
    if file.is_symlink() or not file.is_file():
        raise SystemExit("BLOCKED: Missing or linked Git source: " + name)
    h = sha256()
    with file.open("rb") as f:
        for block in iter(lambda: f.read(1048576), b""):
            h.update(block)
    lines.append(f"{h.hexdigest()}  {name}")
(root / "SHA256SUMS.txt").write_text("\n".join(lines)+"\n", encoding="utf-8", newline="\n")
print("PASS:", len(lines), "tracked file checksums refreshed; SHA256SUMS.txt excludes itself")
