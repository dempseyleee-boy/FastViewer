# FastViewer 交互与功能演进总结

本文档总结了 FastViewer 从最初的手机 Camera RAW14 查看器，到当前 RAW / RGB / YUV 多格式查看与转换工具的完整交互过程、关键设计决策和已解决问题。

## 背景目标

最初目标是做一个可以直接落盘运行的 RAW 图片查看程序，优先支持手机 Camera 的 `RAW14_GRBG_16B` 格式。随后需求逐步扩展为：

- 自动从文件名解析宽高和格式。
- 支持更多手机 Camera dump 格式。
- 提高解析性能。
- 支持完整画面预览、缩放、旋转。
- 支持多图同时查看。
- 支持拖拽导入。
- 支持导出图片格式和 Camera dump 格式互转。
- 将项目整理为 `FastViewer`，上传到 GitHub。

## 技术路线演进

### 1. Python / Tkinter 原型

最初实现了 `raw14_grbg_16b_viewer.py`，用于打开 `RAW14_GRBG_16B` 文件。

原型阶段遇到的问题：

- Tkinter 异步回调里 `exc` 闭包变量失效导致 `NameError`。
- `PhotoImage` 读取 PPM 数据失败。
- 大图解析和预览较慢。
- 预览不是完整画面，而是类似只显示局部或缩略区域。

这些问题推动后续切换到编译型 Windows 桌面程序。

### 2. C# WinForms 高性能版本

由于当前环境没有稳定可用的 C++ 编译链，但 Windows 自带 .NET Framework C# 编译器可用，因此切换到 C# WinForms。

主要收益：

- exe 可以直接落盘运行。
- 文件读取、像素转换和 UI 响应比 Python 原型更稳定。
- 方便嵌入图标、拖拽、文件对话框、多图 UI 等 Windows 桌面能力。

当前源码位于：

```text
src/FastViewer.cs
```

构建产物位于：

```text
dist/FastViewer.exe
```

## 当前核心功能

### 文件导入

支持以下导入方式：

- 点击 `Browse / Multi` 选择单张或多张文件。
- 直接拖拽文件到窗口、画布或左侧路径框。
- 拖入文件夹时，会读取文件夹第一层文件，不递归扫描。

单图导入时进入单图查看模式；多图导入时进入卡片墙对比模式。

### 文件名解析规则

推荐文件名格式：

```text
<任意名字>_<width>x<height>.<FORMAT>
```

例如：

```text
face_3280x2464.RAW14_GRBG_16B
face_3280x2464.RGB48
face_1920x1280.NV21
```

宽高解析支持：

```text
3280x2464
3280X2464
w3280h2464
3280_2464
3280-2464
```

格式后缀不区分大小写。导出时默认使用大写格式后缀，便于识别。

### 支持的输入格式

#### Bayer RAW

- `RAW8_8B`
- `RAW10_16B`
- `RAW10_PACKED`
- `RAW12_16B`
- `RAW12_PACKED`
- `RAW14_16B`
- `RAW14_PACKED`
- `RAW16_16B`

RAW 文件名后缀中可以带 Bayer pattern：

- `GRBG`
- `RGGB`
- `BGGR`
- `GBRG`

示例：

```text
xxx_3280x2464.RAW14_GRBG_16B
xxx_3280x2464.RAW10_RGGB_PACKED
```

#### RGB

- `RGB24`
- `BGR24`
- `RGBA32`
- `BGRA32`
- `RGB48`
- `BGR48`

兼容简写：

- `.rgb` -> `RGB24`
- `.bgr` -> `BGR24`
- `.rgba` -> `RGBA32`
- `.bgra` -> `BGRA32`

#### YUV

- `NV21`
- `NV12`
- `I420`
- `YV12`
- `YUV420P`
- `P010`

## 查看能力

### 单图模式

- 渲染完整原图。
- 默认适配窗口完整显示。
- `Fit Window` 可重新适配窗口。
- `Ctrl + 鼠标滚轮` 缩放。
- 支持 `0 / 90 / 180 / 270` 旋转。
- RAW 支持 Color、Bayer gray、Bayer site RGB 视图。

### 多图模式

- 多张文件以卡片墙形式显示。
- 每张图独立解析自己的宽高、格式、stride、Bayer pattern 等参数。
- `Ctrl + 鼠标滚轮` 调整卡片大小。
- `Fit Window` 重置多图卡片缩放。

## 参数刷新策略

曾发现一个问题：导入过一张图后，再导入另一张图，部分选项可能沿用上一张图的状态，例如：

- `Format`
- `Stride`
- `Endian`
- `Bits`
- `Bayer`

已修复为：

- 每次打开 / 拖入新文件，都会重新按文件名检测并刷新 UI 参数。
- 单图导入会刷新左侧选项。
- 多图导入会为每张图独立检测参数，不再反复污染左侧 UI。

需要注意：如果文件名没有明确格式后缀，例如 `.bin`、`.raw`、`.yuv`，程序会使用安全默认值 `RAW14_16B / GRBG / Little Endian / LSB aligned`。最稳的规避方式是始终使用完整格式后缀。

## Endian 与 Bits 对齐说明

### Endian

`Endian` 表示 16-bit 数据的字节顺序。

例如数值 `0x1234`：

```text
Little Endian: 34 12
Big Endian:    12 34
```

它会影响：

- `RAW10_16B`
- `RAW12_16B`
- `RAW14_16B`
- `RAW16_16B`
- `RGB48`
- `BGR48`
- `P010`

### LSB aligned / MSB aligned

该选项用于低 bit RAW 存在 16-bit 容器里的情况，例如 `RAW14_16B`。

`LSB aligned`：有效 bit 放在低位。

```text
00xxxxxxxxxxxxxx
```

`MSB aligned`：有效 bit 放在高位。

```text
xxxxxxxxxxxxxx00
```

常见手机 dump 中，`RAW14_GRBG_16B` 多数是：

```text
Little Endian + LSB aligned
```

## 导出 / 转换能力

左侧 `Export` 下拉框可以选择图片格式或 Camera frame dump 格式。

### 图片格式

- `PNG`
- `BMP`
- `JPEG`
- `TIFF`

### Camera dump 格式

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

### 导出行为

单图模式：

- 点击 `Export Image`。
- 选择输出路径。
- 保存对话框的“保存类型”会同步列出当前允许的全部导出格式，例如 `BGR48 frame dump (*.BGR48)`、`RGB48 frame dump (*.RGB48)`、`NV21 frame dump (*.NV21)`。
- 选择哪个保存类型，就会按哪个格式编码，并自动补对应后缀。
- 导出当前图像到选定格式。

多图模式：

- 点击 `Export Image`。
- 选择输出文件夹。
- 批量导出当前已渲染的多张图像。
- 若目标文件已存在，会自动追加 `_2`、`_3` 等后缀，避免覆盖。

### 导出后缀规则

导出 Camera dump 格式时，格式直接作为文件后缀：

```text
image_3280x2464.NV21
image_3280x2464.RGB48
image_3280x2464.RAW14_GRBG_16B
image_3280x2464.RAW10_RGGB_PACKED
```

RAW 导出会使用当前 `Bayer` 下拉框里的 pattern 生成后缀。单图导出时，Windows 保存对话框里的每个 Camera dump 类型都有自己的 filter，例如 `BGR48` 对应 `*.BGR48`，不再只显示当前下拉框选中的单一类型。

### RAW 导出语义

RAW 导出是从当前解码后的 RGB 画面重新编码成 Bayer dump。它适合格式互转和喂给下游工具链，但不能恢复原 sensor RAW 中已经丢失的信息。

### RGB48 / BGR48 字节序

`RGB48` / `BGR48` 是 16-bit per channel dump。读取和导出都会尊重左侧 `Endian` 选项。

如果从 `NV21` 转 `RGB48` 后，下游工具显示发黑或颜色异常，通常是字节序不一致。可以尝试：

```text
Endian = Big Endian
Export = RGB48
```

再重新导出。

导出时会写出 sidecar 元数据文件 `导出文件名 + .json`，记录 source/output 的宽高、格式、stride、offset、endian、alignment、Bayer、rotate、YUV matrix/range 等，用于复现导出条件和排查参数歧义。

### YUV 限制

`NV21`、`NV12`、`I420`、`YV12`、`YUV420P`、`P010` 这些 YUV420 / P010 系列导出要求宽高为偶数。这符合手机 camera dump 的常见要求。

当前 YUV 转换支持 `YUV Matrix = BT.601 / BT.709 / BT.2020` 和 `YUV Range = Limited / Full`。读取 YUV 预览、YUV 导出都会使用当前设置。默认值为 `BT.601 / Limited`，这是很多手机 camera dump 的常见默认，但不是所有场景都正确。

## 已解决的主要问题

- Tkinter 回调闭包变量 `exc` 失效导致 `NameError`。
- Tkinter `PhotoImage` 无法识别 PPM 数据。
- Python 原型解析速度慢。
- 预览只显示局部，不是完整画面。
- NV21 文件打开失败，原因是旧逻辑 stride 默认错误。
- RAW14 与 RGB48 对比时，状态栏里 source / preview / shown 概念混淆。
- `Preview` 选项容易让人误以为只读局部，已移除。
- `1:1` 对当前使用场景价值不高，已移除。
- UI 过于朴素，先改为深色专业工具风格，再尝试 Apple / macOS 浅色圆角卡片，随后参考 Next.js / Vercel 站点语言收敛为黑白极简、细边框、小圆角的开发者工具风格。
- 为提升严谨性，新增 YUV Matrix / Range 显式设置，并在导出时写出 `.json` sidecar 元数据。
- 多图导入时参数可能污染，已改为每张图独立检测。
- RGB48 / BGR48 字节序固定小端导致下游显示异常，已改为尊重 `Endian`。

## GitHub 上传与仓库整理

项目已整理为仓库：

```text
dempseyleee-boy/FastViewer
```

主要目录：

```text
README.md
src/FastViewer.cs
dist/FastViewer.exe
dist/run_fastviewer.bat
assets/FastViewer.ico
assets/FastViewer_icon.png
docs/interaction-summary.md
```

项目从早期多个本地迭代版本清理为：

- `outputs/FastViewer.exe`
- `outputs/FastViewer.cs`
- `outputs/run_fastviewer.bat`
- `FastViewer/` 本地 Git 仓库

旧的 v4-v14 中间版本、Python 原型和临时 README 已从本地工作目录清理。

## 当前建议使用方式

### 文件命名

尽量使用完整后缀：

```text
<name>_<width>x<height>.<FORMAT>
```

例如：

```text
face_3280x2464.RAW14_GRBG_16B
face_3280x2464.RGB48
face_1920x1280.NV21
```

### NV21 转 RGB48

如果转换后给其它工具查看不正常，优先检查 `Endian`：

- 默认尝试 `Little Endian`。
- 若发黑、颜色异常，再尝试 `Big Endian`。

### 多图导入

多图最好每张文件都带完整格式后缀。这样每张图都能独立识别，不依赖 UI 当前状态。


## 导出通路锁定策略

`Export` 下拉框现在是动态白名单，不再固定显示所有格式。打开文件后，只会显示当前源格式允许导出的目标格式；多图模式会取所有图片共同允许的格式。

已锁定 / 不可选的通路：

- 任意格式导出为 `RAW*`：锁定。因为当前画面已经是解码后的 RGB 结果，无法恢复真实 sensor Bayer RAW，只能合成 RAW，容易误用。
- `RGB24 / BGR24 / RGBA32 / BGRA32` 导出为 `P010`：锁定。8-bit RGB 不能生成真实 10-bit YUV 信息。
- `NV21 / NV12 / I420 / YV12 / YUV420P` 导出为 `P010`：锁定。8-bit YUV420 不能生成真实 10-bit P010 信息。
- 奇数宽高导出为 `NV21 / NV12 / I420 / YV12 / YUV420P / P010`：锁定。YUV420 / P010 要求偶数宽高。

状态栏会显示类似 `RAW output is locked...` 的提示，说明被隐藏格式的原因。

## 后续可扩展方向

- 为未知后缀文件增加弹窗询问策略。
- 增加“使用当前 UI 参数打开未知格式文件”的开关。
- 增加批量导出时的进度条和取消按钮。
- 增加 RGB48 端序自动探测或预览对比。
- 增加更多 YUV packed / semi-planar 变体。
- 将核心转换逻辑拆成独立库，UI 与格式编解码分离。

