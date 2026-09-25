# 新人入职文档 (Onboarding) — Sandbox Menu

> 本文档面向刚加入项目、熟悉 C# 但不了解本工程或 Barotrauma 模组开发的同学。
> 前半部分（第 1–2 章）让你**快速跑起来并了解背景**；后半部分（第 3–5 章）是给**真正要维护/扩展代码**的工程师的架构与代码导览。
> 关键术语均附英文对照，例如 *插件加载上下文 (AssemblyLoadContext)*。

---

## 目录 (Contents)

1. [新人快速上手 (Quick Start)](#1-新人快速上手-quick-start)
2. [Barotrauma 模组背景入门 (Modding Primer)](#2-barotrauma-模组背景入门-modding-primer)
3. [架构与运作 (Architecture & Operation)](#3-架构与运作-architecture--operation)
4. [代码导览 (Code Map)](#4-代码导览-code-map)
5. [新人红线 (Engineering Rules for New Hires)](#5-新人红线-engineering-rules-for-new-hires)

---

## 1. 新人快速上手 (Quick Start)

### 1.1 环境前置 (Prerequisites)

- 安装 **.NET 8 SDK**（运行时 `net8.0`，x64）。
- 本机路径配置：把仓库根目录的 `UserBuildData.props.example` 复制为 **`UserBuildData.props`**，填入两个路径：
  - `GameExecutableDir`：Barotrauma 可执行文件所在目录（用于读取游戏版本号写入程序集元数据）。
  - `ModDeployDir`：本地模组部署目录（必须以 `\` 结尾）。`Build-Package.ps1` 会把内容包镜像同步到这里。

```powershell
# 仓库根目录执行
Copy-Item UserBuildData.props.example UserBuildData.props
# 然后编辑 UserBuildData.props，填好两个路径
```

### 1.2 构建 (Build)

```powershell
# 只编译（默认 Windows / Release），最快的本地验证方式
dotnet build ClientProject/WindowsClient.csproj -c Release
dotnet build ServerProject/WindowsServer.csproj -c Release

# 编译三平台 × 客户端/服务端 + 同步到本地模组目录
pwsh ContentPackageBuilder/Build-Package.ps1
```

> 构建后 `BuildData.props` 的 `CopyFiles` 目标会把 `dll` / `deps.json` / `pdb` 拷到
> `ContentPackageBuilder/Content/bin/<平台>`。`Build-Package.ps1` 再按 `win-x64` / `linux-x64` / `osx-x64`
> 三个 RID 构建，并用 robocopy `/MIR` 把内容包镜像同步到 `ModDeployDir`（目标目录多出的文件会被删除，注意别把别的东西放进去）。

### 1.3 在游戏里跑起来 (Run In-Game)

- 默认按 **F5**（或在游戏控制台输入 `sandboxmenu`）开关菜单。按键可在游戏设置里改（`togglekey`，默认 F5）。
- 单机自己生成；联机时由客户端把请求交给服务端生成（见 [3.2](#32-数据流-data-flow)）。内容包含客户端与服务端两个程序集，**两端都要装本模组**。
- 自检标记引擎：控制台输入 **`sandboxmenu.markup`**，会跑一遍内嵌示例标记并汇报加载结果——改了 UI 框架或某段标记后，用它快速验证。

### 1.4 头 30 分钟阅读路线 (First 30 Minutes)

按这个顺序打开文件，就能建立整体心智模型：

1. `SharedProject/SharedSource/Plugin/Plugin.cs` + `ClientProject/ClientSource/SandboxMenu/PluginClient.cs` + `PluginHooks.cs` —— 插件生命周期、hook 注册、`StaticState`。
2. `UiFramework/StaticState.cs`，然后 `UiFramework/ViewLoader.cs` + `UiFramework/ElementRegistry.cs` —— UI 是怎么从 XML 建出来的。
3. `ClientProject/ClientSource/SandboxMenu/UI/SandboxMenuWindow.cs` + `UI/UiWindow.cs` —— 窗口外壳。
4. `UI/ViewModels/SpawnMenuViewModel.cs` + `Domain/Model/SpawnSet.cs` / `ItemEntry.cs` —— 数据模型。
5. `SharedProject/SharedSource/Domain/Spawning/ItemSpawnService.cs` + `SpawnExecutor.cs` —— 真正生成物品的地方。
6. `SharedProject/SharedSource/Networking/SpawnPayloadCodec.cs` + `ClientSpawnDispatcher.cs` + `ServerProject/ServerSource/ServerSpawnHandler.cs` —— 联机那条路（单机不看这三个文件也不会缺什么）。
7. `BuildData.props` + `SharedProject/SharedItems.props` + `ContentPackageBuilder/Build-Package.ps1` —— 构建与部署（两个程序集各一份产物）。

---

## 2. Barotrauma 模组背景入门 (Modding Primer)

### 2.1 Barotrauma 是什么

Barotrauma 是一款基于 XNA / `Microsoft.Xna.Framework` 的 2D 合作潜艇模拟游戏。它的客户端用 C# 编写，模组（mod）以**内容包 (content package)** 形式分发，而带逻辑代码的模组则以**插件 (plugin)** 形式挂载。本工程就是这样一个客户端插件。

### 2.2 插件如何加载 (Plugin Loading)

- 插件实现宿主接口 **`IBarotraumaPlugin`**（`Init` / `OnContentLoaded` / `Dispose`）。本工程里 `Plugin` 是一个**跨三个项目拆分的 partial 类**：
  - 两端共用的骨架在 `SharedProject/SharedSource/Plugin/`（生命周期、服务解析）。
  - 客户端专用逻辑在 `ClientProject/ClientSource/SandboxMenu/PluginClient.cs`（hook、分辨率事件、菜单命令、按键设置、网络客户端半边）。
  - 服务端专用逻辑在 `ServerProject/ServerSource/PluginServer.cs`（注册请求 handler；`ServerSpawnHandler` 做校验与生成）。
- 插件通过宿主自己的 **`PluginServiceProvider`** 获取插件服务（`IDebugConsole`、`ISettingsService`、`IGameScreen`、`ISimpleHookService`、`IGameNetwork` 等）。获取方式是惰性字段，调用直接写在本程序集的属性里，详见 [3.4](#34-挂载与卸载-mount--unmount)。
- 加载点在 `PluginInfo.xml` 里声明：两条 `MainAssemblyInfo`，`targets="Client"` 指向 `bin/<平台>/SandboxMenuClient.dll`、`targets="Server"` 指向 `bin/<平台>/SandboxMenuServer.dll`。宿主的 `PluginAssemblyInfo.GetCurrentTarget()` 在客户端程序里固定返回 `Client`、在 DedicatedServer 里固定返回 `Server`——**游戏的"主机"模式另行拉起 DedicatedServer 进程**，所以服务端角色只能由独立程序集承担。

### 2.3 Publicized 程序集 (Publicized Assemblies)

宿主（`Barotrauma` / `BarotraumaCore` / `DedicatedServer`）的大量内部 (internal) 成员本不允许外部访问。本工程用两套机制打通：

- **编译期**：`BuildData.props` 里的 `<UsePublicizedAssemblies>true</UsePublicizedAssemblies>` 选用
  `Barotrauma.ReferenceAssemblies.Vanilla.Publicized` 这个 NuGet 包（已把 internal 提升为 public 的参考程序集）。
  想切回原版参考程序集，只改这一处开关即可（无需改代码）。
- **运行期**：`SharedProject/SharedSource/AssemblySettings.cs` 里的
  `[assembly: IgnoresAccessChecksTo("Barotrauma" / "BarotraumaCore" / "DedicatedServer")]`
  放行对这些程序集的访问检查。

> 因为是 publicized，代码里可以直接访问宿主的内部类型/字段/方法，**不需要反射**。

### 2.4 内容包 (Content Package)

内容包描述文件在 `ContentPackageBuilder/Content/`：

- `PluginInfo.xml` —— 每个平台两条 `MainAssemblyInfo`（客户端与服务端各一条，`targets="Client"` / `targets="Server"`）。
- `filelist.xml` —— `contentpackage` 根，引用 `PluginInfo.xml` 与三份 `Texts` 文案。
- `Texts/{English,SimplifiedChinese,TraditionalChinese}.xml` —— 本地化字符串，键前缀 `sandboxmenu.`，运行时走 `TextManager.Get`。

### 2.5 键位与控制台 (Keybind & Console)

- 键位：`SharedProject/SharedSource/Plugin/PluginSettings.cs` 注册了 `togglekey`（默认 **F5**），并通过 `ToggleKey` 暴露。
- 控制台命令：`PluginCommands.cs` 注册了 `sandboxmenu`（开关菜单）与 `sandboxmenu.markup`（标记自检），都在 `#if CLIENT` 内。

### 2.6 可回收的卸载 (Reclaimable Unload)

游戏在**可回收的插件加载上下文 (reclaimable AssemblyLoadContext)** 里加载插件。这意味着卸载时若存在任何对插件代码的**强引用残留**，就会触发"未能完整卸载"警告并导致内存滞留。因此本工程有一条铁律：任何订阅/注册都必须有对称退订，所有静态可变状态都集中登记到 `StaticState`，在关闭时统一清空（详见 [3.5](#35-生命周期与静态态清理-lifecycle--static-state) 与第 5 章）。

---

## 3. 架构与运作 (Architecture & Operation)

### 3.1 工程分层 (Layering)

仓库里有四类工程：

| 工程 | 类型 | 作用 |
| --- | --- | --- |
| `SharedProject` | `.shproj` 共享项目 | 两端共用：`Plugin` 生命周期、`Infrastructure`、`Domain`（条目模型 / 生成 / 属性编辑）、`Networking` |
| `UiFramework` | `.shproj` 共享项目 | 自研标记式 MVVM UI 框架（只在客户端编译） |
| `ClientProject/WindowsClient.csproj` 等 | 三平台 csproj | 客户端程序集，定义平台常量并导入上述共享项目 |
| `ServerProject/WindowsServer.csproj` 等 | 三平台 csproj | 服务端程序集，导入同一份共享项目但不导入 UI 框架 |
| `ContentPackageBuilder` | 目录 + 脚本 | 内容包定义与打包/同步脚本 |

`SharedProject` 被两端 csproj 导入，分别**编译成 `SandboxMenuClient.dll` 与 `SandboxMenuServer.dll`**（`AssemblyName=$(ModName)$(BuildTarget)`）；`UiFramework` 由 `BuildData.props` 在 `BuildTarget=Server` 时跳过，服务端产物里因此没有任何 GUI 类型。各平台只是设一个 `PlatformName` 与 `WINDOWS` / `LINUX` / `MAC` 常量；两端差异由 `ClientItems.props` / `ServerItems.props` 里的 **`CLIENT` / `SERVER` 编译常量** 控制（`#if CLIENT`、`#if SERVER`）。

### 3.2 数据流 (Data Flow)

```
SpawnSet (领域模型, 可序列化)
   │  被 ViewModel 包装
   ▼
SpawnMenuViewModel (根 VM)
   ├─ Entries  → TreeEntryViewModel[]  (左栏条目树, 拖拽/重排/嵌套)
   ├─ Editor   → EntryEditorViewModel  (右栏, 随选中项重建)
   ├─ Browser  → ItemBrowserViewModel  (物品浏览器弹窗)
   └─ 命令中继 → 预设存取 / 生成
   │  UI 标记 (Markup/*.xml) 经 ViewLoader 绑定到 VM
   ▼
Domain.Spawning (生成执行)
   ItemSpawnService → SpawnExecutor → 落进背包 / 世界坐标
   │  属性覆盖经 Editing.PropertyEditService 写进刚生成的物品
   ▼
结果汇总到状态栏 (SpawnResult)
```

- 模型 (`Domain/Model`) 只描述"要生成什么"，不碰 UI 与宿主。
- ViewModel (`UI/ViewModels`) 把模型适配成 UI 可绑定的形态，并承载命令。
- UI 本体是内嵌在程序集里的 **XML 标记**（`UI/Markup/*.xml`），由 `UiFramework` 加载器解析为 `GUIComponent` 树并绑定到 VM。

联机时最后一步改走网络（单机路径不变）：

```
SpawnMenuViewModel
   ├─ 单机   → ItemSpawnService（本地直接生成）
   └─ 联机   → ClientSpawnDispatcher
                 ├─ 收集引用到的预设 → SpawnPayload.ToXml() → Brotli 压缩
                 ├─ IGameNetwork.Send(SpawnRequest)          ← 客户端半边
                 └─ ServiceSpawnHandler / Respond            ← 服务端半边（独立程序集）
                        ├─ 权限 + 限流 → 解包解析（上限）→ client.Character
                        ├─ ItemSpawnService.Spawn(...)      ← 与单机同一套执行器
                        └─ SendToClient(SpawnResponse)      → 状态栏（SandboxMenuWindow.HandleNotices 取用）
```

组件属性覆盖不另立通道：服务端把**真正写进去**的那些属性交给宿主自己的属性事件（`Item.ChangePropertyEventData` + `IGameNetwork.CreateVanillaEntityEvent`，封装在 `SpawnPropertySync` 里），客户端由游戏的物品读取代码按同一套规则应用——线格式、读写两端都在宿主手里。宿主这条事件带不了的东西（不是 `[Editable]` 的属性、它的写入分支没有的值类型、物品只有一个可编辑属性的边角情形）会作为**问题**进回执与日志，而不是静默地只留在服务端。

### 3.3 自研 UI 框架 (UiFramework 速览)

窗口外壳只有几十行 C#，界面本体是 XML 标记：

- **元素 (Element)**：用 `[Element("List")]` 标注的适配器类包住一个 `GUIComponent`，并声明标记可写属性（`[ElementProperty]` / `[ElementProperty(Mode = BindingMode.TwoWay)]`）。新增控件 = 新增适配器，不动加载器。
- **绑定 (Binding)**：`{Binding Path}` / `{Binding Path, Mode=TwoWay}`，支持多段路径、索引器、值转换器、回退值、格式化；控件改动经 `IPropertyObserver` 写回 VM。
- **样式与模板 (Style & Template)**：`Style` + `Setter` + `DataTrigger`；资源字典按元素链向上查找；`DataTemplate` 按数据类型或键选中。
- **布局 (Layout)**：`Flow` / `Grid` 面板自己做"度量—排列"两趟，`Length` 支持 `Auto`、`*`（权重）、DIP、百分比；其余情况直接用引擎的 `GUILayoutGroup`。
- **本地化 (Localization)**：属性声明为 `LocalizedString` 时，标记文字按翻译键读取（`TextManager.Get`）；语言切换由 `LocalizedString` 自己跟进，界面无需重读。
- **诊断 (Diagnostics)**：标记写错不抛异常，而是报成带行号的 `MarkupDiagnostic`，界面降级继续跑；`sandboxmenu.markup` 可随时自检。

### 3.4 挂载与卸载 (Mount & Unmount)

本工程**零 Harmony 补丁 (HarmonyX 引用仍在但代码已不使用)**。所有挂载点都走宿主原生 hook：

- `PluginGameModeAddToGUIUpdateListDelegate` —— 把窗口挂进 GUI 更新列表。
- `PluginOnPostUpdateDelegate` —— 热键、位置选择器、弹窗的每帧更新。
- `PluginGameModeDrawDelegate` —— 绘制位置选择器画在关卡里的提示。
- `IGameScreen.RegisterResolutionChangeEvent` —— 分辨率变化时按新的 DIP 缩放重建界面。
- `IGameNetwork`（分别由两半注册）—— 头枚举、请求 handler（服务端）/ 回执 handler（客户端）、`Send` / `SendToClient`。属性覆盖不注册任何东西，它走宿主自己的物品属性事件。

宿主服务经惰性字段直接解析获得：调用就写在本程序集的属性里，不再套一层 `NoInlining` 的解析方法。真正的约束只有一条——入口用 `Assembly.GetCallingAssembly()` 识别插件，所以 `GetService<T>()` 必须由本程序集的某一帧发起（交给 `Lazy` 的工厂委托就丢了这层栈帧）。

**注册进插件服务的项一律不用手工注销**：命令、hook、网络 handler、各类注册器都由服务自己记着本插件注册了什么，宿主卸载插件时随服务释放一并清掉（清掉即注销）。手工 `Deregister*` 除了多余，还会踩时序坑（服务释放后再 `Register` 直接抛 `ObjectDisposedException`）。所以客户端 `DisposeProjectSpecific` 里只剩 `SandboxMenuWindow.Shutdown()` 与静态态清理。

### 3.5 生命周期与静态态清理 (Lifecycle & Static State)

```
Init()
  └─ ServerOptions.Register() + InitializeProjectSpecific()
        ├─ 客户端半边: CreateSettings() + RegisterCommands() + 注册 3 个 hook + 分辨率事件
        │              + 注册网络头与回执 handler
        └─ 服务端半边: 注册网络头与请求 handler（属性覆盖不需注册，走宿主的物品事件）

每帧 (仅 Game 界面, 客户端):
  AddToGUIUpdateListHook  → SandboxMenuWindow.Current.AddToUpdateList()
  GameModeDrawHook        → 绘制生成位置提示
  PostUpdateHook          → F5 热键 / 位置选择器 / 弹窗更新 (菜单或控制台打开时忽略输入)

Dispose()
  └─ DisposeProjectSpecific()
        ├─ 客户端: SandboxMenuWindow.Shutdown() → StaticState.ResetAll()
        │           （命令 / hook / 网络注册都由宿主服务在卸载时清掉）
        ├─ 服务端: StaticState.ResetAll()
        └─ 客户端: 解除 togglekey 绑定
```

**`StaticState` 是卸载安全的核心**：任何持有宿主服务引用或缓存状态的类型，都通过 `StaticState.Register(Action)` 登记一个清理回调；`SandboxMenuWindow.Shutdown()` 调用 `StaticState.ResetAll()` 统一执行。`Plugin` 的静态构造函数登记了对四个服务字段的置空与 togglekey 解绑。`UiMetrics.Reset()`、日志去重状态等也都登记在这里。

### 3.6 贯穿全工程的约定 (Cross-Cutting Conventions)

- **卸载可回收**：见 [3.5](#35-生命周期与静态态清理-lifecycle--static-state)。
- **分辨率无关 (Resolution-independent)**：尺寸一律走设备无关像素 **DIP**，由 `UiFramework/UiMetrics.cs` 的 `Dip` / `DipInt` 按游戏 HUD 缩放换算，文字走 `FontScale`；不直接依赖分辨率数值。
- **跨平台 (Cross-platform)**：不碰 Windows 独有 API（WPF / WinForms / `System.Drawing` / 注册表 / P-Invoke）；路径一律 `Path.Combine`，保存与读取共用同一条拼接函数以保证大小写一致。`System.Windows.Input`（`ICommand`）是全平台基础库，不算违规。
- **不写游戏全局态**：`GUIStyle.*` / `GUI.*` / `GameMain.*` / `PlayerInput.*` 只读不写，卸载后不留残留。
- **失败隔离 (Failure isolation)**：从游戏更新与 GUI 遍历进来的调用统一走 `Guard` / `MenuActions`，异常只记日志，不打断游戏；预设读写与目录扫描失败也只落日志并给状态栏提示。

---

## 4. 代码导览 (Code Map)

下面按区域给出**文件路径 + 最该先读的类型**。所有路径相对于仓库根。

### 4.1 插件与生命周期 (Plugin & Lifecycle) — `SharedProject/SharedSource/` + `ClientProject/ClientSource/SandboxMenu/`

| 文件 | 首要类型 / 作用 |
| --- | --- |
| `SharedProject/SharedSource/Plugin/Plugin.cs` | `Plugin : IBarotraumaPlugin`，`Init/OnContentLoaded/Dispose`，服务字段，静态构造函数登记 `StaticState` 清理 |
| `ClientProject/ClientSource/SandboxMenu/PluginClient.cs` | `Plugin` 的客户端半边：`InitializeProjectSpecific` / `DisposeProjectSpecific` |
| `ClientProject/ClientSource/SandboxMenu/PluginHooks.cs` | `PluginHooks`：每个 hook **缓存一个委托实例**（游戏按委托精确匹配，不能传方法组） |
| `SharedProject/SharedSource/Plugin/PluginCommands.cs` | `sandboxmenu` / `sandboxmenu.markup` 控制台命令（`#if CLIENT`） |
| `SharedProject/SharedSource/Plugin/PluginSettings.cs` | `togglekey`（`KeySetting`，默认 F5），暴露 `ToggleKey` |
| `SharedProject/SharedSource/AssemblySettings.cs` | global using、`IgnoresAccessChecksTo`、`ModPrefix = "sandboxmenu"` |

> `SharedProject/SharedSource/Settings/` 里是 `KeyBind` / `KeySetting`（按键捕获控件，整体包在 `#if CLIENT` 内，服务端不编译）；按键设置项的注册在 `Plugin/PluginSettings.cs`。本仓库**无 Harmony 补丁**：`HarmonyX` 仍引用着留给将来，代码里一处没用；真要打补丁时用宿主给的 `IHarmonyProvider`（见第 5 章红线）。另外，运行时承认的 `IgnoresAccessChecksTo` 特性基础库只在运行时包里提供、编译期看不到，工程用的是 HarmonyX 的 MonoMod 里那一份：`AssemblySettings.cs` 的三条程序集级声明与这条包引用是一体的，去掉引用就直接编译不过。

### 4.2 UI 层 (UI Layer) — `ClientProject/ClientSource/SandboxMenu/UI/`

**窗口外壳（先读这些）**

| 文件 | 作用 |
| --- | --- |
| `UI/SandboxMenuWindow.cs` | `SandboxMenuWindow : IDialogHost` 单例；`Instance` 按需构建，`Current` 是不构建的访问器；`Shutdown()` 关闭一切后 `StaticState.ResetAll()` |
| `UI/UiWindow.cs` | `UiWindow : IPopupWindow`，包 `GUIFrame` + `ViewLoader` 的 `ViewLoadContext`；`Open/Close/Register/Update/Dispose`，`Find<T>(name)` |
| `UI/IPopupWindow.cs` | 弹窗契约（`IsOpen` / `Closing` / `Rect` / `Open` / `Register` / `Update` / `Dispose` / `Find<T>`） |
| `UI/MenuTheme.cs` | DIP / 尺寸 / 颜色常量，委托给 `UiMetrics` |
| `UI/MenuPaint.cs` | `Fill` / `Outline` 的 `SpriteBatch` 辅助 |
| `UI/SpawnLocationPicker.cs` | 世界坐标拾取（`Begin/Update/Cancel/DrawHint`），静态态由 `StaticState` 清理 |
| `UI/Controls/RowElement.cs` | 列表模板用的自定义行控件 |

**标记视图 (`UI/Markup/*.xml`，以 `LogicalName = SandboxMenu.<名>.xml` 嵌入)**

- `MainWindow.xml` —— 主菜单：标题/关闭条、主体（条目树 + 编辑器行）、预设名称/保存/加载/重新加载/打开条、操作条（清空 / 给背包 / 世界生成 / 状态）。
- `Browser.xml` —— 物品浏览器弹窗（搜索框 + 内容包/分类筛选 + `Results` 列表）。
- `ContextMenu.xml` —— 由 `MenuOptionViewModel` 行构建的通用右键菜单。
- `MultiPicker.xml` —— 多选开关列表（`ToggleRowViewModel`），用于筛选与装备槽。
- `Options.xml` —— 单选列表（`PickerRowViewModel`）。

**视图模型 (`UI/ViewModels/`)**

- `SpawnMenuViewModel.cs` —— **根 VM**，拥有 `SpawnSet`、扁平化的 `Entries`（`TreeEntryViewModel` 树）、命令中继、预设存取、拖放（`IDropTarget` / `IListBackground`）。**新人从这里入手。**
- `EntryEditorViewModel.cs` —— 为选中条目构建编辑器行集合（`IEditorRow`）。
- `ItemBrowserViewModel.cs` —— 物品目录浏览器，复用 `ItemPrefabCatalog`。
- `PickerViewModels.cs` —— `MultiPickerViewModel` / `OptionsPickerViewModel` / `PickerRow` / `ToggleRowViewModel`。
- 其余：`ContextMenuViewModel.cs`、`ItemPreviewRow.cs`、`ItemRowViewModel.cs`、`PropertyRow.cs`、`RowViewModel.cs`、`SlotRow.cs`、`SpawnTree.cs`（树嵌套/重父化辅助）、`TreeEntryViewModel.cs`、`ValueRows.cs`。

### 4.3 领域层 (Domain) — `SharedProject/SharedSource/Domain/`（两端共用）

**Model/**（可序列化的生成规格）

- `SpawnEntry.cs` —— 抽象基类（数量/取整、`ToXml`/`FromXml`/`Clone`，"Item"/"Ref" 的 XML 读取注册表）。
- `ItemEntry.cs` —— 具体物品生成（identifier、堆叠、装满容器、品质、标签、装备、装备槽、安装潜艇、继承频道、属性覆盖、嵌套 `Inventory`）。
- `RefEntry.cs` —— 指向命名预设模板的引用。
- `SpawnSet.cs` —— 命名 `SpawnEntry` 列表（预设载荷）。
- `PropertyOverride.cs` —— `ComponentName` / `ComponentIndex` / `PropertyName` / `Value`。
- `ValueRange.cs` —— `record struct`，含 `Roll()` 随机掷值与区间检测。
- `SpawnSlots.cs` —— `InvSlotType[]` ↔ 逗号字符串（用于 XML）。
- `ItemDisplay.cs` —— 包 `ItemPrefab` 的展示包装（名称/图标/富 tooltip）。**留在客户端**（`ClientProject/.../Domain/Model/`），和下面两节一样是纯 UI 关心的事。

**Prefabs/**（游戏内容自省，客户端专用，在 `ClientProject/ClientSource/SandboxMenu/Domain/Prefabs/`）

- `ItemPrefabCatalog.cs` —— 惰性从 `ItemPrefab.Prefabs` 构建不可变 `ItemPrefabEntry` 列表；暴露 `All` / `Packages` / `Categories` 及匹配类型。
- `ItemPrefabLookup.cs` —— `By(identifier)` 解析器。
- `PropertyOverrideCatalog.cs` —— 发现组件类型（`ReflectionUtils.GetDerivedNonAbstract<ItemComponent>`）与可序列化属性，驱动属性覆盖编辑器；`Targets()` / `Properties()`。
- `ContentRevision.cs` —— `Now => TextManager.LanguageVersion`，用于检测内容/语言变化。

**Spawning/**（把 `SpawnSet` 变成真实物品）

- `ItemSpawnService.cs` —— **公开入口**：`SpawnIntoInventory` / `SpawnAtWorld`（返回排队数量）与 `Spawn(...)`（返回整份 `SpawnResult`，服务端回执要用）。都吃一张 `IReadOnlyList<SpawnEntry>`，所以整份集合（`Set.Entries`）和右键菜单里"只生成这一条"的单个条目走同一条路。
- `SpawnExecutor.cs` —— 递归执行器：展开 `RefEntry` 模板（带环检测）、生成物品、解析堆叠、放进背包/世界、装备/安装、应用属性覆盖、生成嵌套物品，把问题记入 `SpawnResult`。应用属性覆盖后由 `SpawnPropertySync.Publish(...)` 把真正写进去的那些交给宿主的属性事件与客户端同步（单机没有网络成员，这一步是空操作）。
- `SpawnPlanner.cs` —— 数量/堆叠/重复的数学辅助。
- `SpawnResult.cs` —— 已生成计数 + 问题列表。
- `SpawnTarget.cs` —— 抽象 `record`：`IntoInventory` / `AtWorld`。

**Persistence/**（客户端专用，在 `ClientProject/ClientSource/SandboxMenu/Domain/Persistence/`）

- `TemplateStore.cs` —— 预设文件在 `<SettingsService.SaveFolder>/Presets` 下的存/取/删/列；清洗文件名并检测引用环（`ReachesTemplate`）。联机时客户端把引用到的预设随请求一起发过去，服务端不需要预设目录。

**Editing/**

- `PropertyEditService.cs` —— 经 `SerializableProperty.TrySetValue` 把 `PropertyOverride` 应用到真实 `Item`；按名称/索引解析目标组件。

### 4.4 基础设施 (Infrastructure) — `SharedProject/SharedSource/Infrastructure/`（两端共用）

| 文件 | 作用 |
| --- | --- |
| `Guard.cs` | `Run(what, body)` / `Try(what, body, fallback)`：包裹 try/catch 并把失败路由到 `Log.Warn`，保证每帧 UI/域/网络错误不崩游戏 |
| `Log.cs` | 带 `[SandboxMenu] ` 前缀的告警/信息；5 秒内合并重复；可由 `StaticState` 清理 |
| `StaticState.cs` | 卸载注册中心：`Register(Action)` / `ResetAll()`，两端各在自己的关闭流程里调用一次 |
| `Identifiers.cs` | `Of(string?)` → 空安全的 `Identifier` |
| `XmlValue.cs` | 不变文化 (invariant-culture) 的 XML 读写辅助（区间/bool/int/float/string） |

`DelayedQueue.cs`（帧内队列）留在客户端 `ClientProject/ClientSource/SandboxMenu/Infrastructure/`，只有菜单用。

### 4.4.1 网络层 (Networking) — `SharedProject/SharedSource/Networking/`（两端共用）

| 文件 | 作用 |
| --- | --- |
| `SandboxNetworkHeaders.cs` | 头枚举（`SpawnRequest` / `SpawnResponse`）；两端必须注册同一个声明 |
| `SpawnRequest.cs` | 请求载荷：压缩后的 XML、目标类型、世界坐标、客户端认为的受控角色 ID、请求序号 |
| `SpawnResponse.cs` | 回执：请求序号、`SpawnStatus`、排队数量、问题行（截断并限条数） |
| `SpawnPayload.cs` | 一次请求的 XML 信封：条目 + 被引用到的预设（服务端据此展开引用，无需本地预设库） |
| `SpawnPayloadCodec.cs` | Brotli 压缩/解压与全部上限（压缩 48 KiB / 解压 256 KiB / 512 条目 / 64 模板 / 深度 24），失败不抛 |
| `SpawnPropertySync.cs` | 把生成时真正写进去的组件属性交给宿主自己的属性事件（`Item.ChangePropertyEventData` / `CreateVanillaEntityEvent`）；宿主事件带不了的覆盖转成问题上报 |
| `ServerOptions.cs` | 两端注册的 `BooleanSetting`（`allowallclients`，`ServerAuthority`，`IsAllowedToSet` 要 `ManageSettings`） |

客户端专属的分流与回执配对在 `ClientProject/ClientSource/SandboxMenu/Domain/Spawning/ClientSpawnDispatcher.cs`；服务端专属的校验与生成在 `ServerProject/ServerSource/ServerSpawnHandler.cs`。

### 4.5 UiFramework — `UiFramework/`（命名空间 `UiFramework`）

自包含、受 WPF 启发的标记式 MVVM 引擎，经 `.shproj`/`.projitems` 编译进本插件。

| 子目录 | 内容 |
| --- | --- |
| `Controls/` | 标记元素类：`ButtonElement` / `FieldElement` / `ListBoxElement`（最大，含列表+拖放+背景菜单）/ `NumberElement` / `PanelElement` / `StackElement` / `TextBoxElement` / `TextElement` / `TickElement`，均经 `[Element]` 发现 |
| `Data/` | 绑定引擎：`Binding` / `BindingMode` / `BindingOptions` / `BindingPath` / `Drag` / `IValueConverter` / `MultiBindingExtension` / `ResourceExtension` |
| `Layout/` | `Layout` / `LayoutPanel` / `Panels`：锚定/尺寸数学 |
| `Styling/` | `DefaultTheme` / `ResourceDictionary` / `Style`（setter + `DataTrigger`） |
| `Templating/` | `DataTemplate`（按类型键控的行模板） |

**最先读的框架文件**：`ViewLoader.cs`（把 XML 树建为 `GUIComponent` 树、解析样式/触发器、发出 `MarkupDiagnostic`）、`ElementRegistry.cs`（反射扫描 `[Element]` 进 `FrozenDictionary`、用表达式树编译工厂与 setter、`StaticState` 可重置）、`UiMetrics.cs`（DIP 缩放与尺寸/颜色）、`StaticState.cs`（卸载注册中心）、`MarkupDiagnostic.cs`（带行号的诊断收集器，经 `Sink` 接到 `Log.Warn`）。辅助文件：`MarkupSource` / `MarkupNode` / `MarkupAttribute` / `MarkupValue` / `ViewLoadContext` / `ViewElement` / `ElementContext` / `NameScope` / `PropertyAssignment` / `BindingExtension` / `MarkupExtension`。

### 4.6 构建与部署 (Build & Deploy)

| 文件 | 要点 |
| --- | --- |
| `BuildData.props` | `ModName=SandboxMenu`、`RepositoryURL`、**`<UsePublicizedAssemblies>true</UsePublicizedAssemblies>`**（publicized 开关）；导入 `SharedProject.projitems` / `SharedItems.props`，并在 `BuildTarget != Server` 时才导入 `UiFramework.projitems`；`CopyFiles` 后构建目标把产物拷进 `ContentPackageBuilder\Content\bin\$(PlatformName)` |
| `SharedProject/SharedItems.props` | `PlatformTarget=x64`、`net8.0`、`Nullable`、`LangVersion=latest`；`AssemblyName=$(ModName)$(BuildTarget)` → `SandboxMenuClient`；按 `UsePublicizedAssemblies` 选 publicized / 原版参考程序集；引用 `System.Collections.Immutable` 9.0.0、`Barotrauma.ReferenceAssemblies.Libraries`、`HarmonyX` 2.15.0；`ReadGameVersion` 目标读 `Barotrauma.dll` 版本并注入 `RepositoryUrl` + `GameVersion` 程序集元数据 |
| `ClientProject/ClientItems.props` | `BuildTarget=Client`、定义 **`CLIENT` 常量**、把 5 个标记 XML 以 `SandboxMenu.*.xml` 嵌入为资源 |
| `ClientProject/Windows|Linux|MacClient.csproj` | 各设 `PlatformName` 与平台常量 `WINDOWS`/`LINUX`/`MAC`，再导入 `ClientItems.props`（RID 由部署脚本在构建时传入，`/p:Platform=AnyCPU`） |
| `ServerProject/ServerItems.props` | `BuildTarget=Server`、定义 **`SERVER` 常量**，再无别的内容（不嵌入标记、不导入 UI 框架） |
| `ServerProject/Windows|Linux|MacServer.csproj` | 与客户端三个 csproj 同构，只是导入 `ServerItems.props`；`AssemblyName` 因此是 `SandboxMenuServer` |
| `ContentPackageBuilder/Content/PluginInfo.xml` | 每平台两条 `MainAssemblyInfo`（`targets="Client"` / `targets="Server"`），并显式 `notsyncedinmultiplayer="false"`（两端都要装） |
| `References.props` | 空的 `<ItemGroup>` 占位 |
| `UserBuildData.props.example` | 本机路径模板：复制为 `UserBuildData.props` 填 `GameExecutableDir` 与 `ModDeployDir`（以 `\` 结尾） |
| `ContentPackageBuilder/Build-Package.ps1` | 构建三平台 `Release`，再用 robocopy 镜像 `ContentPackageBuilder\Content\` 到 `ModDeployDir`（可选排除二进制） |

> **Publicized 开关很关键**：`UsePublicizedAssemblies=true` 选 `Barotrauma.ReferenceAssemblies.Vanilla.Publicized` 包，运行期配合 `AssemblySettings.cs` 的 `IgnoresAccessChecksTo`。设 `false` 即改用非 publicized 原版参考程序集。

---

## 5. 新人红线 (Engineering Rules for New Hires)

维护/扩展本工程时，以下规则是**硬性约束**，违反会导致崩溃、跨平台失败或"未能完整卸载"滞留内存：

1. **零 Harmony 补丁，除非别无选择**：挂载点全部用宿主原生 hook 与服务表达；确实需要打补丁时走宿主的 `IHarmonyProvider`（`GetHarmony()` / `PatchAll()`，实例由游戏创建并负责释放），不要自己 `new Harmony`，也不要为补丁另拉库。
2. **不写游戏全局态**：`GUIStyle.*` / `GUI.*` / `GameMain.*` / `PlayerInput.*` 只读不写，卸载后不留残留。
3. **卸载必须可回收**：挂到**宿主对象**上的东西（窗口、拖拽句柄、覆盖层、输入订阅者）关闭时 `Parent = null` 并移出 GUI 更新列表；新增静态可变状态必须 `StaticState.Register(...)` 登记清理。**注册进插件服务的项不要手工注销**——命令、hook、网络 handler 都由服务在卸载时随服务释放一并清掉，手工 `Deregister*` 多余且易踩"服务已释放"的时序（详见 3.4）。验证：卸载后无"未能完整卸载"警告。
4. **分辨率无关**：尺寸走 DIP（`UiMetrics.Dip/DipInt`），文字走 `FontScale`，不依赖具体分辨率数值。
5. **跨平台禁用清单**：禁用 `WPF`/`WinForms`（`System.Windows.*` 表现层、`System.Drawing`）、`Microsoft.Win32.Registry`、`DllImport` (P/Invoke)、`Clipboard`、`Environment.OSVersion` 分支。打开文件/目录用 `Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })` 并带失败兜底。路径一律 `Path.Combine`。
6. **优先宿主原生接口**：服务经 `GetService<T>()` 获取，间接调用点必须标 `[MethodImpl(MethodImplOptions.NoInlining)]`；能用宿主 hook/回调表达就不打补丁。
7. **只构建、只验证**：改完跑构建到 **0 警告 0 错误**并报告，**到此为止**——不部署、不启停宿主程序、不改 `*.csproj`/`*.props`/打包脚本等构建配置（除非你明确被要求）。
8. **失败隔离**：从游戏更新、GUI 遍历与网络回调进来的调用统一走 `Guard` / `MenuActions`，异常只记日志不打断游戏。
9. **服务端不信任客户端**：请求先过权限（`ServerPermissions`/开关）、再过 `SpawnPayloadCodec` 的全部上限，任何失败都只回执 + 记日志；新增一种请求时，校验与上限要一起加。服务端只按自己知道的角色（`client.Character`）生成，请求里的角色 ID 只用来发现两端认知不一致。

> 以上规则的完整版见仓库的工程约束（workspace rules）。遇到拿不准的取舍，先到反编译源码（`d:\Code Repository\FakeFish\Decompile\`）确认宿主有无现成能力，有就用，确实没有才自研并注明原生缺什么。

---

*文档基于仓库当前结构生成，随代码演进而更新。修改了架构相关代码时，请同步修订对应章节。*
