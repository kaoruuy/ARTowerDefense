using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>
    /// ImageTarget の下にユーザーが置いた元の子オブジェクト（モデル）を、そのままゲームオブジェクトとして使う。
    /// 元の子をルート(Placeable/Health/各機能)で包み、Placeables プレハブとして保存する。
    /// 生成済みの仮(プリミティブ)オブジェクトは削除する。
    /// </summary>
    public static partial class TowerDefenseBuilder
    {
        [MenuItem("Tools/AR Tower Defense/3. Use Original Marker Children As Objects", priority = 3)]
        static void ConvertMenu()
        {
            var log = ConvertMarkerChildren(SceneManager.GetActiveScene());
            Debug.Log("[ARTD] Convert\n" + log);
        }

        public static string ConvertMarkerChildren(Scene s)
        {
            Build(false); // 依存アセット(Stats/エフェクト/兵士/弾/HPバー)が無ければ作る
            var log = new System.Text.StringBuilder();
            log.AppendLine("Convert: " + s.path);
            SetupSystems(s, log);

            var healthBar = Load(P("UI", "HealthBar"));
            GameObject Fx(string n) => Load(P("Effects", n));
            T Data<T>(string n) where T : Object => AssetDatabase.LoadAssetAtPath<T>($"{DataDir}/{n}.asset");

            foreach (var ob in FindInScene<ObserverBehaviour>(s))
            {
                var kind = Classify(ob);
                if (kind == Kind.None) continue;

                // 元の子（Placeable でも Vuforia 内部でもないもの）
                var originals = new List<Transform>();
                foreach (Transform c in ob.transform)
                    if (!IsVuforiaInternal(c) && !c.GetComponent<Placeable>()) originals.Add(c);
                if (originals.Count == 0)
                {
                    log.AppendLine($"  = {ob.name}: 元の子が無いのでスキップ（変換済み？）");
                    continue;
                }

                // 生成した仮オブジェクトを削除
                var generated = new List<GameObject>();
                foreach (Transform c in ob.transform)
                    if (c.GetComponent<Placeable>()) generated.Add(c.gameObject);
                foreach (var g in generated)
                {
                    log.AppendLine($"  - 削除: {g.name}");
                    Undo.DestroyObjectImmediate(g);
                }

                string prefabName = PrefabNameFor(kind);
                foreach (var o in originals)
                {
                    Undo.RecordObject(o.gameObject, "AR TD Convert");
                    o.gameObject.SetActive(true);
                }

                // ルートは元の子の位置（マーカー面上）に置き、モデルのワールド姿勢は変えない
                Vector3 lp = originals[0].localPosition; lp.y = 0f;
                var root = new GameObject(prefabName);
                Undo.RegisterCreatedObjectUndo(root, "AR TD Convert");
                root.transform.SetParent(ob.transform, false);
                root.transform.localPosition = lp;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;

                Transform modelParent = NewChild(kind == Kind.Cannon ? "YawPivot" : "Model", root.transform);
                foreach (var o in originals) Undo.SetTransformParent(o, modelParent, "AR TD Convert");

                Bounds b = LocalBounds(root.transform, modelParent);
                bool hasCollider = false;
                foreach (var col in modelParent.GetComponentsInChildren<Collider>(true))
                    if (!col.isTrigger) hasCollider = true;
                if (!hasCollider)
                {
                    var bc = root.AddComponent<BoxCollider>();
                    bc.center = b.center; bc.size = b.size;
                }

                var pl = root.AddComponent<Placeable>();
                Set(pl, "placeEffectPrefab", Fx("Effect_Place"));
                if (kind == Kind.Tower) Set(pl, "alignToTowerPlane", false);

                var h = root.AddComponent<Health>();
                switch (kind)
                {
                    case Kind.Wall:
                        SetHealth(h, Team.Ally, TargetKind.Structure, Data<HealthStats>("Wall_Stats"), Fx("Effect_Hit"), Fx("Effect_StructureDestroyed"));
                        break;
                    case Kind.Tower:
                        SetHealth(h, Team.Ally, TargetKind.Core, Data<HealthStats>("Tower_Stats"), Fx("Effect_Hit"), Fx("Effect_StructureDestroyed"));
                        root.AddComponent<TowerCore>();
                        break;
                    case Kind.Spawner:
                    {
                        SetHealth(h, Team.Ally, TargetKind.Structure, null, Fx("Effect_Hit"), Fx("Effect_StructureDestroyed"));
                        var sp = NewChild("SpawnPoint", root.transform);
                        sp.localPosition = new Vector3(b.center.x, 0.5f, b.max.z + 8f);
                        var spawner = root.AddComponent<SoldierSpawner>();
                        Set(spawner, "stats", Data<SoldierSpawnerStats>("EraserSpawner_Stats"));
                        var soldier = Load(P("Units", "Soldier"));
                        Set(spawner, "soldierPrefab", soldier ? soldier.GetComponent<CharacterUnit>() : null);
                        Set(spawner, "spawnPoint", sp);
                        Set(spawner, "spawnEffectPrefab", Fx("Effect_SoldierSpawn"));
                        break;
                    }
                    case Kind.Cannon:
                    {
                        SetHealth(h, Team.Ally, TargetKind.Structure, null, Fx("Effect_Hit"), Fx("Effect_StructureDestroyed"));
                        Bounds yb = LocalBounds(modelParent, modelParent);
                        var muzzle = NewChild("Muzzle", modelParent);
                        muzzle.localPosition = new Vector3(yb.center.x, yb.center.y, yb.max.z);
                        var t = root.AddComponent<CannonTurret>();
                        Set(t, "stats", Data<CannonStats>("Cannon_Stats"));
                        var ball = Load(P("Projectiles", "CannonBall"));
                        Set(t, "projectilePrefab", ball ? ball.GetComponent<Projectile>() : null);
                        Set(t, "yawPivot", modelParent);
                        Set(t, "muzzle", muzzle);
                        Set(t, "muzzleFlashPrefab", Fx("Effect_MuzzleFlash"));
                        break;
                    }
                }

                var tint = root.AddComponent<DamageTint>();
                var so = new SerializedObject(tint);
                var rp = so.FindProperty("renderers");
                var rends = modelParent.GetComponentsInChildren<Renderer>(true);
                rp.arraySize = rends.Length;
                for (int i = 0; i < rends.Length; i++) rp.GetArrayElementAtIndex(i).objectReferenceValue = rends[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                if (healthBar)
                {
                    var bar = (GameObject)PrefabUtility.InstantiatePrefab(healthBar, root.transform);
                    float width = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.6f, 14f, 60f);
                    bar.transform.localPosition = new Vector3(b.center.x, b.max.y + Mathf.Max(6f, width * 0.2f), b.center.z);
                    bar.transform.localScale = new Vector3(width, Mathf.Max(1.5f, width * 0.12f), 1);
                    if (kind == Kind.Tower) Set(bar.GetComponent<HealthBar>(), "hideWhenFull", false);
                }

                // プレハブとして保存（既存の仮プレハブを置き換え。GUID は維持されるので参照は切れない）
                string path = P("Placeables", prefabName);
                var asset = PrefabUtility.SaveAsPrefabAssetAndConnect(root, path, InteractionMode.AutomatedAction);

                var mp = ob.GetComponent<MarkerPlacer>();
                if (!mp) mp = Undo.AddComponent<MarkerPlacer>(ob.gameObject);
                Set(mp, "placeablePrefab", asset.GetComponent<Placeable>());
                Set(mp, "currentPreview", root.GetComponent<Placeable>());

                var names = new List<string>();
                foreach (var o in originals) names.Add(o.name);
                log.AppendLine($"  + {ob.name}: [{string.Join(", ", names)}] -> {path}  (bounds size {b.size})");
            }

            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }

        public const string SetRootName = "ARTD_GameSet";

        [MenuItem("Tools/AR Tower Defense/4. Make Marker Prefabs", priority = 4)]
        static void MakeSetMenu()
        {
            var log = MakeSetPrefabs(SceneManager.GetActiveScene());
            Debug.Log("[ARTD] Marker prefabs\n" + log);
        }

        /// <summary>
        /// マーカー(ImageTarget + MarkerPlacer + 文房具)を1つずつ Prefabs/TowerDefense/Markers にプレハブ化する。
        /// ARCamera も Systems/ARCamera.prefab にする。システム類はもともと個別プレハブ。
        /// </summary>
        public static string MakeSetPrefabs(Scene s)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("Marker prefabs: " + s.path);
            EnsureFolder(PrefabDir + "/Markers");

            foreach (var ob in FindInScene<ObserverBehaviour>(s))
            {
                if (!ob.GetComponent<MarkerPlacer>()) continue;
                string path = $"{PrefabDir}/Markers/{ob.gameObject.name}.prefab";
                SaveOrApply(ob.gameObject, path);
                log.AppendLine("  marker -> " + path);
            }

            foreach (var vb in FindInScene<VuforiaBehaviour>(s))
            {
                string camPath = P("Systems", "ARCamera");
                SaveOrApply(vb.gameObject, camPath);
                log.AppendLine("  camera -> " + camPath);
                break;
            }

            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }

        /// <summary>ARTD_GameSet をほどいて中身をシーン直下に戻し、セットのプレハブを削除する（個別プレハブは残す）。</summary>
        public static string RemoveSet(Scene s)
        {
            var log = new System.Text.StringBuilder();
            foreach (var r in s.GetRootGameObjects())
            {
                if (r.name != SetRootName) continue;
                if (PrefabUtility.IsOutermostPrefabInstanceRoot(r))
                    PrefabUtility.UnpackPrefabInstance(r, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                var children = new List<Transform>();
                foreach (Transform c in r.transform) children.Add(c);
                foreach (var c in children)
                {
                    c.SetParent(null, true);
                    SceneManager.MoveGameObjectToScene(c.gameObject, s);
                    log.AppendLine("  unparent " + c.name);
                }
                Object.DestroyImmediate(r);
            }
            string setPath = P("Systems", SetRootName);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(setPath))
            {
                AssetDatabase.DeleteAsset(setPath);
                log.AppendLine("  deleted " + setPath);
            }
            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }

        static void SaveOrApply(GameObject go, string path)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(go) &&
                AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(go)) == path)
                PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);
            else
                PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.AutomatedAction);
        }

        static string PrefabNameFor(Kind k)
        {
            switch (k)
            {
                case Kind.Wall: return "Pencil_Wall";
                case Kind.Spawner: return "Eraser_SoldierSpawner";
                case Kind.Cannon: return "Pen_Cannon";
                default: return "Pencase_Tower";
            }
        }

        static Transform NewChild(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        /// <summary>modelRoot 以下の描画範囲を space ローカル座標で求める。</summary>
        static Bounds LocalBounds(Transform space, Transform modelRoot)
        {
            bool any = false;
            var result = new Bounds(Vector3.zero, Vector3.zero);
            void Add(Vector3 world)
            {
                Vector3 p = space.InverseTransformPoint(world);
                if (!any) { result = new Bounds(p, Vector3.zero); any = true; }
                else result.Encapsulate(p);
            }
            void AddBox(Bounds lb, Matrix4x4 m)
            {
                Vector3 c = lb.center, e = lb.extents;
                for (int x = -1; x <= 1; x += 2)
                for (int y = -1; y <= 1; y += 2)
                for (int z = -1; z <= 1; z += 2)
                    Add(m.MultiplyPoint3x4(c + Vector3.Scale(e, new Vector3(x, y, z))));
            }
            foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh) AddBox(mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            foreach (var sm in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (sm.sharedMesh) AddBox(sm.sharedMesh.bounds, sm.transform.localToWorldMatrix);
            if (!any) result = new Bounds(new Vector3(0, 5, 0), new Vector3(10, 10, 10));
            return result;
        }
    }

    /// <summary>プロジェクト直下に ARTD_Convert.request（中身 = シーンパス）があれば、コンパイル後に一度だけ変換する。</summary>
    [InitializeOnLoad]
    static class TowerDefenseConvertRequest
    {
        const string RequestFile = "ARTD_Convert.request";

        static TowerDefenseConvertRequest()
        {
            if (File.Exists(RequestFile)) EditorApplication.delayCall += Run;
        }

        static void Run()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Run;
                return;
            }
            if (!File.Exists(RequestFile)) return;
            // 中身: "シーンパス" または "シーンパス|all"（all = 変換 + セットのプレハブ化）
            string[] req = File.ReadAllText(RequestFile).Trim().Split('|');
            string scenePath = req[0].Trim();
            bool makeSet = req.Length > 1 && req[1].Trim() == "all";
            File.Delete(RequestFile);
            string log;
            try
            {
                Scene scene = default;
                bool wasDirty = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var sc = SceneManager.GetSceneAt(i);
                    if (sc.path == scenePath) { scene = sc; wasDirty = sc.isDirty; }
                }
                bool opened = false;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    opened = true;
                }
                if (req.Length > 1 && req[1].Trim() == "unset") log = TowerDefenseBuilder.RemoveSet(scene);
                else
                {
                    log = TowerDefenseBuilder.ConvertMarkerChildren(scene);
                    if (makeSet) log += TowerDefenseBuilder.MakeSetPrefabs(scene);
                }
                if (!wasDirty) { EditorSceneManager.SaveScene(scene); log += "Scene saved.\n"; }
                else log += "Scene had unsaved changes -> NOT saved.\n";
                if (opened && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                log += "DONE\n";
            }
            catch (System.Exception e) { log = "ERROR: " + e; }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ARTD_Convert.log", log);
            Debug.Log("[ARTD] Convert\n" + log);
        }
    }
}
