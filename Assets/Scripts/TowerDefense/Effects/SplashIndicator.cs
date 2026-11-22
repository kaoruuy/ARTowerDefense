using UnityEngine;

namespace ARTD
{
    /// <summary>範囲攻撃の範囲を、着弾地点の地面に円で一瞬表示する。</summary>
    public class SplashIndicator : MonoBehaviour
    {
        float duration;
        float age;
        float radius;
        RangeRing ring;

        public static void Show(Vector3 point, float radius, Color color, float width, float duration)
        {
            if (radius <= 0f) return;
            var gm = GameManager.Instance;
            Vector3 up = gm ? gm.GroundUp : Vector3.up;
            if (gm && gm.HasTower) point = gm.ProjectToGround(point);
            var go = new GameObject("SplashIndicator");
            go.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, up));
            var s = go.AddComponent<SplashIndicator>();
            s.radius = radius;
            s.duration = Mathf.Max(0.05f, duration);
            s.ring = RangeRing.Create(go.transform, radius * 0.3f, color, width, "Ring");
        }

        void Update()
        {
            age += Time.deltaTime;
            float n = age / duration;
            // 素早く広がって、最大の大きさ（= 範囲）で少し止まってから消える
            if (ring) ring.SetRadius(radius * Mathf.Lerp(0.3f, 1f, Mathf.Clamp01(n * 4f)));
            if (n >= 1f) Destroy(gameObject);
        }
    }
}
