# AGENTS.md · JH-ImageGlass-ResearchMode AI 协作上下文导航

本文档为 `JH-ImageGlass-ResearchMode` 仓库的 AI 协作导航与工程规范。参与本仓库开发的 AI Agent（Claude Code、Antigravity、OpenCode、Cursor、GitHub Copilot 等）在阅读、修改或提交代码前，请先阅读本文件。

---

## 1. 项目定位与研究目标

`JH-ImageGlass-ResearchMode` 是基于开源 Windows 图片查看器 [ImageGlass](https://github.com/d2phap/ImageGlass)（`v9.5.0.515`，遵循 GPLv3 协议）二次开发的**学术研究专用筛图行为采集工具**。

### 核心研究问题
在 AIGC 图像生产工作流中，当单批次候选图生成规模达到 100～1,000+ 张时，人工筛选、局部缺陷识别、多图比对、淘汰与保留决策成为主要工作流瓶颈。本工具在不改变真实看图体验的前提下，无感采集以下五类认知负荷的客观行为指标：
1. **识别负荷**：`viewDurationMs`（单图观看耗时）、`ZoomCount`（放大检查次数）、局部缺陷淘汰理由；
2. **比较负荷**：`visitCount`（同一图片回看次数）、`uncertain`（犹豫/待对比状态）；
3. **判断负荷**：`reasons`（多选决策理由）、`note`（主观备注）、保留/删除决策耗时；
4. **修复预期负荷**：理由中选择「后期修复成本太高」或「适合二次修改」；
5. **决策疲劳**：随批次推进的 `skip` / `uncertain` 比例变化与单图耗时漂移。

---

## 2. 仓库目录与架构索引

```text
JH-ImageGlass-ResearchMode/
├── .github/workflows/
│   └── build-release.yml     # GitHub Actions 发布流水线 (Node 22 + .NET 10 + MSBuild + 资产打包)
├── Source/
│   ├── ImageGlass.slnx       # Visual Studio 2026 解决方案文件（含 9 个子项目）
│   ├── .editorconfig         # C# 编码风格配置 (4 空格缩进、CRLF、file-scoped namespace)
│   ├── ImageGlass/           # 主程序 WinForms 项目 (net10.0-windows10.0.17763.0, WinExe)
│   │   ├── FrmMain.cs                        # 主窗口核心逻辑（含图片加载完成与缩放埋点、删除菜单绑定）
│   │   └── FrmMain/
│   │       ├── FrmMain.Research.cs           # [核心新增] 研究模式状态机、理由弹窗、快捷键与 JSONL 落盘
│   │       ├── FrmMain.IGMethods.cs          # 核心命令实现（IG_Delete 中接入删除理由弹窗与日志记录）
│   │       ├── FrmMain.PicMainEvents.cs      # 画布交互事件（鼠标移动、点击、键盘平移、手动缩放埋点）
│   │       └── FrmMain.Configs.cs            # 窗口关闭与配置持久化（FormClosing 中结算最后一帧记录）
│   ├── igcmd/                # 辅助命令行与工具进程项目 (igcmd.csproj)
│   └── Components/           # 7 个底层组件库
│       ├── ImageGlass.Base/                  # 基础工具库、BHelper、COM 互操作 (IWshRuntimeLibrary)、Magick.NET
│       ├── ImageGlass.Gallery/               # 缩略图画廊控件
│       ├── ImageGlass.Settings/              # 配置管理与 WebUI/ (TypeScript + Webpack + Sass 设置前端)
│       ├── ImageGlass.UI/                    # 主题化 WinForms 控件库
│       ├── ImageGlass.Views/                 # Direct2D / WebView2 图像渲染画布 (ImageGlass.Viewer.csproj)
│       ├── ImageGlass.Webp/                  # libwebp 封装 (ImageGlass.WebP.csproj)
│       └── ImageGlass.WinTouch/              # 触控手势互操作
├── Setup/
│   └── Assets/
│       ├── Themes/           # 运行时必需的内置主题包 (Kobe, fluent-dark, fluent-light)
│       └── Language/         # 运行时必需的多语言包 (*.iglang.json)
├── DATA_SCHEMA.md            # JSONL 日志字段规范与 Python 数据转换示例
├── RESEARCH_NOTES.md         # 认知负荷指标映射与论文实验设计说明
└── README.md                 # 项目说明与快捷键指南
```

---

## 3. ResearchMode 核心实现索引 (`FrmMain.Research.cs` 及钩子)

本研究分支遵循**最小侵入原则**：核心逻辑全部集中在 `Source/ImageGlass/FrmMain/FrmMain.Research.cs`（`public partial class FrmMain`），其余 `FrmMain*.cs` 文件仅做单行级方法调用钩入。

### 3.1 核心状态与数据结构 (`FrmMain.Research.cs`)
- **`_researchSessionId`**：每次启动 `FrmMain` 生成唯一会话标识（格式：`yyyyMMdd-HHmmss-<8位随机Hex>`）。
- **`_researchVisitCounts`**：`Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)`，按文件路径累计当前会话内的第几次查看（`visitCount`）。
- **`_researchView` (`ResearchViewState?`)**：记录当前正在浏览图片的起点时间戳（`StartedAt` 与高精度 `Stopwatch.GetTimestamp()`）、索引、总图数及 5 项交互计数器（`MouseMoveCount`、`MouseClickCount`、`MouseWheelCount`、`ZoomCount`、`PanCount`）。
- **`ResearchJsonOptions`**：使用 `JavaScriptEncoder.Create(UnicodeRanges.All)`，确保中文路径、中文多选理由与备注以未转义 UTF-8 明文写入 JSONL。

### 3.2 跨文件埋点位置清单
| 触发时机 / 事件 | 所在文件与方法 | 调用的 Research 方法 |
| :--- | :--- | :--- |
| 图片加载完成（WebView2 渲染分支） | `Source/ImageGlass/FrmMain.cs` -> `HandleImageProgress_LoadedAsync` | `Research_StartImageView(e.FilePath, e.Index)` |
| 图片加载完成（原生 Direct2D 分支） | `Source/ImageGlass/FrmMain.cs` -> `HandleImageProgress_LoadedAsync` | `Research_StartImageView(e.FilePath, e.Index)` |
| 菜单放大 / 缩小 | `Source/ImageGlass/FrmMain.cs` -> `MnuZoomIn_Click` / `MnuZoomOut_Click` | `Research_RecordZoom()` |
| 画布手动缩放 / 缩放模式切换 | `Source/ImageGlass/FrmMain/FrmMain.PicMainEvents.cs` -> `PicMain_OnZoomChanged` | `Research_RecordZoom()` |
| 画布鼠标移动 / 鼠标点击 | `Source/ImageGlass/FrmMain/FrmMain.PicMainEvents.cs` -> `PicMain_MouseMove` / `PicMain_MouseClick` | `Research_RecordMouseMove()` / `Research_RecordMouseClick()` |
| 画布键盘上下平移 | `Source/ImageGlass/FrmMain/FrmMain.PicMainEvents.cs` -> `PicMain_KeyDown` | `Research_RecordPan()` |
| 删除菜单触发（含原回收站菜单） | `Source/ImageGlass/FrmMain.cs` -> `MnuMoveToRecycleBin_Click` / `MnuDeleteFromHardDisk_Click` | 统一调用 `IG_Delete(false)`（真实磁盘删除） |
| 执行删除并弹窗采集理由 | `Source/ImageGlass/FrmMain/FrmMain.IGMethods.cs` -> `IG_Delete` | `Research_ShowReasonDialog("删除这张图", "delete")` + `Research_EndCurrentImage("delete", ...)` |
| 窗口关闭结算当前图片 | `Source/ImageGlass/FrmMain/FrmMain.Configs.cs` -> `FrmMainConfig_FormClosing` | `Research_EndCurrentImage("session_end", "form_closing")` |

### 3.3 已知代码接地点注意事项（供后续迭代排查）
1. **快捷键分发函数挂载点**：`FrmMain.Research.cs` 中已完整实现 `Research_HandleShortcut(Keys keyData)`（处理 `Ctrl+1` 收藏、`Ctrl+2` 删除、`Ctrl+3` 不确定、`Space` 跳过），但目前在 `FrmMain.cs` 的主键盘事件（或 `ProcessCmdKey`）中尚未调用 `Research_HandleShortcut`（当前 `Delete` / `Shift+Delete` 通过菜单快捷键正常工作，切图通过 `Research_StartImageView` 自动结算前一张图为 `action: "skip"` / `trigger: "auto_replaced_by_loaded_image"`）。若需激活 `Ctrl+1` / `Ctrl+2` / `Ctrl+3` 理由弹窗快捷键，请在 `FrmMain` 的按键预处理入口接入 `if (Research_HandleShortcut(keyData)) return true;`。
2. **滚轮与全向平移计数**：`FrmMain.Research.cs` 已定义 `Research_RecordMouseWheel()`，若需采集非零 `MouseWheelCount`，可在 `FrmMain.PicMainEvents.cs` 的 `PicMain_MouseWheel` 中补入调用；同理 `Research_RecordPan()` 可扩展至左右平移与鼠标拖拽平移。

### 3.4 日志落盘路径与 Schema
- **主路径**：当前浏览图片所在目录下的 `imageglass_research_log.jsonl`
- **回退路径**（当图片目录只读或写入异常时）：`%USERPROFILE%\Documents\ImageGlassResearchLogs\imageglass_research_log.jsonl`
- 完整 JSON 字段定义参见根目录 [DATA_SCHEMA.md](DATA_SCHEMA.md)。

---

## 4. 构建、打包与验证命令

### 4.1 环境要求
- **操作系统**：Windows 10 / 11 64-bit
- **IDE 与 SDK**：Visual Studio 2026（支持 `.slnx`）、.NET 10.0 Windows Desktop SDK (`net10.0-windows10.0.17763.0`)
- **前端构建环境**：Node.js 22 + npm（用于编译 `ImageGlass.Settings` 的 `WebUI`）
- **重要说明**：由于 `ImageGlass.Base.csproj` 包含 `IWshRuntimeLibrary` COM 引用（依赖 `tlbimp`），**必须使用 `msbuild` 编译与发布**，不可直接使用裸 `dotnet publish`。

### 4.2 完整本地构建与发布流程（与 `.github/workflows/build-release.yml` 对齐）
```powershell
# 步骤 1：构建设置面板 WebUI 前端静态资源（ImageGlass.csproj PostBuild 依赖此产物）
Push-Location Source/Components/ImageGlass.Settings/WebUI
npm ci
npm run build
Pop-Location

# 步骤 2：使用 MSBuild 编译或发布主程序 (x64 Release)
msbuild Source/ImageGlass/ImageGlass.csproj `
  /t:Restore,Publish `
  /p:Configuration=Release `
  /p:Platform=x64 `
  /p:RuntimeIdentifier=win-x64 `
  /p:SelfContained=false `
  /p:PublishSingleFile=false `
  /p:PublishDir="$PWD\artifacts\JH-ImageGlass-ResearchMode-win-x64\"

# 步骤 3：复制运行时必需的 Themes 与 Language 资产到 Publish 目录
# （注：MSBuild Publish 不会对 PublishDir 执行 PostBuild 拷贝，打包发布时必须显式拷贝）
$dest = "artifacts\JH-ImageGlass-ResearchMode-win-x64"
Copy-Item -Recurse "Setup\Assets\Themes" "$dest\Themes" -Force
Copy-Item -Recurse "Setup\Assets\Language" "$dest\Language" -Force
Copy-Item "README.md", "DATA_SCHEMA.md", "RESEARCH_NOTES.md", "LICENSE" $dest -Force
```

---

## 5. 编码规范与实验安全红线

1. **真实删除高危警告（测试必读）**：
   - 本研究版本将 `Delete`、`Shift+Delete`、`Ctrl+2` 以及「移至回收站」菜单项全部重定向为 `IG_Delete(false)`，即**从磁盘永久物理删除图片文件**（不经过回收站）。
   - Agent 在做功能验证或自动化测试时，**必须使用临时创建的副本测试图片目录**，严禁在原始素材库或重要图片目录上测试删除功能。
2. **代码风格与隔离约定**：
   - 严格遵守 `Source/.editorconfig`：使用 file-scoped namespace（`namespace ImageGlass;`）、4 空格缩进、`CRLF` 换行、启用 `Nullable`。
   - 所有研究模式新增字段以 `_research` 开头，新增方法以 `Research_` 开头，逻辑集中维护在 `Source/ImageGlass/FrmMain/FrmMain.Research.cs`。
3. **隐私脱敏与零泄露红线**：
   - **严禁提交真实姓名与个人隐私**：文档、代码、注释与 Commit Message 中统一使用匿名代号 `JH`，不得包含个人真实姓名、私人邮箱、电话或带个人用户名的本机路径。
   - **实验日志脱敏**：`imageglass_research_log.jsonl` 含有本地完整文件路径，严禁将未脱敏的实验 `.jsonl` 日志直接提交到公开仓库。
4. **开源协议合规（GPLv3）**：
   - 必须保留原项目 `LICENSE`（GPLv3）、原作者版权声明（`Copyright (C) 2010 - 2026 DUONG DIEU PHAP`）及「非官方研究分支」声明。
5. **Git 提交规范**：
   - 提交前执行 `git status` 与 `git diff --cached` 检查，避免将编译产物（`bin/`、`obj/`、`artifacts/`）、备份工程文件或测试日志混入提交。
