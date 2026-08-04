# FastViewer

FastViewer 是一个 Windows 桌面查看器，用来直接打开手机 Camera 导出的 RAW / YUV / RGB 原始帧文件。它可以从文件名里自动解析常见的宽高和格式后缀，并支持单图查看、多图同时对比。

当前版本：**FastViewer**

## 快速开始

1. 打开 `dist/FastViewer.exe`。
2. 点击 **Browse / Multi**。
3. 选择 1 张图进入单图查看；一次选择多张图会进入多图对比模式。
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

## 界面说明

- 单图模式会渲染完整原图，并默认适配到窗口内完整显示。
- 多图模式会把一次选择的多张文件显示成卡片墙，方便对比 RAW / RGB / YUV 输出。
- `Black`、`White`、`Gamma` 用于 RAW tone mapping；填写 `auto` 会自动估计黑白场。
- `Stride` 可留空，程序会按当前格式自动给默认 stride。
- `Rotate` 支持 `0`、`90`、`180`、`270` 度旋转。
- `Export BMP` 当前支持单图导出；多图批量导出后续可扩展。

## 从源码构建

在 Windows 上，如果存在 .NET Framework C# 编译器，可以执行：

```powershell
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /platform:x64 /optimize+ /win32icon:assets\FastViewer.ico /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:dist\FastViewer.exe src\FastViewer.cs
```

## License

见仓库中的 `LICENSE`。


