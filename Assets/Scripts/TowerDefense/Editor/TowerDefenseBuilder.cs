using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vuforia;
using Object = UnityEngine.Object;
using Image = UnityEngine.UI.Image;

namespace ARTD.EditorTools
{
    /// <summary>
    /// 仮モデル（プリミティブ）でプレハブ・ステータス・マテリアルを生成し、シーンに組み込む。
    /// メニュー: Tools > AR Tower Defense
    /// 既にあるアセットは上書きしない（Rebuild を選んだときだけ上書き）。
    /// </summary>
    public static partial class TowerDefenseBuilder
    {
        public const string PrefabDir = "Assets/Prefabs/TowerDefense";
        public const string DataDir = "Assets/Data/TowerDefense";
        public const string MatDir = "Assets/Art/TowerDefense/Materials";

        static string P(string sub, string name) => $"{PrefabDir}/{sub}/{name}.prefab";

        static bool overwrite;
        static Scene buildScene;
        static Font font;

        // ------------------------------------------------------------------ menu

        [MenuItem("Tools/AR Tower Defense/1. Build Prefabs + Data (missing only)", priority = 1)]
        public static void BuildMissing() => Build(false);

        [MenuItem("Tools/AR Tower Defense/Rebuild Prefabs (overwrite prefabs, keep Data)", priority = 20)]
        static void RebuildMenu()
        {
            if (EditorUtility.DisplayDialog("AR Tower Defense",
                    "Prefabs/TowerDefense 以下のプレハブを作り直します（モデル差し替え等の変更は消えます）。\nステータス(Data)とマテリアルはそのまま残します。", "作り直す", "やめる"))
                Build(true);
        }

        [MenuItem("Tools/AR Tower Defense/2. Setup Current Scene", priority = 2)]
        static void SetupCurrentSceneMenu()
        {
            Build(false);
            var s = SceneManager.GetActiveScene();
            var log = SetupScene(s);
            Debug.Log("[ARTD] シーンセットアップ完了\n" + log);
        }

        // ------------------------------------------------------------------ build

        public static void Build(bool force)
        {
            overwrite = force;
            font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/DotGothic16/DotGothic16-Regular.ttf");
            EnsureFolder(PrefabDir + "/Systems");
            EnsureFolder(PrefabDir + "/Placeables");
            EnsureFolder(PrefabDir + "/Units");
            EnsureFolder(PrefabDir + "/Projectiles");
            EnsureFolder(PrefabDir + "/Effects");
            EnsureFolder(PrefabDir + "/UI");
            EnsureFolder(DataDir);
            EnsureFolder(MatDir);

            buildScene = EditorSceneManager.NewPreviewScene();
            try
            {
                BuildAll();
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(buildScene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        // マテリアル
        static Material mWall, mWallTip, mWallLead, mWallCap, mEraser, mEraserSleeve, mManhole, mManholeRim,
            mPen, mPenCap, mMetal, mTurret, mPencase, mPencaseTrim, mStone, mFlag, mSoldier, mSoldierDark,
            mEnemy, mEnemyHeavy, mEnemyHorn, mBall, mBarBg, mBarFill,
            fxHit, fxExplosion, fxMuzzle, fxSpawn, fxSoldierSpawn, fxPlace, fxDeath;

        static void BuildAll()
        {
            // ---- materials
            mWall = Mat("Pencil_Body", new Color(1f, 0.8f, 0.15f));
            mWallTip = Mat("Pencil_Wood", new Color(0.93f, 0.75f, 0.55f));
            mWallLead = Mat("Pencil_Lead", new Color(0.15f, 0.15f, 0.15f));
            mWallCap = Mat("Pencil_Eraser", new Color(1f, 0.55f, 0.65f));
            mEraser = Mat("Eraser_Body", new Color(0.97f, 0.97f, 0.97f));
            mEraserSleeve = Mat("Eraser_Sleeve", new Color(0.2f, 0.45f, 0.9f));
            mManhole = Mat("Eraser_Manhole", new Color(0.12f, 0.12f, 0.14f));
            mManholeRim = Mat("Eraser_ManholeRim", new Color(0.45f, 0.45f, 0.5f));
            mPen = Mat("Pen_Body", new Color(0.1f, 0.3f, 0.85f));
            mPenCap = Mat("Pen_Cap", new Color(0.05f, 0.12f, 0.4f));
            mMetal = Mat("Metal", new Color(0.6f, 0.62f, 0.65f));
            mTurret = Mat("Cannon_Turret", new Color(0.25f, 0.3f, 0.25f));
            mPencase = Mat("Pencase_Body", new Color(0.65f, 0.15f, 0.15f));
            mPencaseTrim = Mat("Pencase_Zipper", new Color(0.9f, 0.85f, 0.3f));
            mStone = Mat("Tower_Stone", new Color(0.8f, 0.78f, 0.72f));
            mFlag = Mat("Tower_Flag", new Color(0.95f, 0.2f, 0.2f));
            mSoldier = Mat("Soldier_Body", new Color(0.2f, 0.55f, 1f));
            mSoldierDark = Mat("Soldier_Helmet", new Color(0.1f, 0.2f, 0.5f));
            mEnemy = Mat("Enemy_Basic", new Color(0.85f, 0.2f, 0.25f));
            mEnemyHeavy = Mat("Enemy_Heavy", new Color(0.4f, 0.15f, 0.5f));
            mEnemyHorn = Mat("Enemy_Horn", new Color(0.15f, 0.1f, 0.1f));
            mBall = Mat("CannonBall", new Color(0.1f, 0.1f, 0.1f));
            mBarBg = Mat("HealthBar_Bg", new Color(0f, 0f, 0f, 1f), true);
            mBarFill = Mat("HealthBar_Fill", Color.green, true);
            fxHit = Mat("FX_Hit", new Color(1f, 0.95f, 0.4f), true);
            fxExplosion = Mat("FX_Explosion", new Color(1f, 0.5f, 0.1f), true);
            fxMuzzle = Mat("FX_Muzzle", new Color(1f, 0.9f, 0.6f), true);
            fxSpawn = Mat("FX_EnemySpawn", new Color(0.6f, 0.1f, 0.8f), true);
            fxSoldierSpawn = Mat("FX_SoldierSpawn", new Color(0.3f, 0.9f, 1f), true);
            fxPlace = Mat("FX_Place", new Color(0.5f, 1f, 0.9f), true);
            fxDeath = Mat("FX_Death", new Color(0.35f, 0.3f, 0.3f), true);

            // ---- stats
            var wallStats = Stats<HealthStats>("Wall_Stats", s => s.maxHealth = 300);
            var towerStats = Stats<HealthStats>("Tower_Stats", s => s.maxHealth = 1000);
            var cannonStats = Stats<CannonStats>("Cannon_Stats", s =>
            {
                s.maxHealth = 200; s.range = 250; s.fireInterval = 1.2f; s.damage = 35; s.projectileSpeed = 300;
                s.splashRadius = 15; s.turnSpeed = 240; s.aimTolerance = 10;
            });
            var spawnerStats = Stats<SoldierSpawnerStats>("EraserSpawner_Stats", s =>
            {
                s.maxHealth = 250; s.firstSpawnDelay = 1; s.spawnInterval = 6; s.maxAlive = 4; s.guardRadius = 150; s.emergeDuration = 0.6f;
            });
            var soldierStats = Stats<CharacterStats>("Soldier_Stats", s =>
            {
                s.maxHealth = 60; s.moveSpeed = 40; s.turnSpeed = 720; s.bodyRadius = 4; s.attackDamage = 10;
                s.attackInterval = 0.8f; s.attackRange = 6; s.detectRange = 120; s.obstacleCheckHeight = 5; s.scoreValue = 0;
            });
            var enemyStats = Stats<CharacterStats>("Enemy_Basic_Stats", s =>
            {
                s.maxHealth = 50; s.moveSpeed = 25; s.turnSpeed = 540; s.bodyRadius = 5; s.attackDamage = 8;
                s.attackInterval = 1f; s.attackRange = 6; s.detectRange = 50; s.obstacleCheckHeight = 5; s.scoreValue = 10;
            });
            var heavyStats = Stats<CharacterStats>("Enemy_Heavy_Stats", s =>
            {
                s.maxHealth = 160; s.moveSpeed = 15; s.turnSpeed = 360; s.bodyRadius = 8; s.attackDamage = 20;
                s.attackInterval = 1.5f; s.attackRange = 8; s.detectRange = 40; s.obstacleCheckHeight = 5; s.scoreValue = 30;
            });

            // ---- effects
            var eHit = Effect("Effect_Hit", PrimitiveType.Sphere, Vector3.one * 6, fxHit, 0.2f);
            var eExplosion = Effect("Effect_Explosion", PrimitiveType.Sphere, Vector3.one * 30, fxExplosion, 0.4f);
            var eBigExplosion = Effect("Effect_StructureDestroyed", PrimitiveType.Sphere, Vector3.one * 80, fxExplosion, 0.6f);
            var eMuzzle = Effect("Effect_MuzzleFlash", PrimitiveType.Sphere, Vector3.one * 8, fxMuzzle, 0.12f);
            var eSpawn = Effect("Effect_EnemySpawn", PrimitiveType.Sphere, Vector3.one * 25, fxSpawn, 0.5f);
            var eSoldierSpawn = Effect("Effect_SoldierSpawn", PrimitiveType.Cylinder, new Vector3(16, 0.5f, 16), fxSoldierSpawn, 0.4f);
            var ePlace = Effect("Effect_Place", PrimitiveType.Cylinder, new Vector3(110, 0.5f, 110), fxPlace, 0.45f);
            var eDeath = Effect("Effect_Death", PrimitiveType.Sphere, Vector3.one * 20, fxDeath, 0.35f);

            // ---- health bar
            var healthBar = GetOrBuild(P("UI", "HealthBar"), () =>
            {
                var root = Root("HealthBar");
                var bar = root.AddComponent<HealthBar>();
                Prim(PrimitiveType.Quad, "Background", root.transform, Vector3.zero, new Vector3(1.06f, 1.4f, 1f), mBarBg);
                var fill = Prim(PrimitiveType.Quad, "Fill", root.transform, new Vector3(0, 0, -0.3f), Vector3.one, mBarFill);
                Set(bar, "fill", fill.transform);
                Set(bar, "fillRenderer", fill.GetComponent<Renderer>());
                return root;
            });

            // ---- projectile
            var cannonBall = GetOrBuild(P("Projectiles", "CannonBall"), () =>
            {
                var root = Root("CannonBall");
                var p = root.AddComponent<Projectile>();
                var model = Model(root);
                Prim(PrimitiveType.Sphere, "Ball", model, Vector3.zero, Vector3.one * 6, mBall);
                Set(p, "impactEffectPrefab", eExplosion);
                Set(p, "hitRadius", 3f);
                return root;
            });

            // ---- units
            var soldier = GetOrBuild(P("Units", "Soldier"), () =>
            {
                var root = Root("Soldier");
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Ally, TargetKind.Unit, null, eHit, eDeath);
                var u = root.AddComponent<CharacterUnit>();
                Set(u, "stats", soldierStats);
                Set(u, "role", (int)CharacterUnit.UnitRole.Defender);
                Capsule(root, new Vector3(0, 8, 0), 4, 16);
                var model = Model(root);
                Prim(PrimitiveType.Capsule, "Body", model, new Vector3(0, 8, 0), new Vector3(8, 8, 8), mSoldier);
                Prim(PrimitiveType.Sphere, "Helmet", model, new Vector3(0, 15, 0), new Vector3(7.5f, 4, 7.5f), mSoldierDark);
                Prim(PrimitiveType.Cube, "Sword", model, new Vector3(4.5f, 9, 4), new Vector3(1, 1, 9), mMetal);
                Prim(PrimitiveType.Cube, "Shield", model, new Vector3(-4.5f, 8, 1), new Vector3(1, 7, 6), mSoldierDark);
                Bar(root, healthBar, 22, 14);
                return root;
            });

            var enemyBasic = GetOrBuild(P("Units", "Enemy_Basic"), () =>
            {
                var root = Root("Enemy_Basic");
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Enemy, TargetKind.Unit, null, eHit, eDeath);
                var u = root.AddComponent<CharacterUnit>();
                Set(u, "stats", enemyStats);
                Set(u, "role", (int)CharacterUnit.UnitRole.Attacker);
                Capsule(root, new Vector3(0, 10, 0), 5, 20);
                var model = Model(root);
                Prim(PrimitiveType.Capsule, "Body", model, new Vector3(0, 10, 0), new Vector3(10, 10, 10), mEnemy);
                Prim(PrimitiveType.Cube, "HornL", model, new Vector3(-2.5f, 20, 0), new Vector3(1.5f, 4, 1.5f), mEnemyHorn, new Vector3(0, 0, 20));
                Prim(PrimitiveType.Cube, "HornR", model, new Vector3(2.5f, 20, 0), new Vector3(1.5f, 4, 1.5f), mEnemyHorn, new Vector3(0, 0, -20));
                Prim(PrimitiveType.Sphere, "Eye", model, new Vector3(0, 15, 4.5f), new Vector3(4, 2, 1.5f), mBarFill);
                Bar(root, healthBar, 27, 16);
                return root;
            });

            var enemyHeavy = GetOrBuild(P("Units", "Enemy_Heavy"), () =>
            {
                var root = Root("Enemy_Heavy");
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Enemy, TargetKind.Unit, null, eHit, eExplosion);
                var u = root.AddComponent<CharacterUnit>();
                Set(u, "stats", heavyStats);
                Set(u, "role", (int)CharacterUnit.UnitRole.Attacker);
                Capsule(root, new Vector3(0, 12, 0), 8, 24);
                var model = Model(root);
                Prim(PrimitiveType.Capsule, "Body", model, new Vector3(0, 12, 0), new Vector3(16, 12, 16), mEnemyHeavy);
                Prim(PrimitiveType.Cube, "Club", model, new Vector3(9, 12, 6), new Vector3(3, 3, 14), mEnemyHorn);
                Prim(PrimitiveType.Sphere, "Eye", model, new Vector3(0, 18, 7.2f), new Vector3(6, 2.5f, 2), mBarFill);
                Bar(root, healthBar, 31, 22);
                return root;
            });

            // ---- waves
            var waves = Stats<WaveDefinition>("Waves_Default", w =>
            {
                var basic = enemyBasic.GetComponent<CharacterUnit>();
                var heavy = enemyHeavy.GetComponent<CharacterUnit>();
                w.spawnRadius = 350; w.spawnRadiusJitter = 50; w.endless = false;
                w.waves = new List<WaveDefinition.Wave>
                {
                    Wave("Wave 1", 3, (basic, 5, 2f)),
                    Wave("Wave 2", 5, (basic, 8, 1.2f), (heavy, 1, 1f)),
                    Wave("Wave 3", 5, (basic, 6, 1f), (heavy, 3, 2.5f), (basic, 6, 0.8f)),
                    Wave("Wave 4", 6, (heavy, 5, 2f), (basic, 12, 0.6f)),
                    Wave("Wave 5", 6, (basic, 15, 0.5f), (heavy, 8, 1.2f)),
                };
            });

            // ---- placeables (4 文房具)
            GetOrBuild(P("Placeables", "Pencil_Wall"), () =>
            {
                var root = Root("Pencil_Wall");
                var pl = root.AddComponent<Placeable>();
                Set(pl, "placeEffectPrefab", ePlace);
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Ally, TargetKind.Structure, wallStats, eHit, eBigExplosion);
                Box(root, new Vector3(0, 5, 0), new Vector3(50, 10, 10));
                var model = Model(root);
                var body = Prim(PrimitiveType.Cube, "Body", model, new Vector3(-3, 5, 0), new Vector3(36, 10, 10), mWall);
                Prim(PrimitiveType.Cube, "Wood", model, new Vector3(18, 5, 0), new Vector3(6, 7, 7), mWallTip);
                Prim(PrimitiveType.Cube, "Lead", model, new Vector3(23, 5, 0), new Vector3(4, 3, 3), mWallLead);
                Prim(PrimitiveType.Cube, "EraserCap", model, new Vector3(-23, 5, 0), new Vector3(4, 10.4f, 10.4f), mWallCap);
                Tint(root, body.GetComponent<Renderer>());
                Bar(root, healthBar, 18, 30);
                return root;
            });

            GetOrBuild(P("Placeables", "Eraser_SoldierSpawner"), () =>
            {
                var root = Root("Eraser_SoldierSpawner");
                var pl = root.AddComponent<Placeable>();
                Set(pl, "placeEffectPrefab", ePlace);
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Ally, TargetKind.Structure, null, eHit, eBigExplosion);
                Box(root, new Vector3(0, 7.5f, 0), new Vector3(40, 15, 24));
                var model = Model(root);
                var body = Prim(PrimitiveType.Cube, "Body", model, new Vector3(0, 7.5f, 0), new Vector3(40, 15, 24), mEraser);
                Prim(PrimitiveType.Cube, "Sleeve", model, new Vector3(8, 7.5f, 0), new Vector3(22, 15.4f, 24.4f), mEraserSleeve);
                Prim(PrimitiveType.Cylinder, "ManholeRim", model, new Vector3(-10, 15.1f, 0), new Vector3(15, 0.15f, 15), mManholeRim);
                Prim(PrimitiveType.Cylinder, "Manhole", model, new Vector3(-10, 15.2f, 0), new Vector3(12, 0.2f, 12), mManhole);
                var sp = new GameObject("SpawnPoint").transform;
                sp.SetParent(root.transform, false);
                sp.localPosition = new Vector3(-10, 15.5f, 0);
                var spawner = root.AddComponent<SoldierSpawner>();
                Set(spawner, "stats", spawnerStats);
                Set(spawner, "soldierPrefab", soldier.GetComponent<CharacterUnit>());
                Set(spawner, "spawnPoint", sp);
                Set(spawner, "spawnEffectPrefab", eSoldierSpawn);
                Tint(root, body.GetComponent<Renderer>());
                Bar(root, healthBar, 25, 24);
                return root;
            });

            GetOrBuild(P("Placeables", "Pen_Cannon"), () =>
            {
                var root = Root("Pen_Cannon");
                var pl = root.AddComponent<Placeable>();
                Set(pl, "placeEffectPrefab", ePlace);
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Ally, TargetKind.Structure, null, eHit, eBigExplosion);
                Box(root, new Vector3(0, 8, 0), new Vector3(74, 16, 16));
                var model = Model(root);
                var body = Prim(PrimitiveType.Cylinder, "PenBody", model, new Vector3(0, 3, 0), new Vector3(6, 33, 6), mPen, new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cylinder, "PenCap", model, new Vector3(-30, 3, 0), new Vector3(6.6f, 7, 6.6f), mPenCap, new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cube, "PenClip", model, new Vector3(-28, 6.6f, 0), new Vector3(14, 1, 1.5f), mMetal);
                Prim(PrimitiveType.Cylinder, "PenTip", model, new Vector3(35, 3, 0), new Vector3(3, 2.5f, 3), mMetal, new Vector3(0, 0, 90));
                Prim(PrimitiveType.Cylinder, "TurretBase", model, new Vector3(0, 7.2f, 0), new Vector3(16, 1.2f, 16), mTurret);

                var yaw = new GameObject("YawPivot").transform;
                yaw.SetParent(root.transform, false);
                yaw.localPosition = new Vector3(0, 8.4f, 0);
                Prim(PrimitiveType.Cube, "Head", yaw, new Vector3(0, 4, 0), new Vector3(12, 8, 12), mTurret);
                var pitch = new GameObject("PitchPivot").transform;
                pitch.SetParent(yaw, false);
                pitch.localPosition = new Vector3(0, 5, 3);
                Prim(PrimitiveType.Cylinder, "Barrel", pitch, new Vector3(0, 0, 9), new Vector3(4, 9, 4), mMetal, new Vector3(90, 0, 0));
                var muzzle = new GameObject("Muzzle").transform;
                muzzle.SetParent(pitch, false);
                muzzle.localPosition = new Vector3(0, 0, 18.5f);

                var t = root.AddComponent<CannonTurret>();
                Set(t, "stats", cannonStats);
                Set(t, "projectilePrefab", cannonBall.GetComponent<Projectile>());
                Set(t, "yawPivot", yaw);
                Set(t, "pitchPivot", pitch);
                Set(t, "muzzle", muzzle);
                Set(t, "muzzleFlashPrefab", eMuzzle);
                Tint(root, body.GetComponent<Renderer>());
                Bar(root, healthBar, 30, 24);
                return root;
            });

            GetOrBuild(P("Placeables", "Pencase_Tower"), () =>
            {
                var root = Root("Pencase_Tower");
                var pl = root.AddComponent<Placeable>();
                Set(pl, "alignToTowerPlane", false);
                Set(pl, "placeEffectPrefab", ePlace);
                var h = root.AddComponent<Health>();
                SetHealth(h, Team.Ally, TargetKind.Core, towerStats, eHit, eBigExplosion);
                root.AddComponent<TowerCore>();
                Box(root, new Vector3(0, 15, 0), new Vector3(120, 30, 40));
                Box(root, new Vector3(0, 57, 0), new Vector3(26, 54, 26));
                var model = Model(root);
                var body = Prim(PrimitiveType.Cube, "Pencase", model, new Vector3(0, 15, 0), new Vector3(120, 30, 40), mPencase);
                Prim(PrimitiveType.Cube, "Zipper", model, new Vector3(0, 30.2f, 0), new Vector3(116, 0.6f, 3), mPencaseTrim);
                var tower = Prim(PrimitiveType.Cylinder, "Tower", model, new Vector3(0, 55, 0), new Vector3(24, 25, 24), mStone);
                Prim(PrimitiveType.Cube, "Battlement", model, new Vector3(0, 82, 0), new Vector3(30, 6, 30), mStone);
                Prim(PrimitiveType.Cylinder, "FlagPole", model, new Vector3(0, 95, 0), new Vector3(1, 10, 1), mMetal);
                Prim(PrimitiveType.Cube, "Flag", model, new Vector3(5.5f, 101, 0), new Vector3(10, 6, 0.5f), mFlag);
                Tint(root, body.GetComponent<Renderer>(), tower.GetComponent<Renderer>());
                Bar(root, healthBar, 112, 60, false);
                return root;
            });

            // ---- systems
            GetOrBuild(P("Systems", "GameManager"), () =>
            {
                var root = Root("GameManager");
                root.AddComponent<GameManager>();
                root.AddComponent<ShutterController>();
                return root;
            });

            GetOrBuild(P("Systems", "EnemySpawner"), () =>
            {
                var root = Root("EnemySpawner");
                var s = root.AddComponent<EnemySpawner>();
                Set(s, "waveDefinition", waves);
                Set(s, "spawnEffectPrefab", eSpawn);
                return root;
            });

            GetOrBuild(P("Systems", "EventSystem"), () =>
            {
                var root = Root("EventSystem");
                root.AddComponent<EventSystem>();
                root.AddComponent<InputSystemUIInputModule>();
                return root;
            });

            GetOrBuild(P("UI", "GameUI"), BuildUI);
        }

        // ------------------------------------------------------------------ UI

        static GameObject BuildUI()
        {
            var root = Root("GameUI");
            root.layer = 5; // UI
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            var ui = root.AddComponent<GameUI>();

            var uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            var knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            var hp = MakeText(root.transform, "TowerHpText", "タワー HP", 30, TextAnchor.UpperLeft);
            Anchor(hp.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(560, 50));

            var wave = MakeText(root.transform, "WaveText", "", 30, TextAnchor.UpperRight);
            Anchor(wave.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(700, 50));

            var score = MakeText(root.transform, "ScoreText", "", 26, TextAnchor.UpperRight);
            Anchor(score.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -64), new Vector2(500, 44));

            var msg = MakeText(root.transform, "MessageText", "", 32, TextAnchor.UpperCenter);
            Anchor(msg.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(1000, 150));

            var toast = MakeText(root.transform, "ToastText", "", 40, TextAnchor.MiddleCenter);
            Anchor(toast.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(900, 80));

            var shutter = MakeButton(root.transform, "ShutterButton", "シャッター", new Color(1, 1, 1, 0.9f), knob, 28, Color.black);
            Anchor((RectTransform)shutter.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(170, 170));

            var start = MakeButton(root.transform, "StartButton", "スタート", new Color(0.2f, 0.75f, 0.3f, 0.95f), uiSprite, 34, Color.white);
            Anchor((RectTransform)start.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 40), new Vector2(240, 100));

            var panel = UIObj("ResultPanel", root.transform);
            Stretch(panel);
            var pimg = panel.gameObject.AddComponent<Image>();
            pimg.color = new Color(0, 0, 0, 0.6f);
            var result = MakeText(panel, "ResultText", "GAME OVER", 64, TextAnchor.MiddleCenter);
            Anchor(result.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1100, 220));
            var restart = MakeButton(panel, "RestartButton", "リトライ", new Color(1f, 0.6f, 0.15f, 0.95f), uiSprite, 36, Color.white);
            Anchor((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -90), new Vector2(280, 100));

            var flash = UIObj("Flash", root.transform);
            Stretch(flash);
            var fimg = flash.gameObject.AddComponent<Image>();
            fimg.color = new Color(1, 1, 1, 0);
            fimg.raycastTarget = false;

            Set(ui, "shutterButton", shutter);
            Set(ui, "startButton", start);
            Set(ui, "restartButton", restart);
            Set(ui, "towerHpText", hp);
            Set(ui, "waveText", wave);
            Set(ui, "scoreText", score);
            Set(ui, "messageText", msg);
            Set(ui, "toastText", toast);
            Set(ui, "flashImage", fimg);
            Set(ui, "resultPanel", panel.gameObject);
            Set(ui, "resultText", result);
            ui.ApplyPirateTheme();
            PirateHUDArt.Apply(root.GetComponent<PirateHUD>());
            panel.gameObject.SetActive(false);
            return root;
        }

        static RectTransform UIObj(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        static Text MakeText(Transform parent, string name, string content, int size, TextAnchor align, Color? color = null)
        {
            var rt = UIObj(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = color ?? Color.white;
            t.text = content;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.85f);
            o.effectDistance = new Vector2(2, -2);
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label, Color color, Sprite sprite, int fontSize, Color textColor)
        {
            var rt = UIObj(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var t = MakeText(rt, "Label", label, fontSize, TextAnchor.MiddleCenter, textColor);
            Stretch(t.rectTransform);
            if (textColor != Color.white) Object.DestroyImmediate(t.GetComponent<Outline>());
            return b;
        }

        // ------------------------------------------------------------------ scene setup

        enum Kind { None, Wall, Spawner, Cannon, Tower }

        static Kind Classify(ObserverBehaviour ob)
        {
            string dataset = "";
            var so = new SerializedObject(ob);
            var p = so.FindProperty("mDataSetPath");
            if (p != null) dataset = p.stringValue;
            string key = (ob.gameObject.name + " " + ob.TargetName + " " + dataset).ToLowerInvariant();
            if (key.Contains("pencase") || key.Contains("fudebako")) return Kind.Tower;
            if (key.Contains("pencil")) return Kind.Wall;
            if (key.Contains("eraser") || key.Contains("keshigomu")) return Kind.Spawner;
            if (key.Contains("pen")) return Kind.Cannon;
            return Kind.None;
        }

        static bool IsVuforiaInternal(Transform t)
            => t.gameObject.hideFlags != HideFlags.None || t.name.StartsWith("ImageTargetPreview");

        static GameObject PrefabFor(Kind k)
        {
            switch (k)
            {
                case Kind.Wall: return Load(P("Placeables", "Pencil_Wall"));
                case Kind.Spawner: return Load(P("Placeables", "Eraser_SoldierSpawner"));
                case Kind.Cannon: return Load(P("Placeables", "Pen_Cannon"));
                case Kind.Tower: return Load(P("Placeables", "Pencase_Tower"));
            }
            return null;
        }

        static List<T> FindInScene<T>(Scene s) where T : Component
        {
            var list = new List<T>();
            foreach (var r in s.GetRootGameObjects()) list.AddRange(r.GetComponentsInChildren<T>(true));
            return list;
        }

        static T EnsureSystem<T>(Scene s, string prefabPath, System.Text.StringBuilder log) where T : Component
        {
            var found = FindInScene<T>(s);
            if (found.Count > 0) return found[0];
            var prefab = Load(prefabPath);
            if (!prefab) { log.AppendLine("  ! prefab missing: " + prefabPath); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, s);
            Undo.RegisterCreatedObjectUndo(go, "AR TD Setup");
            log.AppendLine("  + " + go.name);
            return go.GetComponent<T>();
        }

        public static string SetupScene(Scene s)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("Scene: " + s.path);
            SetupSystems(s, log);
            SetupMarkers(s, log);
            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }

        /// <summary>GameManager / EnemySpawner / GameUI / EventSystem をシーンに用意する。</summary>
        public static void SetupSystems(Scene s, System.Text.StringBuilder log)
        {
            var gm = EnsureSystem<GameManager>(s, P("Systems", "GameManager"), log);
            var spawner = EnsureSystem<EnemySpawner>(s, P("Systems", "EnemySpawner"), log);
            EnsureSystem<GameUI>(s, P("UI", "GameUI"), log);

            var es = FindInScene<EventSystem>(s);
            if (es.Count == 0) EnsureSystem<EventSystem>(s, P("Systems", "EventSystem"), log);
            else
            {
                // 新 Input System 専用設定なので旧 StandaloneInputModule を置き換える
                foreach (var e in es)
                {
                    var old = e.GetComponent<StandaloneInputModule>();
                    if (old) { Undo.DestroyObjectImmediate(old); log.AppendLine("  - StandaloneInputModule removed from " + e.name); }
                    if (!e.GetComponent<InputSystemUIInputModule>()) Undo.AddComponent<InputSystemUIInputModule>(e.gameObject);
                }
            }
            if (gm && spawner) Set(gm, "enemySpawner", spawner);
            if (gm && !gm.GetComponent<ShutterController>() && FindInScene<ShutterController>(s).Count == 0)
                Undo.AddComponent<ShutterController>(gm.gameObject);
        }

        static void SetupMarkers(Scene s, System.Text.StringBuilder log)
        {
            foreach (var ob in FindInScene<ObserverBehaviour>(s))
            {
                var kind = Classify(ob);
                if (kind == Kind.None) { log.AppendLine($"  ? {ob.name}: 種類を判定できないのでスキップ"); continue; }
                var prefab = PrefabFor(kind);
                if (!prefab) continue;

                // Vuforia のエディタ用プレビュー（マーカー画像）は触らない／誤って消していたら戻す
                foreach (Transform child in ob.transform)
                    if (IsVuforiaInternal(child) && !child.gameObject.activeSelf) child.gameObject.SetActive(true);

                var mp = ob.GetComponent<MarkerPlacer>();
                if (!mp) mp = Undo.AddComponent<MarkerPlacer>(ob.gameObject);

                var existing = ob.GetComponentInChildren<Placeable>(true);
                if (existing)
                {
                    Set(mp, "currentPreview", existing);
                    if (!new SerializedObject(mp).FindProperty("placeablePrefab").objectReferenceValue)
                        Set(mp, "placeablePrefab", prefab.GetComponent<Placeable>());
                    log.AppendLine($"  = {ob.name}: 既に {existing.name} が設定済み");
                    continue;
                }

                // 既存の子（ユーザーが置いた仮モデル）は消さずに非表示にする
                foreach (Transform child in ob.transform)
                {
                    if (!child.gameObject.activeSelf || IsVuforiaInternal(child)) continue;
                    Undo.RecordObject(child.gameObject, "AR TD Setup");
                    child.gameObject.SetActive(false);
                    log.AppendLine($"    (既存の子 {child.name} を非アクティブ化)");
                }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, ob.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                Undo.RegisterCreatedObjectUndo(inst, "AR TD Setup");

                Set(mp, "placeablePrefab", prefab.GetComponent<Placeable>());
                Set(mp, "currentPreview", inst.GetComponent<Placeable>());
                log.AppendLine($"  + {ob.name} ({ob.TargetName}) -> {prefab.name}");
            }
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Load(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static Material Mat(string name, Color color, bool unlit = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")
                         ?? Shader.Find(unlit ? "Unlit/Color" : "Standard");
            m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static T Stats<T>(string name, Action<T> init) where T : ScriptableObject
        {
            string path = $"{DataDir}/{name}.asset";
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a) return a;
            a = ScriptableObject.CreateInstance<T>();
            init(a);
            AssetDatabase.CreateAsset(a, path);
            return a;
        }

        static WaveDefinition.Wave Wave(string name, float delay, params (CharacterUnit prefab, int count, float interval)[] groups)
        {
            var w = new WaveDefinition.Wave { name = name, delayBefore = delay };
            foreach (var g in groups)
                w.groups.Add(new WaveDefinition.SpawnGroup { enemyPrefab = g.prefab, count = g.count, interval = g.interval });
            return w;
        }

        static GameObject GetOrBuild(string path, Func<GameObject> build)
        {
            var existing = Load(path);
            // Placeables は元のモデルから作るので Rebuild でも上書きしない
            if (existing && (!overwrite || path.Contains("/Placeables/"))) return existing;
            var temp = build();
            var asset = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            Debug.Log("[ARTD] prefab saved: " + path);
            return asset;
        }

        static GameObject Root(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, buildScene);
            return go;
        }

        static Transform Model(GameObject root)
        {
            var m = new GameObject("Model").transform;
            m.SetParent(root.transform, false);
            return m;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            var col = go.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
            var r = go.GetComponent<Renderer>();
            if (r && mat) r.sharedMaterial = mat;
            return go;
        }

        static GameObject Effect(string name, PrimitiveType type, Vector3 scale, Material mat, float duration)
        {
            return GetOrBuild(P("Effects", name), () =>
            {
                var root = Root(name);
                var vis = Prim(type, "Visual", root.transform, Vector3.zero, scale, mat);
                var r = vis.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var pop = root.AddComponent<PopEffect>();
                Set(pop, "duration", duration);
                return root;
            });
        }

        static void Box(GameObject root, Vector3 center, Vector3 size)
        {
            var b = root.AddComponent<BoxCollider>();
            b.center = center; b.size = size;
        }

        static void Capsule(GameObject root, Vector3 center, float radius, float height)
        {
            var c = root.AddComponent<CapsuleCollider>();
            c.center = center; c.radius = radius; c.height = height; c.direction = 1;
        }

        static void Bar(GameObject root, GameObject barPrefab, float height, float width, bool hideWhenFull = true)
        {
            var bar = (GameObject)PrefabUtility.InstantiatePrefab(barPrefab, buildScene);
            bar.transform.SetParent(root.transform, false);
            bar.transform.localPosition = new Vector3(0, height, 0);
            bar.transform.localScale = new Vector3(width, Mathf.Max(1.5f, width * 0.12f), 1);
            if (!hideWhenFull) Set(bar.GetComponent<HealthBar>(), "hideWhenFull", false);
        }

        static void Tint(GameObject root, params Renderer[] renderers)
        {
            var t = root.AddComponent<DamageTint>();
            var so = new SerializedObject(t);
            var p = so.FindProperty("renderers");
            p.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetHealth(Health h, Team team, TargetKind kind, HealthStats stats, GameObject hitFx, GameObject deathFx)
        {
            Set(h, "team", (int)team);
            Set(h, "kind", (int)kind);
            if (stats) { Set(h, "stats", stats); Set(h, "maxHealth", stats.maxHealth); }
            Set(h, "hitEffectPrefab", hitFx);
            Set(h, "deathEffectPrefab", deathFx);
        }

        static void Set(Object target, string prop, object value)
        {
            if (!target) return;
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[ARTD] {target.GetType().Name}.{prop} が見つかりません"); return; }
            switch (value)
            {
                case null: p.objectReferenceValue = null; break;
                case Object o: p.objectReferenceValue = o; break;
                case float f: p.floatValue = f; break;
                case bool b: p.boolValue = b; break;
                case string str: p.stringValue = str; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i;
                    else p.intValue = i;
                    break;
                default: Debug.LogError($"[ARTD] unsupported value type for {prop}"); break;
            }
            if (PrefabUtility.IsPartOfPrefabInstance(target)) so.ApplyModifiedProperties();
            else so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
