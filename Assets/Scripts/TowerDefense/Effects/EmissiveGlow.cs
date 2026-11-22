using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// オブジェクトを光らせる（エミッションを HDR で強くして、ブルームでにじませる）。
    /// 光らせたいモデルに付けるだけで動く。点滅や、被弾時の一瞬の発光にも使える。
    /// ※ ビルドで光らない場合は、そのマテリアルの Inspector で Emission にチェックを入れておく。
    /// </summary>
    public class EmissiveGlow : MonoBehaviour
    {
        [Tooltip("発光色（HDR。Intensity を上げるほど強くにじむ）")]
        [ColorUsage(false, true)] public Color glowColor = new Color(1f, 0.8f, 0.3f);
        [Tooltip("明るさの倍率。ブルームの Threshold(=1) を超えるとにじむ")]
        [Min(0)] public float intensity = 3f;
        [Tooltip("開始時から光らせる")]
        public bool glowOnStart = true;

        [Header("点滅")]
        public bool pulse = false;
        public float pulseSpeed = 2f;
        [Range(0, 1)] public float pulseMin = 0.3f;

        [Header("被弾で光る（Health があれば）")]
        public bool flashOnDamage = false;
        [ColorUsage(false, true)] public Color flashColor = new Color(4f, 4f, 4f);
        public float flashDuration = 0.1f;

        [Tooltip("空なら子の Renderer すべて")]
        [SerializeField] Renderer[] renderers;

        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        Material[] mats;
        bool on;
        float flashTimer;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<Renderer>(true);
            var list = new System.Collections.Generic.List<Material>();
            foreach (var r in renderers)
            {
                if (!r || r is ParticleSystemRenderer || r.GetComponentInParent<HealthBar>()) continue;
                foreach (var m in r.materials) // マテリアルを個別に複製（他のオブジェクトに影響しない）
                {
                    if (!m || !m.HasProperty(EmissionId)) continue;
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    list.Add(m);
                }
            }
            mats = list.ToArray();
            on = glowOnStart;
            if (flashOnDamage)
            {
                var h = GetComponentInParent<Health>();
                if (h) h.onDamaged.AddListener(Flash);
            }
            Apply();
        }

        void OnDestroy()
        {
            if (mats == null) return;
            foreach (var m in mats) if (m) Destroy(m);
        }

        void Update()
        {
            if (flashTimer > 0f) flashTimer -= Time.deltaTime;
            if (pulse || flashTimer > -0.1f) Apply();
        }

        public void SetGlow(bool enable) { on = enable; Apply(); }
        public void SetIntensity(float value) { intensity = value; Apply(); }
        public void Flash() { flashTimer = flashDuration; Apply(); }

        void Apply()
        {
            Color c = Color.black;
            if (on)
            {
                float k = pulse ? Mathf.Lerp(pulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f)) : 1f;
                c = glowColor * (intensity * k);
            }
            if (flashTimer > 0f) c = flashColor;
            foreach (var m in mats) if (m) m.SetColor(EmissionId, c);
        }
    }
}
