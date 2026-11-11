"""Read both release ZIPs completely and verify transfer-critical metadata."""
import hashlib
import json
import stat
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def verify(name):
    archive = ROOT / 'Builds' / f'Neonflow-{name}.zip'
    with archive.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    expected = archive.with_suffix('.zip.sha256').read_text().split()[0]
    assert digest == expected, f'{name}: SHA-256 mismatch'
    with zipfile.ZipFile(archive) as package:
        assert package.testzip() is None, f'{name}: corrupt ZIP entry'
        names = set(package.namelist())
        assert len(names) == len(package.infolist()), 'Duplicate archive paths'
        assert all(p.startswith(name + '/') and '..' not in p.split('/') for p in names)
        assert not any('DontShip' in p or 'DoNotShip' in p for p in names)
        for doc in ('RUN_NEONFLOW.txt', 'THIRD_PARTY_NOTICES.txt', 'AcceptanceReport.txt'):
            assert package.read(f'{name}/{doc}') == (ROOT / doc).read_bytes(), doc
        if name == 'Windows':
            required = ('Neonflow.exe', 'UnityPlayer.dll',
                        'Neonflow_Data/Managed/Assembly-CSharp.dll',
                        'Recognition/python/python.exe', 'Recognition/python/python311.dll',
                        'Neonflow_Data/StreamingAssets/Recognition/worker.py')
            for path in required:
                assert f'Windows/{path}' in names, path
        else:
            app = 'macOS/Neonflow.app/Contents/'
            assert app + 'Info.plist' in names
            executables = [app + 'MacOS/Neonflow']
            for arch in ('arm64', 'x86_64'):
                runtime = app + f'Resources/Data/StreamingAssets/Recognition/mac-runtime/{arch}/python/'
                executables.append(runtime + 'bin/python3.11')
                assert runtime + 'lib/python3.11/site-packages/mediapipe/__init__.py' in names
                assert runtime + 'lib/python3.11/site-packages/vosk/__init__.py' in names
            for path in executables:
                info = package.getinfo(path)
                mode = info.external_attr >> 16
                assert info.create_system == 3 and stat.S_ISREG(mode) and mode & 0o111 == 0o111, path
        return {'archive': str(archive.relative_to(ROOT)), 'bytes': archive.stat().st_size,
                'files': len(names), 'uncompressedBytes': sum(i.file_size for i in package.infolist()),
                'sha256': digest, 'crcAllFiles': 'PASS', 'requiredFiles': 'PASS',
                'currentDocumentation': 'PASS', 'unixExecuteBits': 'PASS' if name == 'macOS' else None,
                'testedOnAnotherComputer': False}


if __name__ == '__main__':
    reports = []
    for target in ('Windows', 'macOS'):
        report = verify(target)
        reports.append(report)
        print(target + ': SHA-256, all CRCs, structure and documentation PASS', flush=True)
    (ROOT / 'TestResults/release-archives.json').write_text(json.dumps(reports, indent=2), encoding='utf-8')
