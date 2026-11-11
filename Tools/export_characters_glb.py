"""Convert the actual Unity skin export to portable glTF 2.0, retaining rig and poses."""
import json, struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def export(name):
    src = json.loads((ROOT / 'Models' / f'{name}.mesh.json').read_text())
    blob = bytearray()
    doc = {'asset': {'version': '2.0', 'generator': 'Neonflow Unity armor builder'},
           'scene': 0, 'scenes': [{'nodes': [0]}], 'nodes': [], 'meshes': [], 'skins': [],
           'bufferViews': [], 'accessors': [], 'animations': []}
    def accessor(rows, fmt, kind, component, bounds=False):
        while len(blob) % 4: blob.append(0)
        start = len(blob)
        for row in rows: blob.extend(struct.pack('<' + fmt, *row))
        view = len(doc['bufferViews'])
        doc['bufferViews'].append({'buffer': 0, 'byteOffset': start, 'byteLength': len(blob)-start})
        acc = {'bufferView': view, 'componentType': component, 'count': len(rows), 'type': kind}
        if bounds:
            acc['min'] = [min(c) for c in zip(*rows)]; acc['max'] = [max(c) for c in zip(*rows)]
        doc['accessors'].append(acc)
        return len(doc['accessors']) - 1
    def vector(v): return [v['x'], v['y'], -v['z']]
    def rotation(q): return [-q['x'], -q['y'], q['z'], q['w']]
    pos = accessor([vector(v) for v in src['vertices']], '3f', 'VEC3', 5126, True)
    normal = accessor([vector(v) for v in src['normals']], '3f', 'VEC3', 5126)
    uv = accessor([[v['x'], 1-v['y']] for v in src['uv']], '2f', 'VEC2', 5126)
    joints = accessor([[v, 0, 0, 0] for v in src['joints']], '4H', 'VEC4', 5123)
    weights = accessor([[1., 0., 0., 0.] for _ in src['joints']], '4f', 'VEC4', 5126)
    for node in src['nodes']:
        doc['nodes'].append({'name': node['name'], 'translation': vector(node['position']),
                             'rotation': rotation(node['rotation']),
                             'scale': [node['scale'][c] for c in 'xyz']})
    for i, node in enumerate(src['nodes']):
        if node['parent'] >= 0: doc['nodes'][node['parent']].setdefault('children', []).append(i)
    signs = [1, 1, -1, 1]
    inv = src['inverseBindMatrices']
    matrices = [[inv[k+j] * signs[j%4] * signs[j//4] for j in range(16)] for k in range(0,len(inv),16)]
    doc['skins'].append({'name': name + ' rig', 'inverseBindMatrices': accessor(matrices,'16f','MAT4',5126),
                         'joints': list(range(len(src['nodes']))), 'skeleton': 0})
    doc['nodes'][0].update(mesh=0, skin=0)
    colors = [('Ivory','fff2e1'), ('Plum','250733'), ('Pink','ff008c'), ('Cyan','00e8f2')]
    doc['materials'] = []
    for mat_name, rgb in colors:
        linear = [((int(rgb[i:i+2],16)/255+.055)/1.055)**2.4 if int(rgb[i:i+2],16)/255 > .04045 else int(rgb[i:i+2],16)/255/12.92 for i in (0,2,4)]
        mat = {'name': mat_name, 'pbrMetallicRoughness': {'baseColorFactor': linear+[1], 'metallicFactor': .08, 'roughnessFactor': .62}}
        if mat_name == 'Cyan': mat['emissiveFactor'] = [v*.65 for v in linear]
        doc['materials'].append(mat)
    primitives = []
    for mat, part in enumerate(src['parts']):
        indices = part['indices']; flipped = []
        for k in range(0,len(indices),3): flipped.extend([[indices[k]], [indices[k+2]], [indices[k+1]]])
        primitives.append({'attributes': {'POSITION': pos, 'NORMAL': normal, 'TEXCOORD_0': uv, 'JOINTS_0': joints, 'WEIGHTS_0': weights},
                           'indices': accessor(flipped,'I','SCALAR',5125), 'material': mat})
    doc['meshes'].append({'name': name + ' armor', 'primitives': primitives})
    time = accessor([[0.], [1.]], 'f', 'SCALAR', 5126, True)
    for pose in src['poses']:
        anim = {'name': pose['name'], 'samplers': [], 'channels': []}
        for i in range(1,len(src['nodes'])):
            for path, values, fmt, kind in [('translation',vector(pose['positions'][i]),'3f','VEC3'), ('rotation',rotation(pose['rotations'][i]),'4f','VEC4')]:
                sampler = len(anim['samplers'])
                anim['samplers'].append({'input':time, 'output':accessor([values,values],fmt,kind,5126), 'interpolation':'LINEAR'})
                anim['channels'].append({'sampler':sampler, 'target':{'node':i, 'path':path}})
        doc['animations'].append(anim)
    doc['buffers'] = [{'byteLength':len(blob)}]
    payload = json.dumps(doc,separators=(',',':')).encode()
    payload += b' ' * (-len(payload)%4); blob += b'\0' * (-len(blob)%4)
    output = struct.pack('<III',0x46546c67,2,12+8+len(payload)+8+len(blob)) + struct.pack('<II',len(payload),0x4e4f534a)+payload+struct.pack('<II',len(blob),0x004e4942)+blob
    (ROOT/'Models'/f'{name}.glb').write_bytes(output)
    assert len(src['nodes']) == 53 and len(doc['animations']) == 23
    assert all(max(p['indices']) < len(src['vertices']) for p in src['parts'])
    print(f'{name}: {len(output):,} bytes; 53 joints; 23 pose animations; 4 materials')

if __name__ == '__main__':
    for name in ('light', 'solid'): export(name)
