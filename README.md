# 经世小镇

一款用 Unity 制作的 2D 小镇经济模拟游戏。玩家从经营果汁店出发，在同一个小镇里观察企业、居民、政府、银行与国际市场如何相互影响。游戏提供按顺序推进的八篇剧情，也支持从指定篇章开始游玩。

## 主要内容

- **经营店铺**：设置售价、备货和店务，开门营业；通过每日报告查看需求、销量、库存、成本与利润，以及这些结果产生的原因。
- **体验不同视角**：在居民生活中安排消费、工作、闲暇和储蓄；在市政页面调整税收、补贴与公共项目；查看宏观经济中的产出、物价和就业。
- **观察金融与贸易**：查看银行资产负债、企业融资和央行政策，也可以探索国际贸易、汇率及相关政策。
- **用数据理解变化**：通过曲线、经济实验室、情景比较和经营日报，对照决策前后的经济结果。
- **探索小镇**：走进店铺和其他建筑，与场景交互，观察时间、天气、居民和街景随游戏进展变化。

## 快速开始

### 环境

- Unity **6000.5.8f1**。项目使用的版本记录在 `ProjectSettings/ProjectVersion.txt`。
- Git，用于获取工程文件。

### 运行

```bash
git clone https://github.com/Chriss09-ui/econ-town.git
```

1. 在 Unity Hub 中添加克隆得到的 `econ-town` 文件夹作为项目，并使用上述 Unity 版本打开。首次打开时，等待 Unity 导入资源和解析包。
2. 打开 `Assets/Scenes/Main.unity`。
3. 点击 Unity 编辑器顶部的 **Play**。启动页可选择「新的剧情」「继续剧情」「选篇开始」或「继续篇章游玩」。

地图、角色和界面由 `GameBootstrap` 在运行时创建，因此只查看静态场景不会看到完整的小镇。

## 如何游玩

1. 在启动页选择完整剧情或篇章游玩。篇章游玩使用独立存档，不会覆盖完整剧情的进度。
2. 走到建筑或交互点附近按 **E**；部分建筑也支持鼠标点击。可以用屏幕下方的导航直接打开任务、店铺、市场、曲线、生活、市政、宏观、金融和手册页面。
3. 在店铺中设置售价、备货和店务，点击「开始营业」。营业结束后查看当日的销量、成本、利润和解释，再从报告进入下一天。
4. 随着剧情推进，尝试居民、政策、金融和国际贸易等视角，观察同一世界中的连锁变化。

| 操作 | 按键或方式 |
| --- | --- |
| 移动 | **WASD** 或方向键 |
| 与附近对象互动 | **E** |
| 关闭窗口或跳过过场 | **Esc** |
| 使用面板、选择建筑 | 鼠标点击 |

如果键盘操作没有响应，先点击 Unity 的 **Game** 视图，让游戏窗口获得焦点。

## 存档

完整剧情和篇章游玩使用各自的存档。游戏将 JSON 存档、备份及关键节点记录保存在 Unity 的 `Application.persistentDataPath` 对应目录中；这些本地数据不会随仓库提交。音量、显示等设置在两种游玩模式之间共享。

## 工程结构

```text
Assets/
  Scenes/Main.unity              主场景
  Scripts/EconTown/
    Art/                         像素美术与建筑外观
    Core/                        经济模拟、进度和存档
    UI/                          HUD、面板与图表
    World/                       地图、角色与场景交互
    Editor/                      编辑器工具和测试
    GameBootstrap.cs             运行时装配入口
  Resources/Fonts/               游戏字体
  Settings/                      输入与渲染资源
  StreamingAssets/Kenney/        小镇素材
  ThirdPartyNotices/             第三方资源授权说明
Packages/                        Unity 包配置
ProjectSettings/                 Unity 项目设置
```

## 继续开发

仓库根目录就是完整的 Unity 工程。首次打开时，Unity 会根据 `Packages/manifest.json` 和 `Packages/packages-lock.json` 恢复依赖并生成缓存，无需原作者的 `Library` 目录或本机编辑器扩展。

- 修改经济模型、结算或存档逻辑，从 `Assets/Scripts/EconTown/Core/` 开始；修改面板和图表，从 `UI/` 开始；修改地图与交互，从 `World/` 开始。
- 新增、移动或删除资源时，优先在 Unity 编辑器内操作，并将资源对应的 `.meta` 文件一起提交，以保留场景和资源之间的引用。
- `Assets/` 中的工程资源、包配置和必要的 `ProjectSettings/` 设置应随修改提交。`Library/`、`Temp/`、构建输出和个人编辑器设置由 `.gitignore` 排除。
- 共享开发文档和工具可以放在 `docs/`、`tools/`；PRD、内部归档和已知本地工作文件仍然排除。`.env.example`、`.env.sample` 可以提交占位配置，真实凭据应保存在本地。

项目包含编辑器测试，位于 `Assets/Scripts/EconTown/Editor/`，可通过 Unity Test Runner 的 **EditMode** 入口运行。修改经济模拟或交互逻辑后，先运行对应测试，再打开 `Main.unity` 试玩。

WebGL 构建入口为 Unity 菜单 **EconTown → Build → WebGL**。构建前需为对应 Unity 版本安装 WebGL Build Support 模块；默认输出到工程目录上一级的 `Builds/WebGL`。

## 第三方资源

- [Kenney Roguelike Modern City](Assets/StreamingAssets/Kenney/LICENSE_kenney.txt)：CC0。
- [Noto Sans CJK 字体](Assets/ThirdPartyNotices/NotoSansCJK-OFL.txt)：SIL Open Font License 1.1。

以上授权适用于各自对应的素材和字体。
