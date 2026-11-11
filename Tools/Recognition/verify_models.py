"""Verify pinned model files without accepting new hashes implicitly."""
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def verify():
    entries = json.loads((Path(__file__).parent / 'models-lock.json').read_text(encoding='utf-8'))
    for item in entries:
        path = ROOT / item['path'].replace('\\', '/')
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != item['sha256']:
            raise ValueError(f'Model checksum mismatch: {path}')
    return len(entries)


if __name__ == '__main__':
    print(f'PASS: {verify()} pinned model files verified.')
