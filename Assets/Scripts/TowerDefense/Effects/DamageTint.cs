using UnityEngine;

namespace ARTD
{
    /// <summary>HP が減るほど見た目の色を damagedColor に近づける（壁などのダメージ表現。任意）。</summary>
    public class DamageTint : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] Renderer[] renderers;
        [SerializeField] Color damagedColor = new Color(0.25f, 0.1f, 0.05f);
        [Tooltip("ヒット時に一瞬光らせる色")]
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] float flashDuration = 0.08f;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        MaterialPropertyBlock mpb;
        Color[][] baseColors;   // [renderer][マテリアル番号]（1つのモデルに複数マテリアルがあっても色を保つ）
        float flashTimer;
        bool tinted;

        void Awake()
        {
            if (!health) health = GetComponentInParent<Health>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<MeshRenderer>(true);
            mpb = new MaterialPropertyBlock();
            baseColors = new Color[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i] ? renderers[i].sharedMaterials : new Material[0];
                baseColors[i] = new Color[mats.Length];
                for (int j = 0; j < mats.Length; j++)
                {
                    var m = mats[j];
                    baseColors[i][j] = m ? (m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white) : Color.white;
                }
            }
            if (health)
            {
                health.onHealthChanged.AddListener((_, __) => Apply());
                health.onDamaged.AddListener(() => flashTimer = flashDuration);
            }
        }

        void Update()
        {
            if (flashTimer > 0f)
            {
                // 一時停止中（timeScale = 0）でも光りっぱなしにならないよう unscaled で数える
                flashTimer -= Time.unscaledDeltaTime;
                Apply();
            }
        }

        void Apply()
        {
            if (!health) return;
            float t = 1f - health.Ratio;
            bool flashing = flashTimer > 0f;

            // 無傷でヒット中でもなければ、元のマテリアルの色のまま（上書きを消す）
            if (!flashing && t <= 0.001f)
            {
                if (tinted)
                {
                    var empty = new MaterialPropertyBlock();
                    for (int i = 0; i < renderers.Length; i++)
                    {
                        if (!renderers[i]) continue;
                        for (int j = 0; j < baseColors[i].Length; j++) renderers[i].SetPropertyBlock(empty, j);
                    }
                    tinted = false;
                }
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                var cols = baseColors[i];
                for (int j = 0; j < cols.Length; j++)
                {
                    Color c = flashing ? flashColor : Color.Lerp(cols[j], damagedColor, t * 0.8f);
                    r.GetPropertyBlock(mpb, j);
                    mpb.SetColor(BaseColorId, c);
                    mpb.SetColor(ColorId, c);
                    r.SetPropertyBlock(mpb, j);   // マテリアルごとに色を設定
                }
            }
            tinted = true;
        }
    }
}
