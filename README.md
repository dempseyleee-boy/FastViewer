# FastViewer

FastViewer 是一个 Windows 桌面查看器，用来直接打开手机 Camera 导出的 RAW / YUV / RGB 原始帧文件。它可以从文件名里自动解析常见的宽高和格式后缀，并支持单图查看、多图同时对比。

当前版本：**FastViewer**

## 快速开始

1. 打开 `dist/FastViewer.exe`。
2. 点击 **Browse / Multi**，或者直接把文件拖进窗口。
3. 选择 / 拖入 1 张图进入单图查看；一次选择 / 拖入多张图会进入多图对比模式。
4. 点击 **Fit Window** 可以重新适配窗口完整显示。
5. 使用 **Ctrl + 鼠标滚轮** 缩放单图，或调整多图模式里的卡片大小。

也可以直接双击启动脚本：

```bat
dist\run_fastviewer.bat
```

## 仓库内容

```text
src/FastViewer.cs                     C# WinForms 源码
dist/FastViewer.exe                   已构建好的 x64 Windows 可执行文件，已嵌入 FastViewer 图标
dist/run_fastviewer.bat               便捷启动脚本
```

## 支持的文件名规则

文件后缀不区分大小写。宽高会从文件名中自动解析，支持类似这些写法：

```text
3280x2464
3280X2464
w3280h2464
3280_2464
3280-2464
```

### Bayer RAW

支持下面这类后缀：

- `.RAW8_GRBG_8B`
- `.RAW10_GRBG_16B`
- `.RAW10_GRBG_PACKED`
- `.RAW12_GRBG_16B`
- `.RAW12_GRBG_PACKED`
- `.RAW14_GRBG_16B`
- `.RAW14_GRBG_PACKED`
- `.RAW16_GRBG_16B`

其中 Bayer pattern 可以替换为：

- `GRBG`
- `RGGB`
- `BGGR`
- `GBRG`

示例：

```text
20251217101_P12U_F_Face_20251217_211423_3280x2464_p_3280x2464_process_1.RAW14_GRBG_16B
```

### RGB

- `.rgb` / `.rgb24`
- `.bgr` / `.bgr24`
- `.rgba` / `.rgba32`
- `.bgra` / `.bgra32`
- `.rgb48`
- `.bgr48`

### YUV

- `.nv21`
- `.nv12`
- `.i420`
- `.yv12`
- `.yuv420p`
- `.p010`


## 图标

`assets/FastViewer.ico` 是程序图标，会在构建时通过 `/win32icon` 嵌入 exe。`assets/FastViewer_icon.png` 是同款 PNG 预览图，方便在 README 或发布页展示。


## 导出 / 转换格式

左侧 `Export` 下拉框用于选择转换/导出格式。单图模式点击 **Export Image** 会弹出保存路径；保存对话框的“保存类型”会列出当前允许的全部导出格式，例如 `BGR48 frame dump (*.BGR48)`、`NV21 frame dump (*.NV21)`，选择哪个保存类型就会按哪个格式编码并补对应后缀。多图模式点击 **Export Image** 会选择导出文件夹，并按原文件名批量导出。若目标文件已存在，会自动追加 `_2`、`_3` 等后缀避免覆盖。

图片格式：

- `PNG`
- `BMP`
- `JPEG`
- `TIFF`

相机 frame dump 格式：

- `RAW8_8B`
- `RAW10_16B`
- `RAW10_PACKED`
- `RAW12_16B`
- `RAW12_PACKED`
- `RAW14_16B`
- `RAW14_PACKED`
- `RAW16_16B`
- `RGB24`
- `BGR24`
- `RGBA32`
- `BGRA32`
- `RGB48`
- `BGR48`
- `NV21`
- `NV12`
- `I420`
- `YV12`
- `YUV420P`
- `P010`

RAW 导出会使用当前 `Bayer` 下拉框里的 pattern 生成后缀，例如 `.RAW14_GRBG_16B`。注意：RAW 导出是把当前解码后的 RGB 画面重新编码成 Bayer dump，适合格式互转和喂给下游工具链；它不能恢复原 sensor RAW 里已经丢失的信息。

YUV420 / P010 系列导出要求宽高为偶数，这是手机 camera dump 的常见要求。

`RGB48` / `BGR48` 是 16-bit per channel dump，导出和读取都会尊重左侧 `Endian` 选项。若下游工具显示发黑或颜色异常，通常是字节序不一致：可尝试把 `Endian` 切换为 `Big Endian` 后再导出。

## 导出通路锁定策略

`Export` 下拉框现在是动态白名单，不再固定显示所有格式。打开文件后，只会显示当前源格式允许导出的目标格式；多图模式会取所有图片共同允许的格式。

已锁定 / 不可选的通路：

- 任意格式导出为 `RAW*`：锁定。因为当前画面已经是解码后的 RGB 结果，无法恢复真实 sensor Bayer RAW，只能合成 RAW，容易误用。
- `RGB24 / BGR24 / RGBA32 / BGRA32` 导出为 `P010`：锁定。8-bit RGB 不能生成真实 10-bit YUV 信息。
- `NV21 / NV12 / I420 / YV12 / YUV420P` 导出为 `P010`：锁定。8-bit YUV420 不能生成真实 10-bit P010 信息。
- 奇数宽高导出为 `NV21 / NV12 / I420 / YV12 / YUV420P / P010`：锁定。YUV420 / P010 要求偶数宽高。

状态栏会显示类似 `RAW output is locked...` 的提示，说明被隐藏格式的原因。

## 界面说明

- 界面采用浅色圆角卡片风格：左侧参数区、画布区域、多图卡片和按钮都按更接近 macOS / Apple 工具 App 的视觉重新整理。
- 支持把文件拖到窗口、画布或左侧路径框打开；拖入多张文件会进入多图模式。
- 每次打开 / 拖入新文件时，`Width`、`Height`、`Format`、`Stride`、`Endian`、`Bits`、`Bayer` 会按文件名重新检测刷新，避免上一张图的参数残留。
- 单图模式会渲染完整原图，并默认适配到窗口内完整显示。
- 多图模式会把一次选择的多张文件显示成卡片墙，方便对比 RAW / RGB / YUV 输出。
- `Black`、`White`、`Gamma` 用于 RAW tone mapping；填写 `auto` 会自动估计黑白场。
- `Stride` 可留空，程序会按当前格式自动给默认 stride。
- `Rotate` 支持 `0`、`90`、`180`、`270` 度旋转。
- `Export` 可以选择图片格式或相机 frame dump 格式，用于 RAW / RGB / YUV / NV21 等格式之间转换导出。

## 从源码构建

在 Windows 上，如果存在 .NET Framework C# 编译器，可以执行：

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\FastViewer.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:dist\FastViewer.exe src\FastViewer.cs
```

## 交互总结

完整需求演进、关键决策和问题修复记录见：[docs/interaction-summary.md](docs/interaction-summary.md)。

## License

见仓库中的 `LICENSE`。

