using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
using TMPro;
using MoonObserver.Rendering;
using MoonObserver.Atmospheric;
using MoonObserver.VR;

/// <summary>
/// Unity メニュー「MoonObserver > Build Moon Scene」でシーンを完全自動構築する。
/// XR Origin・XR Device Simulator・TMP・テクスチャまで一括セットアップ。
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

        // ── 0. TMP Essential Resources ───────────────────────────────────
        EnsureTMPResources();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

        // ── 2. XR Origin (VR) ── 手動生成 ───────────────────────────────
        var xrOriginGO = BuildXROrigin(out var vrCamera);

        // ── 3. Moon ─────────────────────────────────────────────────────
        var moonGO       = new GameObject("Moon");
        moonGO.AddComponent<MeshFilter>();
        var meshRenderer = moonGO.AddComponent<MeshRenderer>();
        meshRenderer.material = moonSurfaceMat;
        var moonRendererComp = moonGO.AddComponent<MoonRenderer>();
        moonRendererComp.moonMaterial       = moonSurfaceMat;
        moonRendererComp.placementDistanceM = 600f; // 星球 (800m) より手前
        moonRendererComp.sunLight           = sunLight;
        moonGO.transform.position = new Vector3(0f, 0f, 600f);

        // テクスチャを自動割り当て
        AutoAssignTextures(moonRendererComp);

        // ── 4. Atmosphere Effects ────────────────────────────────────────
        var atmosphereGO = new GameObject("Atmosphere Effects");
        var atmEffects   = atmosphereGO.AddComponent<AtmosphericEffects>();
        atmEffects.moonAtmosphereMaterial = moonAtmosphereMat;
        atmEffects.moonRenderer           = meshRenderer;

        // ── 5. Moon Glow (Point Light) ───────────────────────────────────
        var glowGO    = new GameObject("Moon Glow");
        var glowLight = glowGO.AddComponent<Light>();
        glowLight.type      = LightType.Point;
        glowLight.range     = 200f;
        glowLight.intensity = 1.5f;
        glowLight.color     = Color.white;

        // ── 5b. Night Sky Controller ─────────────────────────────────────
        var nightSkyGO  = new GameObject("Night Sky");
        var nightSkyCon = nightSkyGO.AddComponent<NightSkyController>();
        nightSkyCon.moonGlowLight = glowLight;

        // ── 6. UI Canvas ─────────────────────────────────────────────────
        var canvasGO = new GameObject("UI Canvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
        canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta          = new Vector2(500, 400);
        canvasGO.transform.position   = new Vector3(0f, 1.6f, 2.5f);
        canvasGO.transform.localScale = Vector3.one * 0.001f;

        var panelGO    = new GameObject("Info Panel");
        panelGO.transform.SetParent(canvasGO.transform, false);
        var panelRect  = panelGO.AddComponent<RectTransform>();
        panelRect.sizeDelta = new Vector2(500, 400);
        var panelImage = panelGO.AddComponent<UnityEngine.UI.Image>();
        panelImage.color = new Color(0f, 0f, 0f, 0.75f);
        panelGO.SetActive(false);

        var altText   = CreateText(panelGO, "Altitude Text",     new Vector2(0,  140));
        var azText    = CreateText(panelGO, "Azimuth Text",      new Vector2(0,   90));
        var ageText   = CreateText(panelGO, "Moon Age Text",     new Vector2(0,   40));
        var illumText = CreateText(panelGO, "Illumination Text", new Vector2(0,  -10));
        var distText  = CreateText(panelGO, "Distance Text",     new Vector2(0,  -60));
        var timeText  = CreateText(panelGO, "Current Time Text", new Vector2(0, -110));

        // ── 7. VRMoonViewer ──────────────────────────────────────────────
        var viewerGO = new GameObject("VRMoonViewer");
        var viewer   = viewerGO.AddComponent<VRMoonViewer>();
        viewer.moonRenderer       = moonRendererComp;
        viewer.moonTransform      = moonGO.transform;
        viewer.atmosphericEffects = atmEffects;
        viewer.infoPanel          = panelGO;
        viewer.altitudeText       = altText;
        viewer.azimuthText        = azText;
        viewer.moonAgeText        = ageText;
        viewer.illuminationText   = illumText;
        viewer.distanceText       = distText;
        viewer.currentTimeText    = timeText;

        // ── 8. Moon Direction Indicator ──────────────────────────────────
        var indicatorGO = new GameObject("Moon Direction Indicator Host");
        var indicator   = indicatorGO.AddComponent<MoonDirectionIndicator>();
        indicator.moonTransform = moonGO.transform;
        if (vrCamera != null) indicator.vrCamera = vrCamera;
        viewer.moonIndicator = indicator;

        // ── 9. XR Device Simulator ───────────────────────────────────────
        InstantiateXRDeviceSimulator();

        // ── シーン保存 ────────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string scenePath = "Assets/Scenes/MoonViewer.unity";
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("構築完了！",
            $"シーンを作成しました: {scenePath}\n\n▶ Play で即実行できます。",
            "OK");

        Debug.Log("[MoonSceneBuilder] シーン構築完了: " + scenePath);
    }

    // ── XR Origin を手動生成 ─────────────────────────────────────────────
    static GameObject BuildXROrigin(out Camera vrCamera)
    {
        var xrOriginGO = new GameObject("XR Origin (VR)");
        var xrOrigin   = xrOriginGO.AddComponent<XROrigin>();
        xrOrigin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;

        // Camera Offset
        var camOffsetGO = new GameObject("Camera Offset");
        camOffsetGO.transform.SetParent(xrOriginGO.transform, false);

        // Main Camera
        var camGO = new GameObject("Main Camera");
        camGO.transform.SetParent(camOffsetGO.transform, false);
        vrCamera              = camGO.AddComponent<Camera>();
        vrCamera.tag          = "MainCamera";
        vrCamera.farClipPlane = 2000f;
        camGO.AddComponent<AudioListener>();
        // PC テスト用マウス視点 (Quest 3 実機では XR トラッキングが上書きする)
        camGO.AddComponent<FreeLookCamera>();

        // XROrigin へ参照を設定
        xrOrigin.CameraFloorOffsetObject = camOffsetGO;
        xrOrigin.Camera = vrCamera;

        return xrOriginGO;
    }

    // ── XR Device Simulator プレハブを自動配置 ───────────────────────────
    static void InstantiateXRDeviceSimulator()
    {
        // Samples フォルダ内のプレハブを検索
        string[] guids = AssetDatabase.FindAssets("XR Device Simulator t:Prefab");
        if (guids.Length == 0)
        {
            Debug.Log("[MoonSceneBuilder] XR Device Simulator プレハブが見つかりません。\n" +
                      "Package Manager → XR Interaction Toolkit → Samples → XR Device Simulator → Import");
            return;
        }

        string path   = AssetDatabase.GUIDToAssetPath(guids[0]);
        var    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
        {
            PrefabUtility.InstantiatePrefab(prefab);
            Debug.Log("[MoonSceneBuilder] XR Device Simulator を自動追加しました: " + path);
        }
    }

    // ── TMP Essential Resources を自動インポート ─────────────────────────
    static void EnsureTMPResources()
    {
        // すでにインポート済みなら何もしない
        if (AssetDatabase.IsValidFolder("Assets/TextMesh Pro")) return;

        // Unity 6 の TMP パッケージパス (com.unity.ugui に統合)
        string[] candidates = new[]
        {
            "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage",
            "Packages/com.unity.textmeshpro/Package Resources/TMP Essential Resources.unitypackage",
        };

        foreach (var pkg in candidates)
        {
            if (File.Exists(pkg))
            {
                AssetDatabase.ImportPackage(pkg, false);
                Debug.Log("[MoonSceneBuilder] TMP Essential Resources をインポートしました。");
                return;
            }
        }

        Debug.LogWarning("[MoonSceneBuilder] TMP パッケージが見つかりません。\n" +
                         "Window > TextMeshPro > Import TMP Essential Resources を手動で実行してください。");
    }

    // ── テクスチャ自動割り当て ────────────────────────────────────────────
    static void AutoAssignTextures(MoonRenderer renderer)
    {
        // Assets/Textures/Moon/ 以下の画像を検索
        string[] colorGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/Moon" });
        foreach (var guid in colorGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLower();

            if (name.Contains("color") || name.Contains("lroc"))
            {
                renderer.colorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log("[MoonSceneBuilder] カラーマップを自動割り当て: " + path);
            }
            else if (name.Contains("normal") || name.Contains("bump") || name.Contains("ldem"))
            {
                renderer.normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Debug.Log("[MoonSceneBuilder] 法線マップを自動割り当て: " + path);
            }
        }
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────

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
