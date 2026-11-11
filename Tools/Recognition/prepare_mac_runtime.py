"""Assemble native macOS runtimes from verified release archives and PyPI wheels.
Cross-packaging only: actual execution/permissions must be tested on a Mac.
"""
import hashlib, json, shutil, subprocess, tarfile, urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BASE = ROOT / 'Tools/Recognition'
PYTHON = BASE / 'python/python.exe'
ARTIFACTS = {
    'arm64': ('aarch64', '3afb0567b2a2b30b3a7341bd143cb4e8a69f3d8e95d4641218eaa45f0fabbd07'),
    'x86_64': ('x86_64', 'c2e4b51e535ead2e526b8974909a1b1e00fbc2ebe2281087b9b8b14dad0cad5d'),
}

def prepare(arch):
    cpu, digest = ARTIFACTS[arch]
    filename = f'cpython-3.11.18+20261110-{cpu}-apple-darwin-install_only_stripped.tar.gz'
    url = 'https://github.com/astral-sh/python-build-standalone/releases/download/20261110/' + filename.replace('+', '%2B')
    cache = BASE / 'mac-downloads'; cache.mkdir(exist_ok=True)
    archive = cache / filename
    if not archive.exists():
        print('Download', filename, flush=True)
        with urllib.request.urlopen(url, timeout=90) as response, archive.open('wb') as out: shutil.copyfileobj(response, out)
    assert hashlib.sha256(archive.read_bytes()).hexdigest() == digest, 'Python archive checksum'
    target = BASE / 'mac-runtime' / arch; target.mkdir(parents=True, exist_ok=True)
    with tarfile.open(archive) as tar:
        for entry in tar:
            dest = (target / entry.name).resolve()
            assert dest.is_relative_to(target.resolve()), entry.name
            if entry.isdir(): dest.mkdir(parents=True, exist_ok=True)
            elif entry.isfile() or entry.issym() or entry.islnk():
                content = tar.extractfile(entry)
                if content:
                    dest.parent.mkdir(parents=True, exist_ok=True)
                    with dest.open('wb') as out: shutil.copyfileobj(content, out)
    wheels = cache / 'pure'; wheels.mkdir(exist_ok=True)
    # srt publishes a pure-Python sdist; build the portable wheel once, not a native extension.
    subprocess.run([str(PYTHON), '-m', 'pip', 'wheel', '--no-deps', '--wheel-dir', str(wheels), 'srt==3.5.3'], check=True)
    site = target / 'python/lib/python3.11/site-packages'
    report = BASE / f'macos-{arch}-packages.json'
    subprocess.run([str(PYTHON), '-m', 'pip', 'install', '--upgrade', '--no-compile', '--only-binary=:all:',
                    '--platform', f'macosx_13_0_{arch}', '--python-version', '3.11', '--implementation', 'cp', '--abi', 'cp311',
                    '--find-links', str(wheels), '--target', str(site), '--report', str(report), '-r', str(BASE/'requirements-macos.txt')], check=True)
    (target/'SOURCE.json').write_text(json.dumps({'url':url,'sha256':digest,'architecture':arch,'python':'3.11.18','testedOnMac':False},indent=2))
    print('ASSEMBLED', arch, 'native runtime; NOT EXECUTED on this Windows host', flush=True)

if __name__ == '__main__':
    import sys
    for arch in (sys.argv[1:] or ARTIFACTS): prepare(arch)
