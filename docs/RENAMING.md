# AetherDrop 名称与兼容迁移

安装配置为 `installer/AetherDrop.iss`，客户端项目为
`app/client/AetherDrop.csproj`，服务项目为
`app/service/AetherDrop.Service.csproj`。构建脚本、测试、XAML 命名空间和资源引用
均使用这些名称。

运行组件统一命名为 `AetherDrop.exe`、`AetherDropService.exe`、
`AetherDrop-radio.exe`、`AetherDrop-radio-rtl.exe`、`AetherDrop-shell.dll`、
`AetherDrop-shell.msix` 和 `AetherDrop-wlan.sys`。通信管道、通知协议、右键菜单
注册、TAP 网卡、系统与用户配置目录均使用 AetherDrop。引擎身份环境变量为
`AETHERDROP_SERVICE_ID`，与客户端保持一致。

源码中的旧名称只用于识别、导入或卸载旧组件，以及原作者署名和上游链接。
安装时停止旧服务、导入系统配置和网卡备份，并重命名旧 TAP 网卡；首次启动时
复制旧用户配置与身份文件，保留已有的新配置。原始配置保留为迁移备份，卸载时
兼容处理两代配置、证书、防火墙规则和右键菜单包。恢复脚本也识别旧内核驱动。

验证包括客户端与服务编译、Intel/Realtek 无线组件及内核驱动构建、服务目录自检、
安装/卸载/恢复/报告脚本自检，以及用户配置迁移、字体和调试界面测试。
0.1.38 同步时已稳定浮窗测试的桌面锚点、隔离真实折叠面板的生命周期干扰，并让
速率断言适配 0.8 秒刷新；浮窗与调试界面烟雾测试均通过。真实设备收发与完整
安装/卸载需另外实测，详见 [0.1.38 同步记录](UPSTREAM-0.1.38.md)。
