using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace ARTD
{
    /// <summary>
    /// 本など「机の上の段差」。マーカー（ImageTarget）に付ける。マーカーは本の上に置く前提。
    /// 子の BoxCollider（BookCollider）を、マーカーの真下にぴったり付く本の大きさにする（エディタ上でも見える）。
    /// 実行中は認識したらマーカーから切り離して盤に残し（見失っても消えない）、認識中は位置を追従する。
    /// Health を持たないので、敵・兵士のルート選びや攻撃対象には影響しない。兵士や敵は飛び乗り/飛び降りする。
    /// </summary>
    [DisallowMultipleComponent]
    public class BookTerrain : MonoBehaviour
    {
        public static readonly List<BookTerrain> All = new List<BookTerrain>();

        [Tooltip("マーカーの実際の一辺（cm）。登録幅（100）がこの長さに相当する")]
        [SerializeField] float markerSizeCm = 4f;
        [Tooltip("本の横（マーカーの X 方向, cm）")]
        [SerializeField] float widthCm = 15f;
        [Tooltip("本の縦（マーカーの Z 方向, cm）")]
        [SerializeField] float lengthCm = 22f;
        [Tooltip("本の高さ（cm）")]
        [SerializeField] float heightCm = 1f;
        [Tooltip("本の中心から見たマーカーの位置（cm, X/Z）。マーカーを本の中央に置くなら 0")]
        [SerializeField] Vector2 markerOffsetCm = Vector2.zero;
        [Tooltip("本の当たり判定（このマーカーの子）")]
        [SerializeField] BoxCollider bookCollider;
        [Tooltip("当たり判定の箱を半透明で表示する（確認用）")]
        [SerializeField] bool showDebugBox = false;
        [Tooltip("認識したら、当たり判定の輪郭を薄く表示する")]
        [SerializeField] bool showOutline = true;
        [SerializeField] Color outlineColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] float outlineWidth = 1.5f;
        LineRenderer outlineTop, outlineBottom;

        ObserverBehaviour observer;
        Transform box;
        bool detached;

        public float UnitsPerCm => 100f / Mathf.Max(0.01f, markerSizeCm);
        public float HeightWorld => heightCm * UnitsPerCm;

        /// <summary>
        /// worldPoint（宝箱など）が、認識済みの本の範囲内（上から見て）にあれば、その本の高さを返す。無ければ 0。
        /// </summary>
        public static float HeightUnder(Vector3 worldPoint)
        {
            float best = 0f;
            foreach (var b in All)
            {
                if (!b || !b.IsFixed || !b.bookCollider || !b.box.gameObject.activeInHierarchy) continue;
                Vector3 local = b.box.InverseTransformPoint(worldPoint) - b.bookCollider.center;
                Vector3 half = b.bookCollider.size * 0.5f;
                if (Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z)
                    best = Mathf.Max(best, b.HeightWorld);
            }
            return best;
        }

        void Reset() { EnsureCollider(); }
        void OnValidate() { if (bookCollider) ApplyShape(); }

        /// <summary>子の BookCollider を用意する（エディタからも呼ぶ）。</summary>
        public BoxCollider EnsureCollider()
        {
            if (!bookCollider)
            {
                var t = transform.Find("BookCollider");
                if (!t)
                {
                    t = new GameObject("BookCollider").transform;
                    t.SetParent(transform, false);
                }
                bookCollider = t.GetComponent<BoxCollider>();
                if (!bookCollider) bookCollider = t.gameObject.AddComponent<BoxCollider>();
                if (!t.GetComponent<TerrainSurface>()) t.gameObject.AddComponent<TerrainSurface>();
            }
            ApplyShape();
            return bookCollider;
        }

        void ApplyShape()
        {
            float u = UnitsPerCm;
            var t = bookCollider.transform;
            if (!detached && t.parent == transform)
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }
            bookCollider.size = new Vector3(widthCm * u, heightCm * u, lengthCm * u);
            // 上面 = マーカー面（ルート位置）。マーカーが本の中央からずれていればその分ずらす
            bookCollider.center = new Vector3(-markerOffsetCm.x * u, -heightCm * u * 0.5f, -markerOffsetCm.y * u);
        }

        void Awake()
        {
            observer = GetComponent<ObserverBehaviour>();
            EnsureCollider();
            box = bookCollider.transform;
            if (showDebugBox)
            {
                var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(vis.GetComponent<Collider>());
                vis.name = "DebugBox";
                vis.transform.SetParent(box, false);
                vis.transform.localPosition = bookCollider.center;
                vis.transform.localScale = bookCollider.size;
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() { All.Remove(this); }
        void OnDestroy() { if (detached && box) Destroy(box.gameObject); }

        bool IsTracked
        {
            get
            {
                if (!observer) return false;
                var s = observer.TargetStatus.Status;
                return s == Status.TRACKED || s == Status.EXTENDED_TRACKED;
            }
        }

        void LateUpdate()
        {
            if (!bookCollider || IsFixed) return;   // 固定後は動かさない（マーカーを外してもよい）
            if (!IsTracked)
            {
                SetOutlineVisible(false);   // 未固定で見えていない間はプレビューを隠す
                return;
            }
            UpdatePose();
            bookCollider.enabled = false;   // 固定するまでは地形として使わない
        }

        /// <summary>シャッター（Enter / ボタン）で呼ばれる。認識中なら今の位置で固定する。</summary>
        public bool TryFix()
        {
            if (IsFixed || !bookCollider || !IsTracked) return false;
            UpdatePose();
            IsFixed = true;
            bookCollider.enabled = true;
            UpdateOutline();
            return true;
        }

        public bool IsFixed { get; private set; }

        void SetOutlineVisible(bool v)
        {
            if (outlineTop) outlineTop.enabled = v;
            if (outlineBottom) outlineBottom.enabled = v;
        }

        void UpdatePose()
        {

            var gm = GameManager.Instance;
            Vector3 top = transform.position;
            Quaternion rot = transform.rotation;

            if (gm && gm.HasTower)
            {
                // 宝箱の机の面を基準に：マーカー面 = 机から本の高さだけ上の面。視線との交点を本当の位置とする
                Vector3 up = gm.GroundUp;
                Vector3 planePoint = gm.GroundPoint + up * (heightCm * UnitsPerCm);
                var cam = Camera.main;
                if (cam)
                {
                    Vector3 c = cam.transform.position;
                    Vector3 d = transform.position - c;
                    float denom = Vector3.Dot(d, up);
                    if (Mathf.Abs(denom) > 1e-4f)
                    {
                        float k = Vector3.Dot(planePoint - c, up) / denom;
                        if (k > 0.1f && k < 10f) top = c + d * k;
                    }
                }
                top -= up * Vector3.Dot(top - planePoint, up);
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up);
                if (fwd.sqrMagnitude < 1e-6f) fwd = Vector3.ProjectOnPlane(transform.right, up);
                rot = Quaternion.LookRotation(fwd.normalized, up);
            }

            // マーカーの子のままだと、見失ったとき Vuforia に当たり判定を切られるので切り離す
            if (!detached)
            {
                box.SetParent(gm && gm.Board ? gm.Board : null, true);
                detached = true;
            }
            else if (gm && gm.Board && box.parent != gm.Board) box.SetParent(gm.Board, true);

            box.SetPositionAndRotation(top, rot);
            Vector3 ls = box.lossyScale, pl = box.localScale;
            box.localScale = new Vector3(pl.x / Mathf.Max(1e-4f, ls.x), pl.y / Mathf.Max(1e-4f, ls.y), pl.z / Mathf.Max(1e-4f, ls.z));
            ApplyShape();
            if (!box.gameObject.activeSelf) box.gameObject.SetActive(true);
            UpdateOutline();
            SetOutlineVisible(true);
        }

        static Material outlineMat;

        LineRenderer MakeOutline(string n)
        {
            var go = new GameObject(n);
            go.transform.SetParent(box, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            if (!outlineMat) outlineMat = new Material(Shader.Find("Sprites/Default")) { name = "BookOutline (runtime)" };
            lr.sharedMaterial = outlineMat;
            return lr;
        }

        /// <summary>本の上面と底面の四角を薄い線で描く（認識できていることがわかるように）。</summary>
        void UpdateOutline()
        {
            if (!showOutline) return;
            if (!outlineTop) outlineTop = MakeOutline("BookOutlineTop");
            if (!outlineBottom) outlineBottom = MakeOutline("BookOutlineBottom");
            Vector3 c = bookCollider.center, h = bookCollider.size * 0.5f;
            foreach (var (lr, y) in new[] { (outlineTop, c.y + h.y + 0.3f), (outlineBottom, c.y - h.y + 0.3f) })
            {
                lr.SetPosition(0, new Vector3(c.x - h.x, y, c.z - h.z));
                lr.SetPosition(1, new Vector3(c.x + h.x, y, c.z - h.z));
                lr.SetPosition(2, new Vector3(c.x + h.x, y, c.z + h.z));
                lr.SetPosition(3, new Vector3(c.x - h.x, y, c.z + h.z));
                lr.widthMultiplier = outlineWidth;
                // 固定前（プレビュー）はさらに薄く
                Color col = IsFixed ? outlineColor : new Color(outlineColor.r, outlineColor.g, outlineColor.b, outlineColor.a * 0.5f);
                lr.startColor = lr.endColor = col;
            }
        }

        void OnDrawGizmos()
        {
            if (!bookCollider) return;
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
            Gizmos.matrix = bookCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(bookCollider.center, bookCollider.size);
        }
    }

    /// <summary>キャラが乗れる地形の目印（本など）。</summary>
    public class TerrainSurface : MonoBehaviour { }
}
