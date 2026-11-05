#!/usr/bin/env python3
"""Extracts a .unitypackage (tar.gz of <guid>/{asset,asset.meta,pathname}) into a Unity project, keeping GUIDs.
Used for TMP Essential Resources: AssetDatabase.ImportPackage is asynchronous and never completes in -batchmode -quit.
Usage: extract_unitypackage.py <package> <projectDir>"""
import os, sys, tarfile

pkg, project = sys.argv[1], sys.argv[2]
entries = {}
with tarfile.open(pkg, "r:gz") as tar:
    for m in tar.getmembers():
        if not m.isfile():
            continue
        guid, _, name = m.name.lstrip("./").partition("/")
        entries.setdefault(guid, {})[name] = tar.extractfile(m).read()

written = 0
for guid, files in entries.items():
    if "pathname" not in files:
        continue
    rel = files["pathname"].decode("utf-8").splitlines()[0].strip()
    if not rel.startswith("Assets/") or ".." in rel.split("/"):
        continue                                    # never write outside Assets/
    dest = os.path.join(project, rel)
    if "asset" in files:
        os.makedirs(os.path.dirname(dest), exist_ok=True)
        with open(dest, "wb") as f:
            f.write(files["asset"])
        written += 1
    else:
        os.makedirs(dest, exist_ok=True)            # folder entry
    if "asset.meta" in files:
        with open(dest + ".meta", "wb") as f:
            f.write(files["asset.meta"])
print(f"extracted {written} files from {os.path.basename(pkg)}")
