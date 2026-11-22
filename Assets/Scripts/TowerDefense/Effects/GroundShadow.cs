using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 足元に置く丸いぼかし影。AR で CG が「宙に浮いて見える」のを減らす（机に接している手がかりになる）。
    /// 親のローカル XZ 面（= 机の面）に寝かせて描く。
    /// </summary>
    public class GroundShadow : MonoBehaviour
    {
        static Material sharedMaterial;
        static Mesh sharedMesh;

        /// <summary>parent の子として、直径 worldDiameter の影を作る。</summary>
        public static GroundShadow Create(Transform parent, float worldDiameter, float opacity = 0.45f, float worldHeight = 0.3f)
        {
            var go = new GameObject("GroundShadow");
            go.transform.SetParent(parent, false);
            float s = Mathf.Max(1e-4f, parent.lossyScale.x);
            go.transform.localPosition = new Vector3(0f, worldHeight / s, 0f);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * (worldDiameter / s);

            go.AddComponent<MeshFilter>().sharedMesh = GetMesh();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0f, 0f, 0f, opacity));
            mpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, opacity));
            mr.SetPropertyBlock(mpb);
            return go.AddComponent<GroundShadow>();
        }

        /// <summary>root の子の描画範囲（HP バーや円などは除く）から、足元の直径を求める。</summary>
        public static float FootprintDiameter(Transform root, float fallback)
        {
            bool any = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is LineRenderer || r is ParticleSystemRenderer) continue;
                if (r.GetComponentInParent<HealthBar>() || r.GetComponent<GroundShadow>()) continue;
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var lp = root.InverseTransformPoint(c);
                    if (!any) { min = max = lp; any = true; }
                    else { min = Vector3.Min(min, lp); max = Vector3.Max(max, lp); }
                }
            }
            if (!any) return fallback;
            float s = root.lossyScale.x;
            return Mathf.Max(max.x - min.x, max.z - min.z) * s;
        }

        static Mesh GetMesh()
        {
            if (sharedMesh) return sharedMesh;
            // XZ 面の 1x1 の板（表が上向き）
            sharedMesh = new Mesh { name = "GroundShadowQuad" };
            sharedMesh.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            sharedMesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            sharedMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            sharedMesh.RecalculateNormals();
            sharedMesh.RecalculateBounds();
            return sharedMesh;
        }

        static Material GetMaterial()
        {
            if (sharedMaterial) return sharedMaterial;
            // 中心が濃く外側に向かって消える円のテクスチャ
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "GroundShadowTex" };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a);   // なめらかに
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();

            Shader sh = Shader.Find("Sprites/Default");   // 透明・頂点色/_Color 対応で URP でも描ける
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
            sharedMaterial = new Material(sh) { name = "GroundShadow (runtime)", mainTexture = tex };
            sharedMaterial.renderQueue = 2990;   // 不透明の後・他の透明物の前
            return sharedMaterial;
        }
    }
}
