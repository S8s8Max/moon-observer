using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using MoonObserver.Rendering;
using MoonObserver.Atmospheric;
using MoonObserver.VR;

/// <summary>
/// Unity メニュー「MoonObserver > Build Moon Scene」でシーンを自動構築する
/// </summary>
public static class MoonSceneBuilder
{
    [MenuItem("MoonObserver/Build Moon Scene")]
    public static void BuildScene()
    {
        if (!EditorUtility.DisplayDialog("Moon Scene Builder",
            "MoonViewer シーンを自動構築します。\n現在の未保存の変更は失われます。続けますか？",
            "はい、構築する", "キャンセル"))
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Materials フォルダ確保 ───────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var moonSurfaceMat    = GetOrCreateMaterial("Assets/Materials/MoonSurface.mat",    "MoonObserver/MoonSurface");
        var moonAtmosphereMat = GetOrCreateMaterial("Assets/Materials/MoonAtmosphere.mat", "MoonObserver/MoonAtmosphere");

        // ── 1. Directional Light (Sun) ───────────────────────────────────
        var sunGO    = new GameObject("Sun");
        var sunLight = sunGO.AddComponent<Light>();
        sunLight.type      = LightType.Directional;
        sunLight.color     = new Color(1.0f, 0.95f, 0.8f);
        sunLight.intensity = 1.0f;
        sunLight.shadows   = LightShadows.Soft;
        sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // ── 2. XR Origin (VR) ───────────────────────────────────────────
        // ExecuteMenuItem は非同期でシーン状態を壊すケースがあるため手動誘導のみ
        Debug.Log("[MoonSceneBuilder] XR Origin は手動で追加してください:\n" +
                  "  GameObject > XR > XR Origin (VR)");

        // ── 3. Moon (MoonRenderer + MeshRenderer + MeshFilter) ──────────
        var moonGO       = new GameObject("Moon");
        moonGO.AddComponent<MeshFilter>();
        var meshRenderer = moonGO.AddComponent<MeshRenderer>();
        meshRenderer.material = moonSurfaceMat;
        var moonRendererComp = moonGO.AddComponent<MoonRenderer>();
        moonRendererComp.moonMaterial        = moonSurfaceMat;
        moonRendererComp.placementDistanceM  = 1000f;
        moonRendererComp.sunLight            = sunLight;
        moonGO.transform.position = new Vector3(0f, 0f, 1000f);

        // ── 4. Atmosphere Effects ────────────────────────────────────────
        var atmosphereGO = new GameObject("Atmosphere Effects");
        var atmEffects   = atmosphereGO.AddComponent<AtmosphericEffects>();
        atmEffects.moonAtmosphereMaterial = moonAtmosphereMat;
        atmEffects.moonRenderer           = meshRenderer; // UnityEngine.Renderer

        // ── 5. Moon Glow (Point Light) ───────────────────────────────────
        var glowGO    = new GameObject("Moon Glow");
        var glowLight = glowGO.AddComponent<Light>();
        glowLight.type      = LightType.Point;
        glowLight.range     = 200f;
        glowLight.intensity = 1.5f;
        glowLight.color     = Color.white;
        glowGO.transform.position = Vector3.zero;

        // ── 6. UI Canvas (World Space 情報パネル) ──────────────────────
        var canvasGO = new GameObject("UI Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta    = new Vector2(500, 400);
        canvasGO.transform.position   = new Vector3(0f, 1.6f, 2.5f);
        canvasGO.transform.localScale = Vector3.one * 0.001f;

        // Info Panel (背景)
        var panelGO    = new GameObject("Info Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRect  = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 400);
        var panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        panelGO.SetActive(false); // A ボタンで表示

        // テキスト行
        var altText   = CreateText(panelGO, "Altitude Text",     new Vector2(0,  140));
        var azText    = CreateText(panelGO, "Azimuth Text",      new Vector2(0,   90));
        var ageText   = CreateText(panelGO, "Moon Age Text",     new Vector2(0,   40));
        var illumText = CreateText(panelGO, "Illumination Text", new Vector2(0,  -10));
        var distText  = CreateText(panelGO, "Distance Text",     new Vector2(0,  -60));
        var timeText  = CreateText(panelGO, "Current Time Text", new Vector2(0, -110));

        // ── 7. VRMoonViewer Controller ───────────────────────────────────
        var viewerGO = new GameObject("VRMoonViewer");
        var viewer   = viewerGO.AddComponent<VRMoonViewer>();
        viewer.moonRenderer        = moonRendererComp;
        viewer.moonTransform       = moonGO.transform;
        viewer.atmosphericEffects  = atmEffects;
        viewer.infoPanel           = panelGO;
        viewer.altitudeText        = altText;
        viewer.azimuthText         = azText;
        viewer.moonAgeText         = ageText;
        viewer.illuminationText    = illumText;
        viewer.distanceText        = distText;
        viewer.currentTimeText     = timeText;

        // ── シーン保存 ─────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string scenePath = "Assets/Scenes/MoonViewer.unity";
        EditorSceneManager.SaveScene(scene, scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("構築完了！",
            $"MoonViewer シーンを作成しました。\n{scenePath}\n\n" +
            "残り手順:\n" +
            "1. GameObject > XR > XR Origin (VR) を追加\n" +
            "2. XR Device Simulator プレハブを Hierarchy にドラッグ\n" +
            "3. ▶ Play で実行",
            "OK");

        Debug.Log("[MoonSceneBuilder] シーン構築完了: " + scenePath);
    }

    // ── ヘルパー ──────────────────────────────────────────────────────────

    static Material GetOrCreateMaterial(string path, string shaderName)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find(shaderName)
                  ?? Shader.Find("Universal Render Pipeline/Lit");
        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static TextMeshProUGUI CreateText(GameObject parent, string objName, Vector2 anchoredPos)
    {
        var go   = new GameObject(objName);
        go.transform.SetParent(parent.transform, false);
        var rect = go.AddComponent<RectTransform>();
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta        = new Vector2(460, 40);
        var tmp  = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = objName;
        tmp.fontSize = 28;
        tmp.color    = Color.white;
        return tmp;
    }
}
