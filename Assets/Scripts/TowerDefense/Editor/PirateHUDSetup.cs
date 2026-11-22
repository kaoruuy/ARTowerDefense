using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ARTD.EditorTools
{
    /// <summary>Upgrades the existing UI prefab in place, preserving its references and file IDs.</summary>
    [InitializeOnLoad]
    public static class PirateHUDSetup
    {
        const string PrefabPath = "Assets/Prefabs/TowerDefense/UI/GameUI.prefab";
        const string RequestPath = "Temp/ARTD_PirateUI.request";
        const string LogPath = "Logs/ARTD_PirateUI.log";
        static double nextCheck;

        static PirateHUDSetup()
        {
            EditorApplication.update += CheckRequest;
        }

        static void CheckRequest()
        {
            if (EditorApplication.timeSinceStartup < nextCheck) return;
            nextCheck = EditorApplication.timeSinceStartup + 1;
            if (File.Exists(RequestPath)) ApplyRequested();
        }

        static void ApplyRequested()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            File.Delete(RequestPath);
            try { Apply(); File.WriteAllText(LogPath, DateTime.Now + "\nPirate HUD applied successfully.\n"); }
            catch (Exception e) { File.WriteAllText(LogPath, e.ToString()); Debug.LogException(e); }
        }

        [MenuItem("Tools/AR Tower Defense/6. Apply Pirate HUD", priority = 6)]
        public static void Apply()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var ui = root.GetComponent<GameUI>();
                if (!ui) throw new InvalidOperationException("GameUI component is missing.");
                var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/DotGothic16/DotGothic16-Regular.ttf");
                if (!font) throw new InvalidOperationException("DotGothic16 font is missing.");
                foreach (var text in root.GetComponentsInChildren<Text>(true)) text.font = font;
                ui.ApplyPirateTheme();
                var so = new SerializedObject(ui);
                so.FindProperty("setupNoTowerMessage").stringValue = "宝箱を配置しよう！";
                so.FindProperty("setupReadyMessage").stringValue = "守備隊を配備せよ！";
                so.FindProperty("gameOverMessage").stringValue = "宝箱が破壊されました";
                so.FindProperty("victoryMessage").stringValue = "宝箱を守り抜きました";
                so.FindProperty("trackingPausedMessage").stringValue = "宝箱のマーカーを映そう！";
                so.FindProperty("toastDuration").floatValue = 2.5f;
                TextField(so, "towerHpText").text = "未配置";
                TextField(so, "waveText").text = "WAVE 1/5";
                TextField(so, "scoreText").text = "";
                TextField(so, "budgetText").text = "100/100";
                TextField(so, "messageText").text = so.FindProperty("setupNoTowerMessage").stringValue;
                TextField(so, "toastText").text = "";
                ((GameObject)so.FindProperty("resultPanel").objectReferenceValue).SetActive(false);
                ((Button)so.FindProperty("startButton").objectReferenceValue).interactable = false;
                so.ApplyModifiedPropertiesWithoutUndo();
                var view = root.GetComponent<PirateHUD>();
                PirateHUDArt.Apply(view);
                view.Refresh(GameState.Setup, false, false, 0, null, 0);
                view.Layout(new Vector2(1280, 720));
                var report = new System.Text.StringBuilder();
                foreach (var state in new[] { GameState.Setup, GameState.Playing, GameState.GameOver, GameState.Victory })
                foreach (var size in new[] { new Vector2(1280, 720), new Vector2(720, 1280), new Vector2(1024, 768) })
                {
                    bool ended = state == GameState.Victory || state == GameState.GameOver;
                    ((GameObject)so.FindProperty("resultPanel").objectReferenceValue).SetActive(ended);
                    TextField(so, "messageText").text = state == GameState.Setup ? "宝箱を配置しよう！" : "戦闘開始！防御せよ！";
                    TextField(so, "towerHpText").text = "750/1000";
                    TextField(so, "resultText").text = state == GameState.Victory ? "宝箱を守り抜きました" : "宝箱が破壊されました";
                    view.Refresh(state, false, state == GameState.Playing, .75f, null, 0);
                    view.Layout(size);
                    report.AppendLine($"{state}: Layout {size.x}x{size.y}");
                    foreach (var text in root.GetComponentsInChildren<Text>())
                        if (!string.IsNullOrEmpty(text.text))
                            report.AppendLine($"{text.name}: font={text.fontSize}, box={text.rectTransform.rect.size}, requiredHeight={text.preferredHeight:0.0}");
                }
                File.WriteAllText("Logs/ARTD_PirateUI_Layout.txt", report.ToString());
                TextField(so, "towerHpText").text = "未配置";
                TextField(so, "messageText").text = "宝箱を配置しよう！";
                TextField(so, "resultText").text = "";
                ((GameObject)so.FindProperty("resultPanel").objectReferenceValue).SetActive(false);
                view.Refresh(GameState.Setup, false, false, 0, null, 0);
                view.Layout(new Vector2(1280, 720));
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[ARTD] Pirate HUD updated. Existing button bindings and scene references preserved.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static Text TextField(SerializedObject so, string name) => (Text)so.FindProperty(name).objectReferenceValue;

    }
}
