using UnityEngine;

namespace ARTD
{
    /// <summary>兵士・敵キャラのステータス。</summary>
    [CreateAssetMenu(menuName = "AR Tower Defense/Stats/Character (Soldier, Enemy)", fileName = "CharacterStats")]
    public class CharacterStats : HealthStats
    {
        [Header("移動")]
        [Tooltip("移動速度（ワールド単位/秒。マーカー幅=100 が目安）")]
        public float moveSpeed = 25f;
        [Tooltip("旋回速度（度/秒）")]
        public float turnSpeed = 540f;
        [Tooltip("体の半径（攻撃距離・押し合いに使う）")]
        public float bodyRadius = 5f;

        [Header("攻撃")]
        public float attackDamage = 10f;
        [Tooltip("攻撃の間隔（秒）")]
        public float attackInterval = 1f;
        [Tooltip("攻撃が届く距離（体の表面から相手の表面まで）")]
        public float attackRange = 8f;
        [Tooltip("敵を見つける距離")]
        public float detectRange = 80f;

        [Header("その他")]
        [Tooltip("壁などの障害物を検出するレイの高さ（足元からの高さ）")]
        public float obstacleCheckHeight = 5f;
        [Tooltip("撃破時にプレイヤーへ入るスコア（敵のみ）")]
        public int scoreValue = 10;
    }
}
