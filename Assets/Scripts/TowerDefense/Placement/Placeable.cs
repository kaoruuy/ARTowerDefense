using UnityEngine;
using UnityEngine.Events;

namespace ARTD
{
    /// <summary>
    /// マーカーに乗せて表示し、シャッターで「固定」できるオブジェクトのルートに付ける。
    /// 固定前（プレビュー中）はゲームロジックが動かず、敵の標的にもならない。
    /// 固定するとマーカーから切り離されてワールドに残る。
    /// </summary>
    [DisallowMultipleComponent]
    public class Placeable : MonoBehaviour
    {
        [Tooltip("マーカー(MarkerPlacer)の子でない状態でシーンに置かれていたら、開始時に自動で固定扱いにする（マーカー無しのテスト用）")]
        [SerializeField] bool placeOnStartIfNoMarker = true;

        [Tooltip("固定時にタワーの面（机の面）に合わせて傾きと高さを揃える。タワー自身は OFF")]
        [SerializeField] bool alignToTowerPlane = true;

        [Tooltip("固定時に出すエフェクト")]
        [SerializeField] GameObject placeEffectPrefab;

        [Tooltip("配置コスト（GameManager の所持コストから引かれる。0 = 無料）")]
        [SerializeField, Min(0)] int cost = 0;
        public int Cost => cost;

        [Header("浮いて見えないように")]
        [Tooltip("見た目（Model）を少し机に沈める量（ワールド単位、マーカー幅=100）")]
        [SerializeField] float modelSink = 1.5f;
        [Tooltip("足元に丸い影を付ける")]
        [SerializeField] bool groundShadow = true;
        [SerializeField, Range(0f, 1f)] float shadowOpacity = 0.45f;

        public UnityEvent onPlaced = new UnityEvent();

        public bool IsPlaced { get; private set; }

        void Awake()
        {
            GameplayScale.Apply(transform);
            var model = transform.Find("Model");
            if (model && modelSink != 0f)
                model.localPosition -= Vector3.up * (modelSink * GameplayScale.Factor / Mathf.Max(1e-4f, transform.lossyScale.y));
        }

        void Start()
        {
            if (groundShadow && !GetComponentInChildren<GroundShadow>(true))
                GroundShadow.Create(transform, GroundShadow.FootprintDiameter(transform, 30f) * 1.1f, shadowOpacity);

            if (!IsPlaced && placeOnStartIfNoMarker && GetComponentInParent<MarkerPlacer>() == null)
                Place();
        }

        /// <summary>現在のワールド座標で固定し、親（マーカー）から切り離して盤（宝箱マーカー基準）の子にする。</summary>
        public void Place()
        {
            if (IsPlaced) return;

            var gm = GameManager.Instance;
            transform.SetParent(gm ? gm.Board : null, true);

            if (alignToTowerPlane && gm && gm.HasTower)
            {
                Vector3 up = gm.GroundUp;
                Vector3 pos = transform.position;
                pos -= up * Vector3.Dot(pos - gm.GroundPoint, up);
                pos += up * BookTerrain.HeightUnder(pos);   // 本の上なら本の上面に載せる
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(transform.right, up);
                transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd.normalized, up));
            }

            // Vuforia の DefaultObserverEventHandler が切っていた描画/当たり判定を戻す
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
            foreach (var cv in GetComponentsInChildren<Canvas>(true)) cv.enabled = true;

            gameObject.SetActive(true);
            IsPlaced = true;

            if (placeEffectPrefab) Fx.Spawn(placeEffectPrefab, transform.position, transform.rotation);
            onPlaced.Invoke();
        }
    }
}
