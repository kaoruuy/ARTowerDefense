using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARTD
{
    /// <summary>敵ウェーブの定義。</summary>
    [CreateAssetMenu(menuName = "AR Tower Defense/Wave Definition", fileName = "WaveDefinition")]
    public class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public class SpawnGroup
        {
            [Tooltip("湧かせる敵のプレハブ（CharacterUnit 付き）")]
            public CharacterUnit enemyPrefab;
            [Min(0)] public int count = 5;
            [Tooltip("1体ごとの間隔（秒）")]
            public float interval = 1.5f;
        }

        [Serializable]
        public class Wave
        {
            public string name = "Wave";
            [Tooltip("このウェーブが始まるまでの待ち時間（秒）")]
            public float delayBefore = 3f;
            [Tooltip("グループは上から順に湧く")]
            public List<SpawnGroup> groups = new List<SpawnGroup>();
        }

        [Tooltip("タワーからこの距離の円周上に敵が湧く")]
        public float spawnRadius = 350f;
        [Tooltip("湧く位置の半径のばらつき")]
        public float spawnRadiusJitter = 50f;

        public List<Wave> waves = new List<Wave>();

        [Header("エンドレス")]
        [Tooltip("ON: 最終ウェーブ後も最後のウェーブを強化しながら繰り返す / OFF: 全滅させたら勝利")]
        public bool endless = false;
        [Tooltip("ループごとに敵の数をこの倍率で増やす")]
        public float loopCountMultiplier = 1.3f;
        [Tooltip("ループごとに敵のHPをこの倍率で増やす")]
        public float loopHealthMultiplier = 1.2f;
    }
}
