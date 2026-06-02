/*
ImageGlass Research Mode - lightweight behavior logging for AI image screening studies
*/
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ImageGlass;

public partial class FrmMain
{
    private readonly string _researchSessionId = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8];
    private readonly Dictionary<string, int> _researchVisitCounts = new(StringComparer.OrdinalIgnoreCase);
    private ResearchViewState? _researchView;

    private static readonly JsonSerializerOptions ResearchJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    private sealed class ResearchViewState
    {
        public required string FilePath { get; init; }
        public required DateTimeOffset StartedAt { get; init; }
        public required long StartedTicks { get; init; }
        public required int ImageIndex { get; init; }
        public required int ImageCount { get; init; }
        public required int VisitCount { get; init; }
        public int MouseMoveCount { get; set; }
        public int MouseClickCount { get; set; }
        public int MouseWheelCount { get; set; }
        public int ZoomCount { get; set; }
        public int PanCount { get; set; }
    }

    private sealed class ResearchDecision
    {
        public required string[] Reasons { get; init; }
        public string Note { get; init; } = string.Empty;
    }

    private void Research_StartImageView(string? filePath, int imageIndex)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        if (_researchView?.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase) == true) return;

        Research_EndCurrentImage("skip", "auto_replaced_by_loaded_image");

        var visitCount = _researchVisitCounts.TryGetValue(filePath, out var count) ? count + 1 : 1;
        _researchVisitCounts[filePath] = visitCount;

        _researchView = new ResearchViewState
        {
            FilePath = filePath,
            StartedAt = DateTimeOffset.Now,
            StartedTicks = Stopwatch.GetTimestamp(),
            ImageIndex = imageIndex,
            ImageCount = Local.Images.Length,
            VisitCount = visitCount,
        };
    }

    private void Research_EndCurrentImage(string action, string trigger, ResearchDecision? decision = null, bool deletedFromDisk = false)
    {
        var view = _researchView;
        if (view == null) return;

        _researchView = null;

        var endedAt = DateTimeOffset.Now;
        var durationMs = (long)(Stopwatch.GetElapsedTime(view.StartedTicks).TotalMilliseconds);
        var fileInfo = new FileInfo(view.FilePath);
        var folderPath = fileInfo.DirectoryName ?? string.Empty;
        var logPath = Path.Combine(folderPath, "imageglass_research_log.jsonl");

        var record = new
        {
            eventType = "image_view_decision",
            sessionId = _researchSessionId,
            timestamp = endedAt,
            startedAt = view.StartedAt,
            endedAt,
            viewDurationMs = durationMs,
            action,
            trigger,
            reasons = decision?.Reasons ?? [],
            note = decision?.Note ?? string.Empty,
            filePath = view.FilePath,
            fileName = fileInfo.Name,
            folderPath,
            fileExistsAtLogTime = File.Exists(view.FilePath),
            deletedFromDisk,
            imageIndex = view.ImageIndex,
            imageCount = view.ImageCount,
            visitCount = view.VisitCount,
            interactions = new
            {
                view.MouseMoveCount,
                view.MouseClickCount,
                view.MouseWheelCount,
                view.ZoomCount,
                view.PanCount,
            },
        };

        try
        {
            File.AppendAllText(logPath, JsonSerializer.Serialize(record, ResearchJsonOptions) + Environment.NewLine);
        }
        catch
        {
            var fallbackDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "ImageGlassResearchLogs");
            Directory.CreateDirectory(fallbackDir);
            var fallbackPath = Path.Combine(fallbackDir, "imageglass_research_log.jsonl");
            File.AppendAllText(fallbackPath, JsonSerializer.Serialize(record, ResearchJsonOptions) + Environment.NewLine);
        }
    }

    private bool Research_HandleShortcut(Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.D1) || keyData == (Keys.Control | Keys.NumPad1))
        {
            Research_MarkDecisionAndGoNext("favorite", "keyboard_ctrl_1", "收藏 / 保留这张图");
            return true;
        }

        if (keyData == (Keys.Control | Keys.D2) || keyData == (Keys.Control | Keys.NumPad2))
        {
            IG_Delete(false);
            return true;
        }

        if (keyData == (Keys.Control | Keys.D3) || keyData == (Keys.Control | Keys.NumPad3))
        {
            Research_MarkDecisionAndGoNext("uncertain", "keyboard_ctrl_3", "暂时不确定");
            return true;
        }

        if (keyData == Keys.Space)
        {
            IG_ViewImage(1);
            return true;
        }

        return false;
    }

    private void Research_MarkDecisionAndGoNext(string action, string trigger, string title)
    {
        if (_researchView == null) return;

        var decision = Research_ShowReasonDialog(title, action);
        if (decision == null) return;

        Research_EndCurrentImage(action, trigger, decision);
        IG_ViewImage(1);
    }

    private ResearchDecision? Research_ShowReasonDialog(string title, string action)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false,
            MaximizeBox = false,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Width = 520,
            Height = 560,
            TopMost = TopMost,
        };

        var prompt = new Label
        {
            Text = "请选择原因（可多选），也可以填写备注：",
            Left = 16,
            Top = 16,
            Width = 470,
            Height = 24,
        };

        var reasons = new CheckedListBox
        {
            Left = 16,
            Top = 48,
            Width = 470,
            Height = 320,
            CheckOnClick = true,
        };

        var options = action switch
        {
            "delete" => new[]
            {
                "质量低 / 第一眼不行",
                "手部或脸部崩坏",
                "结构 / 解剖 / 透视错误",
                "语义不符合提示词",
                "风格不符合目标",
                "主体不清晰",
                "构图不好",
                "重复或相似度太高",
                "后期修复成本太高",
                "其他"
            },
            "favorite" => new[]
            {
                "整体质量高",
                "构图好",
                "主体 / 角色好",
                "风格符合目标",
                "细节丰富",
                "情绪 / 氛围好",
                "可直接使用",
                "适合二次修改",
                "比同组图片更好",
                "其他"
            },
            _ => new[]
            {
                "有潜力但需要对比",
                "局部好但整体一般",
                "可能需要二次修改",
                "风格接近但不确定",
                "与其他图太接近",
                "需要放大检查",
                "当前疲劳 / 暂不判断",
                "其他"
            },
        };

        reasons.Items.AddRange(options);

        var noteLabel = new Label
        {
            Text = "备注：",
            Left = 16,
            Top = 382,
            Width = 470,
            Height = 22,
        };

        var note = new TextBox
        {
            Left = 16,
            Top = 408,
            Width = 470,
            Height = 58,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
        };

        var ok = new Button
        {
            Text = "确定记录",
            Left = 286,
            Top = 480,
            Width = 96,
            DialogResult = DialogResult.OK,
        };

        var cancel = new Button
        {
            Text = "取消",
            Left = 390,
            Top = 480,
            Width = 96,
            DialogResult = DialogResult.Cancel,
        };

        form.Controls.AddRange([prompt, reasons, noteLabel, note, ok, cancel]);
        form.AcceptButton = ok;
        form.CancelButton = cancel;

        if (form.ShowDialog(this) != DialogResult.OK) return null;

        return new ResearchDecision
        {
            Reasons = reasons.CheckedItems.Cast<string>().ToArray(),
            Note = note.Text.Trim(),
        };
    }

    private void Research_RecordMouseMove()
    {
        if (_researchView == null) return;
        _researchView.MouseMoveCount++;
    }

    private void Research_RecordMouseClick()
    {
        if (_researchView == null) return;
        _researchView.MouseClickCount++;
    }

    private void Research_RecordMouseWheel()
    {
        if (_researchView == null) return;
        _researchView.MouseWheelCount++;
    }

    private void Research_RecordZoom()
    {
        if (_researchView == null) return;
        _researchView.ZoomCount++;
    }

    private void Research_RecordPan()
    {
        if (_researchView == null) return;
        _researchView.PanCount++;
    }
}
