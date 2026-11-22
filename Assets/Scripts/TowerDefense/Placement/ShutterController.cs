using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace ARTD
{
    /// <summary>
    /// シャッター操作。Enter キー / UI ボタン(Shutter() を呼ぶ)で、
    /// 今認識されている全マーカーのオブジェクトを固定する。
    /// </summary>
    public class ShutterController : MonoBehaviour
    {
        [Serializable] public class ShutterEvent : UnityEvent<int> { }

        public static ShutterController Instance { get; private set; }

        [SerializeField] Key shutterKey = Key.Enter;
        [SerializeField] Key alternateKey = Key.NumpadEnter;
        [Tooltip("ゲームオーバー/勝利後はシャッターを無効にする")]
        [SerializeField] bool disableAfterGameEnd = true;
        [Tooltip("配置フェーズ（スタート前）だけシャッターを使えるようにする")]
        [SerializeField] bool onlyDuringSetup = true;
        [Tooltip("シャッター音など")]
        [SerializeField] AudioSource shutterSound;

        [Tooltip("シャッターが押された（引数 = 固定できた数）")]
        public ShutterEvent onShutter = new ShutterEvent();

        public event Action<int> Shuttered;

        void Awake()
        {
            if (Instance && Instance != this) { Debug.LogWarning("[ARTD] ShutterController が複数あります", this); }
            Instance = this;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if ((shutterKey != Key.None && kb[shutterKey].wasPressedThisFrame) ||
                (alternateKey != Key.None && kb[alternateKey].wasPressedThisFrame))
                Shutter();
        }

        /// <summary>UI ボタンからも呼ぶ。</summary>
        public void Shutter()
        {
            var gm = GameManager.Instance;
            if (disableAfterGameEnd && gm && (gm.State == GameState.GameOver || gm.State == GameState.Victory))
                return;
            if (onlyDuringSetup && gm && gm.State != GameState.Setup)
                return;

            int placed = 0;
            // タワーを先に固定（他のオブジェクトがタワーの面に揃うように）
            var markers = MarkerPlacer.All.ToArray();
            Array.Sort(markers, (a, b) => IsTowerMarker(b).CompareTo(IsTowerMarker(a)));
            // 宝箱 → 本 → その他 の順に固定（本の上に置いた物が本の高さに載るように）
            foreach (var m in markers)
                if (m && IsTowerMarker(m) && m.TryPlace()) placed++;
            foreach (var b in BookTerrain.All.ToArray())
                if (b && b.TryFix()) placed++;
            foreach (var m in markers)
                if (m && !IsTowerMarker(m) && m.TryPlace()) placed++;

            if (shutterSound) shutterSound.Play();
            onShutter.Invoke(placed);
            Shuttered?.Invoke(placed);
        }

        static bool IsTowerMarker(MarkerPlacer m)
            => m && m.CurrentPreview && m.CurrentPreview.GetComponent<TowerCore>();
    }
}
