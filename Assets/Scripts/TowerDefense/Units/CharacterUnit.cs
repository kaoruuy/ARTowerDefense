using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>
    /// 兵士・敵キャラ共通の AI（物理/NavMesh 不要）。机の面（タワーの面）の上を歩く。
    /// Attacker（敵）: 宝箱（タワー）を目指して攻撃する。行く手をふさぐ兵士とだけ戦う。生まれたときに2種類のどちらかになる。
    ///   ・壊す型: 進路を塞ぐ壁などを攻撃して壊しながら進む
    ///   ・避ける型: 壁などを迂回して進む（色が変わる。どうしても通れないときだけ壊す）
    /// Defender（兵士）: 持ち場（トラップドア）の周りをランダムに徘徊し、近くに敵が来たら追いかけて戦う。
    /// 持ち場などの位置は「盤（宝箱マーカー基準）」のローカル座標で覚えるので、盤が動いてもずれない。
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class CharacterUnit : MonoBehaviour
    {
        public enum UnitRole { Attacker, Defender }

        [SerializeField] CharacterStats stats;
        [SerializeField] UnitRole role = UnitRole.Attacker;

        [Header("アニメーション（Animator にそのパラメータがあれば送る）")]
        [SerializeField] Animator animator;
        [SerializeField] string speedParam = "Speed";
        [SerializeField] string attackTrigger = "Attack";
        [SerializeField] string deathTrigger = "Die";

        [Header("演出")]
        [Tooltip("攻撃が当たったときに相手の位置に出すエフェクト")]
        [SerializeField] GameObject attackEffectPrefab;
        [Tooltip("攻撃が当たったときの効果音")]
        [SerializeField] AudioClip attackHitSound;
        [SerializeField, Range(0f, 1f)] float attackHitVolume = 1f;
        [Tooltip("歩いている間ループする効果音")]
        [SerializeField] AudioClip moveLoopSound;
        [SerializeField, Range(0f, 1f)] float moveLoopVolume = 0.4f;
        [Tooltip("走っている間、足元に出すパーティクル（土煙など）")]
        [SerializeField] ParticleSystem dashParticlePrefab;
        [Tooltip("足元の煙の向き（キャラのローカル角度）。(−20, 180, 0) = 後ろ向きで少し上へ")]
        [SerializeField] Vector3 dashLocalEuler = new Vector3(-20f, 180f, 0f);
        [Tooltip("ヒット（エフェクト・効果音・ダメージ）を攻撃アニメの開始から何秒ずらすか。マイナス = アニメより早く出す")]
        [SerializeField] float hitTimingOffset = 0.1f;
        [Tooltip("攻撃パーティクルを攻撃アニメ開始の何秒後に出すか（ヒットのタイミングとは別）")]
        [SerializeField] float attackEffectDelay = 0.1f;
        [Tooltip("宝箱を攻撃するときだけ攻撃距離をこれだけ延ばす（群がった後ろの敵も攻撃できるように）")]
        [SerializeField] float towerAttackBonus = 15f;

        [Header("細かい挙動")]
        [SerializeField] float retargetInterval = 0.25f;
        [Tooltip("机の面に高さを合わせる速さ（落下速度）")]
        [SerializeField] float groundSnapSpeed = 150f;
        [Tooltip("同じ陣営のキャラ同士が重ならないように押し合う強さ")]
        [SerializeField] float separationStrength = 1f;

        [Header("敵：壁を避ける型")]
        [Tooltip("ON = 壁を避けて進む型 / OFF = 壁を壊して進む型（EnemySpawner が生成時にランダムで決める）")]
        [SerializeField] bool avoidObstacles = false;
        [Tooltip("避ける型の見た目の色（Body に付ける）")]
        [SerializeField] Color avoiderTint = new Color(0.25f, 0.6f, 1f);
        [Tooltip("迂回を続けてもこの秒数通れなければ、あきらめて壁を壊す")]
        [SerializeField] float detourGiveUpTime = 8f;

        [Header("敵：狙い")]
        [Tooltip("この距離（体の表面から）まで近づいてきた兵士とだけ戦う。それ以外は宝箱を目指す")]
        [SerializeField] float engageRange = 12f;

        [Header("浮いて見えないように")]
        [Tooltip("見た目（Model）を少し机に沈める量（ワールド単位）")]
        [SerializeField] float modelSink = 1f;
        [SerializeField] bool groundShadow = true;
        [SerializeField, Range(0f, 1f)] float shadowOpacity = 0.4f;

        [Header("兵士：徘徊")]
        [Tooltip("徘徊中の速さ（moveSpeed に対する倍率）")]
        [SerializeField] float wanderSpeedFactor = 0.5f;
        [SerializeField] Vector2 wanderPause = new Vector2(0.5f, 2f);

        public UnityEvent onAttack = new UnityEvent();

        public CharacterStats Stats => stats;
        public bool AvoidsObstacles => avoidObstacles;
        public Health Health => health;
        public Health CurrentTarget => target;

        Health health;
        Health target;
        Vector3 home;
        Vector3 guardCenter;
        float guardRadius = -1f;
        float attackTimer;
        float retargetTimer;
        bool initialized;

        // 徘徊
        Vector3 wanderCenter;
        float wanderRadius = -1f;
        Vector3 wanderTarget;
        bool hasWanderTarget;
        float wanderWait;

        // 迂回
        int detourSide;
        float blockedTime;
        float unblockedTime;

        bool emerging;
        Vector3 emergeFrom, emergeTo;
        float emergeT, emergeDuration;

        static readonly RaycastHit[] hitBuffer = new RaycastHit[16];

        float ScaledBodyRadius => stats.bodyRadius * GameplayScale.Factor;

        void Awake()
        {
            GameplayScale.Apply(transform);
            health = GetComponent<Health>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            health.onDeath.AddListener(OnDeath);
            var model = transform.Find("Model");
            if (model && modelSink != 0f)
                model.localPosition -= Vector3.up * (modelSink * GameplayScale.Factor / Mathf.Max(1e-4f, transform.lossyScale.y));
        }

        void Start()
        {
            if (!initialized) Initialize(health.Team, transform.position);
            if (groundShadow && stats) GroundShadow.Create(transform, ScaledBodyRadius * 3.2f, shadowOpacity);
        }

        // 位置は盤（宝箱マーカー基準）のローカル座標で覚える
        static Vector3 ToLocal(Vector3 world) { var gm = GameManager.Instance; return gm ? gm.ToBoard(world) : world; }
        static Vector3 ToWorld(Vector3 local) { var gm = GameManager.Instance; return gm ? gm.FromBoard(local) : local; }

        /// <summary>スポナーが生成直後に呼ぶ。</summary>
        public void Initialize(Team team, Vector3 homePosition, float healthMultiplier = 1f)
        {
            health.SetTeam(team);
            home = ToLocal(homePosition);
            if (guardRadius < 0f) guardCenter = home;
            if (stats) health.ApplyStats(stats, healthMultiplier);
            initialized = true;
        }

        public void SetGuardArea(Vector3 center, float radius)
        {
            guardCenter = ToLocal(center);
            guardRadius = radius;
        }

        /// <summary>兵士用：center を中心に radius 以内をランダムに徘徊させる。</summary>
        public void SetWanderArea(Vector3 center, float radius)
        {
            wanderCenter = ToLocal(center);
            wanderRadius = radius;
            hasWanderTarget = false;
        }

        /// <summary>敵用：壁を避ける型にするか（生成直後に EnemySpawner が呼ぶ）。</summary>
        public void SetAvoidObstacles(bool avoid)
        {
            avoidObstacles = avoid;
            if (avoid) ApplyTint(avoiderTint);
        }

        void ApplyTint(Color c)
        {
            var mpb = new MaterialPropertyBlock();
            var renderers = GetComponentsInChildren<Renderer>(true);
            bool hasBody = false;
            foreach (var r in renderers) if (r.name == "Body") { hasBody = true; break; }
            foreach (var r in renderers)
            {
                if (r is LineRenderer || r.GetComponentInParent<HealthBar>()) continue;
                if (hasBody && r.name != "Body") continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                r.SetPropertyBlock(mpb);
            }
        }

        /// <summary>トラップドアなどから from → to へせり上がる。その間 AI は止まる。</summary>
        public void Emerge(Vector3 from, Vector3 to, float duration)
        {
            emerging = duration > 0f;
            emergeFrom = ToLocal(from); emergeTo = ToLocal(to);
            emergeT = 0f; emergeDuration = duration;
            transform.position = emerging ? from : to;
        }

        void Update()
        {
            if (!health.IsAlive || !stats) return;
            float dt = Time.deltaTime;
            var gm = GameManager.Instance;
            TickAttackTiming(dt);

            if (emerging)
            {
                emergeT += dt;
                float n = Mathf.Clamp01(emergeT / emergeDuration);
                transform.position = ToWorld(Vector3.Lerp(emergeFrom, emergeTo, Mathf.SmoothStep(0f, 1f, n)));
                if (n >= 1f) emerging = false;
                return;
            }

            Vector3 up = gm ? gm.GroundUp : Vector3.up;
            bool active = gm == null || gm.State == GameState.Playing;
            float speed = 0f;

            if (active)
            {
                attackTimer -= dt;
                retargetTimer -= dt;
                if (retargetTimer <= 0f || !target || !target.IsTargetable)
                {
                    retargetTimer = retargetInterval;
                    target = ChooseTarget(gm);
                }

                Vector3 pos = transform.position;
                if (target)
                {
                    Vector3 cp = target.ClosestPoint(pos);
                    Vector3 flat = Vector3.ProjectOnPlane(cp - pos, up);
                    float dist = flat.magnitude - ScaledBodyRadius;
                    // 宝箱は敵が群がると表面まで近づけない子が出るので、少し離れていても攻撃できるようにする
                    float range = stats.attackRange + (target.Kind == TargetKind.Core ? towerAttackBonus : 0f);
                    if (dist <= range)
                    {
                        Face(flat, up, dt);
                        TryAttack(cp);
                    }
                    else
                    {
                        speed = MoveTowards(flat / Mathf.Max(flat.magnitude, 1e-4f), up, dt);
                    }
                }
                else if (role == UnitRole.Defender)
                {
                    if (wanderRadius > 0f)
                    {
                        speed = Wander(pos, up, dt);
                    }
                    else
                    {
                        Vector3 flat = Vector3.ProjectOnPlane(ToWorld(home) - pos, up);
                        if (flat.magnitude > ScaledBodyRadius * 2f)
                            speed = MoveTowards(flat.normalized, up, dt);
                    }
                }

                Separate(up, dt);
            }

            AnimUtil.Float(animator, speedParam, speed);
            UpdateMoveSound(speed > 0f);
            UpdateDash(speed > 0f);
            KeepOnGround(gm, up, dt);
        }

        Health ChooseTarget(GameManager gm)
        {
            Vector3 pos = transform.position;
            Team team = health.Team;

            if (role == UnitRole.Defender)
            {
                // 持ち場の範囲内にいる敵キャラ
                Health h = Health.FindNearestHostile(team, pos, stats.detectRange, x => x.Kind == TargetKind.Unit && InGuardArea(x));
                if (!h && guardRadius > 0f)
                    h = Health.FindNearestHostile(team, ToWorld(guardCenter), guardRadius, x => x.Kind == TargetKind.Unit);
                return h;
            }

            // Attacker：基本は宝箱（タワー）を目指す
            if (!avoidObstacles)
            {
                // 壊す型：近くに丸太（壁）があればそれを壊しに行く。無ければ近くの兵士 → 宝箱
                var wall = Health.FindNearestHostile(team, pos, stats.detectRange, IsWall);
                if (wall) return wall;
                var near = Health.FindNearestHostile(team, pos, ScaledBodyRadius + stats.attackRange + engageRange, x => x.Kind == TargetKind.Unit);
                if (near) return near;
            }
            else
            {
                // 壊さない型：近くに兵士がいれば攻撃、いなければ宝箱
                var unit = Health.FindNearestHostile(team, pos, stats.detectRange, x => x.Kind == TargetKind.Unit);
                if (unit) return unit;
            }
            // 2) 宝箱。壁などは「進路をふさいだとき」だけ MoveTowards で攻撃対象になる（壊す型）／迂回する（避ける型）
            if (gm && gm.HasTower && gm.Tower.Health.IsTargetable) return gm.Tower.Health;
            // 3) 宝箱が無ければ近くの何か
            return Health.FindNearestHostile(team, pos, stats.detectRange * 3f);
        }

        /// <summary>丸太などの壁（大砲・トラップドア・宝箱は含まない）。</summary>
        static bool IsWall(Health h)
            => h.Kind == TargetKind.Structure && !h.GetComponent<CannonTurret>() && !h.GetComponent<SoldierSpawner>();

        bool InGuardArea(Health h)
        {
            if (guardRadius <= 0f) return true;
            return (h.transform.position - ToWorld(guardCenter)).sqrMagnitude <= guardRadius * guardRadius * 2.25f;
        }

        float MoveTowards(Vector3 dir, Vector3 up, float dt, float speedFactor = 1f)
        {
            float moveSpeed = stats.moveSpeed * speedFactor;
            float step = moveSpeed * dt;
            bool steering = role == UnitRole.Defender || avoidObstacles;   // 丸太を避けて回り込むキャラ

            if (role == UnitRole.Attacker)
            {
                Vector3 origin = transform.position + up * (stats.obstacleCheckHeight * GameplayScale.Factor);
                float len = ScaledBodyRadius + stats.attackRange + step;
                Health blocker = FindBlocker(origin, dir, len);

                // 避ける型：丸太などの建物は攻撃せず、左右に回り込む
                if (avoidObstacles && blocker && blocker.Kind == TargetKind.Structure) blocker = null;
                if (blocker)
                {
                    // 兵士・宝箱・（壊す型の）壁など：攻撃対象にする
                    target = blocker;
                    retargetTimer = 1f;
                    Face(dir, up, dt);
                    return 0f;
                }
            }

            if (steering)
            {
                dir = SteerAround(dir, up, ScaledBodyRadius + step + 8f, dt, out bool noWay);
                if (noWay) { blockedTime += dt; Face(dir, up, dt); return 0f; }
            }

            transform.position += dir * step;
            if (steering) PushOutOfObstacles(up);
            CheckStuck(step, dt);
            Face(dir, up, dt);
            return moveSpeed;
        }

        // ---------- 丸太などを避ける ----------

        static readonly RaycastHit[] sphereBuffer = new RaycastHit[16];
        float clearTime;
        Vector3 progressStart;
        float progressTimer, progressExpected;

        /// <summary>このキャラにとって避けるべき建物か（兵士 = 陣営を問わず全部 / 避ける型の敵 = 敵対する丸太など。宝箱は攻撃対象なので除く）。</summary>
        bool IsObstacle(Health h)
        {
            if (!h || h == health || !h.IsTargetable || h.Kind == TargetKind.Unit) return false;
            if (role == UnitRole.Defender) return true;
            return h.Kind == TargetKind.Structure;
        }

        /// <summary>体の太さで前方をチェック（最初から重なっているものは PushOutOfObstacles で処理）。</summary>
        bool IsBlocked(Vector3 origin, Vector3 dir, float len)
        {
            float r = Mathf.Max(0.5f, ScaledBodyRadius * 0.8f);
            int n = Physics.SphereCastNonAlloc(origin, r, dir, sphereBuffer, len, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                if (sphereBuffer[i].distance <= 0f) continue;
                if (IsObstacle(sphereBuffer[i].collider.GetComponentInParent<Health>())) return true;
            }
            return false;
        }

        /// <summary>
        /// 前が丸太などで塞がれていたら、左右どちらか（一度決めた側を優先）に角度を広げて通れる向きを探す。
        /// 塞がれている間は同じ側に回り続けるので、丸太の端まで沿って進み、抜けたら目的地へ向かい直す。
        /// </summary>
        Vector3 SteerAround(Vector3 dir, Vector3 up, float len, float dt, out bool noWay)
        {
            noWay = false;
            Vector3 origin = transform.position + up * (stats.obstacleCheckHeight * GameplayScale.Factor);
            if (!IsBlocked(origin, dir, len))
            {
                clearTime += dt;
                if (clearTime > 0.6f) { detourSide = 0; blockedTime = 0f; }
                return dir;
            }
            clearTime = 0f;
            blockedTime += dt;

            if (detourSide == 0)
            {
                // 近い角度で通れる側を選ぶ
                foreach (float a in steerAngles)
                {
                    bool l = !IsBlocked(origin, Quaternion.AngleAxis(-a, up) * dir, len);
                    bool r = !IsBlocked(origin, Quaternion.AngleAxis(a, up) * dir, len);
                    if (l || r) { detourSide = l && r ? (UnityEngine.Random.value < 0.5f ? 1 : -1) : (r ? 1 : -1); break; }
                }
                if (detourSide == 0) { noWay = true; return dir; }
            }

            for (int k = 0; k < 2; k++)
            {
                foreach (float a in steerAngles)
                {
                    Vector3 d = Quaternion.AngleAxis(a * detourSide, up) * dir;
                    if (!IsBlocked(origin, d, len)) return d;
                }
                detourSide = -detourSide;   // こちら側は全部ふさがっている → 反対側へ
            }
            noWay = true;
            return dir;
        }

        static readonly float[] steerAngles = { 25f, 45f, 65f, 85f, 105f, 125f, 150f };

        /// <summary>丸太などにめり込んでいたら外へ押し出す。</summary>
        void PushOutOfObstacles(Vector3 up)
        {
            Vector3 probe = transform.position + up * (stats.obstacleCheckHeight * GameplayScale.Factor);
            var all = Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (!IsObstacle(h)) continue;
                Vector3 cp = h.ClosestPoint(probe);
                Vector3 d = Vector3.ProjectOnPlane(probe - cp, up);
                float m = d.magnitude;
                if (m > 1e-3f && m < ScaledBodyRadius) transform.position += d / m * (ScaledBodyRadius - m);
            }
        }

        /// <summary>動こうとしているのにほとんど進めていなければ、回り込む向きを反対にする。</summary>
        void CheckStuck(float step, float dt)
        {
            if (progressTimer <= 0f) progressStart = transform.position;
            progressTimer += dt;
            progressExpected += step;
            if (progressTimer < 0.8f) return;
            float moved = (transform.position - progressStart).magnitude;
            if (progressExpected > 1f && moved < progressExpected * 0.25f && detourSide != 0) detourSide = -detourSide;
            progressTimer = 0f;
            progressExpected = 0f;
        }
        ParticleSystem dash;

        void UpdateDash(bool moving)
        {
            if (!dashParticlePrefab) return;
            if (!dash)
            {
                dash = Instantiate(dashParticlePrefab, transform);   // 足元（ルートの位置）に付ける
                GameplayScale.ConfigureParticles(dash.transform);
                dash.transform.localPosition = Vector3.zero;
                dash.transform.localRotation = Quaternion.Euler(dashLocalEuler);   // 後ろ向きに出す
                // 一度出た煙はワールドに残す（キャラの向き・移動に引っ張られない）
                foreach (var ps in dash.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                }
                dash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            if (moving && !dash.isEmitting) dash.Play(true);
            else if (!moving && dash.isEmitting) dash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        AudioSource moveLoop;

        /// <summary>同時に鳴らす足音の最大数。キャラが何十体いても足音が重なって BGM を消さないようにする。</summary>
        public static int MaxMoveLoops = 3;
        static int activeMoveLoops;
        bool moveLoopCounted;

        void UpdateMoveSound(bool moving)
        {
            if (!moveLoopSound) return;
            if (moving && !moveLoopCounted && activeMoveLoops >= MaxMoveLoops) moving = false;   // 枠がいっぱいなら鳴らさない
            if (!moveLoop) moveLoop = Sfx.CreateLoop(gameObject, moveLoopSound, moveLoopVolume);
            if (moving && !moveLoop.isPlaying) { moveLoop.Play(); CountMoveLoop(true); }
            else if (!moving && moveLoop.isPlaying) { moveLoop.Stop(); CountMoveLoop(false); }
        }

        void CountMoveLoop(bool on)
        {
            if (on == moveLoopCounted) return;
            moveLoopCounted = on;
            activeMoveLoops = Mathf.Max(0, activeMoveLoops + (on ? 1 : -1));
        }

        void OnDisable() { CountMoveLoop(false); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMoveLoopCount() { activeMoveLoops = 0; }

        Health FindBlocker(Vector3 origin, Vector3 dir, float len)
        {
            int n = Physics.RaycastNonAlloc(origin, dir, hitBuffer, len, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Health blocker = null;
            for (int i = 0; i < n; i++)
            {
                var h = hitBuffer[i].collider.GetComponentInParent<Health>();
                if (!h || h == health || !h.IsTargetable || h.Team == health.Team) continue;
                if (hitBuffer[i].distance < best) { best = hitBuffer[i].distance; blocker = h; }
            }
            return blocker;
        }

        static readonly float[] detourAngles = { 30f, 55f, 80f, 105f, 130f };

        /// <summary>前が塞がっているとき、左右に少しずつ角度を変えて通れる向きを探す。一度決めた側を優先する。</summary>
        bool FindDetour(Vector3 origin, Vector3 dir, Vector3 up, float len, out Vector3 result)
        {
            int first = detourSide != 0 ? detourSide : (UnityEngine.Random.value < 0.5f ? 1 : -1);
            foreach (float a in detourAngles)
            {
                for (int k = 0; k < 2; k++)
                {
                    int side = k == 0 ? first : -first;
                    Vector3 d = Quaternion.AngleAxis(a * side, up) * dir;
                    var b = FindBlocker(origin, d, len * 1.5f);
                    if (!b || b.Kind == TargetKind.Unit)
                    {
                        detourSide = side;
                        result = d;
                        return true;
                    }
                }
            }
            result = dir;
            return false;
        }

        /// <summary>兵士：徘徊範囲内のランダムな地点へ歩き、着いたら少し休んで次の地点へ。</summary>
        float Wander(Vector3 pos, Vector3 up, float dt)
        {
            if (wanderWait > 0f) { wanderWait -= dt; return 0f; }

            // 徘徊範囲の外に出ていたら（追跡の後など）まず中へ戻る
            if (!hasWanderTarget || (wanderTarget - wanderCenter).sqrMagnitude > wanderRadius * wanderRadius * 1.1f)
            {
                wanderTarget = PickWanderPoint(up);
                hasWanderTarget = true;
            }

            Vector3 flat = Vector3.ProjectOnPlane(ToWorld(wanderTarget) - pos, up);
            if (flat.magnitude <= ScaledBodyRadius * 1.5f)
            {
                hasWanderTarget = false;
                wanderWait = UnityEngine.Random.Range(wanderPause.x, wanderPause.y);
                return 0f;
            }
            return MoveTowards(flat.normalized, up, dt, wanderSpeedFactor);
        }

        Vector3 PickWanderPoint(Vector3 up)
        {
            Vector3 a = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (a.sqrMagnitude < 1e-4f) a = Vector3.ProjectOnPlane(Vector3.right, up);
            a.Normalize();
            Vector3 b = Vector3.Cross(up, a);
            // 中心（トラップドア）付近に固まらないよう、半径の 35%〜100% の範囲から選ぶ
            Vector2 r = UnityEngine.Random.insideUnitCircle.normalized * (wanderRadius * Mathf.Sqrt(UnityEngine.Random.Range(0.12f, 1f)));
            Vector3 p = ToWorld(wanderCenter) + a * r.x + b * r.y;
            var gm = GameManager.Instance;
            return ToLocal(gm ? gm.ProjectToGround(p) : p);   // 盤ローカルで返す
        }

        void Face(Vector3 flatDir, Vector3 up, float dt)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            Quaternion want = Quaternion.LookRotation(flatDir.normalized, up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, want, stats.turnSpeed * dt);
        }

        float pendingAnimTimer = -1f;
        float pendingHitTimer = -1f;
        Health pendingTarget;

        void TryAttack(Vector3 hitPoint)
        {
            if (attackTimer > 0f) return;
            attackTimer = stats.attackInterval;
            effectTarget = target;
            if (hitTimingOffset < 0f)
            {
                // ヒットを先に出し、アニメを少し遅らせて始める
                DoHit(target, hitPoint);
                pendingAnimTimer = -hitTimingOffset;
            }
            else
            {
                StartAttackAnim();
                if (hitTimingOffset <= 0f) DoHit(target, hitPoint);
                else { pendingHitTimer = hitTimingOffset; pendingTarget = target; }
            }
        }

        float pendingEffectTimer = -1f;
        Health effectTarget;

        /// <summary>攻撃アニメを始め、attackEffectDelay 秒後に攻撃パーティクルを出す。</summary>
        void StartAttackAnim()
        {
            AnimUtil.Trigger(animator, attackTrigger);
            if (attackEffectPrefab)
            {
                if (attackEffectDelay <= 0f)
                {
                    Vector3 p = effectTarget ? effectTarget.ClosestPoint(transform.position) : transform.position;
                    Fx.Spawn(attackEffectPrefab, p, Quaternion.identity);
                }
                else pendingEffectTimer = attackEffectDelay;
            }
        }

        void DoHit(Health t, Vector3 hitPoint)
        {
            if (!t || !t.IsTargetable) return;
            t.TakeDamage(stats.attackDamage, hitPoint);
            Sfx.Play(attackHitSound, attackHitVolume);
            onAttack.Invoke();
        }

        void TickAttackTiming(float dt)
        {
            if (pendingAnimTimer > 0f)
            {
                pendingAnimTimer -= dt;
                if (pendingAnimTimer <= 0f) StartAttackAnim();
            }
            if (pendingEffectTimer > 0f)
            {
                pendingEffectTimer -= dt;
                if (pendingEffectTimer <= 0f && attackEffectPrefab)
                {
                    Vector3 p = effectTarget ? effectTarget.ClosestPoint(transform.position) : transform.position + transform.forward * (ScaledBodyRadius + stats.attackRange * 0.5f);
                    Fx.Spawn(attackEffectPrefab, p, Quaternion.identity);
                }
            }
            if (pendingHitTimer > 0f)
            {
                pendingHitTimer -= dt;
                if (pendingHitTimer <= 0f && pendingTarget)
                    DoHit(pendingTarget, pendingTarget.ClosestPoint(transform.position));
            }
        }

        void Separate(Vector3 up, float dt)
        {
            if (separationStrength <= 0f) return;
            Vector3 pos = transform.position;
            Vector3 push = Vector3.zero;
            float minDist = ScaledBodyRadius * 2f;
            var all = Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                var h = all[i];
                if (!h || h == health || h.Kind != TargetKind.Unit || h.Team != health.Team) continue;
                Vector3 d = Vector3.ProjectOnPlane(pos - h.transform.position, up);
                float m = d.magnitude;
                if (m < minDist && m > 1e-4f) push += d / m * (minDist - m);
            }
            if (push != Vector3.zero)
                transform.position += Vector3.ClampMagnitude(push, minDist) * Mathf.Clamp01(separationStrength * 5f * dt);
        }

        [Header("段差（本など）")]
        [Tooltip("この高さ以上の段差は飛び乗る/飛び降りる")]
        [SerializeField] float hopThreshold = 3f;
        [SerializeField] float hopTime = 0.6f;
        [Tooltip("ジャンプの頂点を段差よりどれだけ高くするか")]
        [SerializeField] float hopArcHeight = 30f;
        [Tooltip("Animator にあればジャンプ時に送るトリガー")]
        [SerializeField] string jumpTrigger = "Jump";

        bool hopping;
        float hopT, hopFrom, hopTo, hopArc;
        static readonly RaycastHit[] groundHits = new RaycastHit[8];

        /// <summary>p の真上から下向きに調べ、本などの地形の上面の高さ（机からの高さ）を返す。無ければ 0。</summary>
        static float TerrainHeightAt(Vector3 p, GameManager gm, Vector3 up)
        {
            if (BookTerrain.All.Count == 0) return 0f;
            Vector3 origin = p - up * Vector3.Dot(p - gm.GroundPoint, up) + up * 500f;
            int n = Physics.RaycastNonAlloc(origin, -up, groundHits, 600f, ~0, QueryTriggerInteraction.Ignore);
            float best = 0f;
            for (int i = 0; i < n; i++)
            {
                if (!groundHits[i].collider.GetComponent<TerrainSurface>()) continue;
                float hh = Vector3.Dot(groundHits[i].point - gm.GroundPoint, up);
                if (hh > best) best = hh;
            }
            return best;
        }

        void KeepOnGround(GameManager gm, Vector3 up, float dt)
        {
            if (!gm || !gm.HasTower) return;
            Vector3 pos = transform.position;
            float h = Vector3.Dot(pos - gm.GroundPoint, up);

            // 足元（と少し前方）の地形の高さ：机 = 0、本の上 = 本の高さ
            float ground = Mathf.Max(TerrainHeightAt(pos, gm, up), TerrainHeightAt(pos + transform.forward * stats.bodyRadius, gm, up));
            float nh;
            if (hopping)
            {
                hopT += dt / hopTime;
                float n = Mathf.Clamp01(hopT);
                nh = Mathf.Lerp(hopFrom, hopTo, n) + hopArc * 4f * n * (1f - n);
                if (n >= 1f) hopping = false;
            }
            else if (Mathf.Abs(ground - h) > hopThreshold && h < ground + 30f)
            {
                // 段差（本の端）：飛び乗る / 飛び降りる
                hopping = true;
                hopT = 0f;
                hopFrom = h;
                hopTo = ground;
                hopArc = Mathf.Abs(ground - h) * 0.5f + hopArcHeight;
                nh = h;
                AnimUtil.Trigger(animator, jumpTrigger);
            }
            else
            {
                nh = Mathf.MoveTowards(h, ground, groundSnapSpeed * dt);
            }
            transform.position = pos - up * (h - nh);

            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up);
            if (fwd.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(fwd.normalized, up);
        }

        void OnDeath()
        {
            AnimUtil.Trigger(animator, deathTrigger);
            if (moveLoop) moveLoop.Stop();
            CountMoveLoop(false);
            if (dash) dash.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (health.Team == Team.Enemy && GameManager.Instance && stats)
                GameManager.Instance.AddScore(stats.scoreValue);
            enabled = false;
        }
    }
}
