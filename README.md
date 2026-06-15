# CRPG-Demo

一个 Unity 2D 叙事调查原型。项目围绕“梦境、记忆、证据与白天调查”的循环展开，玩家在卧室、梦境长廊、医院、警局、心理咨询室、案发客厅等场景之间推进剧情，通过调查透明热点、阅读证据和选择分支逐步还原案件真相。

## 项目状态

- 引擎版本：Unity `6000.3.16f1`
- 类型：2D 叙事调查 / 剧情解谜原型
- 核心场景：`Assets/Scenes/SampleScene.unity`
- 核心脚本：`Assets/Scripts/SceneMirrorCaseRuntime.cs`
- 文本资源：`Assets/Resources/Narrative`
- 音效资源：`Assets/Resources/Audio`

## 玩法说明

玩家每天从现实调查推进到夜晚梦境。白天调查场景通常需要回到警局办公室进行中转；夜晚则从卧室上床，进入梦境长廊，再通过对应的门进入当夜梦境。

主要交互：

- 鼠标点击：调查当前场景中的可交互物品
- `Tab`：显示/隐藏可交互物品高亮
- `F`：在满足当前场景条件时尝试前往下一场景
- 卧室场景：需要调查床并确认睡觉，进入梦境流程
- 证据按钮：打开证据陈列页面，查看已收集证物

## 内容结构

```text
Assets/
  Art/                         美术背景、人物与场景素材
  Resources/
    Audio/                     运行时加载的音效
    Narrative/                 剧情、证词、梦境与调查文本
  Scenes/
    SampleScene.unity          当前主场景
  Scripts/
    PixelMirrorCase.cs         早期像素场景逻辑
    SceneAudioController.cs    场景音效控制
    SceneHotspot.cs            交互热点组件
    SceneMirrorCaseRuntime.cs  当前主要游戏流程与 UI 逻辑
```

## 运行方式

1. 安装 Unity `6000.3.16f1` 或兼容版本。
2. 使用 Unity Hub 打开仓库根目录。
3. 打开 `Assets/Scenes/SampleScene.unity`。
4. 点击 Play 运行。

如果 Unity 提示重新导入资源，等待导入完成后再运行场景。

## 开发注意事项

- 不要把 `game/`、`gameweb/`、`game1.app/`、`game.zip` 等导出产物提交进仓库。
- 不要提交 `.DS_Store` 或 Burst debug 临时目录。
- 剧情文本应放在 `Assets/Resources/Narrative`，开发说明和大纲文本不要直接混入玩家可见内容。
- 可交互物品应保持透明，默认只在鼠标悬停或按下 `Tab` 时高亮。
- 白天调查的场景切换应经过警局办公室；夜晚梦境应经过“卧室 -> 梦境长廊 -> 对应梦境”的流程。
- 新增场景时，需要检查热点是否贴合背景图中的真实物品，而不是漂浮在空白处。

## 发布流程

建议发布前先确认：

- 主场景能正常进入游戏。
- 每个可交互热点都能点击并显示正确文本。
- `F` 键只在当前场景条件满足后允许进入下一场景。
- 卧室床、梦境长廊门、警局中转、证据页面都能正常工作。
- 构建包来自最新 Unity 工程，而不是旧的导出目录。

常用 Git 流程：

```bash
git status
git add .gitignore Assets/Scripts Assets/Scenes Assets/Resources/Narrative ProjectSettings Packages
git commit -m "Prepare story flow release"
git tag -a v0.1.0 -m "v0.1.0"
git push origin main
git push origin v0.1.0
```

之后在 GitHub Releases 页面创建新 release，并上传最新构建包。
