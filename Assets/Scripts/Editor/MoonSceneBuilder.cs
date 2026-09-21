using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.SceneManagement;
using Unity.XR.CoreUtils;
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

        // ── 0. URP パイプラインを有効化 ──────────────────────────────────
        // URP アセットが未割り当てだと Built-in RP で描画され、
        // URP シェーダーがすべてピンクになるため最初に保証する
        URPSetup.EnsureURPAssigned();
        URPSetup.LogActivePipeline();

        // ── 0b. TMP Essential Resources ──────────────────────────────────
        EnsureTMPResources();

        // ── 0c. シェーダーを検証 (コンパイルエラーを可視化) ───────────────
        ValidateAllShaders();

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Materials フォルダ確保 ───────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        var moonSurfaceMat    = GetOrCreateMaterial("Assets/Materials/MoonSurface.mat",
                                                    "MoonObserver/MoonSurface",
                                                    "Universal Render Pipeline/Lit");
        var moonAtmosphereMat = GetOrCreateMaterial("Assets/Materials/MoonAtmosphere.mat",
                                                    "MoonObserver/MoonAtmosphere",
                                                    "Universal Render Pipeline/Unlit");

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
        // エディター上では material を使うとインスタンスがシーンにリークするため sharedMaterial
        meshRenderer.sharedMaterial = moonSurfaceMat;
        var moonRendererComp = moonGO.AddComponent<MoonRenderer>();
        moonRendererComp.moonMaterial       = moonSurfaceMat;
        moonRendererComp.placementDistanceM = 600f; // 星球 (800m) より手前
        moonRendererComp.sunLight           = sunLight;
        moonGO.transform.position = new Vector3(0f, 0f, 600f);

        // テクスチャを自動割り当て (MoonRenderer とマテリアル両方へ)
        AutoAssignTextures(moonRendererComp, moonSurfaceMat);

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

        // ── 5c. Horizon Reference (地上グリッド + 方位 N/E/S/W) ──────────
        var horizonGO = new GameObject("Horizon Reference");
        horizonGO.AddComponent<HorizonReference>();

        // ── 5d. Moon Path (月の日周軌道) ─────────────────────────────────
        var pathGO   = new GameObject("Moon Path Renderer");
        var moonPath = pathGO.AddComponent<MoonPathRenderer>();
        moonPath.pathDistance = moonRendererComp.placementDistanceM;

        // ── 6. VRMoonViewer ──────────────────────────────────────────────
        var viewerGO = new GameObject("VRMoonViewer");
        var viewer   = viewerGO.AddComponent<VRMoonViewer>();
        viewer.moonRenderer       = moonRendererComp;
        viewer.moonTransform      = moonGO.transform;
        viewer.atmosphericEffects = atmEffects;
        viewer.nightSky           = nightSkyCon;
        viewer.moonPath           = moonPath;

        // ── 7. HUD (観測情報 + 時刻スクラバー) ───────────────────────────
        var hudGO = new GameObject("Observation HUD");
        var hud   = hudGO.AddComponent<MoonObserver.UI.ObservationHUD>();
        hud.viewer = viewer;
        viewer.hud = hud;

        // ── 7a. 現在地 (IP 測位) と天気 (Open-Meteo) ─────────────────────
        var servicesGO = new GameObject("Location & Weather");
        var location   = servicesGO.AddComponent<ObserverLocationProvider>();
        var weather    = servicesGO.AddComponent<WeatherService>();
        location.moonViewer = viewer;
        weather.moonViewer  = viewer;
        viewer.locationService = location;
        viewer.weatherService  = weather;

        // ── 8. Moon Direction Indicator ──────────────────────────────────
        var indicatorGO = new GameObject("Moon Direction Indicator Host");
        var indicator   = indicatorGO.AddComponent<MoonDirectionIndicator>();
        indicator.moonTransform = moonGO.transform;
        if (vrCamera != null) indicator.vrCamera = vrCamera;
        viewer.moonIndicator = indicator;

        // ── 9. XR Device Simulator ───────────────────────────────────────
        // PC テストでは XR Device Simulator がマウス入力を横取りするため自動配置しない。
        // VR コントローラーをエディターで模擬したい場合は
        // Assets/Samples/XR Interaction Toolkit/.../XR Device Simulator を手動で配置してください。
        Debug.Log("[MoonSceneBuilder] XR Device Simulator は自動配置されません。\n" +
                  "必要な場合は Samples フォルダから手動で追加してください。");

        // ── シーン保存 ────────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            AssetDatabase.CreateFolder("Assets", "Scenes");

        string scenePath = "Assets/Scenes/MoonViewer.unity";
        EditorSceneManager.SaveScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("構築完了！",
            $"シーンを作成しました: {scenePath}\n\n" +
            "▶ Play で即実行できます。\n\n" +
            "【PC 操作方法】\n" +
            "・右クリック長押し + マウス移動 → 視点回転\n" +
            "・画面下部のスライダーをドラッグ → 時刻を前後に変更\n" +
            "・Now ボタン → 現在時刻へ戻る\n" +
            "・← → 矢印キー → 時刻スクロール\n" +
            "・F キー → 情報パネル表示/非表示\n" +
            "・R キー → 現在時刻にリセット",
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
        // PC テストでは XR トラッキングが無くカメラが y=0 のままになり、
        // 地平面グリッドと同一平面で見えなくなるため目線高さを与えておく
        // (Quest 実機では XR トラッキングがこの値を上書きする)
        var camOffsetGO = new GameObject("Camera Offset");
        camOffsetGO.transform.SetParent(xrOriginGO.transform, false);
        camOffsetGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);

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
    static void AutoAssignTextures(MoonRenderer renderer, Material moonMat)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Textures/Moon"))
        {
            Debug.LogWarning("[MoonSceneBuilder] Assets/Textures/Moon フォルダがありません。\n" +
                             "NASA のカラーマップを配置すると月面テクスチャが適用されます。");
            return;
        }

        // Assets/Textures/Moon/ 以下の画像を検索
        string[] colorGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Textures/Moon" });
        foreach (var guid in colorGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string name = Path.GetFileNameWithoutExtension(path).ToLower();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            if (name.Contains("color") || name.Contains("lroc"))
            {
                renderer.colorMap = tex;
                // マテリアルにも直接設定して Play 前から正しく見えるようにする
                if (moonMat != null && moonMat.HasProperty("_BaseMap"))
                    moonMat.SetTexture("_BaseMap", tex);
                Debug.Log("[MoonSceneBuilder] カラーマップを自動割り当て: " + path);
            }
            else if (name.Contains("normal") || name.Contains("bump") || name.Contains("ldem"))
            {
                renderer.normalMap = tex;
                if (moonMat != null && moonMat.HasProperty("_BumpMap"))
                    moonMat.SetTexture("_BumpMap", tex);
                Debug.Log("[MoonSceneBuilder] 法線マップを自動割り当て: " + path);
            }
        }

        if (moonMat != null)
        {
            EditorUtility.SetDirty(moonMat);
            AssetDatabase.SaveAssetIfDirty(moonMat);
        }
    }

    // ── ヘルパー ─────────────────────────────────────────────────────────

    static Material GetOrCreateMaterial(string path, string shaderName, string fallbackShaderName)
    {
        var shader = ResolveShader(shaderName, fallbackShaderName);
        if (shader == null)
        {
            Debug.LogError($"[MoonSceneBuilder] シェーダーを解決できませんでした: {shaderName}");
            return null;
        }

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            // 既存マテリアルが壊れたシェーダーを参照している場合は修復する
            if (existing.shader != shader)
            {
                Debug.Log($"[MoonSceneBuilder] マテリアル '{path}' のシェーダーを " +
                          $"'{existing.shader?.name}' → '{shader.name}' に差し替えました。");
                existing.shader = shader;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            return existing;
        }

        var mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>
    /// シェーダーを取得する。コンパイルエラーがある場合は内容をログ出力し、
    /// フォールバックシェーダーを返す (ピンク表示を防ぐ)。
    /// </summary>
    static Shader ResolveShader(string shaderName, string fallbackShaderName)
    {
        var shader = Shader.Find(shaderName);

        if (shader == null)
        {
            Debug.LogWarning($"[MoonSceneBuilder] シェーダー '{shaderName}' が見つかりません " +
                             $"→ '{fallbackShaderName}' を使用します。");
            return Shader.Find(fallbackShaderName);
        }

        if (ShaderUtil.ShaderHasError(shader))
        {
            LogShaderMessages(shader);
            Debug.LogWarning($"[MoonSceneBuilder] '{shaderName}' はコンパイルエラーのため " +
                             $"'{fallbackShaderName}' で代替します。");
            return Shader.Find(fallbackShaderName);
        }

        return shader;
    }

    /// <summary>プロジェクト内の自作シェーダーを検証し、エラー内容をコンソールへ出力する。</summary>
    [MenuItem("MoonObserver/Validate Shaders")]
    public static void ValidateAllShaders()
    {
        // シェーダーが正常でもパイプラインが URP でなければピンクになるため最初に確認
        URPSetup.LogActivePipeline();

        string[] names =
        {
            "MoonObserver/MoonSurface",
            "MoonObserver/MoonAtmosphere",
            "MoonObserver/StarField",
            "MoonObserver/VertexColorLine",
            "MoonObserver/NightSkyGradient",
        };

        int errorCount = 0;
        foreach (var name in names)
        {
            var shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogWarning($"[ShaderCheck] '{name}' が見つかりません。");
                continue;
            }
            if (ShaderUtil.ShaderHasError(shader))
            {
                errorCount++;
                LogShaderMessages(shader);
            }
            else
            {
                Debug.Log($"[ShaderCheck] OK: {name}");
            }
        }

        if (errorCount == 0)
            Debug.Log("[ShaderCheck] すべてのシェーダーが正常にコンパイルされています。");
    }

    /// <summary>シェーダーのコンパイルメッセージを行番号付きで出力する。</summary>
    static void LogShaderMessages(Shader shader)
    {
        var messages = ShaderUtil.GetShaderMessages(shader);
        if (messages == null || messages.Length == 0)
        {
            Debug.LogError($"[ShaderCheck] '{shader.name}' にエラーがありますが詳細を取得できませんでした。\n" +
                           "Project ウィンドウでシェーダーを選択して Inspector を確認してください。");
            return;
        }

        foreach (var m in messages)
        {
            string text = $"[ShaderCheck] {shader.name} ({m.platform}) line {m.line}: {m.message}";
            if (!string.IsNullOrEmpty(m.messageDetails))
                text += "\n" + m.messageDetails;

            if (m.severity == ShaderCompilerMessageSeverity.Error)
                Debug.LogError(text);
            else
                Debug.LogWarning(text);
        }
    }

}
