using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>
    /// ヒットエフェクト(Red/BlueParticle)・効果音・コスト・大砲間隔・敵HP・巨大敵・ブルームを一括で適用する。
    /// メニュー: Tools > AR Tower Defense > 6. Apply Effects/Sounds/Cost Update
    /// </summary>
    public static partial class TowerDefenseBuilder
    {
        [MenuItem("Tools/AR Tower Defense/6. Apply Effects, Sounds, Cost Update (current scene)", priority = 6)]
        static void Update2Menu() => Debug.Log("[ARTD] Update2\n" + ApplyUpdate2(SceneManager.GetActiveScene()));

        static T A<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!a) Debug.LogWarning("[ARTD] not found: " + path);
            return a;
        }

        static void EditPrefab(string path, Action<GameObject> edit, System.Text.StringBuilder log)
        {
            if (!Load(path)) { log.AppendLine("  ! missing " + path); return; }
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); log.AppendLine("  * " + path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static string ApplyUpdate2(Scene s)
        {
            var log = new System.Text.StringBuilder();
            var red = A<GameObject>("Assets/Effects/RedParticle.prefab");
            var blue = A<GameObject>("Assets/Effects/BlueParticle.prefab");
            var sPuyon = A<AudioClip>("Assets/Sounds/ぷよん.mp3");
            var sSword = A<AudioClip>("Assets/Sounds/剣で打ち合う2.mp3");
            var sDecide = A<AudioClip>("Assets/Sounds/決定ボタンを押す44.mp3");
            var sBoom = A<AudioClip>("Assets/Sounds/爆発1.mp3");
            var sRun = A<AudioClip>("Assets/Sounds/芝生の上を走る.mp3");
            var sCrumble = A<AudioClip>("Assets/Sounds/建物が少し崩れる1.mp3");

            // ---- ステータス
            var cannon = A<CannonStats>($"{DataDir}/Cannon_Stats.asset");
            if (cannon) { cannon.fireInterval = 5f; EditorUtility.SetDirty(cannon); log.AppendLine("  Cannon fireInterval = 5"); }
            var basic = A<CharacterStats>($"{DataDir}/Enemy_Basic_Stats.asset");
            if (basic) { basic.maxHealth = 150f; EditorUtility.SetDirty(basic); log.AppendLine("  Enemy_Basic HP = 150"); }
            var heavy = A<CharacterStats>($"{DataDir}/Enemy_Heavy_Stats.asset");
            if (heavy) { heavy.maxHealth = 450f; EditorUtility.SetDirty(heavy); log.AppendLine("  Enemy_Heavy HP = 450"); }

            string giantStatsPath = $"{DataDir}/Enemy_Giant_Stats.asset";
            var giant = AssetDatabase.LoadAssetAtPath<CharacterStats>(giantStatsPath);
            if (!giant)
            {
                giant = heavy ? Object.Instantiate(heavy) : ScriptableObject.CreateInstance<CharacterStats>();
                giant.maxHealth = 1500f; giant.moveSpeed = 10f; giant.turnSpeed = 240f; giant.bodyRadius = 14f;
                giant.attackDamage = 40f; giant.attackInterval = 2f; giant.attackRange = 10f; giant.detectRange = 40f;
                giant.obstacleCheckHeight = 8f; giant.scoreValue = 100;
                AssetDatabase.CreateAsset(giant, giantStatsPath);
                log.AppendLine("  + " + giantStatsPath + " (HP 1500)");
            }

            // ---- ユニット：ヒットエフェクト（味方の攻撃 = Red / 敵の攻撃 = Blue）と効果音
            EditPrefab(P("Units", "Soldier"), r =>
            {
                var u = r.GetComponent<CharacterUnit>();
                Set(u, "attackEffectPrefab", red);
                Set(u, "attackHitSound", sDecide);
                Set(u, "moveLoopSound", sRun);
                Set(r.GetComponent<Health>(), "hitEffectPrefab", null);
            }, log);
            foreach (var n in new[] { "Enemy_Basic", "Enemy_Heavy" })
                EditPrefab(P("Units", n), r =>
                {
                    var u = r.GetComponent<CharacterUnit>();
                    Set(u, "attackEffectPrefab", blue);
                    Set(u, "attackHitSound", sSword);
                    Set(r.GetComponent<Health>(), "hitEffectPrefab", null);
                }, log);
            EditPrefab(P("Projectiles", "CannonBall"), r => Set(r.GetComponent<Projectile>(), "impactEffectPrefab", red), log);

            // ---- 巨大敵（Enemy_Heavy のバリアント、1.8 倍）
            string giantPath = P("Units", "Enemy_Giant");
            if (!Load(giantPath) && Load(P("Units", "Enemy_Heavy")))
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(Load(P("Units", "Enemy_Heavy")));
                inst.name = "Enemy_Giant";
                inst.transform.localScale = Vector3.one * 1.8f;
                Set(inst.GetComponent<CharacterUnit>(), "stats", giant);
                PrefabUtility.SaveAsPrefabAsset(inst, giantPath);
                Object.DestroyImmediate(inst);
                log.AppendLine("  + " + giantPath);
            }

            // ---- ウェーブに巨大敵を追加
            var waves = A<WaveDefinition>($"{DataDir}/Waves_Default.asset");
            var giantUnit = Load(giantPath) ? Load(giantPath).GetComponent<CharacterUnit>() : null;
            if (waves && giantUnit)
            {
                int[] counts = { 0, 0, 1, 2, 3 };
                for (int i = 0; i < waves.waves.Count && i < counts.Length; i++)
                {
                    if (counts[i] == 0) continue;
                    if (waves.waves[i].groups.Exists(g => g.enemyPrefab == giantUnit)) continue;
                    waves.waves[i].groups.Add(new WaveDefinition.SpawnGroup { enemyPrefab = giantUnit, count = counts[i], interval = 4f });
                }
                EditorUtility.SetDirty(waves);
                log.AppendLine("  Waves: Giant x1/x2/x3 in wave 3/4/5");
            }

            // ---- 配置物：コスト・効果音・ヒットエフェクト
            EditPrefab(P("Placeables", "Pen_Cannon"), r =>
            {
                Set(r.GetComponent<CannonTurret>(), "fireClip", sBoom);
                Set(r.GetComponent<Placeable>(), "cost", 10);
                Set(r.GetComponent<Health>(), "hitEffectPrefab", null);
            }, log);
            EditPrefab(P("Placeables", "Eraser_SoldierSpawner"), r =>
            {
                Set(r.GetComponent<SoldierSpawner>(), "spawnSound", sPuyon);
                Set(r.GetComponent<Placeable>(), "cost", 15);
                Set(r.GetComponent<Health>(), "hitEffectPrefab", null);
            }, log);
            EditPrefab(P("Placeables", "Pencil_Wall"), r =>
            {
                Set(r.GetComponent<Placeable>(), "cost", 5);
                var h = r.GetComponent<Health>();
                Set(h, "deathSound", sCrumble);
                Set(h, "hitEffectPrefab", null);
            }, log);
            EditPrefab(P("Placeables", "Pencase_Tower"), r => Set(r.GetComponent<Health>(), "hitEffectPrefab", null), log);

            // ---- マーカー：何回でも置ける（コストで制限）
            foreach (var n in new[] { "pen_image", "pencil_image", "eraser_image" })
                EditPrefab($"{PrefabDir}/Markers/{n}.prefab", r => { var mp = r.GetComponent<MarkerPlacer>(); if (mp) Set(mp, "maxPlacements", 0); }, log);

            // ---- UI：残りコスト表示
            EditPrefab(P("UI", "GameUI"), r =>
            {
                var ui = r.GetComponent<GameUI>();
                var existing = r.transform.Find("BudgetText");
                Text t;
                if (existing) t = existing.GetComponent<Text>();
                else
                {
                    var score = r.transform.Find("ScoreText");
                    var go = score ? Object.Instantiate(score.gameObject, r.transform) : new GameObject("BudgetText", typeof(RectTransform), typeof(Text));
                    go.name = "BudgetText";
                    t = go.GetComponent<Text>();
                    var rt = (RectTransform)go.transform;
                    if (score) rt.anchoredPosition = ((RectTransform)score).anchoredPosition + new Vector2(0, -44);
                    t.text = "コスト 100 / 100";
                }
                Set(ui, "budgetText", t);
            }, log);

            AssetDatabase.SaveAssets();

            // ---- シーン：上書き設定があっても揃える + ブルーム
            foreach (var root in s.GetRootGameObjects())
            {
                foreach (var mp in root.GetComponentsInChildren<MarkerPlacer>(true))
                {
                    var pl = mp.GetComponentInChildren<Placeable>(true);
                    if (pl && pl.GetComponent<TowerCore>()) continue;
                    Set(mp, "maxPlacements", 0);
                }
                foreach (var pl in root.GetComponentsInChildren<Placeable>(true))
                {
                    int cost = pl.GetComponent<CannonTurret>() ? 10 : pl.GetComponent<SoldierSpawner>() ? 15 : pl.GetComponent<TowerCore>() ? 0 : 5;
                    Set(pl, "cost", cost);
                }
                foreach (var ui in root.GetComponentsInChildren<GameUI>(true))
                    if (!new SerializedObject(ui).FindProperty("budgetText").objectReferenceValue)
                    {
                        var bt = ui.transform.Find("BudgetText");
                        if (bt) Set(ui, "budgetText", bt.GetComponent<Text>());
                    }
            }
            log.Append(SetupBloom(s));
            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }
    }

    [InitializeOnLoad]
    static class TowerDefenseUpdate2Request
    {
        const string RequestFile = "ARTD_Update.request";
        static double nextCheck;

        static TowerDefenseUpdate2Request()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 2.0;
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                string scenePath = File.ReadAllText(RequestFile).Trim();
                File.Delete(RequestFile);
                string log;
                try
                {
                    Scene scene = default;
                    for (int i = 0; i < SceneManager.sceneCount; i++)
                        if (SceneManager.GetSceneAt(i).path == scenePath) scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid() || !scene.isLoaded) log = "Scene not open: " + scenePath + "\n";
                    else
                    {
                        bool wasDirty = scene.isDirty;
                        log = TowerDefenseBuilder.ApplyUpdate2(scene);
                        if (wasDirty) log += "Scene had unsaved changes -> NOT saved\n";
                        else { EditorSceneManager.SaveScene(scene); log += "Scene saved.\n"; }
                    }
                }
                catch (Exception e) { log = "ERROR: " + e; }
                Directory.CreateDirectory("Logs");
                File.WriteAllText("Logs/ARTD_Update.log", log);
                Debug.Log("[ARTD] Update2\n" + log);
            };
        }
    }
}
