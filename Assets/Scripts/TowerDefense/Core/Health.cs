using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>
    /// 耐久値(HP)。壁・タワー・大砲・スポナー・兵士・敵すべてに付ける。
    /// 全インスタンスを静的リストに登録し、索敵に使う（レイヤー/タグ設定は不要）。
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour
    {
        [Serializable] public class HealthChangedEvent : UnityEvent<float, float> { }

        [SerializeField] Team team = Team.Ally;
        [SerializeField] TargetKind kind = TargetKind.Structure;

        [Tooltip("HP の元データ。未設定なら下の Max Health を使う。CannonTurret 等が実行時に上書きすることもある")]
        [SerializeField] HealthStats stats;
        [SerializeField, Min(1)] float maxHealth = 100f;

        [Header("演出（差し替え可）")]
        [Tooltip("被弾時に出すエフェクト")]
        [SerializeField] GameObject hitEffectPrefab;
        [Tooltip("破壊/死亡時に出すエフェクト")]
        [SerializeField] GameObject deathEffectPrefab;
        [Tooltip("破壊/死亡時の効果音")]
        [SerializeField] AudioClip deathSound;
        [SerializeField, Range(0f, 1f)] float deathSoundVolume = 1f;
        [Tooltip("破壊/死亡してから GameObject を消すまでの秒数（死亡アニメ用）")]
        [SerializeField] float destroyDelay = 0f;
        [SerializeField] bool destroyOnDeath = true;

        [Tooltip("照準点（未設定なら Collider の中心）")]
        [SerializeField] Transform aimPoint;

        [Header("イベント")]
        public HealthChangedEvent onHealthChanged = new HealthChangedEvent();
        public UnityEvent onDamaged = new UnityEvent();
        public UnityEvent onDeath = new UnityEvent();

        public static readonly List<Health> All = new List<Health>();

        public event Action<Health> Died;

        float current;
        bool initialized;
        Placeable placeable;
        Collider[] colliders;

        public Team Team { get => team; set => team = value; }
        public TargetKind Kind => kind;
        public float MaxHealth => maxHealth;
        public float Current => current;
        public float Ratio => maxHealth > 0 ? Mathf.Clamp01(current / maxHealth) : 0f;
        public bool IsAlive => current > 0f;

        /// <summary>索敵・攻撃の対象にしてよいか（生存中で、プレビューではなく設置済み）。</summary>
        public bool IsTargetable => IsAlive && isActiveAndEnabled && (placeable == null || placeable.IsPlaced);

        public Vector3 AimPosition
        {
            get
            {
                if (aimPoint) return aimPoint.position;
                foreach (var c in colliders)
                    if (c && c.enabled) return c.bounds.center;
                return transform.position;
            }
        }

        void Awake()
        {
            placeable = GetComponentInParent<Placeable>();
            colliders = GetComponentsInChildren<Collider>(true);
            if (!initialized) Init();
        }

        void Init()
        {
            if (stats) maxHealth = stats.maxHealth;
            current = maxHealth;
            initialized = true;
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>ステータスを外から適用（CannonTurret / CharacterUnit などが呼ぶ）。</summary>
        public void ApplyStats(HealthStats s, float healthMultiplier = 1f)
        {
            if (s) stats = s;
            maxHealth = (stats ? stats.maxHealth : maxHealth) * healthMultiplier;
            current = maxHealth;
            initialized = true;
            onHealthChanged.Invoke(current, maxHealth);
        }

        public void SetTeam(Team t) => team = t;

        public void TakeDamage(float amount) => TakeDamage(amount, AimPosition);

        public void TakeDamage(float amount, Vector3 hitPoint)
        {
            if (!IsAlive || amount <= 0f) return;
            current = Mathf.Max(0f, current - amount);
            onHealthChanged.Invoke(current, maxHealth);
            onDamaged.Invoke();
            if (hitEffectPrefab) Fx.Spawn(hitEffectPrefab, hitPoint, Quaternion.identity);
            if (current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;
            current = Mathf.Min(maxHealth, current + amount);
            onHealthChanged.Invoke(current, maxHealth);
        }

        public void Kill() { if (IsAlive) TakeDamage(current + 1f); }

        void Die()
        {
            All.Remove(this);
            if (deathEffectPrefab) Fx.Spawn(deathEffectPrefab, AimPosition, transform.rotation);
            Sfx.Play(deathSound, deathSoundVolume);
            onDeath.Invoke();
            Died?.Invoke(this);
            if (destroyOnDeath) Destroy(gameObject, destroyDelay);
        }

        /// <summary>from から見たこのオブジェクトの表面上の最近点。細長い壁でも正しく距離が取れる。</summary>
        public Vector3 ClosestPoint(Vector3 from)
        {
            Vector3 best = transform.position;
            float bestSqr = float.MaxValue;
            bool found = false;
            foreach (var c in colliders)
            {
                if (!c || !c.enabled || c.isTrigger) continue;
                Vector3 p;
                if (c is MeshCollider mc && !mc.convex) p = c.bounds.ClosestPoint(from);
                else p = c.ClosestPoint(from);
                float d = (p - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; found = true; }
            }
            return found ? best : transform.position;
        }

        // ---------- 索敵ヘルパー ----------

        public static Health FindNearest(Vector3 from, float range, Func<Health, bool> filter)
        {
            Health best = null;
            float bestSqr = range * range;
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (!h || !h.IsTargetable) continue;
                if (filter != null && !filter(h)) continue;
                float d = (h.ClosestPoint(from) - from).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = h; }
            }
            return best;
        }

        public static Health FindNearestHostile(Team myTeam, Vector3 from, float range, Func<Health, bool> filter = null)
        {
            return FindNearest(from, range, h => h.team != myTeam && (filter == null || filter(h)));
        }

        public static void GetHostilesInRadius(Team myTeam, Vector3 center, float radius, List<Health> results)
        {
            results.Clear();
            float sqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                var h = All[i];
                if (!h || !h.IsTargetable || h.team == myTeam) continue;
                if ((h.ClosestPoint(center) - center).sqrMagnitude <= sqr) results.Add(h);
            }
        }
    }
}
