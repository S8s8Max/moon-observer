# NASA CGI Moon Kit テクスチャ配置手順

このディレクトリに以下のファイルを配置してください。

## 必要なファイル

| ファイル名 | 解像度 | 用途 |
|------------|--------|------|
| `lroc_color_8k.png` | 8192×4096 | カラーマップ (albedo) |
| `moon_normal_8k.png` | 8192×4096 | 法線マップ (高度マップから変換) |

## ダウンロード元

NASA SVS (Scientific Visualization Studio):
**https://svs.gsfc.nasa.gov/4720**

1. ページ内の "CGI Moon Kit" からダウンロード
2. `lroc_color_poles_16k.tif` → Photoshop / ImageMagick で 8K PNG に変換
3. `ldem_16.tif` (高度マップ) → 法線マップ生成ツールで `moon_normal_8k.png` に変換

## ImageMagick での変換コマンド例

```bash
# カラーマップ 16K→8K変換
magick lroc_color_poles_16k.tif -resize 8192x4096 lroc_color_8k.png

# 高度マップから法線マップ生成
magick ldem_16.tif -resize 8192x4096 \
  -define convolve:scale=1 \
  -morphology Convolve Sobel:> moon_normal_8k.png
```

## ライセンス

NASA CGI Moon Kit はパブリックドメイン (Public Domain) です。
商用利用・改変・再配布が自由に行えます。
参照: https://www.nasa.gov/multimedia/guidelines/index.html

## Unity インポート設定

カラーマップ:
- Texture Type: Default
- sRGB: ON
- Compression: Normal Quality

法線マップ:
- Texture Type: Normal Map
- sRGB: OFF
- Compression: BC5 (Normal Map)
