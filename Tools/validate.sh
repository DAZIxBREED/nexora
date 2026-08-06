#!/usr/bin/env bash
set -euo pipefail
python3 - <<'PY'
import json
from pathlib import Path
packages = sorted(Path('Packages').glob('com.nexora.*'))
assert packages, 'No Nexora packages found'
for package in packages:
    with open(package / 'package.json', encoding='utf-8') as handle:
        data = json.load(handle)
    assert data['name'] == package.name
print(f'Validated {len(packages)} Nexora packages')
PY
