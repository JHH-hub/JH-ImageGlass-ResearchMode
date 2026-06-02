# JH-ImageGlass-ResearchMode

基于开源图片查看器 [ImageGlass](https://github.com/d2phap/ImageGlass) 改造的 AI 生成图片筛选研究工具。

本项目用于论文研究场景：当 AI 能够快速生成 100、1000 甚至更多图片后，人工筛选、比较、删除、保留和解释选择原因逐渐成为创作流程中的主要瓶颈。本工具在 ImageGlass 的图片浏览能力基础上，增加筛选行为记录模块，用于分析 AI 生成流程中的认知负荷。

> This is a research fork of ImageGlass. It is not an official ImageGlass release.

## 研究目标

本项目关注的问题是：

> 在 AIGC 图片生产流程中，生成成本下降后，人类筛选与决策成本如何上升，并如何形成新的工作流阻碍？

工具主要记录以下行为数据：

- 每张图片的观看时长
- 收藏 / 保留行为
- 删除行为
- 不确定判断
- 跳过行为
- 删除或收藏原因
- 人工备注
- 鼠标移动、点击、滚轮次数
- 缩放和平移次数
- 对同一张图的反复查看次数

这些数据可用于分析筛图过程中的识别负荷、比较负荷、判断负荷、修复预期负荷和决策疲劳。

## 基于项目

- Upstream: <https://github.com/d2phap/ImageGlass>
- Website: <https://imageglass.org>
- License: GPLv3

原项目 ImageGlass 是一款 Windows 平台轻量图片查看器。本仓库保留原项目许可证和版权声明，并在其基础上增加研究记录功能。

## 新增功能

### 1. 自动观看计时

图片加载完成后开始计时。当用户切换图片、关闭窗口、收藏、删除或标记不确定时，自动结束当前图片记录。

### 2. 收藏 / 保留记录

快捷键：`Ctrl + 1`

触发后弹出理由选择窗口，用户可多选原因并填写备注。记录完成后自动进入下一张。

### 3. 删除记录

快捷键：

- `Delete`
- `Shift + Delete`
- `Ctrl + 2`

触发后弹出删除原因选择窗口，用户确认后会真实删除图片文件，并写入日志。

> 注意：本研究版本按论文实验需求执行真实删除。使用前建议先备份原始图片。

### 4. 不确定记录

快捷键：`Ctrl + 3`

用于标记“有潜力但暂时无法判断”“需要后续比较”“疲劳状态下暂不判断”等情况。

### 5. 快速跳过

快捷键：`Space`

切换到下一张，并把当前图片记录为 `skip`。

### 6. 细粒度交互记录

每张图片会记录：

- `MouseMoveCount`：鼠标移动次数
- `MouseClickCount`：鼠标点击次数
- `MouseWheelCount`：滚轮次数
- `ZoomCount`：缩放次数
- `PanCount`：平移次数

## 日志输出

日志文件默认保存在当前图片所在文件夹：

```text
imageglass_research_log.jsonl
```

如果图片所在文件夹无法写入，则保存到：

```text
Documents\ImageGlassResearchLogs\imageglass_research_log.jsonl
```

日志格式为 `JSONL`，即每一行是一条独立 JSON 记录，便于后续使用 Python、R、Excel、数据库或 AI 工具分析。

详细字段说明见：

- [DATA_SCHEMA.md](./DATA_SCHEMA.md)
- [RESEARCH_NOTES.md](./RESEARCH_NOTES.md)

## 快捷键

| 快捷键 | 行为 |
|---|---|
| `Ctrl + 1` | 收藏 / 保留，弹出理由窗口，记录后下一张 |
| `Ctrl + 2` | 删除，弹出理由窗口，真实删除文件 |
| `Ctrl + 3` | 标记不确定，弹出理由窗口，记录后下一张 |
| `Space` | 跳过 / 下一张 |
| `Delete` | 删除，弹出理由窗口，真实删除文件 |
| `Shift + Delete` | 删除，弹出理由窗口，真实删除文件 |

## 开发环境

沿用 ImageGlass 原项目环境要求：

- Windows 10/11 64-bit
- Visual Studio 2026
- .NET 10 Windows Desktop SDK

解决方案文件：

```text
Source\ImageGlass.slnx
```

## 论文引用建议

论文中可描述为：

> 本研究工具基于开源图片查看器 ImageGlass 进行二次开发。ImageGlass 是一款面向 Windows 平台的开源图片查看器，遵循 GPLv3 协议。本研究在其基础上增加筛选行为记录模块，用于采集 AI 生成图片筛选过程中的观看时长、操作类型、删除/保留理由及交互次数等数据。

## License

本项目基于 ImageGlass 修改，继续遵循 GPLv3。原项目版权归 ImageGlass 作者所有。本研究版本新增修改用于学术研究。

- Original project: Copyright (C) 2010 - 2026 DUONG DIEU PHAP
- Research modification: 王继辉 / JH, for thesis research on cognitive load in AI-generated image screening
