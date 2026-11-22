using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>大砲 4 秒・範囲攻撃、大砲着弾用の大きい赤エフェクト、敵出現円 0.7 倍。</summary>
    public static partial class TowerDefenseBuilder
    {
        public static string ApplyUpdate3()
        {
            var log = new System.Text.StringBuilder();

            var cannon = AssetDatabase.LoadAssetAtPath<CannonStats>($"{DataDir}/Cannon_Stats.asset");
            if (cannon)
            {
                cannon.fireInterval = 4f;
                cannon.splashRadius = 45f;
                EditorUtility.SetDirty(cannon);
                log.AppendLine("  Cannon fireInterval = 4, splashRadius = 45");
            }

            // 大砲の着弾用：RedParticle のバリアントを 2.5 倍に
            string bigPath = P("Effects", "RedParticle_Cannon");
            var red = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/RedParticle.prefab");
            if (red && !Load(bigPath))
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(red);
                inst.name = "RedParticle_Cannon";
                inst.transform.localScale = red.transform.localScale * 2.5f;
                PrefabUtility.SaveAsPrefabAsset(inst, bigPath);
                Object.DestroyImmediate(inst);
                log.AppendLine("  + " + bigPath + " (scale x2.5)");
            }
            var big = Load(bigPath);
            if (big) EditPrefab(P("Projectiles", "CannonBall"), r => Set(r.GetComponent<Projectile>(), "impactEffectPrefab", big), log);

            var waves = AssetDatabase.LoadAssetAtPath<WaveDefinition>($"{DataDir}/Waves_Default.asset");
            if (waves)
            {
                waves.spawnRadius *= 0.7f;
                waves.spawnRadiusJitter *= 0.7f;
                EditorUtility.SetDirty(waves);
                log.AppendLine($"  spawnRadius -> {waves.spawnRadius}");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }
    }

    [InitializeOnLoad]
    static class TowerDefenseUpdate3Request
    {
        const string RequestFile = "ARTD_Update3.request";
        static double nextCheck;

        static TowerDefenseUpdate3Request()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 2.0;
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(RequestFile);
                string log;
                try { log = TowerDefenseBuilder.ApplyUpdate3(); }
                catch (Exception e) { log = "ERROR: " + e; }
                File.WriteAllText("Logs/ARTD_Update3.log", log);
                Debug.Log("[ARTD] Update3\n" + log);
            };
        }
    }
}
