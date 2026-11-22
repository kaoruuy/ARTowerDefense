using UnityEngine;

namespace ARTD
{
    /// <summary>ボールペン大砲のステータス。</summary>
    [CreateAssetMenu(menuName = "AR Tower Defense/Stats/Cannon (Pen)", fileName = "CannonStats")]
    public class CannonStats : HealthStats
    {
        [Tooltip("射程")]
        public float range = 250f;
        [Tooltip("発射間隔（秒）")]
        public float fireInterval = 1.2f;
        [Tooltip("弾1発のダメージ")]
        public float damage = 35f;
        [Tooltip("弾の速さ")]
        public float projectileSpeed = 300f;
        [Tooltip("爆発の範囲ダメージ半径（0で単体）")]
        public float splashRadius = 15f;
        [Tooltip("砲塔の旋回速度（度/秒）")]
        public float turnSpeed = 240f;
        [Tooltip("この角度以内を向いたら撃つ")]
        public float aimTolerance = 10f;
    }
}
