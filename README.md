# MouseGestureDesktop

Windows 10/11 x64 鼠标侧键手势切换虚拟桌面的托盘工具。

## 下载与运行

在 GitHub 仓库的 **Actions → Windows CI → 成功的运行 → Artifacts** 中下载
`MouseGestureDesktop-win-x64-portable`。解压下载的产物，再解压其中的
`MouseGestureDesktop-win-x64-portable.zip`，打开目录中的 `MouseGestureDesktop.exe`。

无需安装 .NET，无需安装程序；请保留并一起移动解压目录内的所有文件。
启动后程序显示在系统托盘，右键托盘图标可设置或退出。
按住鼠标侧键水平滑动，松开时切换一次虚拟桌面；普通侧键点击行为仍保留。

配置保存在 `%APPDATA%\MouseGestureDesktop\config.json`。
移除程序前，若开启了开机自启，请先在设置中关闭，退出后删除解压目录即可；
如需同时清除设置，再删除上述配置目录。

包面向 Windows 10/11 x64；实际可运行范围仍受 [.NET 10 的操作系统要求](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)约束，
建议系统更新到最新可用版本。仓库 CI 在 GitHub 的 Windows runner 上执行，
不代表已在所有 Windows 10/11 版本验证鼠标钩子和虚拟桌面行为。

## 构建与打包

开发需要 Windows 和 .NET 10 SDK：

```powershell
dotnet build MouseGestureDesktop.slnx -c Release
dotnet test MouseGestureDesktop.slnx -c Release
./scripts/package.ps1
```

打包脚本使用 `Portable` 发布配置，包含完整的 win-x64 .NET 运行时，
输出 `artifacts/MouseGestureDesktop-win-x64-portable.zip` 及 `.zip.sha256` 校验文件。
可以用 `Get-FileHash <ZIP路径> -Algorithm SHA256` 核对校验值。

每次 push、拉取请求及手动触发都会执行 CI：构建、测试、发布、打包，
产物保留 30 天。自包含运行时随包分发，更新运行时需使用新 SDK 重新打包。
