using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ARTD.EditorTools
{
    /// <summary>Reuses the supplied Pirate Kit models as static HUD portraits (no runtime cameras).</summary>
    public static class PirateHUDArt
    {
        const string IconDirectory = "Assets/Art/TowerDefense/UI";

        public static void Apply(PirateHUD hud)
        {
            var swords = Icon("UI_Swords");
            var shark = Icon("Characters_Sharky");
            var skeleton = Icon("Characters_Skeleton");
            var sharks = new[] { "Enemy_Basic", "Enemy_Heavy", "Enemy_Giant" }
                .Select(name => AssetDatabase.LoadAssetAtPath<CharacterStats>($"Assets/Data/TowerDefense/{name}_Stats.asset")).ToArray();
            var skull = AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Data/TowerDefense/Enemy_Skeleton_Stats.asset");
            if (sharks.Any(s => !s) || !skull) throw new InvalidOperationException("Enemy HUD stats are missing.");
            hud.SetArt(swords, shark, skeleton, sharks, skull);
        }

        static Sprite Icon(string modelName)
        {
            string path = $"{IconDirectory}/{modelName}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing && AssetImporter.GetAtPath(path).userData == "PirateHUDIcon-v3") return existing;
            Directory.CreateDirectory(IconDirectory);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Art/PirateKit/{modelName}.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/PirateKit/PirateKit.mat");
            if (!prefab || !material) throw new InvalidOperationException($"Missing Pirate Kit model/material: {modelName}");
            var preview = new PreviewRenderUtility();
            Texture2D pixels = null;
            try
            {
                var model = Object.Instantiate(prefab);
                preview.AddSingleGO(model);
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                var renderers = model.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) throw new InvalidOperationException($"No renderer: {modelName}");
                Bounds bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                var camera = preview.camera;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.clear;
                camera.orthographic = true;
                camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.15f;
                float distance = Mathf.Max(bounds.size.magnitude * 2, 1);
                camera.transform.position = bounds.center + new Vector3(0, 0, distance);
                camera.transform.LookAt(bounds.center);
                camera.nearClipPlane = .01f; camera.farClipPlane = distance * 4;
                preview.lights[0].intensity = 1.2f;
                preview.lights[0].transform.rotation = Quaternion.Euler(35, -25, 0);
                preview.lights[1].intensity = .7f;
                preview.ambientColor = new Color(.65f, .65f, .65f);
                float previewSize = 256 / EditorGUIUtility.pixelsPerPoint;
                preview.BeginPreview(new Rect(0, 0, previewSize, previewSize), GUIStyle.none);
                camera.pixelRect = new Rect(0, 0, 256, 256); camera.aspect = 1;
                preview.Render(true);
                var texture = preview.EndPreview();
                var previous = RenderTexture.active;
                try
                {
                    RenderTexture.active = (RenderTexture)texture;
                    pixels = new Texture2D(256, 256, TextureFormat.RGBA32, false);
                    pixels.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; }
            }
            finally { if (pixels) Object.DestroyImmediate(pixels); preview.Cleanup(); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.userData = "PirateHUDIcon-v3";
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
