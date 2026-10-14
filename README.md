# 额度浮窗 1.4.1 · 仅官方数据

Windows x64 工具，显示当前 Codex 登录账号的官方五小时／每周剩余额度和刷新时间。ChatGPT Work 与 Codex 共享额度，不重复统计。跨电脑安装请使用发布的 `QuotaFloat-Setup-win-x64.zip`，完整解压后运行 `Install.cmd`；详见 [安装说明](安装说明.md)。安装包自带 .NET 运行时。克隆源码后可运行 `tools/package.ps1` 自行生成安装包。

项目源码采用 [MIT 许可证](LICENSE)。你可以在其他电脑上修改、构建和再发布；开发说明见 [CONTRIBUTING.md](CONTRIBUTING.md)。安装包只用于运行，修改软件请使用源码或 `QuotaFloat-source.zip`。

## 使用

- 已安装时通过开始菜单或安装目录中的 `QuotaFloat.exe` 打开。使用旧版免安装包时需另外安装 .NET 10 Desktop Runtime。需要保持 Codex 的 ChatGPT 登录有效。
- 拖动空白处移动。⋯ 或右键菜单可切换迷你条、设置、退出；− 收进托盘，双击托盘图标恢复。
- ↻ 立即查询官方额度。底部“已更新”表示最近成功查询时间；刷新倒计时悬停可查看完整日期。
- 详细信息展示近 7 天／30 天的官方额度快照与官方每日用量。设置支持置顶、迷你模式、Windows 登录与 Codex 重开时启动；移动程序文件夹后需重新设置自启动路径。
- 悬浮窗和详细信息展示官方返回的可用重置次数，以及最早到期的重置次数与时间。迷你模式可悬停查看。服务未返回明细时显示未知；明细不完整时标记为“已知最近到期”。
- 可用重置在未来 3 天（72 小时）内到期时，次数旁显示橙色 ⚠ 标志；五小时或每周剩余 ≤20% 时，对应额度旁显示橙色 ⚠ 标志。悬停标志可查看原因，指标每分钟重新判断。未知或已经到期的数据不会触发提醒。
- 迷你模式直接显示五小时、每周剩余额度和可用重置次数，并保留对应提醒标志。断网时提醒依据缓存，悬停标志会注明缓存状态。
- 界面跟随系统深浅色，时间按本机时区显示。

## 只使用官方数据

- 实时额度来自官方 account/rateLimits/read，剩余量为 100 - usedPercent。五小时与每周窗口按时长识别；缺失字段显示 —。
- 可用重置次数来自同一接口的 `rateLimitResetCredits.availableCount`。最早到期的次数根据返回的 `credits[].expiresAt` 统计；服务可能限制明细数量，因此不完整明细仅供参考。工具不会自动消耗重置。
- “官方剩余额度变化”来自本工具定时保存的官方返回值，从开始运行时积累。它不是服务端完整历史，也不推算 Token 或提问次数。重置窗口或采样缺口会断开曲线。
- 每日 Token 只使用官方 account/usage/read。当前账号接口返回空历史时，显示“暂无官方历史记录”；空记录不代表零消耗。
- **不读取本机会话日志，不统计 DeepSeek 或其他第三方模型，不从本机记录补算官方用量。** 模型统计和本机数据源入口已移除。
- 升级到 1.1 后自动清除旧版导入的本机模型事件与扫描检查点，保留官方额度和官方每日历史。不会修改原始 Codex 会话文件。
- 普通 ChatGPT 聊天暂无已验证的额度与历史来源，保留状态说明和官网入口。

## 刷新与存储

悬浮窗或详情可见时约每 2 分钟查询，仅托盘时约每 10 分钟；后台每分钟检查一次，因此可能有约一分钟调度延迟。启动、唤醒、打开详情和手动刷新会主动查询。官方每日历史约每 30 分钟更新，打开详情也会更新。

查询临时启动 Codex app-server，完成后关闭，不调用模型推理。断网保留缓存并显示缓存状态；到达额度刷新时间后重新查询，不自行恢复为 100%。

开启自启动后，Windows 任务计划程序独立维护轻量启动器，在登录时打开悬浮窗，每 2 秒检查一次 Codex 桌面窗口。重新打开窗口或更换主进程时，会启动已退出的悬浮窗，或恢复托盘中的现有悬浮窗；不重复运行。后台辅助进程不会阻挡重开检测。Windows 每分钟检查监视器是否需要恢复，已有监视器时忽略重复启动。关闭设置中的启动开关会移除启动任务并停止监视器，保留当前悬浮窗。

%LOCALAPPDATA%\QuotaFloat\usage.db 仅保存按账号隔离的官方数据，保留 90 天；settings.json 保存偏好。凭据不写入数据库或日志。

如果悬浮窗提示“Codex 当前为 API Key 登录”，请在 Codex 中切换为 ChatGPT 账号登录，再点击刷新。API Key 登录不能读取 ChatGPT 订阅的官方额度。其他连接失败时，检查网络和 Codex 登录状态。卸载前关闭自启动，在托盘菜单退出，然后移走程序文件夹；可自行保留或删除本地数据。

## 开发

C# / WPF / .NET 10，SQLite 使用 Microsoft.Data.Sqlite。src/QuotaFloat 是界面与刷新服务；src/QuotaFloat.Core 是官方协议及数据存储；tests/QuotaFloat.Tests 验证数据与升级行为。

安装 .NET 10 SDK 后运行 powershell -ExecutionPolicy Bypass -File tools/build.ps1 -Test 制作免安装开发包；运行 powershell -ExecutionPolicy Bypass -File tools/package.ps1 -Test 制作含运行时与安装器的跨电脑安装包；运行 powershell -ExecutionPolicy Bypass -File tools/source-package.ps1 制作可修改的源码包。发布结果位于 artifacts/。

QuotaFloat.exe --verify <输出目录> 使用隔离数据库读取官方额度，保存截图和资源报告后自动退出。

接口参考：https://learn.chatgpt.com/docs/app-server

共享额度：https://learn.chatgpt.com/docs/pricing

