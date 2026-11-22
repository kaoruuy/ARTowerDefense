using UnityEngine;

namespace ARTD
{
    /// <summary>消しゴム（マンホール）から兵士を出すスポナーのステータス。</summary>
    [CreateAssetMenu(menuName = "AR Tower Defense/Stats/Soldier Spawner (Eraser)", fileName = "SoldierSpawnerStats")]
    public class SoldierSpawnerStats : HealthStats
    {
        [Tooltip("ゲーム開始から最初に出てくるまでの秒数")]
        public float firstSpawnDelay = 1f;
        [Tooltip("兵士を出す間隔（秒）")]
        public float spawnInterval = 6f;
        [Tooltip("同時に存在できる兵士の最大数")]
        public int maxAlive = 4;
        [Tooltip("兵士が守る範囲（この半径内の敵を迎撃する）")]
        public float guardRadius = 150f;
        [Tooltip("敵がいないとき、兵士がトラップドアの周りを徘徊する半径")]
        public float wanderRadius = 60f;
        [Tooltip("トラップドアからせり上がる時間（秒）")]
        public float emergeDuration = 0.6f;
    }
}
