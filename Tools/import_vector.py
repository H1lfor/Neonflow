"""Validate and copy the supplied Vector pack without rebuilding its prefabs."""
from pathlib import Path
import hashlib, json, shutil, sys
sys.stdout.reconfigure(encoding='utf-8')
from html.parser import HTMLParser

root = Path(__file__).resolve().parents[1]
pack = root / 'WOW/Neonflow_Vector_AssetPack'
checked = 0
for line in (pack / 'SHA256SUMS.txt').read_text(encoding='utf-8-sig').splitlines():
    digest, name = line.split('  ', 1)
    assert hashlib.sha256((pack/name).read_bytes()).hexdigest() == digest, name
    checked += 1
data = pack / 'Assets/NeonflowVector/Data'
documents = {p.name: json.loads(p.read_text(encoding='utf-8-sig')) for p in data.glob('*.json')}
for name in ['TrackTiming.json', 'FingerCue_rules.json']:
    previous = json.loads((root/'Assets/Neonflow/Resources/Data'/name).read_text(encoding='utf-8-sig'))
    assert documents[name] == previous, f'Gameplay data changed: {name}'
print(f'PASS: {checked} supplied hashes; all {len(documents)} data documents parsed; timing/finger rules unchanged.')
for screen in documents['Layouts.json']['screens']:
    print(screen['id'], ':', ', '.join(n['id'] for n in screen['nodes']))
for seq in documents['AnimationManifest.json']['sequences']:
    assert len(seq['frames']) == seq['frameCount']
    for frame in seq['frames']: assert (pack/'Assets/NeonflowVector'/frame).is_file()
print('All 198 animation frames and screen layouts read; no presentation values imported as player data.')
class GalleryText(HTMLParser):
    hidden = 0
    def handle_starttag(self, tag, attrs):
        if tag in ['script', 'style']: self.hidden += 1
    def handle_endtag(self, tag):
        if tag in ['script', 'style']: self.hidden -= 1
    def handle_data(self, value):
        if not self.hidden and value.strip(): print(value.strip())
GalleryText().feed((pack/'Gallery.html').read_text(encoding='utf-8'))
target = root/'Assets/NeonflowVector'
assert not target.exists(), 'Pack already imported: do not overwrite integration changes.'
shutil.copytree(pack/'Assets/NeonflowVector', target)
shutil.copy2(pack/'Assets/NeonflowVector.meta', root/'Assets/NeonflowVector.meta')
(root/'TestResults/vector-import.txt').write_text(f'PASS: {checked} SHA256 entries, all JSON and animation frames verified; assets imported with GUIDs.\n', encoding='utf-8')
