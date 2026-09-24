点击 `build/build.bat` 即可自动构建 `build/得吃准星 v6.3.exe`，生成的是单文件便携版。

设置会在停止修改约 1 秒后自动保存，持续修改时约每 5 秒保存一次，退出时再次保存。配置导入会先校验完整性，无效文件不会覆盖当前设置。

弱网操作在修改 MTU 前保存恢复记录，支持部分恢复失败后的重试；后台命令设有 30 秒超时。取消管理员授权会重新打开普通模式。倒计时使用单调时钟，不受系统时间调整影响。

构建后可运行以下回归测试：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tests/settings-position-regression.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/ui-regression.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/fov-regression.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/weak-network-restore.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tests/reliability-regression.ps1
```

弱网测试使用模拟网卡命令；可靠性测试使用独立配置和模拟网络恢复，不会修改真实网络，也不会弹出管理员授权窗口。
