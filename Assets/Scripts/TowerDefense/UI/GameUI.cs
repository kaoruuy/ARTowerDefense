using UnityEngine;
using UnityEngine.UI;

namespace ARTD
{
    /// <summary>
    /// 画面 UI：シャッターボタン（スマホ用）、スタート、リトライ、タワー HP、ウェーブ、メッセージ。
    /// ボタンの処理はコードで登録するので、Inspector では参照を差し替えるだけでよい。
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        [Header("ボタン")]
        [SerializeField] Button shutterButton;
        [SerializeField] Button startButton;
        [SerializeField] Button restartButton;

        [Header("表示")]
        [SerializeField] Text towerHpText;
        [SerializeField] Slider towerHpSlider;
        [SerializeField] Text waveText;
        [SerializeField] Text scoreText;
        [Tooltip("残りコストの表示")]
        [SerializeField] Text budgetText;
        [SerializeField] Text messageText;
        [SerializeField] Text toastText;
        [Tooltip("シャッター時に白く光る全画面 Image")]
        [SerializeField] Image flashImage;
        [SerializeField] GameObject resultPanel;
        [SerializeField] Text resultText;

        [Header("文言")]
        [SerializeField] string setupNoTowerMessage = "宝箱を配置しよう！";
        [SerializeField] string setupReadyMessage = "守備隊を配備せよ！";
        [SerializeField] string gameOverMessage = "宝箱が破壊されました";
        [SerializeField] string victoryMessage = "宝箱を守り抜きました";
        [SerializeField] string trackingPausedMessage = "宝箱のマーカーを映そう！";

        [SerializeField] float flashDuration = 0.25f;
        [SerializeField] float toastDuration = 2.5f;

        float flashTimer;
        float toastTimer;
        ShutterController shutter;
        PirateHUD pirateHUD;

        void Awake() => ApplyPirateTheme();

        /// <summary>Shared by runtime, prefab generation and the non-destructive editor upgrade.</summary>
        public void ApplyPirateTheme()
        {
            if (!towerHpText || !waveText || !scoreText || !messageText || !toastText ||
                !resultText || !shutterButton || !startButton || !restartButton || !resultPanel) return;
            if (!budgetText)
            {
                var go = new GameObject("BudgetText", typeof(RectTransform), typeof(Text));
                go.layer = 5;
                go.transform.SetParent(transform, false);
                budgetText = go.GetComponent<Text>();
                budgetText.font = towerHpText.font;
            }
            pirateHUD = PirateHUD.Install(transform, towerHpText, waveText, scoreText, budgetText,
                messageText, toastText, resultText, shutterButton, startButton, restartButton, resultPanel);
            if (flashImage) flashImage.transform.SetAsLastSibling();
        }

        void Start()
        {
            shutter = ShutterController.Instance ? ShutterController.Instance : FindFirstObjectByType<ShutterController>();
            if (shutterButton) shutterButton.onClick.AddListener(() => { if (shutter) shutter.Shutter(); });
            if (startButton) startButton.onClick.AddListener(() => { if (GameManager.Instance) GameManager.Instance.StartGame(); });
            if (restartButton) restartButton.onClick.AddListener(() => { if (GameManager.Instance) GameManager.Instance.Restart(); });
            if (shutter) shutter.Shuttered += OnShutter;
            if (flashImage) { flashImage.raycastTarget = false; SetFlashAlpha(0f); }
            if (resultPanel) resultPanel.SetActive(false);
            if (toastText) toastText.text = "";
        }

        void OnDestroy()
        {
            if (shutter) shutter.Shuttered -= OnShutter;
        }

        void OnShutter(int placed)
        {
            if (placed > 0) flashTimer = flashDuration;
            Toast(placed > 0 ? $"{placed} 個 配置しました" : "マーカーと残り兵力を確認しよう");
        }

        public void Toast(string msg)
        {
            if (!toastText) return;
            toastText.text = msg;
            toastTimer = toastDuration;
        }

        void Update()
        {
            var gm = GameManager.Instance;
            var state = gm ? gm.State : GameState.Setup;

            if (flashTimer > 0f) { flashTimer -= Time.unscaledDeltaTime; SetFlashAlpha(Mathf.Clamp01(flashTimer / Mathf.Max(.01f, flashDuration)) * 0.25f); }
            if (toastTimer > 0f) { toastTimer -= Time.unscaledDeltaTime; if (toastTimer <= 0f && toastText) toastText.text = ""; }

            if (startButton) startButton.gameObject.SetActive(state == GameState.Setup);
            if (startButton) startButton.interactable = gm && gm.CanStart;
            if (shutterButton) shutterButton.gameObject.SetActive(state == GameState.Setup);

            if (budgetText) budgetText.text = gm ? $"{gm.Budget}/{gm.StartingBudget}" : "100/100";

            // タワー HP
            if (gm && gm.Tower && gm.Tower.Health)
            {
                var h = gm.Tower.Health;
                if (towerHpText) towerHpText.text = $"{Mathf.CeilToInt(h.Current)}/{Mathf.CeilToInt(h.MaxHealth)}";
                if (towerHpSlider) towerHpSlider.value = h.Ratio;
            }
            else
            {
                if (towerHpText) towerHpText.text = state == GameState.GameOver ? "0 HP" : "未配置";
                if (towerHpSlider) towerHpSlider.value = 0f;
            }

            // ウェーブ
            if (waveText)
            {
                var sp = gm ? gm.Spawner : null;
                if (sp && state == GameState.Playing)
                {
                    string total = sp.IsEndless ? "∞" : sp.TotalWaves.ToString();
                    waveText.text = $"WAVE {Mathf.Max(1, sp.CurrentWave)}/{total}";
                }
                else waveText.text = "WAVE 1/5";
            }
            if (scoreText) scoreText.gameObject.SetActive(false);

            // メッセージ
            if (messageText)
            {
                string msg = "";
                if (state == GameState.Setup)
                {
                    msg = SetupOrder(gm);
                    if (gm && gm.AutoStartRemaining > 0f) msg = $"あと {Mathf.CeilToInt(gm.AutoStartRemaining)} 秒！防衛に備えよ！";
                }
                else if (state == GameState.Playing && gm && gm.IsTrackingPaused)
                {
                    msg = trackingPausedMessage;
                }
                else if (state == GameState.Playing)
                {
                    msg = "戦闘開始！防御せよ！";
                }
                messageText.text = msg;
            }

            bool ended = state == GameState.GameOver || state == GameState.Victory;
            if (resultPanel && resultPanel.activeSelf != ended) resultPanel.SetActive(ended);
            if (ended && resultText) resultText.text = state == GameState.GameOver ? gameOverMessage : victoryMessage;
            if (pirateHUD)
                pirateHUD.Refresh(state, gm && gm.IsTrackingPaused, gm && gm.HasTower,
                    gm && gm.Tower && gm.Tower.Health ? gm.Tower.Health.Ratio : 0f,
                    gm ? gm.Spawner : null, toastTimer);
        }

        string SetupOrder(GameManager gm)
        {
            if (!gm || !gm.HasTower) return setupNoTowerMessage;
            foreach (var marker in MarkerPlacer.All)
            {
                if (!marker || !marker.IsTracked || !marker.CanPlaceMore || !marker.CurrentPreview) continue;
                var preview = marker.CurrentPreview;
                if (preview.GetComponent<TowerCore>()) continue;
                if (preview.Cost > gm.Budget) return "兵力不足！戦闘に備えよ！";
                if (preview.GetComponent<CannonTurret>()) return "大砲を配備せよ！";
                if (preview.GetComponent<SoldierSpawner>()) return "兵士を配備せよ！";
                return "防壁を配備せよ！";
            }
            return gm.Budget == 0 ? "準備完了！戦闘を開始せよ！" : setupReadyMessage;
        }

        void SetFlashAlpha(float a)
        {
            if (!flashImage) return;
            var c = flashImage.color; c.a = a; flashImage.color = c;
            flashImage.enabled = a > 0.001f;
        }
    }
}
