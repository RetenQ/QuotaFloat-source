# 参与开发

## 环境与构建

- Windows 10/11 x64、.NET 10 SDK、PowerShell。
- 如果 SDK 没有加入 PATH，可将环境变量 `QUOTAFLOAT_DOTNET` 指向 `dotnet.exe` 的完整路径。
- 离线构建时可将 `QUOTAFLOAT_NUGET_PACKAGES` 指向已缓存所需 NuGet 包的目录。
- 在项目根目录运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/build.ps1 -Test`，生成依赖系统运行时的开发包。
- 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/package.ps1 -Test`，生成自带运行时的安装包。
- 运行 `powershell -NoProfile -ExecutionPolicy Bypass -File tools/source-package.ps1`，生成可供其他电脑修改的源码包。

`src/QuotaFloat.Core` 负责 Codex app-server 协议与数据存储，`src/QuotaFloat` 是 WPF 界面，`src/QuotaFloat.Launcher` 负责 Windows 登录与 Codex 重新启动联动。测试位于 `tests/QuotaFloat.Tests`。

## 贡献约定

- 提交前运行现有测试，并说明手工验证过的 Windows 行为。
- 不提交 `%LOCALAPPDATA%\QuotaFloat`、`%USERPROFILE%\.codex` 中的数据，以及凭据、访问令牌、构建产物或个人日志。
- 保持官方额度与本地推算数据明确分开；新增数据源时说明来源与覆盖范围。
- 如新增第三方依赖或打包运行时，请更新 `THIRD_PARTY_NOTICES.md` 和 `licenses/`。
