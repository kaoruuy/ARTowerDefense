using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 地面（親のローカル XZ 面）に円を描く。大砲の射程や、敵の出現円の表示に使う。
    /// 親の上方向 = 机の面の法線として描くので、タワーの面に揃えて固定された物の子にすればそのまま机に乗る。
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class RangeRing : MonoBehaviour
    {
        [SerializeField] float radius = 100f;          // ワールド単位
        [SerializeField] float lineWidth = 1.5f;       // ワールド単位
        [SerializeField] Color color = new Color(1f, 0.6f, 0.1f, 0.8f);
        [SerializeField] float heightOffset = 0.5f;    // 机からの浮かせ量（ワールド単位）
        [SerializeField, Range(16, 256)] int segments = 96;

        LineRenderer lr;
        static Material sharedMaterial;

        public float Radius => radius;

        /// <summary>parent の子として円を作る。</summary>
        public static RangeRing Create(Transform parent, float worldRadius, Color color, float worldLineWidth, string name = "RangeRing")
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var ring = go.AddComponent<RangeRing>();
            ring.Set(worldRadius, color, worldLineWidth);
            return ring;
        }

        void Awake() { EnsureLine(); }

        void EnsureLine()
        {
            if (lr) return;
            lr = GetComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.alignment = LineAlignment.TransformZ;   // 地面に寝かせた帯として描く
            lr.numCornerVertices = 2;
            lr.sharedMaterial = GetMaterial();
        }

        public void Set(float worldRadius, Color c, float worldLineWidth)
        {
            radius = worldRadius;
            color = c;
            lineWidth = worldLineWidth;
            Rebuild();
        }

        public void SetRadius(float worldRadius)
        {
            if (Mathf.Approximately(worldRadius, radius)) return;
            radius = worldRadius;
            Rebuild();
        }

        void Rebuild()
        {
            EnsureLine();
            // 円は XY 面に作り、X 軸まわりに倒して親の XZ 面（地面）に寝かせる（帯の表が上を向く）
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            float s = Mathf.Max(1e-4f, transform.parent ? transform.parent.lossyScale.x : 1f);
            float r = radius / s;
            transform.localPosition = new Vector3(0f, heightOffset / s, 0f);

            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f));
            }
            lr.widthMultiplier = lineWidth;
            lr.startColor = color;
            lr.endColor = color;
            var m = lr.sharedMaterial;
            if (m && m.HasProperty("_BaseColor")) { /* URP Unlit の場合は頂点色を使わないので個別に色を入れる */
                var mpb = new MaterialPropertyBlock();
                lr.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", color);
                lr.SetPropertyBlock(mpb);
            }
        }

        static Material GetMaterial()
        {
            if (sharedMaterial) return sharedMaterial;
            // 頂点色で色が付く Sprites/Default を優先。無ければ URP の Unlit
            Shader sh = Shader.Find("Sprites/Default");
            if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (!sh) sh = Shader.Find("Unlit/Color");
            sharedMaterial = new Material(sh) { name = "RangeRing (runtime)" };
            return sharedMaterial;
        }
    }
}
