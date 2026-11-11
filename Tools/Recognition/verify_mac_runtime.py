"""Static native-format checks, deliberately not a substitute for running on macOS."""
import json, struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CPUS = {0x01000007: 'x86_64', 0x0100000c: 'arm64'}

def macho(path):
    with path.open('rb') as f:
        magic = f.read(4)
        if magic in (b'\xca\xfe\xba\xbe', b'\xca\xfe\xba\xbf'):
            count = struct.unpack('>I', f.read(4))[0]
            if count > 20: return []
            entries = []
            for _ in range(count):
                record = f.read(32 if magic[-1] == 0xbf else 20)
                cpu = struct.unpack_from('>I', record)[0]
                offset = struct.unpack_from('>Q' if magic[-1] == 0xbf else '>I', record,8)[0]
                entries.append((cpu,offset))
        elif magic == b'\xcf\xfa\xed\xfe':
            f.seek(4); entries = [(struct.unpack('<I',f.read(4))[0],0)]
        else: return []
        result = []
        for cpu, offset in entries:
            f.seek(offset); header = f.read(32)
            assert header[:4] == b'\xcf\xfa\xed\xfe', str(path)
            ncmds = struct.unpack_from('<I',header,16)[0]
            deps, minimum, signed = [], (0,0,0), False
            for _ in range(ncmds):
                command, size = struct.unpack('<II',f.read(8)); data = f.read(size-8)
                if command in (0xc,0x80000018,0x8000001f,0x80000023):
                    start = struct.unpack_from('<I',data)[0]-8;deps.append(data[start:].split(b'\0')[0].decode())
                if command in (0x24,0x32):
                    version = struct.unpack_from('<I',data,4 if command==0x32 else 0)[0]
                    minimum=(version>>16,(version>>8)&255,version&255)
                if command == 0x1d: signed=True
            result.append({'arch':CPUS.get(cpu,hex(cpu)),'minimum':minimum,'dependencies':deps,'hasCodeSignature':signed})
        return result

def verify(folder, arch):
    count=0;minimum=(0,0,0);unsigned=[];unused=[]
    for path in folder.rglob('*'):
        if not path.is_file():continue
        if path.suffix not in ('.so','.dylib','.bundle') and path.parent.name!='bin':continue
        slices=macho(path)
        if not slices:continue
        count+=1
        selected=next((s for s in slices if s['arch']==arch),None)
        # Upstream MediaPipe also ships unimportable source/build variants in
        # namespace directories. Python loads the adjacent tagged extension first.
        if selected is None and path.name=='arm64.cpython-311-darwin.so' and not (path.parent/'__init__.py').exists():
            active=path.parent.with_name(path.parent.name+'.cpython-311-darwin.so')
            assert active.is_file() and any(s['arch']==arch for s in macho(active)),str(path)
            unused.append(str(path.relative_to(folder)));continue
        assert selected is not None,(str(path),arch,slices)
        minimum=max(minimum,selected['minimum'])
        for dependency in selected['dependencies']:
            assert not dependency.startswith('/') or dependency.startswith(('/System/Library/','/usr/lib/')), (str(path),dependency)
        if arch=='arm64' and not selected['hasCodeSignature']:unsigned.append(str(path.relative_to(folder)))
    assert count>30,('Too few native binaries',count)
    assert (folder/'python/bin/python3.11').is_file()
    site=folder/'python/lib/python3.11/site-packages'
    for package in ('mediapipe','numpy','cv2','vosk','sounddevice.py','_sounddevice_data'):
        assert (site/package).exists(),package
    return {'architecture':arch,'nativeFiles':count,'minimumOS':'.'.join(map(str,minimum)),'unsignedArm64':unsigned,'unusedUpstreamVariants':unused,'executedOnMac':False}

if __name__=='__main__':
    results=[verify(ROOT/'Tools/Recognition/mac-runtime'/arch,arch) for arch in ('arm64','x86_64')]
    (ROOT/'TestResults/mac-runtime-static.json').write_text(json.dumps(results,indent=2))
    print(json.dumps(results,indent=2))
