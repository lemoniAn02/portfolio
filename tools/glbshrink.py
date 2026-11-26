"""Shrink GLB files in place: embedded PNG textures → max 512px JPEG (or PNG when alpha matters), BIN rebuilt with new offsets."""
import io, json, os, struct, sys
from PIL import Image
folder = sys.argv[1]; maxpx = int(sys.argv[2]) if len(sys.argv) > 2 else 512
for f in sorted(os.listdir(folder)):
    if not f.endswith('.glb'): continue
    p = os.path.join(folder, f)
    raw = open(p, 'rb').read()
    magic, ver, total = struct.unpack_from('<III', raw, 0)
    jlen, jtype = struct.unpack_from('<II', raw, 12)
    j = json.loads(raw[20:20 + jlen].decode('utf8'))
    blen, btype = struct.unpack_from('<II', raw, 20 + jlen)
    bin0 = raw[28 + jlen:28 + jlen + blen]
    if not j.get('images'):
        continue
    views = j['bufferViews']
    data = [bin0[v['byteOffset']:v['byteOffset'] + v['byteLength']] for v in views]
    before = os.path.getsize(p)
    for img in j['images']:
        bv = img['bufferView']
        im = Image.open(io.BytesIO(data[bv])); im.load()
        alpha = im.mode in ('RGBA', 'LA') and im.getchannel('A').getextrema()[0] < 250
        w, h = im.size; s = min(1.0, maxpx / max(w, h))
        if s < 1: im = im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.LANCZOS)
        out = io.BytesIO()
        if alpha: im.save(out, 'PNG', optimize=True); img['mimeType'] = 'image/png'
        else: im.convert('RGB').save(out, 'JPEG', quality=85, optimize=True); img['mimeType'] = 'image/jpeg'
        data[bv] = out.getvalue()
    # rebuild BIN
    nb = bytearray()
    for v, d in zip(views, data):
        while len(nb) % 4: nb.append(0)
        v['byteOffset'] = len(nb); v['byteLength'] = len(d); nb += d
    while len(nb) % 4: nb.append(0)
    j['buffers'][0]['byteLength'] = len(nb)
    jb = json.dumps(j, separators=(',', ':')).encode('utf8')
    while len(jb) % 4: jb += b' '
    total = 12 + 8 + len(jb) + 8 + len(nb)
    with open(p, 'wb') as fh:
        fh.write(struct.pack('<III', 0x46546C67, 2, total)); fh.write(struct.pack('<II', len(jb), 0x4E4F534A)); fh.write(jb)
        fh.write(struct.pack('<II', len(nb), 0x004E4942)); fh.write(nb)
    print(f'{f:22s} {before//1024:6d}KB -> {os.path.getsize(p)//1024:6d}KB')
