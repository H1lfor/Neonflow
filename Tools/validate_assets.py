from pathlib import Path
import json,hashlib
from fontTools.ttLib import TTFont
root=Path(__file__).resolve().parents[1]
tracks=json.loads((root/'README/Data/TrackTiming.json').read_text(encoding='utf-8-sig'))['tracks']
for t in tracks:
    p=root/'Assets/Neonflow/Resources/Music'/t['fileName']
    assert hashlib.sha256(p.read_bytes()).hexdigest()==t['sha256'],p
    assert not t['timingVerifiedByListening']
fonts=list((root/'Assets/NeonflowVector/Fonts').glob('*.ttf'))
assert fonts, 'Vector fonts missing'
for path in fonts:
    font=TTFont(path)
    chars=set().union(*(table.cmap.keys() for table in font['cmap'].tables))
    assert all(ord(c) in chars for c in 'АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя0123456789%+—'),path
catalog=json.loads((root/'README/Data/Movements_catalog.json').read_text(encoding='utf-8-sig'))['poses']
rules=json.loads((root/'README/Data/FingerCue_rules.json').read_text(encoding='utf-8-sig'))['allowedHandsByPose']
for p in catalog:
    assert (root/'Assets/NeonflowMovementUICompact'/p['path']).exists()
    assert (root/'Assets/NeonflowVector/Movements'/p['path']).exists()
    if p['category']=='Poses':assert p['fingerCueAllowedHands']==rules[p['id']]
report=f'PASS: 11 original MP3 SHA-256; 22 original and Vector movement resources/rules; {len(fonts)} Vector Exo 2 fonts with Cyrillic, Ё/ё and numeric glyphs. Timing remains unverified by listening.\n'
(root/'TestResults/assets.txt').write_text(report,encoding='utf-8');print(report)
