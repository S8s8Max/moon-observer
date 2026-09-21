# VR 月鑑賞アプリ — Moon Observer

Unity 6 (URP) + XR Interaction Toolkit 3.x で実装した物理ベース VR 月鑑賞アプリ。
現在地・現在時刻に基づき、天文暦 (Meeus Chapter 47/48) で月の正確な位置を計算し、
物理ベースの大気散乱エフェクトで月の色変化をリアルタイムに再現します。

## 主要機能

- **高精度天文暦**: 誤差 < 0.1° の月の位置計算 (ELP2000 主要摂動項)
- **大気光学エフェクト**: Rayleigh + Mie 散乱による月の色変化
- **地平線効果**: 地平線付近での月の赤み・見かけの拡大 (月の錯覚)
- **秤動アニメーション**: 光学秤動による月面の揺れ
- **VR 対応**: Meta Quest 3 / SteamVR (OpenXR)

## プロジェクト構成

```
Assets/
├── Scripts/
│   ├── Astronomy/
│   │   ├── MoonAstronomyEngine.cs  # 天文暦計算エンジン (純粋 C#)
│   │   ├── MoonState.cs            # 月の状態データ構造体
│   │   └── AstronomyTests.cs       # Unity Test Runner 用テスト
│   ├── Rendering/
│   │   ├── MoonRenderer.cs         # 月 Mesh・Material・スケール制御
│   │   └── SkyboxController.cs     # 動的スカイボックス
│   ├── VR/
│   │   └── VRMoonViewer.cs         # VR リグ・入力・UI 統合
│   └── Atmospheric/
│       └── AtmosphericEffects.cs   # 大気効果パラメータ管理
├── Shaders/
│   ├── MoonAtmosphere.shader       # Rayleigh + Mie 大気散乱 HLSL
│   └── MoonSurface.shader          # URP Lit 拡張 月面シェーダー
├── Textures/Moon/
│   ├── README.md                   # NASA テクスチャ取得手順
│   ├── lroc_color_8k.png           # [要配置] NASA LROC カラーマップ
│   └── moon_normal_8k.png          # [要配置] 法線マップ
├── Scenes/
│   └── MoonViewer.unity.meta.txt   # シーン構成の説明
└── ...
Tests/
├── AstronomyEngineTests.cs         # スタンドアロン .NET テスト
└── MoonObserver.Tests.csproj
Packages/
└── manifest.json                   # Unity パッケージ依存関係
```

## セットアップ手順

### 1. Unity プロジェクト作成

1. Unity Hub で **Unity 6 (6000.0.x LTS)** の新規プロジェクトを作成
2. テンプレート: **Universal 3D** (URP)
3. このリポジトリの `Assets/`、`Packages/` を既存プロジェクトに上書きコピー

### 2. パッケージインストール

Unity Package Manager で以下を確認:
- Universal RP 17.x
- XR Interaction Toolkit 3.0.7
- Input System 1.8.x
- OpenXR Plugin 1.11.x
- TextMeshPro 3.x

### 3. NASA テクスチャの配置

`Assets/Textures/Moon/README.md` の手順に従い、NASA CGI Moon Kit のテクスチャを配置してください。

### 4. シーン構築

`Assets/Scenes/MoonViewer.unity.meta.txt` の手順に従い、シーンを構築してください。

### 5. ビルド設定

**Meta Quest 3:**
- File > Build Settings > Android
- XR Plugin Management > OpenXR を有効化
- Target Architecture: ARM64
- Scripting Backend: IL2CPP

**SteamVR / PC VR:**
- Build Settings > PC, Mac & Linux Standalone
- XR Plugin Management > OpenXR + SteamVR 互換プロファイル

## コントローラー操作

| 操作 | 機能 |
|------|------|
| 右スティック 左右 | 時刻を ±1 時間変更 |
| 右スティック 前後 | 時刻を ±10 分変更 |
| 左スティック | 視点回転 |
| A/X ボタン | 情報パネル表示切替 |
| B ボタン | 月の錯覚 ON/OFF |
| 右グリップ長押し | 現在時刻にリセット |

## 受け入れ基準 (テスト)

| ID | 内容 | 合格条件 |
|----|------|---------|
| T01 | 月の位置計算 | Stellarium との差異 < 0.2° |
| T02 | 輝面比計算 | 満月=100%、新月=0%、上弦=50% ±1% |
| T03 | 大気色変化 | 高度 0°=赤、15°=黄、30°以上=白 |
| T04 | VR 表示 | Quest 3 で ≥ 72fps |
| T05 | 時刻操作 | スティックで月位置がリアルタイム変化 |
| T06 | 月の錯覚 | ON 時に地平線付近で 1.2〜1.3 倍 |

### スタンドアロンテスト実行

```bash
cd Tests
dotnet test
```

## 参考文献

- Meeus, J. "Astronomical Algorithms" 2nd ed. (1998) — Chapter 22, 47, 48
- Nishita, T. et al. "Display of the Earth Taking into Account Atmospheric Scattering" SIGGRAPH 1993
- NASA CGI Moon Kit: https://svs.gsfc.nasa.gov/4720
- Unity XR Interaction Toolkit: https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.0/manual/index.html
