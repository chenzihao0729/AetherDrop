# 原格式媒体接收

此前上游接收端只声明 `ReceiverMediaCapabilities = {"Version": 1}`，没有编码器
或容器能力。上游注释以及 [OpenDrop 的接收能力样例](https://github.com/seemoo-lab/opendrop/blob/master/opendrop/server.py)
说明此兼容声明会让发送端导出旧格式，例如把 HEIF 转成 JPEG。HEVC 视频也可能
触发兼容导出，体积、编码与帧率会在 iPhone 端变化；Windows 接收端没有重编码。

现在继续使用该协议格式，补充 HEVC `hvc1` 的 Main、Main10、Still Picture
profile，以及 HEIF 容器支持。视频最高级别声明为 6.2，覆盖 4K60 等规格，
避免把接收能力声明成仅支持旧格式/低级别。此处表示能够原样接收文件，
不是承诺 Windows 已安装相应播放解码器。

能力同时随 Discover 和 Ask 应答中的 identity plist 返回。不伪造 Apple
系统版本，不删除该必需字段。构建通过 `scripts/patch-engine.py` 应用到引擎，
不直接修改上游依赖目录。

验证：`build/python/python.exe -I tests/verify_media_capabilities.py`，检查应答中的
媒体能力，以及 MOV 容器字节经过解包保存前后的 SHA-256 一致。
这只能证明接收侧不修改内容；iOS 是否原样导出仍需真机核对源文件与接收文件的
大小、编码和帧率，不能仅凭声明保证 59.97fps、HDR 或编辑后的素材全部保留。

真机核对时，可在 iPhone 分享面板的“选项”中选择“当前格式”，若提供
“所有照片数据”也可启用；具体选项随 iOS 版本变化。从“文件”App 分享已经
导出的原文件也能帮助排除“照片”App 的自动格式选择，但不会修复无线链路中断。
