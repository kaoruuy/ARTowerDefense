using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace ARTD
{
    /// <summary>
    /// Vuforia の ImageTarget（マーカー）に付ける。
    /// 子のプレビュー(Placeable)をマーカー認識中だけ表示し、シャッター時に固定させる。
    /// </summary>
    [DisallowMultipleComponent]
    public class MarkerPlacer : MonoBehaviour
    {
        public static readonly List<MarkerPlacer> All = new List<MarkerPlacer>();

        [Tooltip("固定するオブジェクトのプレハブ。プレビューが無いときにこれから生成する")]
        [SerializeField] Placeable placeablePrefab;
        [Tooltip("今マーカー上に出ているプレビュー（シーン上の子オブジェクト）")]
        [SerializeField] Placeable currentPreview;

        [Tooltip("このマーカーから固定できる回数（0 = 無制限。マーカーを動かしてシャッターを押すたびに増やせる）")]
        [SerializeField, Min(0)] int maxPlacements = 1;

        [Tooltip("EXTENDED_TRACKED（画面外だが位置推定中）も「表示中」とみなす")]
        [SerializeField] bool acceptExtendedTracked = false;
        [Tooltip("マーカーが見えていない間はプレビューを非表示にする")]
        [SerializeField] bool hidePreviewWhenNotTracked = true;
        [Tooltip("デバッグ用：カメラが無くても常に認識中とみなす")]
        [SerializeField] bool debugAlwaysTracked = false;
        [Tooltip("宝箱マーカーを基準に位置と大きさを補正する（印刷サイズが違うマーカーでも、宝箱と同じ机の上・同じ縮尺に置く）")]
        [SerializeField] bool correctToTowerPlane = true;

        ObserverBehaviour observer;
        int placedCount;

        public int PlacedCount => placedCount;
        public bool CanPlaceMore => maxPlacements == 0 || placedCount < maxPlacements;
        public Placeable CurrentPreview => currentPreview;

        public bool IsTracked
        {
            get
            {
                if (debugAlwaysTracked) return true;
                if (!observer) return false;
                var s = observer.TargetStatus.Status;
                return s == Status.TRACKED || (acceptExtendedTracked && s == Status.EXTENDED_TRACKED);
            }
        }

        void Awake()
        {
            observer = GetComponent<ObserverBehaviour>();
        }

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            Application.onBeforeRender += CorrectPreview;
        }

        void OnDisable()
        {
            All.Remove(this);
            Application.onBeforeRender -= CorrectPreview;
        }

        // プレビューの「設計上の」ローカル姿勢（マーカーに対する置き方）
        Vector3 designLocalPos;
        Quaternion designLocalRot = Quaternion.identity;

        void Start()
        {
            if (!currentPreview) currentPreview = GetComponentInChildren<Placeable>(true);
            if (!currentPreview) SpawnPreview();
            else
            {
                designLocalPos = currentPreview.transform.localPosition;
                designLocalRot = currentPreview.transform.localRotation;
            }
        }

        void LateUpdate() => CorrectPreview();

        /// <summary>
        /// マーカーの印刷サイズが宝箱マーカーと違うと、Vuforia が推定する距離がずれる
        /// （小さいマーカーは遠くに・大きいマーカーは近くにあると誤認され、物が小さく/大きく、浮いて/沈んで見える）。
        /// カメラ → マーカーの視線と、宝箱の机の面との交点が本当の位置なので、そこへプレビューを置き直す。
        /// 大きさはワールドで固定（宝箱と同じ縮尺）なので、正しい距離に置けば見た目の大きさも揃う。
        /// </summary>
        void CorrectPreview()
        {
            if (!currentPreview || !currentPreview.gameObject.activeInHierarchy) return;
            var t = currentPreview.transform;
            var gm = GameManager.Instance;
            var cam = Camera.main;
            bool isTower = currentPreview.GetComponent<TowerCore>();
            if (!correctToTowerPlane || isTower || !gm || !gm.HasTower || !cam)
            {
                t.SetLocalPositionAndRotation(designLocalPos, designLocalRot);
                return;
            }
            if (TryProjectToTowerPlane(transform, gm, cam.transform.position, out Vector3 origin, out Quaternion flatRot))
                t.SetPositionAndRotation(origin + flatRot * designLocalPos, flatRot * designLocalRot);
            else
                t.SetLocalPositionAndRotation(designLocalPos, designLocalRot);
        }

        /// <summary>マーカー原点を、カメラからの視線と宝箱の面の交点に補正する。向きは面に沿わせる。</summary>
        public static bool TryProjectToTowerPlane(Transform marker, GameManager gm, Vector3 camPos, out Vector3 origin, out Quaternion flatRot)
        {
            origin = marker.position;
            flatRot = marker.rotation;
            Vector3 up = gm.GroundUp;
            Vector3 d = marker.position - camPos;
            float denom = Vector3.Dot(d, up);
            if (Mathf.Abs(denom) < 1e-4f) return false;
            float k = Vector3.Dot(gm.GroundPoint - camPos, up) / denom;   // = 実際の印刷サイズ / 登録サイズ
            if (k < 0.1f || k > 10f) return false;
            origin = camPos + d * k;
            // 本の上に置いたマーカー：机ではなく本の上面との交点にする
            float bookH = BookTerrain.HeightUnder(origin);
            if (bookH > 0f)
            {
                float k2 = Vector3.Dot(gm.GroundPoint + up * bookH - camPos, up) / denom;
                if (k2 > 0.1f && k2 < 10f && BookTerrain.HeightUnder(camPos + d * k2) > 0f) origin = camPos + d * k2;
            }
            Vector3 fwd = Vector3.ProjectOnPlane(marker.forward, up);
            if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(marker.right, up);
            flatRot = Quaternion.LookRotation(fwd.normalized, up);
            return true;
        }

        void Update()
        {
            if (hidePreviewWhenNotTracked && currentPreview)
            {
                bool show = IsTracked;
                if (currentPreview.gameObject.activeSelf != show) currentPreview.gameObject.SetActive(show);
            }
        }

        void SpawnPreview()
        {
            if (!placeablePrefab || !CanPlaceMore) return;
            currentPreview = Instantiate(placeablePrefab, transform);
            currentPreview.transform.SetLocalPositionAndRotation(designLocalPos, designLocalRot);
            currentPreview.name = placeablePrefab.name + " (Preview)";
        }

        /// <summary>シャッター時に呼ばれる。認識中ならプレビューを今の位置に固定する。</summary>
        public bool TryPlace()
        {
            if (!IsTracked || !currentPreview || !CanPlaceMore) return false;
            // コストが足りなければ置けない
            var gm = GameManager.Instance;
            if (gm && !gm.TrySpend(currentPreview.Cost)) return false;

            CorrectPreview();   // 固定直前に宝箱基準の位置へ

            var placed = currentPreview;
            currentPreview = null;
            placed.gameObject.SetActive(true);
            placed.Place();
            placedCount++;

            if (CanPlaceMore) SpawnPreview();
            return true;
        }
    }
}
