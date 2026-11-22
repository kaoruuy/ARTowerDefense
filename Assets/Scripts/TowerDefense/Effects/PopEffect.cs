using UnityEngine;

namespace ARTD
{
    /// <summary>
    /// 仮エフェクト：大きくなりながら縮んで消える。
    /// 本番ではパーティクル等のプレハブに差し替える（その場合 TimedDestroy を付けるか、そのままでも Fx が自動で消す）。
    /// </summary>
    public class PopEffect : MonoBehaviour
    {
        [SerializeField] float duration = 0.35f;
        [SerializeField] float startScale = 0.3f;
        [SerializeField] float peakScale = 1f;
        [SerializeField] AnimationCurve scaleCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.4f, 1f), new Keyframe(1f, 0f));

        Vector3 baseScale;
        float t;

        void Awake()
        {
            GameplayScale.Apply(transform);
            baseScale = transform.localScale;
            transform.localScale = baseScale * startScale;
        }

        void Update()
        {
            t += Time.deltaTime;
            float n = duration > 0 ? t / duration : 1f;
            float s = Mathf.LerpUnclamped(startScale, peakScale, scaleCurve.Evaluate(Mathf.Clamp01(n)));
            transform.localScale = baseScale * Mathf.Max(0.0001f, s);
            if (n >= 1f) Destroy(gameObject);
        }
    }
}
