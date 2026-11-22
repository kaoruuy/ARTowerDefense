using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Vuforia;

namespace ARTD
{
    /// <summary>
    /// ゲーム全体の進行。配置(Setup) → 開始(Playing) → ゲームオーバー/勝利。
    /// タワー(筆箱)が固定されると、その位置と向きが「机の面」の基準になる。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [SerializeField] EnemySpawner enemySpawner;

        [Header("開始")]
        [Tooltip("タワーが固定されたら自動でゲームを開始する")]
        [SerializeField] bool autoStartWhenTowerPlaced = false;
        [Tooltip("自動開始までの秒数（その間に他の文房具も固定できる）")]
        [SerializeField] float autoStartDelay = 5f;
        [Tooltip("ゲーム開始キー（UI のスタートボタンと同じ）")]
        [SerializeField] Key startKey = Key.Space;
        [Tooltip("リトライキー（ゲーム終了後）")]
        [SerializeField] Key restartKey = Key.R;

        [Header("盤（宝箱のマーカーが基準）")]
        [Tooltip("基準にするマーカー。空なら TowerCore を子に持つマーカー（宝箱）を自動で探す")]
        [SerializeField] ObserverBehaviour worldAnchor;
        [Tooltip("ゲーム中に基準マーカーを見失ったら一時停止する")]
        [SerializeField] bool pauseWhenAnchorLost = true;
        [Tooltip("盤が基準マーカーに追いつく速さ（大きいほど速い。小さいほど揺れが減る）")]
        [SerializeField] float boardFollowSpeed = 8f;
        [Tooltip("この距離（ワールド単位、マーカー幅=100）以下の小さな揺れは無視する")]
        [SerializeField] float boardDeadZone = 0.6f;
        [Tooltip("この角度（度）以下の小さな回転の揺れは無視する")]
        [SerializeField] float boardDeadZoneAngle = 0.8f;
        [Tooltip("この距離以上ずれたら、なめらかにせず一気に合わせる（マーカーを置き直したときなど）")]
        [SerializeField] float boardSnapDistance = 40f;

        [Header("BGM")]
        [Tooltip("ゲーム中（スタート〜ゲームオーバー/クリア）にループ再生する曲。空なら Resources の bgmResourcePath から読み込む")]
        [SerializeField] AudioClip bgm;
        [SerializeField] string bgmResourcePath = "Audio/maou_bgm_ethnic22";
        [SerializeField, Range(0f, 1f)] float bgmVolume = 0.35f;
        [Tooltip("終了時にフェードアウトする秒数")]
        [SerializeField] float bgmFadeOutTime = 1.5f;

        [Header("イベント")]
        public UnityEvent onTowerPlaced = new UnityEvent();
        public UnityEvent onGameStarted = new UnityEvent();
        public UnityEvent onGameOver = new UnityEvent();
        public UnityEvent onVictory = new UnityEvent();

        public event Action<GameState> StateChanged;

        public GameState State { get; private set; } = GameState.Setup;
        public TowerCore Tower { get; private set; }
        public bool HasTower => Tower && Tower.Health && Tower.Health.IsAlive;
        public EnemySpawner Spawner => enemySpawner;
        public int Score { get; private set; }
        public float AutoStartRemaining { get; private set; } = -1f;
        /// <summary>基準マーカーを見失って一時停止している。</summary>
        public bool IsTrackingPaused { get; private set; }

        /// <summary>
        /// 宝箱のマーカーに追従する「盤」。固定したオブジェクト・敵・兵士はすべてこの子になるので、
        /// 宝箱のマーカーさえ映っていれば、全体が宝箱を中心に机の上に固定される。
        /// </summary>
        public Transform Board { get; private set; }
        public ObserverBehaviour Anchor => worldAnchor;
        bool boardInitialized;
        AudioSource bgmSource;
        float bgmFadeTimer = -1f;

        Vector3 lastGroundPoint;
        Vector3 lastGroundUp = Vector3.up;

        /// <summary>机の面の法線（タワーの上方向）。</summary>
        public Vector3 GroundUp => Tower ? Tower.transform.up : lastGroundUp;
        /// <summary>机の面上の点（タワーの位置）。</summary>
        public Vector3 GroundPoint
        {
            get
            {
                if (!Tower) return lastGroundPoint;
                // 宝箱が本の上に載っているときは、机の面 = 宝箱の面から本の高さだけ下
                Vector3 p = Tower.transform.position;
                return p - Tower.transform.up * BookTerrain.HeightUnder(p);
            }
        }

        void Awake()
        {
            if (Instance && Instance != this) { Debug.LogWarning("[ARTD] GameManager が複数あります", this); }
            Instance = this;
            if (!enemySpawner) enemySpawner = FindFirstObjectByType<EnemySpawner>();
            Board = new GameObject("Board (宝箱マーカー基準)").transform;

            if (!bgm && !string.IsNullOrEmpty(bgmResourcePath)) bgm = Resources.Load<AudioClip>(bgmResourcePath);
            if (bgm)
            {
                bgmSource = gameObject.AddComponent<AudioSource>();
                bgmSource.clip = bgm;
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
                bgmSource.spatialBlend = 0f;   // 2D（どこにいても同じ音量）
                bgmSource.priority = 0;         // 最優先：効果音が多くても BGM は間引かれない
                bgmSource.volume = bgmVolume;
            }
            else Debug.LogWarning("[ARTD] BGM が見つかりません: Resources/" + bgmResourcePath);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            if (!worldAnchor) FindAnchor();
            UpdateTrackingPause();
            UpdateBgmFade();

            var kb = Keyboard.current;
            if (kb != null)
            {
                if (startKey != Key.None && kb[startKey].wasPressedThisFrame) StartGame();
                if (restartKey != Key.None && kb[restartKey].wasPressedThisFrame &&
                    (State == GameState.GameOver || State == GameState.Victory)) Restart();
            }

            if (State == GameState.Setup && AutoStartRemaining >= 0f)
            {
                AutoStartRemaining -= Time.deltaTime;
                if (AutoStartRemaining <= 0f) { AutoStartRemaining = -1f; StartGame(); }
            }

            if (State == GameState.Playing && enemySpawner && enemySpawner.IsFinished && enemySpawner.AliveCount == 0)
                Victory();
        }

        public void RegisterTower(TowerCore tower)
        {
            if (Tower == tower) return;
            Tower = tower;
            lastGroundPoint = tower.transform.position;
            lastGroundUp = tower.transform.up;
            tower.Health.Died += OnTowerDied;
            onTowerPlaced.Invoke();
            if (autoStartWhenTowerPlaced && State == GameState.Setup) AutoStartRemaining = autoStartDelay;
        }

        public bool CanStart => State == GameState.Setup && HasTower;

        public void StartGame()
        {
            if (!CanStart)
            {
                if (State == GameState.Setup && !HasTower)
                    Debug.Log("[ARTD] 先に筆箱（タワー）を認識させてシャッターで固定してください");
                return;
            }
            AutoStartRemaining = -1f;
            SetState(GameState.Playing);
            if (enemySpawner) enemySpawner.Begin(this);
            onGameStarted.Invoke();
        }

        void OnTowerDied(Health h)
        {
            if (Tower)
            {
                lastGroundPoint = Tower.transform.position;
                lastGroundUp = Tower.transform.up;
            }
            GameOver();
        }

        public void GameOver()
        {
            if (State == GameState.GameOver || State == GameState.Victory) return;
            if (enemySpawner) enemySpawner.Stop();
            SetState(GameState.GameOver);
            onGameOver.Invoke();
        }

        public void Victory()
        {
            if (State != GameState.Playing) return;
            SetState(GameState.Victory);
            onVictory.Invoke();
        }

        public void AddScore(int value) => Score += value;

        [Header("コスト")]
        [Tooltip("配置に使える所持コスト")]
        [SerializeField] int initialBudget = 150;
        int spent;
        public int StartingBudget => initialBudget;
        public int Budget => initialBudget - spent;

        /// <summary>コストを払えれば払って true。</summary>
        public bool TrySpend(int cost)
        {
            if (cost <= 0) return true;
            if (Budget < cost) return false;
            spent += cost;
            return true;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) { SceneManager.LoadScene(scene.buildIndex); return; }
#if UNITY_EDITOR
            // Build Settings に入っていないシーン（テストシーン）でもエディタ上ではリトライできるように
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(scene.name);
#endif
        }

        void SetState(GameState s)
        {
            State = s;
            if (bgmSource)
            {
                if (s == GameState.Playing)
                {
                    bgmFadeTimer = -1f;
                    bgmSource.volume = bgmVolume;
                    if (!bgmSource.isPlaying) bgmSource.Play();
                }
                else if (s == GameState.GameOver || s == GameState.Victory)
                {
                    bgmFadeTimer = bgmFadeOutTime;   // 終了したらフェードアウトして止める
                }
            }
            StateChanged?.Invoke(s);
        }

        void UpdateBgmFade()
        {
            if (!bgmSource || bgmFadeTimer < 0f) return;
            bgmFadeTimer -= Time.unscaledDeltaTime;
            if (bgmFadeTimer <= 0f || bgmFadeOutTime <= 0f)
            {
                bgmSource.Stop();
                bgmSource.volume = bgmVolume;
                bgmFadeTimer = -1f;
            }
            else bgmSource.volume = bgmVolume * (bgmFadeTimer / bgmFadeOutTime);
        }

        /// <summary>TowerCore（宝箱）を子に持つマーカーを基準マーカーにする。</summary>
        void FindAnchor()
        {
            foreach (var m in MarkerPlacer.All)
            {
                if (m && m.GetComponentInChildren<TowerCore>(true))
                {
                    worldAnchor = m.GetComponent<ObserverBehaviour>();
                    if (worldAnchor) Debug.Log($"[ARTD] 基準マーカー（盤の中心）: {worldAnchor.name}");
                    return;
                }
            }
        }

        public bool IsAnchorTracked
        {
            get
            {
                if (!worldAnchor) return false;
                var s = worldAnchor.TargetStatus.Status;
                return s == Status.TRACKED || s == Status.EXTENDED_TRACKED;
            }
        }

        void LateUpdate() => UpdateBoard();

        /// <summary>盤を基準マーカーの位置・向きに追従させる。小さな揺れは無視し、動いたときはなめらかに追う。</summary>
        void UpdateBoard()
        {
            if (!Board || !worldAnchor || !IsAnchorTracked) return;
            var t = worldAnchor.transform;
            if (!boardInitialized)
            {
                Board.SetPositionAndRotation(t.position, t.rotation);
                Board.localScale = t.lossyScale;
                boardInitialized = true;
                return;
            }
            float d = Vector3.Distance(Board.position, t.position);
            float a = Quaternion.Angle(Board.rotation, t.rotation);
            if (d >= boardSnapDistance)
            {
                Board.SetPositionAndRotation(t.position, t.rotation);
            }
            else if (d > boardDeadZone || a > boardDeadZoneAngle)
            {
                float k = 1f - Mathf.Exp(-boardFollowSpeed * Time.unscaledDeltaTime);
                Board.SetPositionAndRotation(Vector3.Lerp(Board.position, t.position, k),
                                             Quaternion.Slerp(Board.rotation, t.rotation, k));
            }
        }

        /// <summary>ワールド座標 → 盤のローカル座標（キャラの持ち場などを盤基準で覚えるため）。</summary>
        public Vector3 ToBoard(Vector3 world) => Board ? Board.InverseTransformPoint(world) : world;
        /// <summary>盤のローカル座標 → ワールド座標。</summary>
        public Vector3 FromBoard(Vector3 local) => Board ? Board.TransformPoint(local) : local;

        /// <summary>ゲーム中に基準マーカーを見失ったら一時停止し、再び映ったら再開する。</summary>
        void UpdateTrackingPause()
        {
            bool pause = pauseWhenAnchorLost && State == GameState.Playing && worldAnchor && !IsAnchorTracked;
            if (pause == IsTrackingPaused) return;
            IsTrackingPaused = pause;
            Time.timeScale = pause ? 0f : 1f;
        }

        /// <summary>点 p を机の面に投影する。</summary>
        public Vector3 ProjectToGround(Vector3 p)
        {
            Vector3 up = GroundUp;
            return p - up * Vector3.Dot(p - GroundPoint, up);
        }
    }
}
