using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>タワーを中心とした円周上から、WaveDefinition に従って敵を湧かせる。</summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Serializable] public class WaveEvent : UnityEvent<int> { }

        [SerializeField] WaveDefinition waveDefinition;
        [Tooltip("湧いた瞬間に出すエフェクト")]
        [SerializeField] GameObject spawnEffectPrefab;
        [Tooltip("湧いた敵をまとめる親（空ならこの GameObject の子にはせずルートに置く）")]
        [SerializeField] Transform enemyParent;

        [Header("敵の種類")]
        [Tooltip("壁を避ける型になる確率（残りは壁を壊す型）")]
        [SerializeField, Range(0f, 1f)] float avoiderChance = 0.5f;

        [Header("出現円の表示")]
        [Tooltip("タワーを中心とした出現円（spawnRadius）を地面に表示する")]
        [SerializeField] bool showSpawnCircle = true;
        [SerializeField] Color spawnCircleColor = new Color(1f, 0.15f, 0.15f, 0.85f);
        [SerializeField] float spawnCircleLineWidth = 2.5f;

        public WaveEvent onWaveStarted = new WaveEvent();

        public int CurrentWave { get; private set; }   // 1 始まり
        public int TotalWaves => waveDefinition ? waveDefinition.waves.Count : 0;
        public bool IsEndless => waveDefinition && waveDefinition.endless;
        public bool IsFinished { get; private set; }
        public int AliveCount { get { alive.RemoveAll(e => !e); return alive.Count; } }
        // The wave lifecycle still waits for death animations. HUD counts only living enemies.
        public int LivingCount(CharacterStats stats = null)
        {
            int count = 0;
            foreach (var enemy in alive)
                if (enemy && enemy.Health && enemy.Health.IsAlive && (!stats || enemy.Stats == stats)) count++;
            return count;
        }
        public float NextWaveCountdown { get; private set; } = -1f;

        readonly List<CharacterUnit> alive = new List<CharacterUnit>();
        Coroutine routine;
        GameManager gm;
        RangeRing spawnCircle;

        void Update()
        {
            if (!showSpawnCircle || !waveDefinition) return;
            var manager = gm ? gm : GameManager.Instance;
            if (!manager || !manager.HasTower)
            {
                if (spawnCircle) spawnCircle.gameObject.SetActive(false);
                return;
            }
            // タワーの子にして、タワーの面（机の面）に円を描く
            if (!spawnCircle || spawnCircle.transform.parent != manager.Tower.transform)
            {
                if (spawnCircle) Destroy(spawnCircle.gameObject);
                spawnCircle = RangeRing.Create(manager.Tower.transform, waveDefinition.spawnRadius,
                                               spawnCircleColor, spawnCircleLineWidth, "SpawnCircle");
            }
            if (!spawnCircle.gameObject.activeSelf) spawnCircle.gameObject.SetActive(true);
            spawnCircle.SetRadius(waveDefinition.spawnRadius);
        }

        public void Begin(GameManager manager)
        {
            gm = manager;
            Stop();
            IsFinished = false;
            CurrentWave = 0;
            if (!waveDefinition || waveDefinition.waves.Count == 0)
            {
                Debug.LogWarning("[ARTD] EnemySpawner に WaveDefinition が設定されていません", this);
                IsFinished = true;
                return;
            }
            routine = StartCoroutine(Run());
        }

        public void Stop()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            NextWaveCountdown = -1f;
        }

        IEnumerator Run()
        {
            int loop = 0;
            while (true)
            {
                for (int w = 0; w < waveDefinition.waves.Count; w++)
                {
                    // エンドレスの2周目以降は最後のウェーブだけ繰り返す
                    if (loop > 0 && w < waveDefinition.waves.Count - 1) continue;

                    var wave = waveDefinition.waves[w];
                    for (NextWaveCountdown = wave.delayBefore; NextWaveCountdown > 0f; NextWaveCountdown -= Time.deltaTime)
                        yield return null;
                    NextWaveCountdown = -1f;

                    CurrentWave++;
                    onWaveStarted.Invoke(CurrentWave);

                    float countMul = Mathf.Pow(waveDefinition.loopCountMultiplier, loop);
                    float hpMul = Mathf.Pow(waveDefinition.loopHealthMultiplier, loop);
                    foreach (var g in wave.groups)
                    {
                        if (g == null || !g.enemyPrefab) continue;
                        int count = Mathf.RoundToInt(g.count * countMul);
                        for (int i = 0; i < count; i++)
                        {
                            SpawnOne(g.enemyPrefab, hpMul);
                            yield return new WaitForSeconds(g.interval);
                        }
                    }

                    // 次のウェーブは今のウェーブを全滅させてから
                    while (AliveCount > 0) yield return null;
                }
                if (!waveDefinition.endless) break;
                loop++;
            }
            IsFinished = true;
            routine = null;
        }

        void SpawnOne(CharacterUnit prefab, float hpMul)
        {
            if (!gm || !gm.HasTower) return;
            Vector3 up = gm.GroundUp;
            Vector3 center = gm.GroundPoint;
            Vector3 baseDir = Vector3.ProjectOnPlane(gm.Tower.transform.forward, up).normalized;
            if (baseDir.sqrMagnitude < 1e-6f) baseDir = Vector3.ProjectOnPlane(Vector3.forward, up).normalized;

            float angle = UnityEngine.Random.Range(0f, 360f);
            float radius = waveDefinition.spawnRadius + UnityEngine.Random.Range(-1f, 1f) * waveDefinition.spawnRadiusJitter;
            Vector3 dir = Quaternion.AngleAxis(angle, up) * baseDir;
            Vector3 pos = center + dir * radius;
            Quaternion rot = Quaternion.LookRotation(-dir, up);

            // 盤（宝箱マーカー基準）の子にして、宝箱と一緒に固定されるようにする
            var e = Instantiate(prefab, pos, rot, enemyParent ? enemyParent : gm.Board);
            e.Initialize(Team.Enemy, pos, hpMul);
            e.SetAvoidObstacles(UnityEngine.Random.value < avoiderChance);
            alive.Add(e);
            if (spawnEffectPrefab) Fx.Spawn(spawnEffectPrefab, pos, rot);
        }
    }
}
