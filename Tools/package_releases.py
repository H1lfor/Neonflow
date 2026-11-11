"""Create transferable archives; preserve Unix executable bits for the Mac app."""
import hashlib, json, plistlib, shutil, stat, sys, zipfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent/'Recognition'))
from verify_mac_runtime import macho, verify

ROOT=Path(__file__).resolve().parents[1]
DOCS=('RUN_NEONFLOW.txt','THIRD_PARTY_NOTICES.txt','AcceptanceReport.txt')

def zip_release(source, target):
    with zipfile.ZipFile(target,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=4) as out:
        for file in sorted(source.rglob('*')):
            if not file.is_file():continue
            if any('DontShip' in p or 'DoNotShip' in p for p in file.relative_to(source).parts):continue
            relative=source.name+'/'+file.relative_to(source).as_posix()
            with file.open('rb') as f:magic=f.read(4)
            executable=bool(macho(file)) or magic[:2]==b'#!' or file.suffix=='.command'
            info=zipfile.ZipInfo.from_file(file,relative)
            info.create_system=3;info.external_attr=(stat.S_IFREG|(0o755 if executable else 0o644))<<16
            info.compress_type=zipfile.ZIP_DEFLATED
            with out.open(info,'w',force_zip64=True) as dest, file.open('rb') as src:shutil.copyfileobj(src,dest,1024*1024)
    digest=hashlib.file_digest(target.open('rb'),'sha256').hexdigest()
    target.with_suffix('.zip.sha256').write_text(digest+'  '+target.name+'\n')
    print(target.name, target.stat().st_size, 'bytes', digest,flush=True)

def prepare_mac():
    folder=ROOT/'Builds/macOS';app=folder/'Neonflow.app'
    plist=plistlib.loads((app/'Contents/Info.plist').read_bytes())
    binary=app/'Contents/MacOS'/plist['CFBundleExecutable']
    assert {s['arch'] for s in macho(binary)}=={'arm64','x86_64'},'Unity player must be Universal'
    assert plist.get('NSCameraUsageDescription') and plist.get('NSMicrophoneUsageDescription')
    stream=app/'Contents/Resources/Data/StreamingAssets'
    source_worker=ROOT/'Assets/StreamingAssets/Recognition/worker.py'
    assert (stream/'Recognition/worker.py').read_bytes()==source_worker.read_bytes()
    results=[verify(stream/'Recognition/mac-runtime'/arch,arch) for arch in ('arm64','x86_64')]
    for item in json.loads((ROOT/'Tools/Recognition/models-lock.json').read_text()):
        path=stream/Path(item['path'].replace('\\','/')).relative_to('Assets/StreamingAssets')
        assert hashlib.sha256(path.read_bytes()).hexdigest()==item['sha256']
    third=folder/'ThirdParty';third.mkdir(exist_ok=True)
    for source in (ROOT/'Builds/Windows/ThirdParty').iterdir():
        if source.name.endswith('.dist-info'):continue
        if source.is_dir():shutil.copytree(source,third/source.name,dirs_exist_ok=True)
        else:shutil.copy2(source,third/source.name)
    for arch in ('arm64','x86_64'):
        site=stream/'Recognition/mac-runtime'/arch/'python/lib/python3.11/site-packages'
        for dist in site.glob('*.dist-info'):
            for file in dist.rglob('*'):
                if file.is_file() and any(token in file.name.lower() for token in ('license','notice','copying')):
                    dest=third/arch/dist.name/file.relative_to(dist);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,dest)
        shutil.copy2(ROOT/'Tools/Recognition/mac-runtime'/arch/'python/lib/python3.11/LICENSE.txt',third/f'Python-{arch}-LICENSE.txt')
    report={'unityPlayer':'Universal arm64+x86_64','runtimes':results,'modelHashes':16,'cameraMicrophoneUsageStrings':True,'executedOnMac':False,'developerIdNotarized':False}
    (ROOT/'TestResults/package-macos.json').write_text(json.dumps(report,indent=2))

if __name__=='__main__':
    if sys.argv[1:]==['--verify-macos']:
        prepare_mac();print('Mac package contents verified; execution on Mac remains untested.');sys.exit(0)
    targets=sys.argv[1:] or ['Windows','macOS']
    for name in targets:
        if name=='macOS':prepare_mac()
        folder=ROOT/'Builds'/name
        for doc in DOCS:shutil.copy2(ROOT/doc,folder/doc)
        zip_release(folder,ROOT/'Builds'/f'Neonflow-{name}.zip')
