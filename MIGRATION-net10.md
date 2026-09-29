# 迁移到 .NET 10（`net10.0-windows`）说明

本项目已从 **.NET Framework 4.6.2**（旧式 csproj）迁移到 **.NET 10** 的 SDK 风格项目。

旧的 .NET Framework 项目文件保留为
`Batch-MLP-Encoder-3/Batch-MLP-Encoder-3.csproj.net462.bak`，
需要时把它改回 `Batch-MLP-Encoder-3.csproj` 即可继续用旧 MSBuild 编译（只用于对比排错）。

---

## 一、改了哪些文件

| 文件 | 改动 | 原因 |
|---|---|---|
| `Batch-MLP-Encoder-3.csproj` | 重写为 SDK 风格：`net10.0-windows` + `UseWindowsForms=true`；`PlatformTarget=x86`、`RuntimeIdentifier=win-x86`；`GenerateAssemblyInfo=false` | 迁移到 .NET 10；保持 32 位 |
| `Program.cs` | 删除“.NET Framework 4.6 是否安装”的注册表检测；启动时注册 `CodePagesEncodingProvider`；调用 `Application.SetHighDpiMode(HighDpiMode.SystemAware)` | .NET 10 由宿主检查运行时；传统代码页不再内置；DPI 不再由 manifest 配置 |
| `LegacyTextEncoding.cs`（**新增**） | 提供“系统 ANSI 代码页”编码（`Ansi`、`AnsiCodePage`、`CanBeAnsiEncoded`） | **.NET 10 的 `Encoding.Default` 变成了 UTF-8**，见下节 |
| `MainForm.cs` | 所有 `Encoding.Default` 用法改为 `LegacyTextEncoding.Ansi` / `LegacyTextEncoding.AnsiCodePage` | 让 `.ssf` 与 eac3to 日志的字节输出与迁移前完全一致 |
| `Properties/AssemblyInfo.cs` | `AssemblyVersion("3.0.6.*")` → 固定版本号（现为 `"4.0.0.0"`）；版权改为 `Sad Pencil, Yuzuriha03`；补 `TargetPlatform` / `SupportedOSPlatform` 特性 | SDK 确定性构建不支持通配符版本；平台特性被 `GenerateAssemblyInfo=false` 关掉了 |
| `app.manifest` | 移除 `<dpiAware>true</dpiAware>` | .NET 10 WinForms 要求 DPI 由 `Application.SetHighDpiMode` 配置（否则报 WFO0003） |
| `app.config` | 删除 `&#60;startup&#62;&#60;supportedRuntime sku=".NETFramework,Version=v4.6.2"&#62;` 与 `EnableWindowsFormsHighDpiAutoResizing`；只保留 `configSections` + `userSettings` | 前者是 .NET Framework 的 CLR 激活配置，后者是 Framework 窗体专用，.NET 10 都不读取 |

`MediaInfoDLL.cs`、`NativeMethods.cs`、各 `*.Designer.cs`、各 `*.resx`、eac3to / Surcode 驱动逻辑均**未改动**。

### 已删干净的 .NET Framework 4.6.2 残留

| 位置 | 内容 | 处理 |
|---|---|---|
| `Program.cs` | `IsDotNet46OrLater()` 及读 `SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\Release` 的注册表检测 | 删除 |
| `app.config` | `<startup><supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.6.2"/></startup>` | 删除 |
| `app.config` | `<appSettings><add key="EnableWindowsFormsHighDpiAutoResizing" value="true"/></appSettings>` | 删除（代码里没有任何地方读 `appSettings`） |
| `app.manifest` | `<dpiAware>true</dpiAware>` | 删除，改写为 `Application.SetHighDpiMode` |
| `Properties/AssemblyInfo.cs` | `AssemblyVersion("3.0.6.*")` | 改为固定版本号（现为 `4.0.0.0`） |
| 旧 csproj | `System.Deployment` 引用、`.NET Framework 3.5 SP1` Bootstrapper 包、四组 `Prefer32Bit`、ClickOnce 发布字段 | 随旧 csproj 一起退役（文件保留为 `.bak`） |

**必须保留、不能当残留删掉的两处：**

1. `app.config` 里的 `configSections` + `userSettings` —— 实测删掉后 `ApplicationSettingsBase` 会直接报错，
   四个路径设置全部存不下来。
2. `Properties/AssemblyInfo.cs` 里的 `TargetPlatform` / `SupportedOSPlatform("Windows7.0")` ——
   删掉会重现 400+ 条 CA1416 假警告。

---

## 二、最关键的一处行为差异：`Encoding.Default`

| 运行环境 | `Encoding.Default` |
|---|---|
| .NET Framework 4.6.2（旧版） | **CP936**（系统 ANSI 代码页） |
| .NET 10（直接迁移，未处理） | **UTF-8 / CP65001** |

`.ssf` 文件是 Surcode MLP Encoder 读取的文本容器，它**只认系统 ANSI 编码**。
如果放任 `Encoding.Default` 变成 UTF-8，含中文/日文路径的 `.ssf` 会直接被 Surcode 读成乱码，
而更隐蔽的问题是 `CanBeAnsiEncoded()` 会把韩文判为“可表示”，
于是**韩文临时改名（`__surcode_xxxx`）绕行逻辑会彻底失效**。

修法：新增 `LegacyTextEncoding`，用 Win32 `GetACP()` 取系统 ANSI 代码页
（刻意不用 `CultureInfo.CurrentCulture.TextInfo.ANSICodePage`，因为本程序启动时会让用户选界面语言，`CurrentCulture` 不一定等于系统 ANSI 代码页）。

实测对比（本机，简体中文系统）：

```
.NET Framework  Encoding.Default.CodePage          = 936  (gb2312)
.NET 10          Encoding.Default.CodePage          = 65001 (utf-8)   <-- 若不管，行为就变了
迁移后           LegacyTextEncoding.AnsiCodePage    = 936  (gb2312)   <-- 与旧版一致

"abc-123"     CanBeAnsiEncoded=True
"中文测试"     CanBeAnsiEncoded=True   bytes=D6 D0 CE C4 B2 E2 CA D4
"한글테스트"    CanBeAnsiEncoded=False  bytes=3F 3F 3F 3F 3F   <-- 仍然触发 ASCII 临时改名绕行
"日本語"       CanBeAnsiEncoded=True
"café"        CanBeAnsiEncoded=True
"🎵music"     CanBeAnsiEncoded=False
```

---

## 三、为什么必须保持 x86

- `MediaInfo.dll` 是 **32 位**原生 DLL（已用 PE 头确认 `machine=0x014C`），进程内加载；
- Surcode MLP Encoder 是 32 位程序，UI Automation 的窗口交互依赖这一环境；
- 旧项目在 `Debug|AnyCPU` 和 `Release|AnyCPU` 下也都写着 `PlatformTarget=x86`。

因此 csproj 里同时固定了 `PlatformTarget=x86`、`RuntimeIdentifier=win-x86`、`Platforms=AnyCPU;x86`，
保证四种配置组合最终都产出 32 位程序。

---

## 四、UIAutomation 引用（迁移时踩到的坑）

旧项目引用了 `UIAutomationClient` 等四个程序集。迁移后第一次编译报：

```
error CS0234: 命名空间“System.Windows”中不存在类型或命名空间名“Automation”
```

原因：`UseWindowsForms=true` 时 SDK 只引入 **`Microsoft.WindowsDesktop.App.WindowsForms`** 这个框架 profile
（见 `Microsoft.NET.Sdk.WindowsDesktop.props`），而在 `Microsoft.WindowsDesktop.App.Ref` 的
`FrameworkList.xml` 里，`UIAutomationClient` / `UIAutomationTypes` / `UIAutomationProvider` /
`UIAutomationClientSideProviders` 都被标记为 `Profile="WPF"`，不在 WindowsForms profile 的引用集里。
另外直接写 `<Reference Include="UIAutomationClient" />` 也无法解析（会报 MSB3245）。

csproj 里的解法是改用完整的桌面框架引用：

```xml
<ItemGroup>
  <FrameworkReference Remove="Microsoft.WindowsDesktop.App.WindowsForms" />
  <FrameworkReference Include="Microsoft.WindowsDesktop.App" />
</ItemGroup>
```

运行时仍是同一个 `Microsoft.WindowsDesktop.App` 共享框架（`runtimeconfig.json` 里也是它），
只是编译期把全部桌面程序集放进引用集。

顺带一提：`System.Configuration.ConfigurationManager`、`System.Resources.Extensions`
同样由该框架提供，**所以本项目不需要任何 NuGet 包**。

---

## 五、构建与发布

```powershell
# 调试构建（框架依赖；开发机需要装 x86 版 .NET 10 桌面运行时）
dotnet build .\Batch-MLP-Encoder-3\Batch-MLP-Encoder-3.csproj -c Debug

# 发布给最终用户的自包含 x86 版本（目标机无需安装任何运行时）
dotnet publish .\Batch-MLP-Encoder-3\Batch-MLP-Encoder-3.csproj -c Release -r win-x86 --self-contained true
```

输出位置：

| 命令 | 目录 | 体积 |
|---|---|---|
| `build -c Debug` | `Batch-MLP-Encoder-3\bin\Debug\net10.0-windows\win-x86\` | 小（不含运行时） |
| `build -c Release -p:Platform=x86` | `Batch-MLP-Encoder-3\bin\x86\Release\net10.0-windows\win-x86\` | 小 |
| `publish ... --self-contained true` | `Batch-MLP-Encoder-3\bin\Release\net10.0-windows\win-x86\publish\` | ~166 MB |

也可以直接 `dotnet build .\Batch-MLP-Encoder-3.sln -c Release`（解决方案配置保持不变，四种组合都能编译）。

> ⚠️ 本机只装了 **x64** 的 .NET 10 运行时（`C:\Program Files\dotnet`），
> x86 运行时目录 `C:\Program Files (x86)\dotnet` 里只有 8.0。
> 所以**框架依赖的 x86 输出在本机无法直接双击运行**，要么装 x86 版 .NET 10 桌面运行时，
> 要么用上面的自包含发布。
>
> 首版没有开启 `PublishTrimmed` / `PublishSingleFile`：
> 本项目用 P/Invoke 原生 DLL、`.resx` 卫星程序集、`ApplicationSettingsBase`、
> UI Automation 和固定文件布局，裁剪与单文件会引入额外风险，等稳定后再单独验证。

---

## 六、已经实测验证过的内容

以下都在本机真实执行过（不是“应该能跑”）：

1. **构建**：Debug / Release / `Platform=x86` / 整个解决方案 —— **全部 0 警告 0 错误**。
2. **输出结构**：`Batch-MLP-Encoder-3.exe`（x86）、`Batch-MLP-Encoder-3.dll.config`
   （`app.config` 被自动复制，`userSettings` 段完整）、`MediaInfo.dll`、
   `es-ES\` 与 `zh-CN\` 两个卫星程序集。
3. **自包含发布**：`Batch-MLP-Encoder-3.exe` / `coreclr.dll` / `clrjit.dll` / `MediaInfo.dll`
   经 PE 头确认**全部为 x86**。
4. **ANSI 编码层**：见第二节的逐字节对比结果。
5. **用户设置**：用迁移后的 `Properties/Settings.Designer.cs` + 项目里真实的 `app.config`
   （已删掉 `<startup>` / `<appSettings>`）在 .NET 10 下做读写测试 ——
   四个设置项（Surcode / eac3to / 临时目录 / 输出目录）写入 `Save()` 后再 `Reload()` 全部正确，
   `user.config` 正常生成于
   `%LOCALAPPDATA%\<App>\<App>_Url_<hash>\<版本>\user.config`。
6. **图片资源（`ResXFileRef` → `Bitmap`）**：`eac3to32` / `surcode32` / `burnCD256` / `audiodvd256`
   四个 PNG 全部成功反序列化为 `System.Drawing.Bitmap`，尺寸与像素格式正确
   （32x32 / 256x256，`Format32bppArgb`）。
7. **GUI 冒烟测试**（用 UI Automation 驱动自包含发布版）：
   - 语言对话框正常显示（标题 `Batch MLP Encoder - Select Langauge`），列出 en-US / es-ES / zh-CN；
   - 点击 OK 后主窗口标题为 `Batch MLP Encoder - Ver.4.0`（说明 `MainForm_Load`
     与其中的 `LoadSettings()` 全部执行完毕）；
   - 顶层窗口数 = 1，**没有出现未处理异常对话框**；
   - 选 **zh-CN**：识别到 12 处中文控件文本（“欢迎”“下一步(N)”“浏览(E)...”等）；
   - 选 **es-ES**：识别到 5 处西语文本，重音字符与弯引号（`á` `ó` `ñ` `…` `“”`）显示正常；
   - 关闭主窗口后进程干净退出。
8. **DPI 感知等级**：用 `GetProcessDpiAwareness` 直接查运行中的进程，得到 **`SYSTEM_DPI_AWARE`**，
   与迁移前 `app.manifest` 里 `<dpiAware>true</dpiAware>` 的效果一致（确认换成
   `Application.SetHighDpiMode(HighDpiMode.SystemAware)` 没有降低 DPI 感知等级）。

---

## 七、仍需人工验收的部分

以下依赖真实的 Surcode / eac3to 与音频素材，**自动化验证没有覆盖**，请按老流程跑一遍：

1. 完整流程：拖入多声道 WAV → eac3to 拆声道 → 位深提升 → 生成 `.ssf` → Surcode 自动化编码 → MLP 产出。
2. **含韩文文件名**：确认仍然走 `__surcode_xxxx` 临时名，并在全部编码完成后正确改名回原名。
3. 位深 16 / 20 / 24，采样率 44.1 / 48 / 88.2 / 96 / 176.4 / 192 kHz 的常见组合。
4. 取消任务、eac3to 失败、Surcode 失败、输出文件被占用等异常分支。
5. 与旧 .NET Framework 版本做同输入对比（`.ssf` 字节、临时 WAV 头、最终产物）。

### 版本号

程序集版本固定为 `4.0.0.0`，界面上显示的 `MainForm.Version` 为 `4.0`。
`user.config` 按程序集版本分目录，所以版本号一变（`3.0.6.<随机数>` → `3.0.6.0` → `4.0.0.0`）
就会**让用户设置重置一次**；考虑到旧版本用的是通配符版本号（每次构建都会变、本来就会重置），
这不构成新的回归。以后要发新版请手工改下面两处，不要再改回 `*`：

- `Batch-MLP-Encoder-3/MainForm.cs` 里的 `public const string Version`（界面显示用）
- `Batch-MLP-Encoder-3/Properties/AssemblyInfo.cs` 里的 `AssemblyVersion` / `AssemblyFileVersion`

### 关于页面（About）

- `MainForm.resx` 里 `Page1AboutTextbox.WordWrap` 原为 `False`，导致长行不会自动换行、
  只能靠水平滚动条看；已改为 `True`，并把 `ScrollBars` 从 `Both` 改为 `Vertical`。
  该设置只在 `MainForm.resx` 里定义，所以三种语言一并生效。
  （已用进程内真实的窗口样式验证：`ES_AUTOHSCROLL` 位已被清除，即自动换行确实生效。）
- 三种语言的关于文本（`MainForm.resx` / `MainForm.zh-CN.resx` / `MainForm.es-ES.resx`
  里的 `Page1AboutTextbox.Text`）都已重写，并统一了版式：
  - 第 1、2 行是产品名与版本（`Batch MLP Encoder` / `版本 4.0（.NET 10 版）`）；
  - 作者、网址两块各自成段；
  - 四个小节（运行环境 / 工作原理 / 译者 / 许可协议）**每节前面都空一行**，
    小节标题单独占行且以冒号结尾，正文缩进 4 个空格；
  - 作者改为 Yuzuriha03，并保留原作者署名（“基于 伤心的笔 的 Batch MLP Encoder 3”）；
  - 版权年份改为 2016-2026；
  - **不展示 GPL 声明文本，只保留“许可协议”小节里的名称 + 链接**（例：
    `GNU 通用公共许可证第 2 版或更新版本` +
    `https://www.gnu.org/licenses/old-licenses/gpl-2.0.html`），避免长段落把关于页挤满；
  - 三种语言各自单语显示，不再出现中文界面里混英文 GPL 段落的情况；
  - 各语言原有的译者信息保留未动。
  版式已用脚本对三语言逐个校验：每节均为“前有空行 + 正文缩进 4 空格”。
- GPL 全文仍然随程序分发：csproj 把仓库根目录的 `LICENSE` 复制到输出目录和发布目录，
  以满足 GPL v2 “随程序附带许可证副本”的要求。关于页里的链接只是方便查阅。
- `VersionLabel.Text` 在设计器里的占位文本由 `Version 3.XX` 改为 `Version 4.XX`；
  运行时本来就会被 `MainForm_Load` 覆盖成 `Version 4.0`，改它只是为了设计器里不误导。

---

## 八、其他说明

- **`MainForm.resx` 有 416 个重复的 `>>控件名.Name/Type/Parent/ZOrder` 条目**
  （104 个控件 × 4 项）。逐条比对过，**重复项的值完全相同**，忽略哪一条都不影响生成的资源
  （旧 MSBuild 也是静默忽略）。新构建会为每条报一次 MSB3568，共 416 条噪音，
  因此在 csproj 里用 `MSBuildWarningsAsMessages` 压掉了 `MSB3568`。
  下次用设计器重存 `MainForm` 时可以顺手清掉这些重复项，然后把该抑制去掉。
- **`MainFormCodeStrings.es-ES.Designer.cs` 是空文件**（仓库里原本就是空的），
  不影响编译：`es-ES` 的字符串由 `MainFormCodeStrings.es-ES.resx` 作为卫星资源提供。
- SDK 风格项目会自动识别 `*.zh-CN.resx` / `*.es-ES.resx` 的文化后缀并生成卫星程序集，
  不需要像旧项目那样手工写 `DependentUpon`。
- 旧的 `bin\Debug\`、`bin\Release\` **顶层**还有迁移前（2026-09-19）的 `.NET Framework` 版产物：
  `Batch-MLP-Encoder-3.exe` / `.exe.config` / `.pdb` 以及 `es-ES\`、`zh-CN\`；
  新产物一律在各自的 `net10.0-windows\...` 子目录下，所以两者不会互相覆盖。
  确认不再需要旧版 .exe 后可以放心删掉这批文件（用 `.bak` 里的 csproj 随时能重建）。
