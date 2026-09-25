# Sandbox Menu

Barotrauma 的客户端模组：在沙盒模式里编辑一份"生成集合"（spawn set）——条目树、逐项编辑器、物品浏览器、预设存取——然后一键生成到角色背包或关卡里的光标位置。

- 默认按 **F5** 开关菜单（可在游戏设置里改键），控制台命令 `sandboxmenu` 同效
- 单机自己生成；联机时把请求交给服务端生成（见 [多人游戏](#多人游戏)），不改游戏本体
- 内容包含**客户端与服务端两个程序集**，两端都要装本模组（`notsyncedinmultiplayer="false"`）
- 文案三语（英 / 简中 / 繁中）；内容包 `Sandbox Menu 1.0.0`，构建时按本机游戏版本写入程序集元数据

## 功能

**生成集合（左栏树）**

- 物品条目：identifier + 数量区间/堆叠/行为 + 组件属性覆盖；引用条目：指向另一份预设 + 重复次数（保存前检查引用环）
- 选中即改右栏；拖拽可以同级重排、拖进容器条目成为内容物、拖到条目上/下插入；拖到自己的子条目上时，通往它的那条分支先顶到原位再接收被拖的条目（不会自包含）
- 右键条目：加物品 / 加引用 / 加子项（仅物品条目）/ 复制 / **生成到背包 / 生成到鼠标位置**（只生成这一条，与其容器内的嵌套条目一并生效）/ 删除；空白处右键：加物品 / 加引用

**条目编辑器（右栏，随选中条目重建）**

- 数量与堆叠支持区间，可勾"数量取整""装满容器"
- 行为：装备、装备槽多选、安装到潜艇、继承耳机频道、槽位序号、品质、标签
- 组件属性覆盖：组件 + 属性 + 值，选择器从该物品实际的可序列化属性里挑，可增删
- 物品条目带图标/名称/描述预览，identifier 旁的 `...` 打开物品浏览器

**物品浏览器**

- 查询框 + 内容包/分类多选筛选；列表持有每个已加载物品的一行且不重建，筛选只切可见性
- 行显示图标、名称、描述开头与 tooltip，点行回填 identifier

**预设**

- 名称 + 保存 / 加载 / 重新加载 / 打开文件（交系统默认程序）；`Clear set` 清空当前集合
- 存在 `ISettingsService.SaveFolder/Presets/<名>.xml`；文件名按 `Path.GetInvalidFileNameChars` 清洗，改过大小写的旧文件在保存时删除

**生成与反馈**

- `Spawn into inventory` 给当前控制的角色（没有控制角色时提示）；`Spawn at the cursor` 点关卡里的位置，左键落下、右键/Esc 取消，提示语画在世界里
- 数量与堆叠按区间掷值，属性覆盖逐条写进刚生成的物品，结果与失败汇总到状态栏
- 分辨率变化时界面按新的 DIP 缩放重建

**多人游戏**

- 发起生成时，客户端把要生成的条目连同**引用到的预设**序列化成 XML、用 Brotli 压成一段字节，附上目标类型（背包 / 世界坐标）与坐标，发给服务端；服务端解压解析后按同一套生成逻辑执行，再把结果（排队数量 / 失败原因）回执给发起的客户端
- 服务端校验：默认只放行带**控制台命令权限**的客户端（主机与管理员）；模组设置里的 `allowallclients`（默认关，服务端权威并同步到客户端）打开后所有客户端都可用
- 载荷有明确上限（压缩后 48 KiB / 解压后 256 KiB / 512 条目 / 64 个模板 / XML 深度 24），每个客户端每 0.5 秒最多一次请求
- 客户端看到的物品状态：品质、标签、条件、位置由宿主自己的生成载荷带上；**组件属性覆盖**交给宿主自己的属性事件（`Item.ChangePropertyEventData`，见 `SpawnPropertySync`），客户端的物品读取代码自己应用
- 宿主这条事件带不了的覆盖（不是 `[Editable]` 的属性、它写不出去的值类型）会作为**问题**进回执并在日志里说明，不会静默地只留在服务端

**自检**：控制台 `sandboxmenu.markup` 跑一遍内嵌示例标记，汇报标记引擎的加载结果。

## 技术栈

| 层 | 选型 |
| --- | --- |
| 运行时 / 语言 | .NET 8（`net8.0`）、C# 12（`LangVersion=latest`），`Nullable` + `ImplicitUsings`，x64 |
| 宿主 | Barotrauma 插件（`IBarotraumaPlugin`）+ 宿主插件服务（`PluginServiceProvider.GetService<T>()`） |
| 挂载方式 | `ISimpleHookService` 注册 3 个 hook + `IGameScreen.RegisterResolutionChangeEvent` + `IGameNetwork`（两端各注册头枚举与一个 handler）；**零 Harmony 补丁**（`HarmonyX` 仍引用但代码一处没用，留给将来；真打补丁时走宿主的 `IHarmonyProvider`） |
| 游戏 API | `Barotrauma.ReferenceAssemblies.Vanilla.Publicized`（`UsePublicizedAssemblies=true`，切回原版只需改这一处）+ `.Libraries`；`IgnoresAccessChecksTo` 放行 Barotrauma / BarotraumaCore / DedicatedServer |
| 图形 / UI | 游戏自带的 XNA（`Microsoft.Xna.Framework`）与 `GUIComponent` 体系：`GUIFrame`、`GUILayoutGroup`、`GUIListBox`、`GUIButton`、`GUITextBlock`、`GUITickBox`、`GUINumberInput`、`GUITextBox`、`GUIImage`、`GUIDragHandle`、`RectTransform`、`SpriteBatch` |
| 自研 UI 框架 | 工程内的 `UiFramework`：XML 标记 + 数据绑定 + 样式/资源/模板 + 度量布局面板 |
| 其它依赖 | `System.Collections.Immutable` 9.0.0（物品目录的 `ImmutableArray`）；压缩用 BCL 的 `System.IO.Compression`（Brotli） |
| 本地化 | `ContentPackageBuilder/Content/Texts/{English,SimplifiedChinese,TraditionalChinese}.xml`，键前缀 `sandboxmenu.`，读取走 `TextManager.Get` |
| 构建 | MSBuild（`*.csproj` + `*.props` + `*.projitems`/`*.shproj`），`ContentPackageBuilder/Build-Package.ps1` 负责打包与同步 |

## 工程结构

```
BuildData.props              公共属性（ModName、RepositoryURL、publicized 开关）、导入共享项目、AfterBuild 拷产物
References.props             额外引用挂载点
UserBuildData.props.example  本机路径模板（首次构建前复制为 UserBuildData.props）
SandboxMenu.sln / .slnf      解决方案；.slnf 只含 WindowsClient 与共享项目，供 IDE 打开
SharedProject/               两端共用的插件主体（客户端与服务端程序集都编译它）
  SharedItems.props          目标框架、引用、程序集元数据
  SharedSource/
    AssemblySettings.cs      global using、IgnoresAccessChecksTo（特性定义也在这里，不借第三方库）
    Plugin/                  Plugin 生命周期、服务解析、控制台命令（仅客户端）
    Settings/                KeyBind 与原生设置项（按键捕获，仅客户端）
    Infrastructure/          Guard / Log / StaticState（卸载注册中心）/ Identifiers / XmlValue
    Domain/                  Model（条目与 XML）/ Spawning（执行器）/ Editing（属性覆盖）
    Networking/              头枚举、SpawnRequest/SpawnResponse、载荷编解码、覆盖通道、服务端设置
UiFramework/                 自研标记式 UI 框架（共享项目，只编译进客户端程序集）
  Controls/ Data/ Layout/    元素适配器、绑定与路径、度量布局面板
  Styling/ Templating/       样式、触发器、资源字典、数据模板
  ViewLoader 等              视图加载、属性赋值、值读取、静态状态登记、度量换算
ClientProject/               客户端程序集（三个平台各一个 csproj）
  ClientItems.props          客户端公共属性 + 内嵌标记清单
  ClientSource/SandboxMenu/
    UI/                      窗口外壳与对话框宿主、主题、标记、控件、视图模型
      Markup/*.xml           5 个视图（主窗 / 右键菜单 / 浏览器 / 单选弹窗 / 多选弹窗），内嵌为 SandboxMenu.<名>.xml
      Framework/             菜单行、提示条、操作包装、拖放接口
      ViewModels/            主窗、条目编辑器、物品浏览器、选择器、各类行视图模型
    Domain/                  Prefabs（物品目录）/ Persistence（预设）/ Spawning/ClientSpawnDispatcher（联机分流）
    Infrastructure/          延迟队列（帧内队列）
ServerProject/               服务端程序集（三个平台各一个 csproj）
  ServerItems.props          服务端公共属性（`BuildTarget=Server`）
  ServerSource/
    PluginServer.cs          服务端半边：注册头枚举与请求 handler
    ServerSpawnHandler.cs    权限与限流校验、解包解析、执行生成、回执
ContentPackageBuilder/
  Content/                   内容包：PluginInfo.xml、Texts/（三语）、filelist.xml、bin/<平台>
  Build-Package.ps1          交互式三平台构建 + 同步到 ModDeployDir
```

`SharedProject` 与 `UiFramework` 都是 `.shproj`/`.projitems` 共享项目，被 csproj 导入后编译进同一个程序集：客户端得到 `SandboxMenuClient.dll`，服务端得到 `SandboxMenuServer.dll`（`AssemblyName=$(ModName)$(BuildTarget)`）。三平台只是各设一个 `PlatformName` 与 `WINDOWS` 之类的常量。

`SharedProject` 里的代码按 `CLIENT` / `SERVER` 常量裁剪：`PluginSettings`、`PluginCommands`、`Settings/KeyBind|KeySetting` 整体包在 `#if CLIENT` 里，条目模型的文案成员（`Summary` 等）同样只在客户端编译；`BuildData.props` 在 `BuildTarget=Server` 时不导入 `UiFramework.projitems`，所以服务端程序集里没有任何 GUI 类型（已核对产物：无 `GUIFrame` / 视图模型 / 物品目录）。

## UiFramework（标记式 UI）

窗口外壳只有几十行 C#，界面本体是内嵌在程序集里的 XML 标记（`ClientSource/SandboxMenu/UI/Markup/*.xml`，以 `LogicalName` 嵌入）。

- **元素**：`[Element("List")]` 标注的适配器类，一个类包住一个 `GUIComponent` 并声明标记可写的属性（`[ElementProperty]`、`[ElementProperty(Mode = BindingMode.TwoWay)]`）。新增控件支持 = 新增适配器，不动加载器。
- **绑定**：`{Binding Path}` / `{Binding Path, Mode=TwoWay}`，支持多段路径、索引器、值转换器、回退值、格式化；控件自身的改动经 `IPropertyObserver` 写回视图模型。
- **样式与模板**：`Style` + `Setter` + `DataTrigger`（`Setters`/`DataTemplate`/`Setter` 等），资源字典按元素链向上查找，`DataTemplate` 按数据类型或键选中。
- **布局**：`Flow` / `Grid` 面板自己做"度量—排列"两趟，`Length` 支持 `Auto`、`*`（权重）、DIP、百分比；其余情况直接用引擎的 `GUILayoutGroup`。
- **本地化**：目标属性声明为 `LocalizedString` 时，标记里的文字按翻译键读取（`TextManager.Get`）；语言切换由 `LocalizedString` 自己跟进，界面不需要重读一遍。
- **诊断**：标记写错了不抛异常，而是报成带行号的诊断，界面降级继续跑；`sandboxmenu.markup` 命令可随时自检。

## 挂载点与卸载

- `PluginGameModeAddToGUIUpdateListDelegate` — 把窗口挂进 GUI 更新列表
- `PluginOnPostUpdateDelegate` — 热键、位置选择器、弹窗的更新
- `PluginGameModeDrawDelegate` — 绘制位置选择器画在关卡里的提示
- `IGameScreen.RegisterResolutionChangeEvent` — 分辨率变化时按新的 DIP 缩放重建界面
- `IGameNetwork`（客户端半边）— `RegisterNetworkHeaders` 注册头枚举、`RegisterHandler` 收服务端回执、生成时 `Send` 发请求
- `IGameNetwork`（服务端半边）— `RegisterNetworkHeaders` 注册同一个头枚举、`RegisterHandler` 收请求、`SendToClient` 回执
- 属性覆盖不注册任何东西：它走宿主自己的物品属性事件，两端都由游戏读写

没有 Harmony 补丁：挂载点全部由宿主的原生 hook 与服务表达。`HarmonyX` 引用着备用，但它并不空闲——运行时承认的 `IgnoresAccessChecksTo` 基础库只在运行时包里带、编译期看不到，`AssemblySettings.cs` 那三条程序集级声明用的就是它 MonoMod 里那一份，所以这条包引用与"能访问宿主内部成员"是一体的，不能顺手删。插件服务（`IDebugConsole`、`ISettingsService`、`IGameScreen`、`ISimpleHookService`、`IGameNetwork`）经惰性字段解析获得（`GetService<T>()` 用 `Assembly.GetCallingAssembly()` 认插件，调用必须留在本程序集的帧上）。注册进插件服务的东西**不用手工退回**：命令、hook、网络 handler 都由服务记着，宿主卸载插件时随服务释放一并清掉；手工 `Deregister*` 既是多余动作，又容易撞上"服务已释放"的时序。插件自己的静态态在 `DisposeProjectSpecific` 里 `StaticState.ResetAll()`。

## 构建

```powershell
# 只编译（默认 Windows / Release）
dotnet build ClientProject/WindowsClient.csproj -c Release
dotnet build ServerProject/WindowsServer.csproj -c Release

# 编译三平台 × 客户端/服务端 + 同步到游戏 LocalMods
pwsh ContentPackageBuilder/Build-Package.ps1
```

- 首次构建前把 `UserBuildData.props.example` 复制成 `UserBuildData.props`，填好 `GameExecutableDir` 与 `ModDeployDir`
- 构建后自动把 `dll` / `deps.json` / `pdb` 拷到 `ContentPackageBuilder/Content/bin/<平台>`（客户端与服务端各自的产物放进同一个平台目录）
- `Build-Package.ps1` 按 `win-x64` / `linux-x64` / `osx-x64` 三个 RID 构建（默认两个目标都构建），再用 robocopy `/MIR` 镜像同步内容包到部署目录——目标目录里多出的文件会被删除

## 几条贯穿全工程的约定

- **卸载可回收**：游戏在可回收的 `AssemblyLoadContext` 里加载插件。所有静态可变状态都在自己的静态构造函数里 `StaticState.Register(...)` 登记清理，客户端由 `SandboxMenuWindow.Shutdown → StaticState.ResetAll()`、服务端由 `PluginServer.DisposeProjectSpecific → StaticState.ResetAll()` 统一清空；引用插件代码的组件在关闭时 `Parent = null` 且 `RemoveFromGUIUpdateList()`。服务端每次请求的限流标记存在宿主自己的弱表里（`Client` 的 extra field），不额外持有玩家对象。
- **分辨率无关**：尺寸一律是设备无关像素（DIP），由 `UiMetrics.Dip/DipInt` 按游戏的 HUD 缩放换算，文字走 `FontScale`；不直接依赖分辨率数值。
- **跨平台**：不碰 Windows 独有 API（WPF/WinForms、`System.Drawing`、注册表、P/Invoke）；路径一律 `Path.Combine`，保存与读取共用同一条拼接函数以保证大小写一致。`System.Windows.Input`（`ICommand`）是全平台基础库，不算违规。
- **不写游戏全局状态**：`GUIStyle.*` / `GUI.*` / `GameMain.*` / `PlayerInput.*` 只读不写，卸载后不留残留。
- **内容变更响应**：内容加载回调后按内容修订号（`ContentRevision`）判断缓存失效，物品目录按 identifier 排序一次，界面自行判定内容是否变化。
- **失败隔离**：从游戏更新与 GUI 遍历里进来的调用统一走 `Guard` / `MenuActions`，异常只记日志，不打断游戏；预设读写与目录扫描失败也只落日志并给出状态栏提示。
