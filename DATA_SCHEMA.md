# DATA_SCHEMA

本文件说明 `JH-ImageGlass-ResearchMode` 输出的筛图行为日志格式。

日志文件名：

```text
imageglass_research_log.jsonl
```

格式：`JSONL`，每一行是一条完整 JSON 记录。

## 示例

```json
{
  "eventType": "image_view_decision",
  "sessionId": "20260601-153012-a1b2c3d4",
  "timestamp": "2026-06-01T15:31:02.123+08:00",
  "startedAt": "2026-06-01T15:30:55.000+08:00",
  "endedAt": "2026-06-01T15:31:02.123+08:00",
  "viewDurationMs": 7123,
  "action": "delete",
  "trigger": "delete_from_disk",
  "reasons": ["手部或脸部崩坏", "后期修复成本太高"],
  "note": "主体不错但脸部错误明显",
  "filePath": "D:\\images\\batch01\\0001.png",
  "fileName": "0001.png",
  "folderPath": "D:\\images\\batch01",
  "fileExistsAtLogTime": false,
  "deletedFromDisk": true,
  "imageIndex": 0,
  "imageCount": 100,
  "visitCount": 1,
  "interactions": {
    "MouseMoveCount": 23,
    "MouseClickCount": 1,
    "MouseWheelCount": 2,
    "ZoomCount": 1,
    "PanCount": 0
  }
}
```

## 顶层字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `eventType` | string | 固定为 `image_view_decision`，表示一次图片观看与决策记录 |
| `sessionId` | string | 本次打开软件产生的会话 ID，用于区分不同筛图批次 |
| `timestamp` | datetime | 日志写入时间，等同于当前记录结束时间 |
| `startedAt` | datetime | 当前图片开始观看时间 |
| `endedAt` | datetime | 当前图片结束观看时间 |
| `viewDurationMs` | number | 当前图片观看时长，单位毫秒 |
| `action` | string | 用户或系统对当前图片的最终操作 |
| `trigger` | string | 触发该记录的具体方式 |
| `reasons` | string[] | 用户选择的原因，可多选 |
| `note` | string | 用户手动填写的备注 |
| `filePath` | string | 图片完整路径 |
| `fileName` | string | 图片文件名 |
| `folderPath` | string | 图片所在文件夹 |
| `fileExistsAtLogTime` | boolean | 写入日志时文件是否仍存在 |
| `deletedFromDisk` | boolean | 是否执行删除文件操作 |
| `imageIndex` | number | 当前图片在 ImageGlass 图片列表中的索引 |
| `imageCount` | number | 当前图片列表总数量 |
| `visitCount` | number | 当前会话内该图片被查看的次数 |
| `interactions` | object | 当前图片观看期间的交互统计 |

## action 取值

| 值 | 含义 |
|---|---|
| `favorite` | 收藏 / 保留 |
| `delete` | 删除 |
| `uncertain` | 不确定，暂缓判断 |
| `skip` | 跳过，通常是切换图片时自动记录 |
| `session_end` | 关闭窗口或会话结束 |

## trigger 取值

| 值 | 含义 |
|---|---|
| `keyboard_ctrl_1` | 通过 `Ctrl + 1` 收藏 / 保留 |
| `keyboard_ctrl_3` | 通过 `Ctrl + 3` 标记不确定 |
| `delete_from_disk` | 真实删除文件 |
| `delete_to_recycle_bin` | 删除到回收站；当前研究版本一般不使用 |
| `auto_replaced_by_loaded_image` | 切换到新图片时自动结束上一张 |
| `form_closing` | 关闭窗口时结束当前图片记录 |

## interactions 字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `MouseMoveCount` | number | 当前图片观看期间鼠标移动事件数量 |
| `MouseClickCount` | number | 鼠标点击数量 |
| `MouseWheelCount` | number | 鼠标滚轮数量 |
| `ZoomCount` | number | 缩放次数 |
| `PanCount` | number | 平移次数 |

这些字段可以作为认知负荷的间接行为指标。例如：

- `viewDurationMs` 增加：可能代表判断困难、比较负荷增加或疲劳导致效率下降。
- `ZoomCount` 增加：可能代表细节检查需求增加。
- `visitCount` 增加：可能代表反复比较或决策不确定。
- `uncertain` 比例增加：可能代表后期筛选疲劳或质量边界模糊。

## Python 读取示例

```python
import json
from pathlib import Path

log_path = Path("imageglass_research_log.jsonl")
records = []

with log_path.open("r", encoding="utf-8") as f:
    for line in f:
        if line.strip():
            records.append(json.loads(line))

print(len(records))
print(records[0])
```

## 转换为 CSV 示例

```python
import json
import csv
from pathlib import Path

src = Path("imageglass_research_log.jsonl")
dst = Path("imageglass_research_log.csv")

rows = []
with src.open("r", encoding="utf-8") as f:
    for line in f:
        if not line.strip():
            continue
        item = json.loads(line)
        interactions = item.pop("interactions", {})
        item.update(interactions)
        item["reasons"] = ";".join(item.get("reasons", []))
        rows.append(item)

with dst.open("w", encoding="utf-8-sig", newline="") as f:
    writer = csv.DictWriter(f, fieldnames=rows[0].keys())
    writer.writeheader()
    writer.writerows(rows)
```
