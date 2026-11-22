using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>
    /// 大砲の発射 = CanonParticle、着弾 = BakuhatuParticle、走りの足元 = DashParticle、
    /// ドクロ敵（Characters_Skeleton）の追加。
    /// </summary>
    public static partial class TowerDefenseBuilder
    {
        public static string ApplyUpdate4()
        {
            var log = new System.Text.StringBuilder();
            var canon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/CanonParticle.prefab");
            var bakuhatu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/BakuhatuParticle.prefab");
            var dashGo = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Effects/DashParticle.prefab");
            var dash = dashGo ? dashGo.GetComponentInChildren<ParticleSystem>(true) : null;
            var skeletonFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/PirateKit/Characters_Skeleton.fbx");

            EditPrefab(P("Placeables", "Pen_Cannon"), r => Set(r.GetComponent<CannonTurret>(), "muzzleFlashPrefab", canon), log);
            EditPrefab(P("Projectiles", "CannonBall"), r => Set(r.GetComponent<Projectile>(), "impactEffectPrefab", bakuhatu), log);
            foreach (var n in new[] { "Soldier", "Enemy_Basic", "Enemy_Heavy" })
                EditPrefab(P("Units", n), r => Set(r.GetComponent<CharacterUnit>(), "dashParticlePrefab", dash), log);

            // ---- ドクロ敵：Sharky（Enemy_Basic）より HP 低め・攻撃力高め
            var basicStats = AssetDatabase.LoadAssetAtPath<CharacterStats>($"{DataDir}/Enemy_Basic_Stats.asset");
            string statsPath = $"{DataDir}/Enemy_Skeleton_Stats.asset";
            var skStats = AssetDatabase.LoadAssetAtPath<CharacterStats>(statsPath);
            if (!skStats && basicStats)
            {
                skStats = Object.Instantiate(basicStats);
                skStats.maxHealth = Mathf.Round(basicStats.maxHealth * 0.8f);
                skStats.attackDamage = basicStats.attackDamage * 2.5f;
                skStats.scoreValue = basicStats.scoreValue + 5;
                AssetDatabase.CreateAsset(skStats, statsPath);
                log.AppendLine($"  + {statsPath} (HP {skStats.maxHealth}, 攻撃 {skStats.attackDamage})");
            }

            string skPath = P("Units", "Enemy_Skeleton");
            var basicPrefab = Load(P("Units", "Enemy_Basic"));
            // 前回失敗時にシーンへ残った作業用オブジェクトを片付ける
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in active.GetRootGameObjects())
                if (go.name == "Enemy_Skeleton" && !PrefabUtility.IsPartOfPrefabInstance(go)) { Object.DestroyImmediate(go); log.AppendLine("  - removed leftover Enemy_Skeleton from scene"); }

            if (!Load(skPath) && basicPrefab && skeletonFbx)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(basicPrefab);
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                inst.name = "Enemy_Skeleton";

                // Model の下の Sharky（Animator 付き）をドクロに差し替える
                var oldAnim = inst.transform.Find("Model") ? inst.transform.Find("Model").GetComponentInChildren<Animator>(true) : null;
                Transform oldModel = oldAnim ? oldAnim.transform : null;
                var parent = oldModel ? oldModel.parent : inst.transform;
                var sk = (GameObject)PrefabUtility.InstantiatePrefab(skeletonFbx);
                sk.name = "Skeleton";
                sk.transform.SetParent(parent, false);
                if (oldModel)
                {
                    sk.transform.localPosition = oldModel.localPosition;
                    sk.transform.localRotation = oldModel.localRotation;
                    sk.transform.localScale = oldModel.localScale;
                }
                var anim = sk.GetComponent<Animator>(); if (!anim) anim = sk.AddComponent<Animator>();
                if (oldAnim) anim.runtimeAnimatorController = oldAnim.runtimeAnimatorController;
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath("Assets/Art/PirateKit/Characters_Skeleton.fbx"))
                    if (a is Avatar av) { anim.avatar = av; break; }
                if (oldModel) Object.DestroyImmediate(oldModel.gameObject);

                var unit = inst.GetComponent<CharacterUnit>();
                Set(unit, "stats", skStats);
                Set(unit, "animator", anim);
                PrefabUtility.SaveAsPrefabAsset(inst, skPath);
                Object.DestroyImmediate(inst);
                log.AppendLine("  + " + skPath);
            }

            // ---- ウェーブに追加（2〜5 ウェーブ）
            var waves = AssetDatabase.LoadAssetAtPath<WaveDefinition>($"{DataDir}/Waves_Default.asset");
            var skUnit = Load(skPath) ? Load(skPath).GetComponent<CharacterUnit>() : null;
            if (waves && skUnit)
            {
                int[] counts = { 0, 3, 4, 5, 6 };
                for (int i = 0; i < waves.waves.Count && i < counts.Length; i++)
                {
                    if (counts[i] == 0 || waves.waves[i].groups.Exists(g => g.enemyPrefab == skUnit)) continue;
                    waves.waves[i].groups.Add(new WaveDefinition.SpawnGroup { enemyPrefab = skUnit, count = counts[i], interval = 1.5f });
                }
                EditorUtility.SetDirty(waves);
                log.AppendLine("  Waves: Skeleton x3/4/5/6 in wave 2-5");
            }
            AssetDatabase.SaveAssets();
            return log.ToString();
        }
    }

    [InitializeOnLoad]
    static class TowerDefenseUpdate4Request
    {
        const string RequestFile = "ARTD_Update4.request";
        static double nextCheck;

        static TowerDefenseUpdate4Request()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 2.0;
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(RequestFile);
                string log;
                try { log = TowerDefenseBuilder.ApplyUpdate4(); }
                catch (Exception e) { log = "ERROR: " + e; }
                File.WriteAllText("Logs/ARTD_Update4.log", log);
                Debug.Log("[ARTD] Update4\n" + log);
            };
        }
    }
}
