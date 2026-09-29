using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// 有关程序集的一般信息由以下
// 控制。更改这些特性值可修改
// 与程序集关联的信息。
[assembly: AssemblyTitle("Batch MLP Encoder 3")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("Batch MLP Encoder 3")]
[assembly: AssemblyCopyright("Copyright © 2016-2026 Sad Pencil, Yuzuriha03")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

//将 ComVisible 设置为 false 将使此程序集中的类型
//对 COM 组件不可见。  如果需要从 COM 访问此程序集中的类型，
//请将此类型的 ComVisible 特性设置为 true。
[assembly: ComVisible(false)]

// 如果此项目向 COM 公开，则下列 GUID 用于类型库的 ID
[assembly: Guid("947dc7e8-f4f2-4931-b4b2-4b0921ed093b")]

//告诉平台兼容性分析器（CA1416）本程序只支持 Windows。
//SDK 自动生成的程序集信息文件里本来会写入这两个特性，但本项目为了保留手写的 AssemblyInfo.cs
//设置了 GenerateAssemblyInfo=false，所以必须手工补上；
//否则 CA1416 会认为 ListView、TextBox 等 WinForms 调用“在所有平台上都能运行”，产生几百条无意义警告。
[assembly: System.Runtime.Versioning.TargetPlatform("Windows7.0")]
[assembly: System.Runtime.Versioning.SupportedOSPlatform("Windows7.0")]

// 程序集的版本信息由下列四个值组成: 
//
//      主版本
//      次版本
//      生成号
//      修订号
//
//可以指定所有这些值，也可以使用“生成号”和“修订号”的默认值，
// 方法是按如下所示使用“*”: :
// [assembly: AssemblyVersion("1.0.*")]
//改成固定版本号：SDK 风格项目使用确定性构建，不再支持 "3.0.6.*" 这种通配符版本号。
//另外 user.config 是按程序集版本分目录存放的，版本号每次构建都变会导致用户设置反复重置。
[assembly: AssemblyVersion("4.0.0.0")]
[assembly: AssemblyFileVersion("4.0.0.0")]
