using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARTD.EditorTools
{
    /// <summary>開いているシーンの book_image マーカーに BookTerrain（本の透明コライダー）を付ける。</summary>
    [InitializeOnLoad]
    static class TowerDefenseUpdate5Request
    {
        const string RequestFile = "ARTD_Update5.request";
        static double nextCheck;

        static TowerDefenseUpdate5Request()
        {
            EditorApplication.update += () =>
            {
                if (EditorApplication.timeSinceStartup < nextCheck) return;
                nextCheck = EditorApplication.timeSinceStartup + 2.0;
                if (!File.Exists(RequestFile)) return;
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.Delete(RequestFile);
                string log = "";
                try
                {
                    var s = SceneManager.GetActiveScene();
                    bool wasDirty = s.isDirty;
                    foreach (var root in s.GetRootGameObjects())
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                            if (t.name == "book_image")
                            {
                                var bt = t.GetComponent<BookTerrain>();
                                if (!bt) bt = Undo.AddComponent<BookTerrain>(t.gameObject);
                                var so = new SerializedObject(bt);
                                so.FindProperty("widthCm").floatValue = 15f;
                                so.ApplyModifiedProperties();
                                var col = bt.EnsureCollider();
                                EditorUtility.SetDirty(bt);
                                log += $"+ BookTerrain + {col.name} on {t.name} (size {col.size}, center {col.center})\n";
                            }
                    EditorSceneManager.MarkSceneDirty(s);
                    if (!wasDirty) { EditorSceneManager.SaveScene(s); log += "Scene saved: " + s.path + "\n"; }
                    else log += "Scene had unsaved changes -> NOT saved\n";
                }
                catch (Exception e) { log = "ERROR: " + e; }
                File.WriteAllText("Logs/ARTD_Update5.log", log);
                Debug.Log("[ARTD] Update5\n" + log);
            };
        }
    }
}
