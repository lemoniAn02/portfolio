"""Print size, node names, triangle count, image count and the overall POSITION bounds of each GLB in a folder."""
import json, os, struct, sys
folder = sys.argv[1]
for f in sorted(os.listdir(folder)):
    if not f.endswith('.glb'): continue
    p = os.path.join(folder, f)
    with open(p, 'rb') as fh:
        magic, ver, total = struct.unpack('<III', fh.read(12))
        jlen, jtype = struct.unpack('<II', fh.read(8))
        j = json.loads(fh.read(jlen).decode('utf8'))
    mn = [1e9]*3; mx = [-1e9]*3; tris = 0
    for m in j.get('meshes', []):
        for pr in m['primitives']:
            a = j['accessors'][pr['attributes']['POSITION']]
            for i in range(3): mn[i] = min(mn[i], a['min'][i]); mx[i] = max(mx[i], a['max'][i])
            tris += j['accessors'][pr['indices']]['count'] // 3
    size = [round(mx[i]-mn[i], 2) for i in range(3)]
    print(f"{f:22s} {os.path.getsize(p)//1024:6d}KB tris={tris:7d} nodes={len(j.get('nodes',[]))} mats={len(j.get('materials',[]))} imgs={len(j.get('images',[]))} "
          f"size=({size[0]},{size[1]},{size[2]}) ymin={round(mn[1],2)} center=({round((mn[0]+mx[0])/2,2)},{round((mn[2]+mx[2])/2,2)}) "
          f"names={[n.get('name','') for n in j.get('nodes',[])][:4]}")
