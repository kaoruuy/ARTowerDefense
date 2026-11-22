using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>
    /// ボールペンの上の大砲。射程内で一番近い敵に砲塔を向けて弾を撃つ。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CannonTurret : MonoBehaviour
    {
        [SerializeField] CannonStats stats;
        [Tooltip("撃つ弾のプレハブ（Projectile 付き）")]
        [SerializeField] Projectile projectilePrefab;

        [Header("砲塔の部品（モデル差し替え時はここを付け替える）")]
        [Tooltip("左右に回る部分（ローカル Y 軸まわりに回転）")]
        [SerializeField] Transform yawPivot;
        [Tooltip("上下に振る部分（任意。ローカル X 軸まわり）")]
        [SerializeField] Transform pitchPivot;
        [SerializeField] float minPitch = -10f, maxPitch = 45f;
        [Tooltip("弾が出る位置。前方向 = 発射方向")]
        [SerializeField] Transform muzzle;

        [Header("演出")]
        [SerializeField] GameObject muzzleFlashPrefab;
        [SerializeField] Animator animator;
        [SerializeField] string fireTrigger = "Fire";
        [SerializeField] AudioSource fireSound;
        [Tooltip("発射時の効果音")]
        [SerializeField] AudioClip fireClip;
        [SerializeField, Range(0f, 1f)] float fireVolume = 1f;
        public UnityEvent onFire = new UnityEvent();

        [Header("索敵")]
        [SerializeField] float retargetInterval = 0.2f;

        [Header("射程の表示")]
        [Tooltip("射程（CannonStats.range）を地面に円で表示する")]
        [SerializeField] bool showRange = true;
        [SerializeField] Color rangeColor = new Color(1f, 0.55f, 0.1f, 0.85f);
        [SerializeField] float rangeLineWidth = 1.5f;

        Health health;
        Placeable placeable;
        Health target;
        float cooldown;
        float retargetTimer;
        RangeRing rangeRing;

        public CannonStats Stats => stats;

        void Awake()
        {
            health = GetComponent<Health>();
            placeable = GetComponent<Placeable>();
            if (stats) health.ApplyStats(stats);
            if (!yawPivot) yawPivot = transform;
            if (!muzzle) muzzle = pitchPivot ? pitchPivot : yawPivot;
        }

        void Start()
        {
            if (showRange && stats)
                rangeRing = RangeRing.Create(transform, stats.range, rangeColor, rangeLineWidth, "RangeRing");
        }

        void Update()
        {
            if (rangeRing)
            {
                if (stats) rangeRing.SetRadius(stats.range);   // Inspector で射程を変えたら追従
                if (!health.IsAlive && rangeRing.gameObject.activeSelf) rangeRing.gameObject.SetActive(false);
            }
            if (!stats || !health.IsAlive) return;
            if (placeable && !placeable.IsPlaced) return;
            var gm = GameManager.Instance;
            if (gm && gm.State != GameState.Playing) return;

            float dt = Time.deltaTime;
            cooldown -= dt;
            retargetTimer -= dt;
            if (retargetTimer <= 0f || !target || !target.IsTargetable)
            {
                retargetTimer = retargetInterval;
                target = Health.FindNearestHostile(health.Team, yawPivot.position, stats.range);
            }
            if (!target) return;

            Vector3 aim = target.AimPosition;
            if ((aim - yawPivot.position).sqrMagnitude > stats.range * stats.range * 1.1f) { target = null; return; }

            // ヨー（砲塔の上方向まわり）
            Vector3 up = yawPivot.parent ? yawPivot.parent.up : Vector3.up;
            Vector3 flat = Vector3.ProjectOnPlane(aim - yawPivot.position, up);
            if (flat.sqrMagnitude > 1e-6f)
            {
                Quaternion want = Quaternion.LookRotation(flat, up);
                yawPivot.rotation = Quaternion.RotateTowards(yawPivot.rotation, want, stats.turnSpeed * dt);
            }

            // ピッチ（任意）
            if (pitchPivot)
            {
                Vector3 local = yawPivot.InverseTransformPoint(aim) - yawPivot.InverseTransformPoint(pitchPivot.position);
                float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(pitch, -maxPitch, -minPitch);
                pitchPivot.localRotation = Quaternion.RotateTowards(pitchPivot.localRotation,
                    Quaternion.Euler(pitch, 0f, 0f), stats.turnSpeed * dt);
            }

            float angle = Vector3.Angle(Vector3.ProjectOnPlane(yawPivot.forward, up), flat);
            if (cooldown <= 0f && angle <= stats.aimTolerance) Fire();
        }

        void Fire()
        {
            cooldown = stats.fireInterval;
            if (projectilePrefab)
            {
                var p = Instantiate(projectilePrefab, muzzle.position, Quaternion.LookRotation(target.AimPosition - muzzle.position));
                p.Launch(target, health.Team, stats.damage, stats.projectileSpeed, stats.splashRadius, health);
            }
            else
            {
                target.TakeDamage(stats.damage);
            }
            if (muzzleFlashPrefab) Fx.Spawn(muzzleFlashPrefab, muzzle.position, muzzle.rotation);
            AnimUtil.Trigger(animator, fireTrigger);
            if (fireSound) fireSound.Play();
            Sfx.Play(fireClip, fireVolume);
            onFire.Invoke();
        }

        void OnDrawGizmosSelected()
        {
            if (!stats) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
            Gizmos.DrawWireSphere(yawPivot ? yawPivot.position : transform.position, stats.range);
        }
    }
}
