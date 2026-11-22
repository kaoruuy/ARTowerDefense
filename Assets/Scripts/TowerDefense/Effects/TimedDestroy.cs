using UnityEngine;

namespace ARTD
{
    /// <summary>一定時間後に自分を消す。パーティクル等のエフェクトプレハブに付ける。</summary>
    public class TimedDestroy : MonoBehaviour
    {
        [SerializeField] float lifetime = 2f;

        void Start() => Destroy(gameObject, lifetime);
    }
}
