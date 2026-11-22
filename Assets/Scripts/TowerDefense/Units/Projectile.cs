using System.Collections.Generic;
using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 大砲の弾。発射元が Launch でダメージ等を渡す。
    /// 何かの Collider に当たるか、追尾中の標的に届くと着弾する（範囲ダメージ対応）。
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        [Tooltip("ON: 標的を追いかける / OFF: 発射方向にまっすぐ飛ぶ")]
        [SerializeField] bool homing = true;
        [Tooltip("追尾の曲がりやすさ（度/秒）")]
        [SerializeField] float homingTurnSpeed = 720f;
        [Tooltip("当たり判定の半径")]
        [SerializeField] float hitRadius = 3f;
        [SerializeField] float lifetime = 4f;
        [Tooltip("着弾エフェクト（爆発など）")]
        [SerializeField] GameObject impactEffectPrefab;
        [Tooltip("着弾時に一緒に出す追加のエフェクト")]
        [SerializeField] GameObject[] extraImpactEffects;
        [Tooltip("着弾エフェクトの向き（机の面を基準にした回転）")]
        [SerializeField] Vector3 impactEffectEuler = new Vector3(-90f, 0f, 0f);
        [Tooltip("着弾時に切り離して少し残す軌跡など（任意）")]
        [SerializeField] TrailRenderer trail;
        [Header("範囲攻撃の表示")]
        [Tooltip("着弾時に範囲（splashRadius）を地面に円で表示する")]
        [SerializeField] bool showSplashRange = true;
        [SerializeField] Color splashRangeColor = new Color(1f, 0.35f, 0.1f, 1f);
        [SerializeField] float splashRangeWidth = 1.5f;
        [SerializeField] float splashRangeDuration = 0.5f;

        Health target;
        Health owner;
        Team ownerTeam;
        float damage = 10f;
        float speed = 200f;
        float splashRadius;
        float age;
        bool launched;
        bool done;

        static readonly RaycastHit[] hits = new RaycastHit[8];
        static readonly List<Health> splashBuffer = new List<Health>();

        void Awake()
        {
            GameplayScale.Apply(transform);
            hitRadius *= GameplayScale.Factor;
        }

        public void Launch(Health target, Team ownerTeam, float damage, float speed, float splashRadius, Health owner = null)
        {
            this.owner = owner;
            this.target = target;
            this.ownerTeam = ownerTeam;
            this.damage = damage;
            this.speed = speed;
            this.splashRadius = splashRadius;
            launched = true;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            age += dt;
            if (age > lifetime) { Destroy(gameObject); return; }
            if (!launched) return;

            if (homing && target && target.IsTargetable)
            {
                Vector3 to = target.AimPosition - transform.position;
                if (to.sqrMagnitude > 1e-6f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(to), homingTurnSpeed * dt);

                if (to.magnitude <= hitRadius) { Impact(target.AimPosition, target); return; }
            }

            Vector3 step = transform.forward * speed * dt;
            if (step.sqrMagnitude < 1e-10f) return;
            int n = Physics.SphereCastNonAlloc(transform.position, hitRadius, step.normalized, hits, step.magnitude,
                ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Health hitHealth = null;
            Vector3 hitPoint = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i].collider.GetComponentInParent<Health>();
                if (!h || h == owner || !h.IsTargetable) continue;
                // 味方の建物（丸太など）はすり抜けずにそこで着弾する（味方にはダメージなし）
                if (h.Team == ownerTeam && h.Kind == TargetKind.Unit) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    hitHealth = h;
                    hitPoint = hits[i].point == Vector3.zero ? transform.position + step : hits[i].point;
                }
            }
            if (hitHealth) { Impact(hitPoint, hitHealth.Team == ownerTeam ? null : hitHealth); return; }

            transform.position += step;
        }

        void Impact(Vector3 point, Health direct)
        {
            done = true;
            if (splashRadius > 0f)
            {
                Health.GetHostilesInRadius(ownerTeam, point, splashRadius, splashBuffer);
                if (direct && !splashBuffer.Contains(direct)) splashBuffer.Add(direct);
                foreach (var h in splashBuffer) h.TakeDamage(damage, point);
            }
            else if (direct)
            {
                direct.TakeDamage(damage, point);
            }

            var gmFx = GameManager.Instance;
            Quaternion fxRot = Quaternion.FromToRotation(Vector3.up, gmFx ? gmFx.GroundUp : Vector3.up) * Quaternion.Euler(impactEffectEuler);
            if (impactEffectPrefab) Fx.Spawn(impactEffectPrefab, point, fxRot);
            if (extraImpactEffects != null)
                foreach (var fx in extraImpactEffects) if (fx) Fx.Spawn(fx, point, fxRot);
            if (showSplashRange && splashRadius > 0f)
                SplashIndicator.Show(point, splashRadius, splashRangeColor, splashRangeWidth, splashRangeDuration);
            if (trail)
            {
                trail.transform.SetParent(null, true);
                trail.autodestruct = true;
                trail.emitting = false;
            }
            Destroy(gameObject);
        }
    }
}
