using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 頭上の簡易 HP バー（Quad 2枚）。カメラの方を向く。
    /// fill の X スケールを HP 割合で縮める（左端固定）。
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Transform fill;
        [SerializeField] Renderer fillRenderer;
        [SerializeField] Gradient colorByHealth = DefaultGradient();
        [Tooltip("HP 満タン時は隠す")]
        [SerializeField] bool hideWhenFull = true;
        [Tooltip("プレビュー中（未固定）は隠す")]
        [SerializeField] bool hideWhilePreview = true;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock mpb;
        Placeable placeable;
        Renderer[] ownRenderers;
        Camera cam;
        float lastRatio = -1f;

        void Awake()
        {
            if (!health) health = GetComponentInParent<Health>();
            placeable = GetComponentInParent<Placeable>();
            ownRenderers = GetComponentsInChildren<Renderer>(true);
            mpb = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (!health) return;
            bool visible = health.IsAlive &&
                           !(hideWhenFull && health.Ratio >= 0.999f) &&
                           !(hideWhilePreview && placeable && !placeable.IsPlaced);
            foreach (var r in ownRenderers) if (r) r.enabled = visible;
            if (!visible) return;

            if (!cam) cam = Camera.main;
            if (cam) transform.rotation = cam.transform.rotation;

            float ratio = health.Ratio;
            if (!Mathf.Approximately(ratio, lastRatio) && fill)
            {
                lastRatio = ratio;
                Vector3 s = fill.localScale;
                s.x = Mathf.Max(0.0001f, ratio);
                fill.localScale = s;
                Vector3 p = fill.localPosition;
                p.x = -(1f - ratio) * 0.5f;
                fill.localPosition = p;

                if (fillRenderer)
                {
                    Color c = colorByHealth.Evaluate(ratio);
                    fillRenderer.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, c);
                    mpb.SetColor(ColorId, c);
                    fillRenderer.SetPropertyBlock(mpb);
                }
            }
        }

        static Gradient DefaultGradient()
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.red, 0f), new GradientColorKey(Color.yellow, 0.5f), new GradientColorKey(Color.green, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return g;
        }
    }
}
