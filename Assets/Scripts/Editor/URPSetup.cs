using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// URP (Universal Render Pipeline) のパイプラインアセットを生成し、
/// Graphics / Quality 設定へ割り当てる。
///
/// 【なぜ必要か】
/// URP パッケージが manifest.json に入っていても、「パイプラインアセット」が
/// 未割り当てだと Unity は Built-in Render Pipeline で描画する。
/// その状態では "RenderPipeline"="UniversalPipeline" タグを持つシェーダーは
/// SubShader ごとスキップされ、すべてマゼンタ (ピンク) 表示になる。
/// Unity 標準の "Universal Render Pipeline/Unlit" ですらピンクになるのが特徴。
/// </summary>
public static class URPSetup
{
    const string SETTINGS_DIR  = "Assets/Settings";
    const string RENDERER_PATH = "Assets/Settings/UniversalRenderer.asset";
    const string PIPELINE_PATH = "Assets/Settings/UniversalRenderPipelineAsset.asset";

    [MenuItem("MoonObserver/Setup URP Pipeline")]
    public static void SetupMenu()
    {
        var asset = EnsureURPAssigned();
        LogActivePipeline();

        if (asset != null)
        {
            EditorUtility.DisplayDialog("URP セットアップ完了",
                $"URP パイプラインアセットを割り当てました。\n\n{PIPELINE_PATH}\n\n" +
                "シェーダーの再コンパイルが走るため、完了まで少し待ってください。",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("URP セットアップ失敗",
                "パイプラインアセットを自動生成できませんでした。\n\n" +
                "手動手順:\n" +
                "1. Project で右クリック → Create → Rendering → URP Asset (with Universal Renderer)\n" +
                "2. Edit → Project Settings → Graphics → Default Render Pipeline に割り当て\n" +
                "3. Edit → Project Settings → Quality → 各レベルの Render Pipeline にも割り当て",
                "OK");
        }
    }

    /// <summary>URP が未割り当てならアセットを生成して割り当てる。割り当て済みなら何もしない。</summary>
    public static UniversalRenderPipelineAsset EnsureURPAssigned()
    {
        // すでに URP が有効ならそのまま使う
        if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset current)
        {
            AssignToAllQualityLevels(current);
            return current;
        }

        Debug.LogWarning("[URPSetup] URP パイプラインアセットが未割り当てです。\n" +
                         "Built-in Render Pipeline で描画されるため URP シェーダーがすべてピンクになります。\n" +
                         "→ 自動生成して割り当てます。");

        if (!AssetDatabase.IsValidFolder(SETTINGS_DIR))
            AssetDatabase.CreateFolder("Assets", "Settings");

        var rendererData = GetOrCreateRendererData();
        if (rendererData == null) return null;

        var pipeline = GetOrCreatePipelineAsset(rendererData);
        if (pipeline == null) return null;

        // Renderer が確実に紐づいているか検証して必要なら修復
        WireRendererData(pipeline, rendererData);

        GraphicsSettings.defaultRenderPipeline = pipeline;
        AssignToAllQualityLevels(pipeline);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[URPSetup] URP を有効化しました: " + PIPELINE_PATH);
        return pipeline;
    }

    // ── Renderer Data ───────────────────────────────────────────────────
    static UniversalRendererData GetOrCreateRendererData()
    {
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RENDERER_PATH);
        if (data != null) return data;

        data = ScriptableObject.CreateInstance<UniversalRendererData>();
        if (data == null)
        {
            Debug.LogError("[URPSetup] UniversalRendererData を生成できませんでした。");
            return null;
        }

        AssetDatabase.CreateAsset(data, RENDERER_PATH);
        AssetDatabase.SaveAssets();
        Debug.Log("[URPSetup] Renderer Data を作成: " + RENDERER_PATH);
        return data;
    }

    // ── Pipeline Asset ──────────────────────────────────────────────────
    static UniversalRenderPipelineAsset GetOrCreatePipelineAsset(UniversalRendererData rendererData)
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PIPELINE_PATH);
        if (pipeline != null) return pipeline;

        // URP のバージョン差異に備え、Create() が使えない場合は手動生成へフォールバック
        try
        {
            pipeline = UniversalRenderPipelineAsset.Create(rendererData);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[URPSetup] UniversalRenderPipelineAsset.Create() が失敗しました: " + e.Message);
            pipeline = null;
        }

        if (pipeline == null)
            pipeline = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

        if (pipeline == null)
        {
            Debug.LogError("[URPSetup] UniversalRenderPipelineAsset を生成できませんでした。");
            return null;
        }

        AssetDatabase.CreateAsset(pipeline, PIPELINE_PATH);
        AssetDatabase.SaveAssets();
        Debug.Log("[URPSetup] Pipeline Asset を作成: " + PIPELINE_PATH);
        return pipeline;
    }

    /// <summary>
    /// Pipeline Asset の内部 Renderer リストへ Renderer Data を紐づける。
    /// m_RendererDataList は private なため SerializedObject 経由で設定する。
    /// </summary>
    static void WireRendererData(UniversalRenderPipelineAsset pipeline, UniversalRendererData rendererData)
    {
        var so   = new SerializedObject(pipeline);
        var list = so.FindProperty("m_RendererDataList");
        if (list == null)
        {
            Debug.LogWarning("[URPSetup] m_RendererDataList が見つかりません。" +
                             "Renderer の紐づけは Inspector で確認してください。");
            return;
        }

        if (list.arraySize == 0)
            list.arraySize = 1;

        var slot = list.GetArrayElementAtIndex(0);
        if (slot.objectReferenceValue == null)
        {
            slot.objectReferenceValue = rendererData;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(pipeline);
            Debug.Log("[URPSetup] Renderer Data を Pipeline Asset へ紐づけました。");
        }
    }

    // ── Quality 設定の全レベルへ割り当て ────────────────────────────────
    static void AssignToAllQualityLevels(RenderPipelineAsset pipeline)
    {
        int      original = QualitySettings.GetQualityLevel();
        string[] levels   = QualitySettings.names;

        for (int i = 0; i < levels.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            if (QualitySettings.renderPipeline != pipeline)
            {
                QualitySettings.renderPipeline = pipeline;
                Debug.Log($"[URPSetup] Quality レベル '{levels[i]}' へ URP を割り当てました。");
            }
        }

        QualitySettings.SetQualityLevel(original, false);
    }

    /// <summary>現在アクティブなレンダーパイプラインをログ出力する。</summary>
    public static void LogActivePipeline()
    {
        var rp = GraphicsSettings.currentRenderPipeline;
        if (rp == null)
        {
            Debug.LogError("[URPSetup] 現在 Built-in Render Pipeline で動作しています。\n" +
                           "URP 用シェーダーはすべてピンク表示になります。\n" +
                           "メニュー MoonObserver > Setup URP Pipeline を実行してください。");
        }
        else
        {
            Debug.Log($"[URPSetup] アクティブなパイプライン: {rp.GetType().Name} ({rp.name})");
        }
    }
}
