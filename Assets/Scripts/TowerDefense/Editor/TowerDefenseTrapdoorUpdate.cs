using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>
    /// 次の Prefab をまとめて更新する（何度実行しても同じ結果になる）。
    /// 未適用ならスクリプトのコンパイル後に自動で1回実行される。手動ではメニュー「Tools > AR Tower Defense > 5. ...」。
    /// ・Eraser_SoldierSpawner : 見た目を Henry → トラップドア（穴 + 蝶番付きの扉）に差し替え
    /// ・Soldier              : 見た目を Henry（アニメーション付き）に差し替え
    /// ・Enemy_Basic / Heavy  : 見た目を Sharky（アニメーション付き）に差し替え
    /// ・Pencil_Wall          : 丸太を横に長くし、HP バーを常に表示
    /// </summary>
    [InitializeOnLoad]
    public static class TowerDefenseTrapdoorUpdate
    {
        const string ArtDir = "Assets/Art/PirateKit";
        const string PirateMatPath = ArtDir + "/PirateKit.mat";
        const string TrapdoorFbx = ArtDir + "/Trapdoor.fbx";
        const string HenryFbx = ArtDir + "/Characters_Henry.fbx";
        const string SharkyFbx = ArtDir + "/Characters_Sharky.fbx";
        const string EnemyBasicPrefab = "Assets/Prefabs/TowerDefense/Units/Enemy_Basic.prefab";
        const string EnemyHeavyPrefab = "Assets/Prefabs/TowerDefense/Units/Enemy_Heavy.prefab";
        const string EraserPrefab = "Assets/Prefabs/TowerDefense/Placeables/Eraser_SoldierSpawner.prefab";
        const string WallPrefab = "Assets/Prefabs/TowerDefense/Placeables/Pencil_Wall.prefab";
        const string SoldierPrefab = "Assets/Prefabs/TowerDefense/Units/Soldier.prefab";
        const string AnimDir = "Assets/Art/TowerDefense/Animations";
        const string HenryControllerPath = AnimDir + "/Soldier_Henry.controller";
        const string SharkyControllerPath = AnimDir + "/Enemy_Sharky.controller";
        const string MatDir = "Assets/Art/TowerDefense/Materials";
        const string HoleMatPath = MatDir + "/Trapdoor_Hole.mat";

        // 見た目のサイズ（ワールド単位。マーカー幅 = 100）
        const float TrapdoorDiameter = 24f;   // Henry 1人分
        const float HoleTop = 0.6f;
        const float SoldierHeight = 18f;
        const float EnemyBasicHeight = 18f;
        const float EnemyHeavyHeight = 26f;
        const float DeathAnimTime = 1.2f;   // 倒れるアニメーションを見せてから消す
        const float WallStretch = 2.5f;

        const string SessionKey = "ARTD_TrapdoorUpdate_AutoRan_v3";

        static TowerDefenseTrapdoorUpdate()
        {
            EditorApplication.delayCall += AutoRun;
        }

        static void AutoRun()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoRun;
                return;
            }
            SessionState.SetBool(SessionKey, true);
            if (NeedsUpdate())
            {
                Debug.Log("[ARTD] トラップドア / 兵士 / 敵 / 丸太の Prefab が未更新なので自動で更新します");
                Run();
            }
            else if (NeedsTrapdoorResize())
            {
                Debug.Log("[ARTD] トラップドアの大きさを Henry 1人分に作り直します");
                var log = new StringBuilder();
                UpdateEraser(log);
                AssetDatabase.SaveAssets();
                Debug.Log("[ARTD] " + log);
            }
            else if (NeedsTrapdoorColor())
            {
                RecolorTrapdoorMenu();
            }
        }

        /// <summary>トラップドアの当たり判定の幅が、今の設定の直径と大きく違えば true。</summary>
        static bool NeedsTrapdoorResize()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(EraserPrefab);
            var box = go ? go.GetComponent<BoxCollider>() : null;
            if (!box) return false;
            return Mathf.Abs(box.size.x - TrapdoorDiameter) > TrapdoorDiameter * 0.25f;
        }

        /// <summary>トラップドアに部品ごとの色マテリアル（Trapdoor_*.mat）が付いていなければ true。</summary>
        static bool NeedsTrapdoorColor()
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(EraserPrefab);
            var door = go ? go.transform.Find("Model/DoorHinge/Trapdoor") : null;
            if (!door) return false;
            foreach (var r in door.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (!m || !m.name.StartsWith("Trapdoor_")) return true;
            return false;
        }

        [MenuItem("Tools/AR Tower Defense/6. Recolor Trapdoor", priority = 6)]
        static void RecolorTrapdoorMenu()
        {
            var root = PrefabUtility.LoadPrefabContents(EraserPrefab);
            try
            {
                var door = root.transform.Find("Model/DoorHinge/Trapdoor");
                if (!door) { Debug.LogWarning("[ARTD] トラップドアが見つかりません。先にメニュー 5 を実行してください"); return; }
                ApplyTrapdoorMaterials(door.gameObject);
                PrefabUtility.SaveAsPrefabAsset(root, EraserPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log("[ARTD] トラップドアに色を付けました（" + MatDir + "/Trapdoor_*.mat）");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // トラップドアの部品ごとの色（FBX のマテリアル名 → 色）。色は Trapdoor_*.mat を開いて Base Map の色で変えられる
        static readonly (string name, Color color)[] TrapdoorColors =
        {
            ("Wood",      new Color(0.62f, 0.38f, 0.22f)),
            ("DarkWood",  new Color(0.40f, 0.23f, 0.14f)),
            ("Metal",     new Color(0.45f, 0.44f, 0.52f)),
            ("DarkMetal", new Color(0.22f, 0.21f, 0.27f)),
        };

        /// <summary>
        /// Trapdoor.fbx はテクスチャ（UV）を持たず、Wood / DarkWood / Metal / DarkMetal の4マテリアルで色分けされている。
        /// 元の並び順どおりに、色付きのマテリアルを作って割り当てる。
        /// </summary>
        static void ApplyTrapdoorMaterials(GameObject door)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(TrapdoorFbx);
            var srcRenderer = src ? src.GetComponentInChildren<Renderer>(true) : null;
            var originals = srcRenderer ? srcRenderer.sharedMaterials : new Material[0];

            foreach (var r in door.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++)
                {
                    string n = i < originals.Length && originals[i] ? originals[i].name : "Wood";
                    mats[i] = GetColorMaterial("Trapdoor_" + n, ColorFor(n, i < originals.Length ? originals[i] : null));
                }
                r.sharedMaterials = mats;
            }
        }

        static Color ColorFor(string name, Material original)
        {
            foreach (var (n, c) in TrapdoorColors) if (n == name) return c;
            if (original && original.HasProperty("_BaseColor")) return original.GetColor("_BaseColor");
            if (original && original.HasProperty("_Color")) return original.GetColor("_Color");
            return new Color(0.6f, 0.4f, 0.25f);
        }

        /// <summary>MatDir に色付きの URP Lit マテリアルを作る（既にあればそれを使い、色は上書きしない）。</summary>
        static Material GetColorMaterial(string name, Color color)
        {
            string path = MatDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            EnsureFolder(MatDir);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            if (!sh) sh = Shader.Find("Standard");
            m = new Material(sh) { name = name };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        static bool NeedsUpdate()
        {
            bool Has(string prefab, string path)
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
                return !go || go.transform.Find(path);
            }
            return !Has(EraserPrefab, "Model/DoorHinge") || !Has(SoldierPrefab, "Model/Henry") ||
                   !Has(EnemyBasicPrefab, "Model/Sharky") || !Has(EnemyHeavyPrefab, "Model/Sharky") ||
                   !Has(WallPrefab, "Model/Stretch");
        }

        [MenuItem("Tools/AR Tower Defense/5. Update Trapdoor + Soldier (Henry) + Enemy (Sharky) + Long Log", priority = 5)]
        static void Run()
        {
            var log = new StringBuilder();
            try
            {
                var henryCtrl = SetupCharacterAnimator(HenryFbx, HenryControllerPath, log);
                ReplaceCharacterModel(SoldierPrefab, HenryFbx, "Henry", henryCtrl, SoldierHeight, log);
                var sharkyCtrl = SetupCharacterAnimator(SharkyFbx, SharkyControllerPath, log);
                ReplaceCharacterModel(EnemyBasicPrefab, SharkyFbx, "Sharky", sharkyCtrl, EnemyBasicHeight, log);
                ReplaceCharacterModel(EnemyHeavyPrefab, SharkyFbx, "Sharky", sharkyCtrl, EnemyHeavyHeight, log);
                UpdateEraser(log);
                UpdateWall(log);
                AssetDatabase.SaveAssets();
                log.AppendLine("完了");
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                log.AppendLine("× エラー: " + e.Message);
            }
            Debug.Log("[ARTD] Trapdoor / Soldier / Log 更新\n" + log);
        }

        // ---------------- Henry のアニメーション ----------------

        static AnimatorController SetupCharacterAnimator(string fbx, string controllerPath, StringBuilder log)
        {
            var imp = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!imp) { log.AppendLine("× FBX が見つかりません: " + fbx); return null; }

            // Idle / Walk / Run をループ再生にする
            var clips = imp.clipAnimations;
            bool hadCustom = clips != null && clips.Length > 0;
            if (!hadCustom) clips = imp.defaultClipAnimations;
            bool changed = !hadCustom;
            foreach (var c in clips)
            {
                bool loop = c.name.EndsWith("Idle") || c.name.EndsWith("Walk") || c.name.EndsWith("Run");
                if (c.loopTime != loop) { c.loopTime = loop; changed = true; }
            }
            if (changed)
            {
                imp.clipAnimations = clips;
                imp.SaveAndReimport();
                log.AppendLine("・" + fbx + " の Idle/Walk/Run をループに設定");
            }

            var all = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>()
                                   .Where(c => !c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Find(params string[] names)
            {
                foreach (var n in names)
                {
                    var hit = all.FirstOrDefault(x => x.name == n || x.name.EndsWith("|" + n));
                    if (hit) return hit;
                }
                return null;
            }
            var idle = Find("Idle");
            var walk = Find("Walk");
            var run = Find("Run", "Walk");
            var attack = Find("Sword", "Punch");
            var death = Find("Death");
            if (!idle) { log.AppendLine("× Idle クリップが見つかりません: " + fbx); return null; }

            EnsureFolder(AnimDir);
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)) AssetDatabase.DeleteAsset(controllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle; sm.defaultState = sIdle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk ? walk : idle;
            var sRun = sm.AddState("Run"); sRun.motion = run ? run : sWalk.motion;
            SpeedTransition(sIdle, sWalk, AnimatorConditionMode.Greater, 1f);
            SpeedTransition(sWalk, sIdle, AnimatorConditionMode.Less, 1f);
            SpeedTransition(sWalk, sRun, AnimatorConditionMode.Greater, 30f);
            SpeedTransition(sRun, sWalk, AnimatorConditionMode.Less, 30f);

            if (attack)
            {
                var sAtk = sm.AddState("Attack"); sAtk.motion = attack;
                var t = sm.AddAnyStateTransition(sAtk);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
                var back = sAtk.AddTransition(sIdle);
                back.hasExitTime = true; back.exitTime = 0.85f; back.duration = 0.1f;
            }
            if (death)
            {
                var sDie = sm.AddState("Die"); sDie.motion = death;
                var t = sm.AddAnyStateTransition(sDie);
                t.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                t.hasExitTime = false; t.duration = 0.05f; t.canTransitionToSelf = false;
            }
            EditorUtility.SetDirty(ctrl);
            log.AppendLine("・Animator Controller を作成: " + controllerPath);
            return ctrl;
        }

        static void SpeedTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, float threshold)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, threshold, "Speed");
            t.hasExitTime = false;
            t.duration = 0.15f;
        }

        // ---------------- キャラの見た目（兵士 = Henry、敵 = Sharky） ----------------

        static void ReplaceCharacterModel(string prefabPath, string fbx, string modelName, AnimatorController ctrl,
                                          float height, StringBuilder log)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            if (!asset) { log.AppendLine("× モデルが見つかりません: " + fbx); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PirateMatPath);

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var model = GetOrCreateChild(root.transform, "Model");
                ClearChildren(model);

                var chara = (GameObject)PrefabUtility.InstantiatePrefab(asset, model);
                chara.name = modelName;
                chara.transform.localPosition = Vector3.zero;
                ApplyMaterial(chara, mat);
                FitHeight(chara.transform, model, height, height / 1.6f);

                var anim = chara.GetComponent<Animator>();
                if (!anim) anim = chara.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var unit = root.GetComponent<CharacterUnit>();
                if (unit) SetRef(unit, "animator", anim);

                var cap = root.GetComponent<CapsuleCollider>();
                if (cap) { cap.height = height; cap.center = new Vector3(0f, height * 0.5f, 0f); }

                var bar = root.GetComponentInChildren<HealthBar>(true);
                if (bar) { var p = bar.transform.localPosition; p.y = height + 4f; bar.transform.localPosition = p; }

                // 倒れるアニメーションを見せてから消す（当たり判定は CharacterUnit が死亡時に切る）
                var health = root.GetComponent<Health>();
                if (health)
                {
                    var so = new SerializedObject(health);
                    var d = so.FindProperty("destroyDelay");
                    if (d != null) { d.floatValue = DeathAnimTime; so.ApplyModifiedPropertiesWithoutUndo(); }
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                log.AppendLine($"・{System.IO.Path.GetFileNameWithoutExtension(prefabPath)} の見た目を {modelName} に変更");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------------- 消しゴム = トラップドア ----------------

        static void UpdateEraser(StringBuilder log)
        {
            var trapAsset = AssetDatabase.LoadAssetAtPath<GameObject>(TrapdoorFbx);
            if (!trapAsset) { log.AppendLine("× Trapdoor が見つかりません"); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(PirateMatPath);

            var root = PrefabUtility.LoadPrefabContents(EraserPrefab);
            try
            {
                var model = GetOrCreateChild(root.transform, "Model");
                ClearChildren(model);

                // 穴（扉が開いたときに見える黒い円盤。兵士はこの下からせり上がる）
                var hole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                hole.name = "Hole";
                Object.DestroyImmediate(hole.GetComponent<Collider>());
                hole.transform.SetParent(model, false);
                hole.GetComponent<Renderer>().sharedMaterial = GetHoleMaterial();

                // 扉（Trapdoor）を直径 TrapdoorDiameter に合わせ、穴の上に置く
                var door = (GameObject)PrefabUtility.InstantiatePrefab(trapAsset, model);
                door.name = "Trapdoor";
                door.transform.localPosition = Vector3.zero;
                ApplyTrapdoorMaterials(door);

                var b = LocalBounds(door, model);
                float dia = Mathf.Max(b.size.x, b.size.z);
                if (dia < 1e-4f) door.transform.localScale = Vector3.one * 30f;
                else door.transform.localScale *= TrapdoorDiameter / dia;
                b = LocalBounds(door, model);
                door.transform.localPosition += new Vector3(-b.center.x, HoleTop + 0.2f - b.min.y, -b.center.z);
                b = LocalBounds(door, model);
                float r = Mathf.Max(b.extents.x, b.extents.z);

                // Cylinder プリミティブは高さ 2・直径 1
                hole.transform.localScale = new Vector3(r * 1.8f, HoleTop * 0.5f, r * 1.8f);
                hole.transform.localPosition = new Vector3(0f, HoleTop * 0.5f, 0f);

                // 蝶番：扉の -X 側の縁。ローカル Z 軸まわりに回すと +X 側が持ち上がって開く
                var hinge = new GameObject("DoorHinge").transform;
                hinge.SetParent(model, false);
                hinge.localPosition = new Vector3(b.min.x, b.min.y, 0f);
                hinge.localRotation = Quaternion.identity;
                door.transform.SetParent(hinge, true);

                var sp = GetOrCreateChild(root.transform, "SpawnPoint");
                sp.localPosition = new Vector3(0f, HoleTop, 0f);
                sp.localRotation = Quaternion.identity;

                var spawner = root.GetComponent<SoldierSpawner>();
                if (spawner)
                {
                    var so = new SerializedObject(spawner);
                    so.FindProperty("doorHinge").objectReferenceValue = hinge;
                    so.FindProperty("spawnPoint").objectReferenceValue = sp;
                    so.FindProperty("emergeDepth").floatValue = SoldierHeight + 2f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                var box = root.GetComponent<BoxCollider>();
                if (box) { box.center = new Vector3(0f, 3f, 0f); box.size = new Vector3(r * 2f, 6f, r * 2f); }

                var bar = root.GetComponentInChildren<HealthBar>(true);
                if (bar) bar.transform.localPosition = new Vector3(0f, 12f, 0f);

                // ダメージで色を変える対象を、扉と穴にする（HP バーは含めない）
                var tint = root.GetComponent<DamageTint>();
                if (tint)
                {
                    var targets = door.GetComponentsInChildren<Renderer>(true).ToList();
                    targets.Add(hole.GetComponent<Renderer>());
                    var so = new SerializedObject(tint);
                    var p = so.FindProperty("renderers");
                    if (p != null)
                    {
                        p.arraySize = targets.Count;
                        for (int i = 0; i < targets.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }

                PrefabUtility.SaveAsPrefabAsset(root, EraserPrefab);
                log.AppendLine("・Eraser_SoldierSpawner をトラップドアに変更");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------------- 丸太の壁 ----------------

        static void UpdateWall(StringBuilder log)
        {
            var root = PrefabUtility.LoadPrefabContents(WallPrefab);
            try
            {
                var model = GetOrCreateChild(root.transform, "Model");
                var stretch = model.Find("Stretch");
                if (!stretch)
                {
                    stretch = new GameObject("Stretch").transform;
                    stretch.SetParent(model, false);
                    foreach (var c in model.Cast<Transform>().Where(c => c != stretch).ToList())
                        c.SetParent(stretch, false);
                }
                float prev = Mathf.Max(0.01f, stretch.localScale.x);
                stretch.localScale = new Vector3(WallStretch, 1f, 1f);

                var box = root.GetComponent<BoxCollider>();
                if (box)
                {
                    var s = box.size; s.x = s.x / prev * WallStretch; box.size = s;
                    var c = box.center; c.x = c.x / prev * WallStretch; box.center = c;
                }

                var bar = root.GetComponentInChildren<HealthBar>(true);
                if (bar)
                {
                    float BarFactor(float k) => 1f + (k - 1f) * 0.5f;
                    var sc = bar.transform.localScale;
                    sc.x = sc.x / BarFactor(prev) * BarFactor(WallStretch);
                    bar.transform.localScale = sc;
                    var so = new SerializedObject(bar);
                    so.FindProperty("hideWhenFull").boolValue = false;   // 満タンでも常に表示
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, WallPrefab);
                log.AppendLine($"・Pencil_Wall の丸太を横に {WallStretch} 倍、HP バーを常に表示");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        // ---------------- ヘルパー ----------------

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) return t;
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Object.DestroyImmediate(t.GetChild(i).gameObject);
        }

        static void ApplyMaterial(GameObject go, Material mat)
        {
            if (!mat) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>go の描画範囲を space のローカル座標で求める。</summary>
        static Bounds LocalBounds(GameObject go, Transform space)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool any = false;
            var result = new Bounds();
            foreach (var r in rs)
            {
                var wb = r.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? wb.min.x : wb.max.x,
                        (i & 2) == 0 ? wb.min.y : wb.max.y,
                        (i & 4) == 0 ? wb.min.z : wb.max.z);
                    var lp = space.InverseTransformPoint(corner);
                    if (!any) { result = new Bounds(lp, Vector3.zero); any = true; }
                    else result.Encapsulate(lp);
                }
            }
            return result;
        }

        /// <summary>高さを targetHeight に合わせ、足元を y=0・中心を x,z=0 に置く。</summary>
        static void FitHeight(Transform t, Transform space, float targetHeight, float fallbackScale)
        {
            var b = LocalBounds(t.gameObject, space);
            if (b.size.y < 1e-4f) t.localScale = Vector3.one * fallbackScale;
            else t.localScale *= targetHeight / b.size.y;
            b = LocalBounds(t.gameObject, space);
            if (b.size.y > 1e-4f) t.localPosition += new Vector3(-b.center.x, -b.min.y, -b.center.z);
        }

        static void SetRef(Object target, string prop, Object value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(prop);
            if (p == null) return;
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Material GetHoleMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(HoleMatPath);
            if (m) return m;
            EnsureFolder(MatDir);
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            if (!sh) sh = Shader.Find("Unlit/Color");
            if (!sh) sh = Shader.Find("Standard");
            m = new Material(sh);
            var black = new Color(0.03f, 0.02f, 0.02f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", black);
            if (m.HasProperty("_Color")) m.SetColor("_Color", black);
            AssetDatabase.CreateAsset(m, HoleMatPath);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            string parent = path.Substring(0, i);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
        }
    }
}
