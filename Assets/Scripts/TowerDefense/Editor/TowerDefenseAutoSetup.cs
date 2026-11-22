using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ARTD.EditorTools
{
    /// <summary>
    /// プロジェクト直下に ARTD_AutoSetup.request があると、コンパイル後に一度だけ
    /// プレハブ生成 + yamada_test シーンのセットアップを実行する（外部ツールからの自動化用）。
    /// 結果は Logs/ARTD_AutoSetup.log に書く。通常はメニュー Tools > AR Tower Defense を使えばよい。
    /// </summary>
    [InitializeOnLoad]
    static class TowerDefenseAutoSetup
    {
        const string RequestFile = "ARTD_AutoSetup.request";
        const string LogFile = "Logs/ARTD_AutoSetup.log";
        const string DefaultScene = "Assets/yamada_test.unity";

        static TowerDefenseAutoSetup()
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

            string scenePath = File.ReadAllText(RequestFile).Trim();
            if (string.IsNullOrEmpty(scenePath)) scenePath = DefaultScene;
            File.Delete(RequestFile);

            var log = new StringBuilder();
            log.AppendLine("ARTD AutoSetup " + DateTime.Now);
            try
            {
                Scene scene = default;
                bool wasDirty = false;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (s.path == scenePath) { scene = s; wasDirty = s.isDirty; }
                }

                TowerDefenseBuilder.Build(false);
                log.AppendLine("Build: OK");

                bool opened = false;
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                    opened = true;
                    wasDirty = false;
                }

                log.Append(TowerDefenseBuilder.SetupScene(scene));

                if (wasDirty)
                {
                    log.AppendLine("Scene had unsaved changes before setup -> NOT saved. Save it manually (Ctrl+S).");
                }
                else
                {
                    EditorSceneManager.SaveScene(scene);
                    log.AppendLine("Scene saved.");
                }
                if (opened && SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
                log.AppendLine("DONE");
            }
            catch (Exception e)
            {
                log.AppendLine("ERROR: " + e);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllText(LogFile, log.ToString());
            UnityEngine.Debug.Log("[ARTD] AutoSetup\n" + log);
        }
    }
}
