"""Check that the standalone folder contains the pinned runtime and models."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[2]
build = root / 'Builds/Windows'
for name in ['Neonflow.exe', 'UnityPlayer.dll', 'Neonflow_Data/Managed/Assembly-CSharp.dll',
             'Recognition/python/python.exe', 'Recognition/python/python311.dll']:
    assert (build / name).is_file(), name
entries = json.loads((Path(__file__).parent / 'models-lock.json').read_text())
for item in entries:
    relative = Path(item['path'].replace('\\', '/')).relative_to('Assets/StreamingAssets')
    path = build / 'Neonflow_Data/StreamingAssets' / relative
    assert hashlib.sha256(path.read_bytes()).hexdigest() == item['sha256'], path
source = root / 'Assets/StreamingAssets/Recognition/worker.py'
packaged = build / 'Neonflow_Data/StreamingAssets/Recognition/worker.py'
assert source.read_bytes() == packaged.read_bytes(), 'stale packaged worker'
message = f'PASS: Windows player files, bundled Python, {len(entries)} model hashes, current worker.\n'
(root / 'TestResults/package.txt').write_text(message, encoding='utf-8')
print(message)
