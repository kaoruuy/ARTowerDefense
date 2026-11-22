using UnityEngine;

namespace ARTD
{
    /// <summary>エフェクト生成ヘルパー。自分で消えない(TimedDestroy/PopEffect 無し)エフェクトは自動で消す。</summary>
    public static class Fx
    {
        public const float DefaultLifetime = 3f;

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!prefab) return null;
            var go = Object.Instantiate(prefab, position, rotation, parent);
            // PopEffect applies the multiplier before caching its animated scale in Awake.
            if (!go.GetComponent<PopEffect>()) GameplayScale.Apply(go.transform);
            if (!go.GetComponent<TimedDestroy>() && !go.GetComponent<PopEffect>())
            {
                float life = DefaultLifetime;
                var ps = go.GetComponentInChildren<ParticleSystem>();
                if (ps) life = Mathf.Max(life, ps.main.duration + ps.main.startLifetime.constantMax);
                Object.Destroy(go, life);
            }
            return go;
        }
    }
}
