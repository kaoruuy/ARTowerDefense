using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ARTD.EditorTools
{
    /// <summary>
    /// ブルームを使えるようにする。ProjectSettings / URP アセット / プレハブは変更しない。
    /// - シーン内のカメラ: HDR と Post Processing を ON
    /// - シーンに Bloom 入りの Volume が既にあればそれを使う（Intensity が 0 なら 1.5 にする）。
    ///   無ければ PostFX_Bloom プロファイル + PostProcessVolume プレハブを作って置く
    /// - 仮エフェクトのマテリアルを HDR の明るさにして光らせる
    /// </summary>
    public static partial class TowerDefenseBuilder
    {
        public const string BloomProfilePath = "Assets/Art/TowerDefense/PostFX_Bloom.asset";

        [MenuItem("Tools/AR Tower Defense/5. Enable Bloom In Current Scene", priority = 5)]
        static void BloomMenu()
        {
            Debug.Log("[ARTD] Bloom\n" + SetupBloom(SceneManager.GetActiveScene()));
        }

        public static string SetupBloom(Scene s)
        {
            var log = new System.Text.StringBuilder();
            log.AppendLine("Bloom: " + s.path);

            // 1) シーンに既にある Bloom 入り Volume を探す
            Volume existing = null;
            foreach (var v in FindInScene<Volume>(s))
                if (v.sharedProfile && v.sharedProfile.TryGet<Bloom>(out _)) { existing = v; break; }

            if (existing)
            {
                existing.sharedProfile.TryGet<Bloom>(out var b);
                log.AppendLine($"  = 既存の Volume '{existing.name}' (profile {AssetDatabase.GetAssetPath(existing.sharedProfile)}) を使用");
                if (!b.active || !b.intensity.overrideState || b.intensity.value <= 0f)
                {
                    Undo.RecordObject(b, "AR TD Bloom");
                    b.active = true;
                    b.intensity.Override(1.5f);
                    EditorUtility.SetDirty(b);
                    EditorUtility.SetDirty(existing.sharedProfile);
                    log.AppendLine("  * Bloom Intensity 0 -> 1.5");
                }
                log.AppendLine($"    threshold={b.threshold.value} intensity={b.intensity.value} scatter={b.scatter.value}");
            }
            else
            {
                EnsureFolder("Assets/Art/TowerDefense");
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(BloomProfilePath);
                if (!profile)
                {
                    profile = ScriptableObject.CreateInstance<VolumeProfile>();
                    AssetDatabase.CreateAsset(profile, BloomProfilePath);
                }
                if (!profile.TryGet<Bloom>(out var bloom))
                {
                    bloom = profile.Add<Bloom>(true);
                    bloom.name = "Bloom";
                    bloom.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                    AssetDatabase.AddObjectToAsset(bloom, profile);
                    bloom.threshold.Override(1f);
                    bloom.intensity.Override(1.5f);
                    bloom.scatter.Override(0.7f);
                    EditorUtility.SetDirty(profile);
                }
                string volPath = P("Systems", "PostProcessVolume");
                var volPrefab = Load(volPath);
                if (!volPrefab)
                {
                    var go = new GameObject("PostProcessVolume");
                    var v = go.AddComponent<Volume>();
                    v.isGlobal = true;
                    v.priority = 10;
                    v.sharedProfile = profile;
                    volPrefab = PrefabUtility.SaveAsPrefabAsset(go, volPath);
                    Object.DestroyImmediate(go);
                }
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(volPrefab, s);
                Undo.RegisterCreatedObjectUndo(inst, "AR TD Bloom");
                log.AppendLine("  + PostProcessVolume (" + BloomProfilePath + ")");
            }

            // 2) シーン内のカメラ
            foreach (var cam in FindInScene<Camera>(s))
            {
                Undo.RecordObject(cam, "AR TD Bloom");
                cam.allowHDR = true;
                EditorUtility.SetDirty(cam);
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data)
                {
                    Undo.RecordObject(data, "AR TD Bloom");
                    data.renderPostProcessing = true;
                    EditorUtility.SetDirty(data);
                }
                if (PrefabUtility.IsPartOfPrefabInstance(cam))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(cam);
                    if (data) PrefabUtility.RecordPrefabInstancePropertyModifications(data);
                }
                log.AppendLine("  * camera " + cam.name + ": HDR + Post Processing ON");
            }

            // 3) 仮エフェクトを HDR で光らせる
            foreach (var n in new[] { "FX_Hit", "FX_Explosion", "FX_Muzzle", "FX_EnemySpawn", "FX_SoldierSpawn", "FX_Place" })
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>($"{MatDir}/{n}.mat");
                if (!m || !m.HasProperty("_BaseColor")) continue;
                var c = m.GetColor("_BaseColor");
                if (c.maxColorComponent > 1.01f) continue;
                m.SetColor("_BaseColor", new Color(c.r * 3f, c.g * 3f, c.b * 3f, c.a));
                EditorUtility.SetDirty(m);
                log.AppendLine("  * " + n + " -> HDR x3");
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(s);
            return log.ToString();
        }
    }

    /// <summary>プロジェクト直下に ARTD_Bloom.request（中身 = シーンパス）があれば一度だけ実行する。</summary>
    [InitializeOnLoad]
    static class TowerDefenseBloomRequest
    {
        const string RequestFile = "ARTD_Bloom.request";
        static double nextCheck;

        static TowerDefenseBloomRequest()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 2.0;
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                Run();
            };
        }

        static void Run()
        {
            string scenePath = File.ReadAllText(RequestFile).Trim();
            File.Delete(RequestFile);
            string log;
            try
            {
                Scene scene = default;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == scenePath) scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    log = "Scene not open: " + scenePath + " -> skipped (open it and use the menu)\n";
                }
                else
                {
                    bool wasDirty = scene.isDirty;
                    log = TowerDefenseBuilder.SetupBloom(scene);
                    if (wasDirty) log += "Scene had unsaved changes -> NOT saved (Ctrl+S で保存してください)\n";
                    else { EditorSceneManager.SaveScene(scene); log += "Scene saved.\n"; }
                }
            }
            catch (System.Exception e) { log = "ERROR: " + e; }
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ARTD_Bloom.log", log);
            Debug.Log("[ARTD] Bloom\n" + log);
        }
    }
}
