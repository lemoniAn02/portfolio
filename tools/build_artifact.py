"""Derive the claude.ai artifact copy of index.html into dist/artifact.html:
no document skeleton, no import map (jsdelivr +esm imports), and model references renamed to .wasm
because the artifact host serves no .glb (GLTFLoader only reads the bytes)."""
import os
import re

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = os.path.join(root, 'index.html')
out_dir = os.path.join(root, 'dist')
out = os.path.join(out_dir, 'artifact.html')

h = open(src, encoding='utf8').read()
for tag in ['<!doctype html>', '<html lang="ko">', '<head>', '</head>', '<body>', '</body>', '</html>',
            '<meta charset="utf-8">', '<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">']:
    assert tag in h, tag
    h = h.replace(tag, '', 1)
h = re.sub(r'<script type="importmap">.*?</script>\s*', '', h, flags=re.S)
h = h.replace("from 'three';", "from 'https://cdn.jsdelivr.net/npm/three@0.160.0/+esm';")
h = re.sub(r"from 'three/addons/([^']+)'", r"from 'https://cdn.jsdelivr.net/npm/three@0.160.0/examples/jsm/\1/+esm'", h)
assert "'three'" not in h and 'three/addons' not in h
h = re.sub(r"models/([A-Za-z0-9_-]+)\.glb", lambda m: "models/" + m.group(1) + ".wasm", h)
os.makedirs(out_dir, exist_ok=True)
open(out, 'w', encoding='utf8').write(h.strip() + '\n')
print('wrote', out, len(h.encode('utf8')) // 1024, 'KB; model refs:', len(set(re.findall(r"models/[A-Za-z0-9_-]+\.wasm", h))))
