// Temporary editor tool: exports prefabs / model assets to GLB (meshes baked to root space, Unity → glTF handedness,
// base colours, main textures as PNG, vertex colours). Driven by environment variables so it runs in -batchmode:
//   PORTFOLIO_GLB_JOBS  path of a text file, one job per line: assetPath|outName[|clipAssetPath|timeSeconds]
//   PORTFOLIO_GLB_OUT   output directory
//   PORTFOLIO_GLB_SKIP  comma-separated substrings; renderers whose mesh asset path contains one are skipped
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PortfolioGlbExport
{
    class Job { public string asset, name, clip, mats; public float time; }

    static Material[] OverrideMaterials(Renderer r, string spec)
    {
        if (string.IsNullOrEmpty(spec)) return r.sharedMaterials;
        foreach (var rule in spec.Split(';'))
        {
            var kv = rule.Split(':');
            if (kv.Length != 2) continue;
            if (kv[0] != "*" && !System.Text.RegularExpressions.Regex.IsMatch(r.name, kv[0])) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(kv[1].Trim());
            if (m == null) { Debug.LogWarning("no material at " + kv[1]); continue; }
            var arr = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < arr.Length; i++) arr[i] = m;
            return arr;
        }
        return r.sharedMaterials;
    }

    public static void Run()
    {
        var jobsFile = Environment.GetEnvironmentVariable("PORTFOLIO_GLB_JOBS");
        var outDir = Environment.GetEnvironmentVariable("PORTFOLIO_GLB_OUT");
        var skip = Environment.GetEnvironmentVariable("PORTFOLIO_GLB_SKIP") ?? "";
        var report = new StringBuilder();
        Directory.CreateDirectory(outDir);
        foreach (var raw in File.ReadAllLines(jobsFile))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            var parts = line.Split('|');
            var job = new Job { asset = parts[0].Trim(), name = parts[1].Trim() };
            for (int i = 2; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.StartsWith("mat=")) job.mats = p.Substring(4);            // nodeRegex:matPath;nodeRegex:matPath  ("*" = every renderer)
                else if (job.clip == null) job.clip = p;
                else job.time = float.Parse(p, CultureInfo.InvariantCulture);
            }
            try { report.AppendLine(Export(job, outDir, skip)); }
            catch (Exception e) { report.AppendLine("FAIL " + job.name + ": " + e.Message + "\n" + e.StackTrace); }
        }
        File.WriteAllText(Path.Combine(outDir, "_report.txt"), report.ToString());
        Debug.Log("PORTFOLIO_GLB_DONE\n" + report);
    }

    static string Export(Job job, string outDir, string skip)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(job.asset);
        if (src == null) return "FAIL " + job.name + ": no GameObject at " + job.asset;
        var inst = UnityEngine.Object.Instantiate(src);
        inst.transform.position = Vector3.zero;
        inst.transform.rotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;
        if (!string.IsNullOrEmpty(job.clip))
        {
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(job.clip))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview")) { clip = c; break; }
            }
            if (clip == null) { UnityEngine.Object.DestroyImmediate(inst); return "FAIL " + job.name + ": no clip in " + job.clip; }
            clip.SampleAnimation(inst, job.time);
        }
        var w = new GlbWriter();
        int tris = 0, renderers = 0, skipped = 0;
        var rootW2L = inst.transform.worldToLocalMatrix;
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            Mesh mesh = null; bool temp = false;
            var smr = r as SkinnedMeshRenderer;
            if (smr != null)
            {
                if (smr.sharedMesh == null) continue;
                if (SkipMesh(smr.sharedMesh, skip)) { skipped++; continue; }
                mesh = new Mesh(); smr.BakeMesh(mesh, false); temp = true;
            }
            else if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                if (SkipMesh(mf.sharedMesh, skip)) { skipped++; continue; }
                mesh = mf.sharedMesh;
            }
            else continue;
            if (!r.enabled || !r.gameObject.activeInHierarchy) { if (temp) UnityEngine.Object.DestroyImmediate(mesh); continue; }
            var m = rootW2L * r.localToWorldMatrix;
            tris += w.AddRenderer(mesh, OverrideMaterials(r, job.mats), m, r.name);
            renderers++;
            if (temp) UnityEngine.Object.DestroyImmediate(mesh);
        }
        UnityEngine.Object.DestroyImmediate(inst);
        var bytes = w.Build();
        File.WriteAllBytes(Path.Combine(outDir, job.name + ".glb"), bytes);
        return "OK " + job.name + ": renderers=" + renderers + " skipped=" + skipped + " tris=" + tris + " textures=" + w.TextureCount + " bytes=" + bytes.Length;
    }

    static bool SkipMesh(Mesh mesh, string skip)
    {
        if (string.IsNullOrEmpty(skip)) return false;
        var p = AssetDatabase.GetAssetPath(mesh) ?? "";
        foreach (var s in skip.Split(',')) if (s.Length > 0 && p.Contains(s)) return true;
        return false;
    }

    class GlbWriter
    {
        readonly MemoryStream bin = new MemoryStream();
        readonly List<string> bufferViews = new List<string>(), accessors = new List<string>(), meshes = new List<string>(),
            nodes = new List<string>(), materials = new List<string>(), textures = new List<string>(), images = new List<string>();
        readonly Dictionary<Material, int> matIndex = new Dictionary<Material, int>();
        readonly Dictionary<Texture, int> texIndex = new Dictionary<Texture, int>();
        int defaultMat = -1;
        public int TextureCount { get { return images.Count; } }

        static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        static string Esc(string s) { return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""); }

        void Align() { while (bin.Length % 4 != 0) bin.WriteByte(0); }

        int AddBufferView(byte[] data, int target)
        {
            Align();
            long off = bin.Length;
            bin.Write(data, 0, data.Length);
            bufferViews.Add("{\"buffer\":0,\"byteOffset\":" + off + ",\"byteLength\":" + data.Length + (target != 0 ? ",\"target\":" + target : "") + "}");
            return bufferViews.Count - 1;
        }

        int AddFloatAccessor(float[] data, int comps, string type, bool minmax)
        {
            var bytes = new byte[data.Length * 4];
            Buffer.BlockCopy(data, 0, bytes, 0, bytes.Length);
            int bv = AddBufferView(bytes, 34962);
            int count = data.Length / comps;
            var sb = new StringBuilder("{\"bufferView\":" + bv + ",\"componentType\":5126,\"count\":" + count + ",\"type\":\"" + type + "\"");
            if (minmax)
            {
                var mn = new float[comps]; var mx = new float[comps];
                for (int c = 0; c < comps; c++) { mn[c] = float.MaxValue; mx[c] = float.MinValue; }
                for (int i = 0; i < count; i++) for (int c = 0; c < comps; c++) { var v = data[i * comps + c]; if (v < mn[c]) mn[c] = v; if (v > mx[c]) mx[c] = v; }
                sb.Append(",\"min\":[" + string.Join(",", Array.ConvertAll(mn, F)) + "],\"max\":[" + string.Join(",", Array.ConvertAll(mx, F)) + "]");
            }
            sb.Append("}");
            accessors.Add(sb.ToString());
            return accessors.Count - 1;
        }

        int AddIndexAccessor(int[] idx)
        {
            var u = new uint[idx.Length];
            for (int i = 0; i < idx.Length; i++) u[i] = (uint)idx[i];
            var bytes = new byte[u.Length * 4];
            Buffer.BlockCopy(u, 0, bytes, 0, bytes.Length);
            int bv = AddBufferView(bytes, 34963);
            accessors.Add("{\"bufferView\":" + bv + ",\"componentType\":5125,\"count\":" + u.Length + ",\"type\":\"SCALAR\"}");
            return accessors.Count - 1;
        }

        public int AddRenderer(Mesh mesh, Material[] mats, Matrix4x4 m, string name)
        {
            var verts = mesh.vertices; var norms = mesh.normals; var uvs = mesh.uv; var cols = mesh.colors;
            if (norms == null || norms.Length != verts.Length)
            {
                var copy = UnityEngine.Object.Instantiate(mesh); copy.RecalculateNormals(); norms = copy.normals; UnityEngine.Object.DestroyImmediate(copy);
            }
            var nm = m.inverse.transpose;
            var P = new float[verts.Length * 3]; var N = new float[verts.Length * 3];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = m.MultiplyPoint3x4(verts[i]); P[i * 3] = -p.x; P[i * 3 + 1] = p.y; P[i * 3 + 2] = p.z;
                var n = nm.MultiplyVector(norms[i]).normalized; N[i * 3] = -n.x; N[i * 3 + 1] = n.y; N[i * 3 + 2] = n.z;
            }
            int posAcc = AddFloatAccessor(P, 3, "VEC3", true);
            int nrmAcc = AddFloatAccessor(N, 3, "VEC3", false);
            int uvAcc = -1, colAcc = -1;
            if (uvs != null && uvs.Length == verts.Length)
            {
                var U = new float[verts.Length * 2];
                for (int i = 0; i < verts.Length; i++) { U[i * 2] = uvs[i].x; U[i * 2 + 1] = 1f - uvs[i].y; }
                uvAcc = AddFloatAccessor(U, 2, "VEC2", false);
            }
            if (cols != null && cols.Length == verts.Length)
            {
                var C = new float[verts.Length * 4];
                for (int i = 0; i < verts.Length; i++) { var c = cols[i].linear; C[i * 4] = c.r; C[i * 4 + 1] = c.g; C[i * 4 + 2] = c.b; C[i * 4 + 3] = c.a; }
                colAcc = AddFloatAccessor(C, 4, "VEC4", false);
            }
            var prims = new List<string>(); int tris = 0;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var idx = mesh.GetTriangles(s);
                if (idx.Length == 0) continue;
                for (int i = 0; i + 2 < idx.Length; i += 3) { var t = idx[i + 1]; idx[i + 1] = idx[i + 2]; idx[i + 2] = t; }   // mirrored X flips winding
                int idxAcc = AddIndexAccessor(idx);
                int mat = MaterialIndex(s < mats.Length ? mats[s] : null);
                var attr = "\"POSITION\":" + posAcc + ",\"NORMAL\":" + nrmAcc + (uvAcc >= 0 ? ",\"TEXCOORD_0\":" + uvAcc : "") + (colAcc >= 0 ? ",\"COLOR_0\":" + colAcc : "");
                prims.Add("{\"attributes\":{" + attr + "},\"indices\":" + idxAcc + ",\"material\":" + mat + "}");
                tris += idx.Length / 3;
            }
            if (prims.Count == 0) return 0;
            meshes.Add("{\"name\":\"" + Esc(name) + "\",\"primitives\":[" + string.Join(",", prims) + "]}");
            nodes.Add("{\"name\":\"" + Esc(name) + "\",\"mesh\":" + (meshes.Count - 1) + "}");
            return tris;
        }

        int MaterialIndex(Material mat)
        {
            if (mat == null)
            {
                if (defaultMat < 0) { materials.Add("{\"name\":\"default\",\"pbrMetallicRoughness\":{\"baseColorFactor\":[0.7,0.7,0.7,1],\"metallicFactor\":0,\"roughnessFactor\":0.9}}"); defaultMat = materials.Count - 1; }
                return defaultMat;
            }
            int found;
            if (matIndex.TryGetValue(mat, out found)) return found;
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            c = c.linear;
            Texture tex = mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
            var t2 = tex as Texture2D;
            int texIdx = t2 != null ? TextureIndex(t2) : -1;
            bool doubleSided = mat.HasProperty("_Cull") && mat.GetFloat("_Cull") == 0f;
            Color em = mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION") ? mat.GetColor("_EmissionColor").linear : Color.black;
            var sb = new StringBuilder();
            sb.Append("{\"name\":\"" + Esc(mat.name) + "\",\"doubleSided\":" + (doubleSided ? "true" : "false"));
            sb.Append(",\"pbrMetallicRoughness\":{\"baseColorFactor\":[" + F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a) + "],\"metallicFactor\":0,\"roughnessFactor\":0.9");
            if (texIdx >= 0) sb.Append(",\"baseColorTexture\":{\"index\":" + texIdx + "}");
            sb.Append("}");
            if (em.maxColorComponent > 0.01f) sb.Append(",\"emissiveFactor\":[" + F(Mathf.Min(1f, em.r)) + "," + F(Mathf.Min(1f, em.g)) + "," + F(Mathf.Min(1f, em.b)) + "]");
            sb.Append("}");
            materials.Add(sb.ToString());
            matIndex[mat] = materials.Count - 1;
            return matIndex[mat];
        }

        int TextureIndex(Texture2D tex)
        {
            int found;
            if (texIndex.TryGetValue(tex, out found)) return found;
            int w = Mathf.Min(tex.width, 1024), h = Mathf.Min(tex.height, 1024);
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt);
            var png = t.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(t);
            int bv = AddBufferView(png, 0);
            images.Add("{\"bufferView\":" + bv + ",\"mimeType\":\"image/png\",\"name\":\"" + Esc(tex.name) + "\"}");
            textures.Add("{\"sampler\":0,\"source\":" + (images.Count - 1) + "}");
            texIndex[tex] = textures.Count - 1;
            return texIndex[tex];
        }

        public byte[] Build()
        {
            Align();
            var nodeIds = new List<string>(); for (int i = 0; i < nodes.Count; i++) nodeIds.Add(i.ToString());
            var json = new StringBuilder();
            json.Append("{\"asset\":{\"version\":\"2.0\",\"generator\":\"PortfolioGlbExport\"},\"scene\":0,\"scenes\":[{\"nodes\":[" + string.Join(",", nodeIds) + "]}]");
            json.Append(",\"nodes\":[" + string.Join(",", nodes) + "],\"meshes\":[" + string.Join(",", meshes) + "],\"materials\":[" + string.Join(",", materials) + "]");
            if (textures.Count > 0) json.Append(",\"textures\":[" + string.Join(",", textures) + "],\"images\":[" + string.Join(",", images) + "],\"samplers\":[{\"magFilter\":9729,\"minFilter\":9987,\"wrapS\":10497,\"wrapT\":10497}]");
            json.Append(",\"accessors\":[" + string.Join(",", accessors) + "],\"bufferViews\":[" + string.Join(",", bufferViews) + "],\"buffers\":[{\"byteLength\":" + bin.Length + "}]}");
            var jb = Encoding.UTF8.GetBytes(json.ToString());
            int jpad = (4 - jb.Length % 4) % 4;
            var bb = bin.ToArray();
            int total = 12 + 8 + jb.Length + jpad + 8 + bb.Length;
            var ms = new MemoryStream(total);
            var bw = new BinaryWriter(ms);
            bw.Write(0x46546C67u); bw.Write(2u); bw.Write((uint)total);
            bw.Write((uint)(jb.Length + jpad)); bw.Write(0x4E4F534Au); bw.Write(jb); for (int i = 0; i < jpad; i++) bw.Write((byte)0x20);
            bw.Write((uint)bb.Length); bw.Write(0x004E4942u); bw.Write(bb);
            bw.Flush();
            return ms.ToArray();
        }
    }
}
