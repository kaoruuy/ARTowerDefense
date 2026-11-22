using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>
    /// 消しゴム：トラップドアを開けて味方兵士を定期的に出す（開く → せり上がる → 閉じる）。
    /// 固定(Place)されていて、ゲーム中のときだけ動く。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class SoldierSpawner : MonoBehaviour
    {
        [SerializeField] SoldierSpawnerStats stats;
        [SerializeField] CharacterUnit soldierPrefab;
        [Tooltip("兵士が出てくる位置（マンホール）。上方向 = 出てくる向き")]
        [SerializeField] Transform spawnPoint;
        [Tooltip("せり上がり開始位置の深さ（spawnPoint からの下向き距離）")]
        [SerializeField] float emergeDepth = 16f;
        [Tooltip("兵士の持ち場をスポナーの周りにばらけさせる半径")]
        [SerializeField] float homeSpread = 35f;

        [Header("トラップドア")]
        [Tooltip("扉の蝶番。ローカル Z 軸まわりに回して開閉する（空なら開閉しない）")]
        [SerializeField] Transform doorHinge;
        [Tooltip("開いたときの角度（度）")]
        [SerializeField] float doorOpenAngle = 110f;
        [SerializeField] float doorOpenTime = 0.25f;
        [Tooltip("兵士が出きってから閉じ始めるまでの時間")]
        [SerializeField] float doorHoldTime = 0.2f;
        [SerializeField] float doorCloseTime = 0.35f;

        [Header("演出")]
        [SerializeField] GameObject spawnEffectPrefab;
        [Tooltip("扉が開いて兵士が出てくる瞬間の効果音")]
        [SerializeField] AudioClip spawnSound;
        [SerializeField, Range(0f, 1f)] float spawnSoundVolume = 1f;
        [SerializeField] Animator animator;
        [SerializeField] string spawnTrigger = "Spawn";
        public UnityEvent onSpawn = new UnityEvent();

        readonly List<CharacterUnit> soldiers = new List<CharacterUnit>();
        Health health;
        Placeable placeable;
        float timer = -1f;
        bool spawning;
        Quaternion doorClosedRotation = Quaternion.identity;

        void Awake()
        {
            health = GetComponent<Health>();
            placeable = GetComponent<Placeable>();
            if (stats) health.ApplyStats(stats);
            if (!spawnPoint) spawnPoint = transform;
            if (doorHinge) doorClosedRotation = doorHinge.localRotation;
        }

        void Update()
        {
            if (!stats || !soldierPrefab || !health.IsAlive) return;
            if (placeable && !placeable.IsPlaced) return;
            var gm = GameManager.Instance;
            if (gm && gm.State != GameState.Playing) return;

            if (timer < 0f) timer = stats.firstSpawnDelay;
            timer -= Time.deltaTime;
            if (timer > 0f || spawning) return;

            soldiers.RemoveAll(s => !s || !s.Health.IsAlive);
            if (soldiers.Count >= stats.maxAlive) { timer = 0.5f; return; }

            timer = stats.spawnInterval;
            StartCoroutine(SpawnRoutine());
        }

        /// <summary>扉を開ける → 兵士がせり上がる → 扉を閉める。</summary>
        IEnumerator SpawnRoutine()
        {
            spawning = true;
            if (doorHinge) yield return AnimateDoor(0f, 1f, doorOpenTime);

            Spawn();

            float wait = (stats ? stats.emergeDuration : 0f) + doorHoldTime;
            for (float t = 0f; t < wait; t += Time.deltaTime) yield return null;

            if (doorHinge) yield return AnimateDoor(1f, 0f, doorCloseTime);
            spawning = false;
        }

        IEnumerator AnimateDoor(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                SetDoor(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / duration)));
                yield return null;
            }
            SetDoor(to);
        }

        void SetDoor(float open01)
        {
            if (!doorHinge) return;
            doorHinge.localRotation = doorClosedRotation * Quaternion.AngleAxis(doorOpenAngle * open01, Vector3.forward);
        }

        void Spawn()
        {
            Vector3 up = spawnPoint.up;
            Vector3 to = spawnPoint.position;
            Vector3 from = to - up * emergeDepth;

            var gmBoard = GameManager.Instance ? GameManager.Instance.Board : null;
            var s = Instantiate(soldierPrefab, from, spawnPoint.rotation, gmBoard);   // 盤（宝箱マーカー基準）の子
            Vector3 groundUp = GameManager.Instance ? GameManager.Instance.GroundUp : transform.up;
            float spread = stats.wanderRadius > 0f ? stats.wanderRadius : homeSpread;
            Vector3 offset = Quaternion.AngleAxis(Random.Range(0f, 360f), groundUp) *
                             Vector3.ProjectOnPlane(transform.forward, groundUp).normalized * Random.Range(spread * 0.3f, spread);
            Vector3 home = transform.position + offset;
            if (GameManager.Instance) home = GameManager.Instance.ProjectToGround(home);

            s.SetGuardArea(transform.position, stats.guardRadius);
            if (stats.wanderRadius > 0f) s.SetWanderArea(transform.position, stats.wanderRadius);
            s.Initialize(Team.Ally, home);
            s.Emerge(from, to, stats.emergeDuration);
            soldiers.Add(s);

            if (spawnEffectPrefab) Fx.Spawn(spawnEffectPrefab, to, spawnPoint.rotation);
            Sfx.Play(spawnSound, spawnSoundVolume);
            AnimUtil.Trigger(animator, spawnTrigger);
            onSpawn.Invoke();
        }
    }
}
