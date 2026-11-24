# Changelog / 更新日志

> Release channels / 发布通道：
> - **Stable（稳定版 / Latest）**：长期保留、经过稳定基线锁定的版本。当前稳定版为 **`v2.1.1`**（首个稳定基线为 `v0.1.22`）。
> - **Hourly（每时预览版 / Pre-release）**：持续迭代的预览版（客户端默认也会收到；取消勾选「包括预览版」就只收稳定版）；GitHub Releases 仅滚动保留最新 4 个每时版 + 稳定版（最多 5 个 Release），历史 Git Tag 全部保留。

---

## v2.1.3 (Preview / 预览版) — 2026-11-24

### 中文
- 修复 Apple ID「退出 / 删除身份」后本机状态未清理干净的 bug：一并清理 `WirDrop\device.txt`、`HKCU\Software\WirDrop` 注册表项及 32 位 `appleid-anisette.exe` 在 `VirtualStore` 中留下的 CoreADI 注册缓存（保留已解压的 `WirDrop\icloud` 避免重复下载），同时在卸载注销（`--unregister`）时同步清理；避免残留或时钟失步的旧设备凭据导致重登持续报 `-22421`

### English
- Fix Apple ID "Sign out / remove" leaving stale per-PC Anisette state behind: now also cleans up `WirDrop\device.txt`, `HKCU\Software\WirDrop`, and the 32-bit `appleid-anisette.exe` CoreADI cache under `VirtualStore` (while keeping the unpacked `WirDrop\icloud` runtime to avoid re-downloading), and runs the same cleanup on `--unregister` during uninstall; prevents stale or clock-skewed provisioning state from causing repeated `-22421` sign-in failures

---

## v2.1.2 (Preview / 预览版) — 2026-11-24

### 中文
- 新增 MediaTek MT7922 / MT7921（AMD RZ616 / RZ608）支持，按需和同时连 Wi-Fi（仅 5 GHz）（[#40](https://github.com/zheznanohana/nekodrop/pull/40)，感谢 @YiHoooong）
- 新增 MediaTek MT7902 同时连 Wi-Fi 模式（[#47](https://github.com/zheznanohana/nekodrop/pull/47)，感谢 @DaiJiequn）
- 新增 Intel BE200（Wi-Fi 7）实验性支持，同时连 Wi-Fi 和按需
- 修复部分 Apple ID 登录报 -27952 的 bug：自动换新版客户端标识重试一次（[#42](https://github.com/zheznanohana/nekodrop/issues/42)、[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- AX210 / AX201：日志记录 Block Ack 通知的去向，排查发送退回单帧的问题（[#39](https://github.com/zheznanohana/nekodrop/issues/39)、[#43](https://github.com/zheznanohana/nekodrop/issues/43)）
- 以上只在 CI 上编译测试；BE200、MT7921 和两张联发科卡的同时连 Wi-Fi 还没有真机验证

### English
- New: MediaTek MT7922 / MT7921 (AMD RZ616 / RZ608), on demand and Concurrent (5 GHz only) ([#40](https://github.com/zheznanohana/nekodrop/pull/40), thanks @YiHoooong)
- New: Concurrent mode for MediaTek MT7902 ([#47](https://github.com/zheznanohana/nekodrop/pull/47), thanks @DaiJiequn)
- New: experimental Intel BE200 (Wi-Fi 7), Concurrent and on demand
- Fix Apple ID sign-in failing with -27952 for some accounts: retries once with a newer client identity ([#42](https://github.com/zheznanohana/nekodrop/issues/42), [#45](https://github.com/zheznanohana/nekodrop/issues/45))
- AX210 / AX201: the log says where Block Ack notifications go, to find why sending falls back to single frames ([#39](https://github.com/zheznanohana/nekodrop/issues/39), [#43](https://github.com/zheznanohana/nekodrop/issues/43))
- Built and tested on CI only; BE200, MT7921 and Concurrent on both MediaTek cards are not yet verified on hardware

---

## v2.1.1 (Stable / 稳定版) — 2026-11-24

### 中文
- 修复删掉用户目录的 Downloads 文件夹后 AirDrop 引擎反复崩溃的 bug（[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- 修复 Explorer 占用右键菜单组件时升级安装失败的 bug；修复安装失败提示是乱码的 bug
- 修复右键「用 nekodrop 发送」和通知里「接受」按钮可能没反应的 bug（[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- 修复从资源管理器往面板拖文件时面板自己收起的 bug（[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- 新增「总是接受的设备」的「添加」按钮（[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- 修复网速较慢时 iCloud 组件下载失败的 bug，下载时显示进度条，一个下载源不通时自动换下一个（[#42](https://github.com/zheznanohana/nekodrop/issues/42)）
- 安装最后一步显示滚动进度条，不再像卡住（[#45](https://github.com/zheznanohana/nekodrop/issues/45)）
- 新增升级后自动删除下载的安装包（[#44](https://github.com/zheznanohana/nekodrop/pull/44)，感谢 @GGBond-cer）
- RTL8852CE：日志记录 iPhone 的信道序列，排查停在 6 信道的问题（[#32](https://github.com/zheznanohana/nekodrop/issues/32)）
- 以上修复只在 CI 上编译测试，还没有真机验证

### English
- Fix the AirDrop engine crashing over and over when the profile has no Downloads folder ([#45](https://github.com/zheznanohana/nekodrop/issues/45))
- Fix updates failing when Explorer holds the right-click menu component; fix the garbled text of the install error
- Fix Explorer's "Send with nekodrop" and the notification's Accept button sometimes doing nothing ([#45](https://github.com/zheznanohana/nekodrop/issues/45))
- Fix the panel closing while files are dragged to it from Explorer ([#45](https://github.com/zheznanohana/nekodrop/issues/45))
- New: an Add button for "Always accept from" ([#45](https://github.com/zheznanohana/nekodrop/issues/45))
- Fix the iCloud component download failing on slower connections, with a progress bar and more download sources ([#42](https://github.com/zheznanohana/nekodrop/issues/42))
- The installer's last step shows a moving bar instead of looking stuck ([#45](https://github.com/zheznanohana/nekodrop/issues/45))
- New: delete the downloaded installer after an update ([#44](https://github.com/zheznanohana/nekodrop/pull/44), thanks @GGBond-cer)
- RTL8852CE: the log records the iPhone's channel sequence, to find why it stays on channel 6 ([#32](https://github.com/zheznanohana/nekodrop/issues/32))
- Built and tested on CI only; not yet verified on real hardware

---

## v2.1.0 (Stable / 稳定版) — 2026-11-24

### 中文
- **新增「仅限联系人」（实验性，默认关闭）**：设置里新的「Apple ID」标签页，顶部有总开关；打开后可选「所有人」或「仅限联系人」。身份两种获得方式：在电脑上用 Apple ID 登录（基于 WirDrop 的 appleid，自动续期、读取 iCloud 通讯录），或导入从 Mac 导出的证书（airdrop-keychain-extractor）加 .vcf 通讯录。有身份时，TLS 用 Apple 签发的证书，/Discover 和 /Ask 带上确认记录，蓝牙信标带上你的哈希前缀，开「仅限联系人」的 iPhone 也能看到这台电脑、收到你发的文件。维护者已真机确认可用
- 点主界面左上角的 AirDrop 标志切换接收模式：接收关闭 / 仅限联系人 / 所有人（和 iPhone 一样）；关闭时标志变灰
- README 更新：新功能说明和截图（含 Apple ID 设置页、发送动画逐帧）
- 安装包附带 appleid.exe / appleid-anisette.exe（从 WirDrop 源码编译，MIT）

### English
- **New: Contacts Only (experimental, off by default)**: a new Apple ID tab in Settings with a switch at the top; on, it lets you choose Everyone or Contacts Only. The identity comes from signing in to an Apple ID on this PC (WirDrop's appleid: renewed on its own, reads the iCloud contacts) or from a certificate exported from a Mac (airdrop-keychain-extractor) plus a .vcf of contacts. With one, TLS uses the Apple-issued certificate, /Discover and /Ask carry the validation record, and the Bluetooth beacon carries your hash prefixes, so iPhones set to Contacts Only see this PC and take what you send. Confirmed working by the maintainer
- Click the AirDrop mark at the top left of the main window to switch receiving: Off / Contacts Only / Everyone, as on an iPhone; the mark turns grey when off
- README: the new features and screenshots (the Apple ID page, sending frame by frame)
- The installer ships appleid.exe / appleid-anisette.exe (built from WirDrop's source, MIT)

---

## v2.0.2 (Stable / 稳定版) — 2026-11-24

> 取代 2.0.0（已撤回）和 2.0.1（安装程序的中文提示是乱码，已撤回）。自 v0.1.35 稳定版以来的界面改动见下方 v0.1.36–v2.0.0。 / Replaces 2.0.0 (withdrawn) and 2.0.1 (garbled Chinese text in the installer, withdrawn). Interface changes since the v0.1.35 stable release are in v0.1.36–v2.0.0 below.

### 中文
- 主窗口改为托盘旁的弹出面板：贴屏幕右下角，点外面自动收起，单击托盘图标开 / 关，双击不会一闪就没
- 修复右上角出现两套最小化 / 最大化 / 关闭按钮
- 修复窗口比屏幕高时底部显示不全；设备名和状态不再被裁切
- 雷达放大，设备不再压住「本机」；删除雷达上的箭头
- 接收时「本机」外圈显示进度，保存完成后变绿打勾；接受 / 拒绝气泡保留，接受后收起
- 新增动效（Windows Fluent 原生缓动曲线）：面板滑入滑出、设备出现时淡入放大、设备增减时平滑移位、请求气泡弹出、雷达 / 列表切换过渡；发送时蓝色波纹送出，进度环匀速（线性）走满，完成时蓝环渐变为绿环、对号一笔画出、图标轻弹并送出绿色波纹；接收完成时「本机」同样变绿弹出对号
- 修复雷达上的发送进度环没有跟随真实进度（一直是转圈的等待弧）
- 修复收起面板后进行中的内容全部消失：面板收起后继续查找 90 秒，有发送或请求进行中就一直保持，重新打开时设备、发送进度和结果都还在
- 修复 AX210 上 A-MPDU 收不到 Block Ack 退回单帧后发送卡住、iPad 无响应（[#39](https://github.com/zheznanohana/nekodrop/issues/39)）：退回时主动发 DELBA 结束对方的聚合会话；同一次 radio 运行中第二次出现就不再尝试聚合。未在真机验证，需要 AX210 用户确认
- 新增自定义全局快捷键：设置 → 常规里点一下快捷键再按下新的组合键（Esc 取消，可恢复默认 Win+Shift+D）；被其他程序占用时会提示

### English
- The main window is now a flyout by the tray: bottom-right corner, closes when you click elsewhere, the tray icon toggles it, and a double click no longer closes it at once
- Fixed a second set of minimize / maximize / close buttons in the corner
- Fixed windows taller than the screen being cut off; device names and states are no longer clipped
- A larger radar, devices no longer cover "This PC"; the arrows on the radar are gone
- While receiving, a progress ring around "This PC", then green with a check mark once saved; the accept / decline bubble stays and closes after accepting
- New motion on Windows' Fluent curves: the panel slides in and out, devices fade and grow in and glide when one comes or goes, the request bubble pops open, radar / list cross-fade; sending sends a blue wave off, the ring fills at a steady (linear) pace, and on arrival it turns green, a check is drawn in one stroke, the icon pops and a green wave leaves it; "This PC" does the same when files arrive
- Fixed the send ring on the radar not following the real progress (it kept turning as if waiting)
- Fixed everything under way disappearing when the panel closed: looking goes on for 90 s after it closes, and for as long as a send or a request is under way, so devices, a send's progress and its outcome are still there when it opens again
- Fixed sending stalling on an AX210 after A-MPDU fell back to single frames for lack of Block Acks, with the iPad never answering ([#39](https://github.com/zheznanohana/nekodrop/issues/39)): the fallback now ends the peer's Block Ack session (DELBA), and a second stall in the same radio run stops aggregation. Not verified on hardware yet; needs an AX210 owner to confirm
- New: record your own global shortcut in Settings → General (click it, press the keys; Esc cancels; Default restores Win+Shift+D); you are told when another program holds it

---

## v2.0.0 (Hourly / Pre-release, withdrawn / 已撤回) — 2026-11-24

> 已撤回，由 2.0.2 取代（双标题栏按钮、窗口显示不全）。 / Withdrawn, replaced by 2.0.2 (two sets of title buttons, windows cut off).

### 中文
- 新增有人发文件时，在对方图标旁直接接受 / 拒绝，并显示接收进度
- 新增雷达上的箭头：等待、发送中、已发送、被拒绝一目了然
- 新增雷达呼吸效果
- 新增「本机」图标可选台式机 / 笔记本（默认自动）
- 新增 Windows「共享」菜单：资源管理器和其他应用的分享面板里可直接选 nekodrop
- 优化 Wi-Fi 窗口：正在使用的网络整块高亮

### English
- New: accept or decline right beside the sender's icon, with the receiving progress
- New: arrows on the radar for waiting, sending, sent and declined
- New: a breathing radar
- New: this PC as a desktop or a laptop (automatic by default)
- New: nekodrop in the Windows Share sheet (Explorer and other apps)
- Better Wi-Fi window: the network in use is highlighted

---

## v0.1.39 (Hourly / Pre-release, withdrawn / 已撤回) — 2026-11-24

### 中文
- 全新界面：自绘标题栏、雷达 / 列表切换、四个标签页的设置、新的 Wi-Fi 窗口和托盘菜单（[#36](https://github.com/zheznanohana/nekodrop/issues/36)）
- 新增全局快捷键 Win+Shift+D：选中文件时直接发送，否则打开主窗口
- 新增主题色切换：多彩、跟随系统，或蓝、靛、青、玫红
- 新增标题栏状态面板：允许接收、切换联网方式
- 新增文件可逐个移除、新版本横幅、各种状态下的操作按钮（重试、启动服务、前往设置等）

### English
- New interface: own title bars, radar / list view, settings in four tabs, new Wi-Fi window and tray menu ([#36](https://github.com/zheznanohana/nekodrop/issues/36))
- New global shortcut Win+Shift+D: sends the files selected in Explorer, or opens the main window
- New accent colour choice: colourful, same as Windows, or blue, indigo, teal, rose
- New status panel in the title bar: allow receiving, switch the network mode
- New: remove files one by one, an update bar, and actions for each state (retry, start the service, choose the card…)

---

## v0.1.38 (Hourly / Pre-release) — 2026-11-24

### 中文
- 修复 Realtek RTL8852CE 一直停在 2.4 GHz、传输很慢的 bug（[#32](https://github.com/zheznanohana/nekodrop/issues/32)）
- 修复 Realtek / MediaTek 网卡传输中途搬信道断开的 bug
- 更正文档：AX210 独立网卡早已可用

### English
- Fixed the Realtek RTL8852CE staying on 2.4 GHz and transferring slowly ([#32](https://github.com/zheznanohana/nekodrop/issues/32))
- Fixed Realtek / MediaTek cards changing channel in the middle of a transfer
- Docs: the discrete AX210 has worked for a while

> 只在 CI 上测过，没有真机验证。 / CI-tested only, not on hardware.

---

## v0.1.37 (Hourly / Pre-release) — 2026-11-24

### 中文
- **新增 MediaTek MT7902 支持（[#34](https://github.com/zheznanohana/nekodrop/pull/34)，感谢 DaiJiequn）**：`nekodrop-radio-mt.exe` 是从 Linux mt76/mt7921 移植的用户态驱动，和 Intel、Realtek 一样通过 register proxy 驱动网卡。这张卡没有 station 模式，所以按「按需」运行：平时 Windows 正常用 Wi-Fi，有 AirDrop 时才接管。作者在一张卡上实测 iPhone ↔ 电脑双向可用，接收约 15 MB/s、发送约 5 MB/s。已知问题：2.4 GHz 上需要 ACK 的帧发不出去，所以 2.4 GHz 上不等 ACK（AirDrop 本身会转到 5 GHz）。目前只测过一张 MT7902。
- **修复 iPhone 刚发现电脑、电脑又从列表里消失（[#32](https://github.com/zheznanohana/nekodrop/issues/32)、[#35](https://github.com/zheznanohana/nekodrop/issues/35)）**：网卡启动时，托盘程序先启动 AirDrop 引擎，过一两秒 `awdl0` 才建好。托盘程序每 5 秒检查一次 `awdl0`，把「之前没有、现在有了」当成「换了一个」，于是把刚开始工作的引擎杀掉重启。这正好发生在 iPhone 刚发现电脑的那几秒：引擎退出时发出 mDNS 下线包，iPhone 把电脑删掉，要关掉分享面板重开才能再看到。两份日志里都能看到：`awdl0` 建好约 4 秒后引擎重新打出 `ready`。引擎自己每秒都会检查 `awdl0`，出现后会自己开始接收，所以现在不再重启它。**只编译测试过，没有真机验证。**
- **网卡找不到连续 DMA 内存时，等一会儿再重试（[#30](https://github.com/zheznanohana/nekodrop/issues/30)）**：以前每 2.7 秒重试一次，6 次都失败后就把网卡还给 Windows；而同一台电脑过 16 秒或 90 秒再启动时都成功了。现在依次等 5、10、20、30、30 秒再重试。**只编译测试过，没有真机验证。**
- **蓝牙与 Wi-Fi 均衡共存转正，Intel 网卡始终打开（[#26](https://github.com/zheznanohana/nekodrop/pull/26)、[#4](https://github.com/zheznanohana/nekodrop/issues/4)、[#35](https://github.com/zheznanohana/nekodrop/issues/35)）**：电脑靠网卡自带的蓝牙发出信标叫醒附近的 iPhone。AX200 上打开后，设备从一两分钟找不到变成几秒内出现（感谢 XXD051030）；AX211 上打开后也没有人报告变差。设置里的开关已删除，旧配置里的值会被忽略。
- **恢复「Wi-Fi 和 AirDrop 不在同一信道时按苹果的方式分时」开关的说明文字**：0.1.35 去掉 A-MPDU 开关时误删了它的文案，设置里显示的是键名本身。[#35](https://github.com/zheznanohana/nekodrop/issues/35) 的 AX211 上，nekodrop 连的 Wi-Fi 在 56、AirDrop 在 149 时，不开它一个 AirDrop 帧都收不到，开了就正常；README 常见问题里写明了什么时候该打开。它仍是实验性开关，默认关闭（AX200 上传输速度时快时慢）。
- **修复卸载后残留快捷方式、安装目录和「添加/删除程序」里的条目（[#38](https://github.com/zheznanohana/nekodrop/pull/38)，感谢 GGBond-cer）**：卸载脚本结束安装目录里的进程时，把 Inno Setup 自己的卸载程序也结束了，它就没法完成后面的清理。

### English
- **New: MediaTek MT7902 support ([#34](https://github.com/zheznanohana/nekodrop/pull/34), thanks to DaiJiequn)**: `nekodrop-radio-mt.exe` is a user-mode port of Linux mt76/mt7921 that drives the card through the register proxy, as the Intel and Realtek radios do. The card has no station mode, so it runs on demand: Windows keeps its Wi-Fi, and nekodrop takes the card only for AirDrop. Tested by the author on one card: iPhone ↔ PC both ways, about 15 MB/s receiving and 5 MB/s sending. Known limit: on 2.4 GHz, frames that need an ACK never complete, so it sends without ACK there (AirDrop itself moves to 5 GHz). Only one MT7902 has been tested.
- **Fixed the PC dropping out of the iPhone's list right after the iPhone found it ([#32](https://github.com/zheznanohana/nekodrop/issues/32), [#35](https://github.com/zheznanohana/nekodrop/issues/35))**: when the card starts, the tray app starts the AirDrop engine a second or two before `awdl0` exists. Its 5-second check took "no awdl0, now awdl0" for a new interface and restarted the engine that had just begun working, exactly while the iPhone was finding the PC: the engine's mDNS goodbye made the iPhone drop it until the share sheet was reopened. Both logs show the engine printing `ready` again about 4 s after `awdl0` appeared. The engine checks `awdl0` every second and starts receiving on its own, so it is no longer restarted then. **CI-tested only, not on hardware.**
- **When the card finds no contiguous DMA memory, the restart waits ([#30](https://github.com/zheznanohana/nekodrop/issues/30))**: six restarts 2.7 s apart all failed and the card went back to Windows, while a start 16 s or 90 s later worked on the same PC. It now waits 5, 10, 20, 30 and 30 s. **CI-tested only, not on hardware.**
- **Balanced Bluetooth coexistence graduates and is always on for Intel cards ([#26](https://github.com/zheznanohana/nekodrop/pull/26), [#4](https://github.com/zheznanohana/nekodrop/issues/4), [#35](https://github.com/zheznanohana/nekodrop/issues/35))**: the PC wakes nearby iPhones with a beacon from the card's own Bluetooth. On AX200, devices appeared within seconds instead of minutes (thanks to XXD051030); on AX211 nobody reported it worse. The switch is gone; an old config's value is ignored.
- **The "Apple-style time sharing when Wi-Fi and AirDrop use different channels" switch has its text back**: it was removed by mistake with the A-MPDU switch in 0.1.35, so the checkbox showed its key. On [#35](https://github.com/zheznanohana/nekodrop/issues/35)'s AX211, with nekodrop's Wi-Fi on 56 and AirDrop on 149, the PC heard no AirDrop frame without it and worked with it; the README troubleshooting table says when to turn it on. It stays experimental and off by default (speeds went both up and down on an AX200).
- **Fixed the uninstaller leaving shortcuts, the install folder and the Add/Remove Programs entry behind ([#38](https://github.com/zheznanohana/nekodrop/pull/38), thanks to GGBond-cer)**: the script's sweep of processes running from the install folder also killed Inno Setup's own uninstaller, which then could not finish.

---

## v0.1.36 (Hourly / Pre-release) — 2026-11-23

### 中文
- **修复网卡重启后 AirDrop 数据全部被丢掉（[#30](https://github.com/zheznanohana/nekodrop/issues/30)）**：radio 每次重启（切信道、连 Wi-Fi）时 awdld 也会重启，新的 awdld 创建 `awdl0` 虚拟网卡时，旧的那个可能还没完全删掉，Wintun 返回 error 183（已存在）。awdld 于是在没有 `awdl0` 的情况下继续运行，日志里是 `no host interface, AWDL data will be dropped`：iPhone 能看到电脑，但 AirDrop 一个数据帧都过不去，直到下一次重启。现在遇到 183 会每 0.25 秒重试，最多等 10 秒。**只编译测试过，没有真机验证。**
- **修复 Realtek RTL8852CE 上 iPhone 打开 AirDrop 时不会唤醒网卡（[#32](https://github.com/zheznanohana/nekodrop/issues/32)）**：RTL8852CE 不能和 Wi-Fi 同时工作，选了「同时」模式时实际按「按需」运行，但服务告诉客户端的还是「同时」，客户端的蓝牙唤醒只在「按需」时才触发，所以从来不会唤醒。现在服务会另外报告实际运行的模式。**只编译测试过，没有真机验证。**
- **蓝牙信标被系统中止时写明原因并重试（[#30](https://github.com/zheznanohana/nekodrop/issues/30)、[#32](https://github.com/zheznanohana/nekodrop/issues/32)）**：日志里 `Bluetooth did not start advertising, status Aborted` 时，电脑叫不醒附近的 iPhone，iPhone 只有自己打开分享面板时才会出现在列表里。现在会再试两次，仍然失败时日志会带上 Windows 给的原因（例如 `RadioNotAvailable`、`ResourceInUse`），方便定位。
- README 常见问题：同时模式下没有 Windows 无线网卡，个别要检测无线网卡的软件（例如某游戏的二维码登录）用不了，请切到「按需」（[#33](https://github.com/zheznanohana/nekodrop/issues/33)）。

### English
- **Fixed all AirDrop data being dropped after the radio restarts ([#30](https://github.com/zheznanohana/nekodrop/issues/30))**: awdld restarts with the radio (a channel move, a Wi-Fi join), and the new one could ask Wintun for `awdl0` while the old adapter was still going away: error 183 (already exists). awdld then ran on without `awdl0` (`no host interface, AWDL data will be dropped`): the iPhone saw the PC but no AirDrop data got through until the next restart. It now retries every 0.25 s for up to 10 s. **CI-tested only, not on hardware.**
- **Fixed a nearby iPhone's AirDrop never waking a Realtek RTL8852CE ([#32](https://github.com/zheznanohana/nekodrop/issues/32))**: that card cannot keep Wi-Fi beside AirDrop, so with *together* chosen it runs on demand; but the service kept reporting *together*, and the client only wakes the card on demand. The service now also reports the mode the card actually runs. **CI-tested only, not on hardware.**
- **When Windows aborts the Bluetooth beacon, the reason is logged and the start retried ([#30](https://github.com/zheznanohana/nekodrop/issues/30), [#32](https://github.com/zheznanohana/nekodrop/issues/32))**: with `Bluetooth did not start advertising, status Aborted` the PC cannot wake iPhones nearby, which then show up only while their own share sheet is open. It is now tried twice more, and a failure carries Windows' reason (such as `RadioNotAvailable` or `ResourceInUse`).
- README troubleshooting: *together* mode has no Windows Wi-Fi adapter, so an app that looks for one (such as a game's QR-code login) fails there; use *on demand* for it ([#33](https://github.com/zheznanohana/nekodrop/issues/33)).

---

## v0.1.35 (Stable / 稳定版) — 2026-11-23

> 自 v0.1.33 稳定版以来：A-MPDU 聚合发送转正、始终打开；修复连 iPhone 热点要试好几次（v0.1.34）。A-MPDU 有 AX201 和 AX200 的真机数据；连热点的修复只在 CI 上测过。 / Since the v0.1.33 stable release: A-MPDU sending graduates and is always on; joining an iPhone hotspot no longer takes several tries (v0.1.34). A-MPDU has hardware data from an AX201 and an AX200; the hotspot fix is CI-tested only.

### 中文
- **A-MPDU 聚合发送转正，始终打开（[#4](https://github.com/zheznanohana/nekodrop/issues/4)）**：设置里的「聚合发送（A-MPDU）」开关删除，发给 iPhone 时总是把多帧聚合发出。依据是两张网卡的真机数据、且没有人报告它让情况变差：AX201 上 30.8 MB/s（0.1.30），AX200 上 9–25 MiB/s（感谢 GGBond-cer 的大量测试）。DMA 内存不够或走内核驱动路径时仍按单帧发送，和以前一样。AX210、AX211 和 Realtek 网卡还没有 A-MPDU 的实测数据，遇到发送变慢或失败请附诊断报告反馈。旧配置里的开关值会被忽略。
- **修复连 iPhone 热点要试好几次（[#27](https://github.com/zheznanohana/nekodrop/issues/27)，0.1.34）**：见下方 v0.1.34。
- README 精简，并写明已在真机上确认可用的网卡和速度。

### English
- **A-MPDU sending graduates and is always on ([#4](https://github.com/zheznanohana/nekodrop/issues/4))**: the "Aggregated sending (A-MPDU)" switch is gone; sends to an iPhone are always aggregated. Two cards measured it with nobody reporting it worse: 30.8 MB/s on an AX201 (0.1.30) and 9–25 MiB/s on an AX200 (thanks to GGBond-cer's many tests). Without DMA room, or on the kernel-driver path, sending stays on single frames as before. The AX210, AX211 and Realtek cards have no A-MPDU measurements yet: if sending gets slower or fails, please post a diagnostic report. An old config's switch value is ignored.
- **Fixed joining an iPhone hotspot taking several tries ([#27](https://github.com/zheznanohana/nekodrop/issues/27), 0.1.34)**: see v0.1.34 below.
- A shorter README that says which cards and speeds are confirmed on hardware.

---

## v0.1.34 (Hourly / Pre-release) — 2026-11-22

### 中文
- **修复连 iPhone 热点要试好几次才连上（[#27](https://github.com/zheznanohana/nekodrop/issues/27)）**：iPhone 热点重新打开后可能换一个 BSSID（无线 MAC 地址）。nekodrop 一直拿上一次扫描记下的旧地址去认证，那个地址已经没人应答：认证帧一个确认都没收到，也从没听到它的信标。直到后台下一次扫描碰巧扫到新地址才连上。现在扫描结果超过 20 秒时，连接前会先按网络名重新扫一次；连接失败、而扫描结果已经旧了时，也会自动重扫再连。认证失败时，radio 日志会注明有没有听到过这个接入点的信标。**只编译测试过，没有真机验证。**

### English
- **Fixed joining an iPhone hotspot taking several tries ([#27](https://github.com/zheznanohana/nekodrop/issues/27))**: an iPhone hotspot can come back with a new BSSID. nekodrop kept authenticating to the address from the previous scan, which nobody answered (no frame acknowledged, no beacon ever heard), until a later background scan happened to catch the new one. When the scan is more than 20 s old, nekodrop now scans for the network by name before joining, and does so again after a failed join. The radio's authentication failure line now says when no beacon was heard from the access point. **Built and tested on CI only; not verified on hardware.**

---

## v0.1.33 (Stable / 稳定版) — 2026-11-22

> 自 v0.1.30 稳定版以来的变化见下方 v0.1.31–v0.1.33。真机已确认：AX211 上 iPhone → 电脑接收恢复（[#9](https://github.com/zheznanohana/nekodrop/issues/9)，`FCS removed`）。其余改动和两个新实验开关（默认关闭）尚无真机数据。 / Changes since the v0.1.30 stable release are in v0.1.31–v0.1.33 below. Confirmed on hardware: iPhone → PC receiving works again on AX211 ([#9](https://github.com/zheznanohana/nekodrop/issues/9), `FCS removed`). The other changes and the two new experimental switches (off by default) have no hardware data yet.

### 中文
- **新的实验性开关「蓝牙与 Wi-Fi 均衡共存（Intel）」（默认关闭，[#26](https://github.com/zheznanohana/nekodrop/pull/26)，感谢 [@XXD051030](https://github.com/XXD051030)）**：电脑发送时，靠网卡自带的蓝牙发出 AirDrop 信标，附近的 iPhone 和 Mac 听到后才会出现在列表里。radio 一直让固件「Wi-Fi 优先」，AWDL 监听时这个信标几乎发不出去，所以找设备经常要等一两分钟甚至找不到（[#4](https://github.com/zheznanohana/nekodrop/issues/4)）。打开后固件均衡分配（和 ForbiddenItlwm 的 `itlawdlbtcoex=1` 相同）：在一台 AX200 上、没开分享面板的情况下，Mac 9 次都在 2.3 秒内出现，iPhone 9 次里出现 8 次，中位数约 5 秒。只作用于 Intel 用户态 radio；关闭时启动参数和发给固件的命令与之前完全一样。服务日志会写一行 `bluetooth coexistence: mode N`。

### English
- **New experimental switch "Balanced Bluetooth coexistence (Intel)" (off by default, [#26](https://github.com/zheznanohana/nekodrop/pull/26), thanks to [@XXD051030](https://github.com/XXD051030))**: to send, the PC wakes nearby iPhones and Macs with an AirDrop Bluetooth beacon from the card's own Bluetooth. The radio always set the firmware to "Wi-Fi first", so while AWDL listened that beacon barely got out and finding devices often took a minute or two, or never happened ([#4](https://github.com/zheznanohana/nekodrop/issues/4)). With the switch on the firmware balances the two (as ForbiddenItlwm's `itlawdlbtcoex=1`): on one AX200, with no share sheet open, the Mac appeared within 2.3 s in 9 of 9 tries and the iPhone in 8 of 9, median about 5 s. Intel user-mode radio only; off, the launch arguments and firmware commands are exactly as before. The service log gets a `bluetooth coexistence: mode N` line.

---

## v0.1.32 (Hourly / Pre-release) — 2026-11-22

### 中文
- **新的实验性开关「Wi-Fi 和 AirDrop 不在同一信道时按苹果的方式分时」（默认关闭，[#4](https://github.com/zheznanohana/nekodrop/issues/4)）**：nekodrop 连的 5 GHz Wi-Fi 和 AirDrop 不在同一个信道时（#4 是 Wi-Fi 在 40、AirDrop 在 149），网卡固件会按自己的时钟在两个信道之间来回切，跟 iPhone 的 AWDL 时隙完全对不上。电脑不在 149 的时间和 iPhone 不在 149 的时间叠在一起，有效传输时间只剩大约三分之一，这和 #4 实测的 8.7–11.2 MiB/s 对得上（同一信道时是 30 MB/s）。打开后照 macOS 的做法：把路由器写进 AirDrop 广告的 16 个时隙里（空闲时第 6、7、14、15 个；传输时按路由器流量给 1–6 个），这些时隙里电脑不发 AirDrop 同步帧、A-MPDU 也暂停，让 iPhone 知道电脑什么时候在路由器那边。只在 awdld 的 AW 计数和 iPhone 同步时改写，否则不动。移植自 [ForbiddenItlwm PR #30](https://github.com/retropineaple/ForbiddenItlwm/pull/30)；PR #30 本身也还没测出提速效果，**这次同样没有真机数据**。服务日志每 5 秒会有一行 `coex plan:` 统计。
- **修复卸载后安装目录删不掉、引擎重启后留下孤儿进程（[#22](https://github.com/zheznanohana/nekodrop/issues/22)，感谢 GGBond-cer 找到根因）**：AirDrop 引擎发送时启动的蓝牙信标脚本（`airdrop-beacon.ps1`，`powershell.exe`）把安装目录下的 `airdrop\` 当作工作目录占着，而且引擎重启时不会跟着退出，越积越多。卸载脚本按路径和进程名都找不到它们，`airdrop\` 就删不掉。现在：信标脚本启动后立刻离开安装目录，并在启动它的引擎退出后几秒内自己退出；安装和卸载时也会结束命令行里带安装目录的进程。

### English
- **New experimental switch "Apple-style time sharing when Wi-Fi and AirDrop use different channels" (off by default, [#4](https://github.com/zheznanohana/nekodrop/issues/4))**: with nekodrop's 5 GHz Wi-Fi on another channel than AirDrop (#4: Wi-Fi on 40, AirDrop on 149), the firmware switches between the two on its own clock, unaligned with the iPhone's AWDL entries. The PC's time away from 149 and the iPhone's add up, leaving about a third of the air for the transfer, which matches #4's 8.7–11.2 MiB/s (30 MB/s on one channel). With the switch on, as macOS does, the router goes into AirDrop's 16 advertised entries (6, 7, 14 and 15 when idle; 1 to 6 while transferring, by the router's traffic), and the PC sends no AWDL sync frames and pauses A-MPDU sending in them, so the iPhone knows when the PC is with the router. Frames are rewritten only while awdld's AW counter is in step with the iPhone's. Ported from [ForbiddenItlwm PR #30](https://github.com/retropineaple/ForbiddenItlwm/pull/30), whose speed benefit is itself not yet measured; **no hardware data for this either**. The service log gets a `coex plan:` line every 5 s.
- **Fixed the install folder surviving an uninstall, and orphan processes after engine restarts ([#22](https://github.com/zheznanohana/nekodrop/issues/22), root cause found by GGBond-cer)**: the Bluetooth beacon script the AirDrop engine starts when sending (`airdrop-beacon.ps1`, `powershell.exe`) kept the install folder's `airdrop\` as its working directory and outlived engine restarts, piling up. The uninstaller found them neither by path nor by name, so `airdrop\` could not be removed. The beacon script now leaves the install folder at once and exits within seconds of the engine that started it; install and uninstall also end any process whose command line names the install folder.

---

## v0.1.31 (Hourly / Pre-release) — 2026-11-22

### 中文
- **修复 AX211 / AX411 / Killer 1690 上 iPhone、Mac 看不到这台电脑（[#24](https://github.com/zheznanohana/nekodrop/issues/24)、[#25](https://github.com/zheznanohana/nekodrop/issues/25)）**：radio 让固件在每个收到的帧后面附上 FCS（校验和），而 RxFrame 约定交给 awdld 的帧不带 FCS。AX210 系列这几张卡的固件确实会附上，awdld（OWL）看到 AWDL 动作帧最后一个 TLV 后面多出 4 个字节，就在记下“收到过 MIF”之前把整帧丢掉，于是 iPhone 一直是 `not valid yet`：电脑不会跟随它的同步，也不会被它列为接收方。发送不受影响，所以“能发不能收”。现在 radio 只在帧尾 4 字节确实等于这个帧的 CRC-32 时才把它去掉（不带 FCS 的帧被误判的概率是 2⁻³²），对所有网卡都安全。
- **修复 A-MPDU 第一次发送成功后，第二次发送失败、iPhone 从列表消失（[#4](https://github.com/zheznanohana/nekodrop/issues/4)，AX200 实测）**：发送结束后 Block Ack 会话一直停在 `agreed`。这期间监听只收组播帧，iPhone 结束会话时发来的单播 DELBA 被固件丢掉，下一次发送仍按旧会话发 A-MPDU，iPhone 不再接收，于是连接被拒（`WinError 10061`），随后 `peer_gone`，只有重启 radio 才能恢复。现在批量发送结束、队列清空 1.5 秒后，电脑主动发 DELBA 结束会话、恢复完整接收，下一次发送重新协商（约 0.3 秒）。
- **诊断**：radio 的 `rx:` 统计行新增 `FCS removed`、`MIF bad tail`（awdld 仍会丢掉的 MIF）、`MIF without version`（缺少 awdld 需要的版本 TLV）三项计数，前 3 个尾部异常的 MIF 会记下最后 16 个字节。
- **还没有在 AX211 真机上验证**，请 #24、#25 的用户帮忙测试。

### English
- **Fixed iPhone/Mac never listing the PC on AX211 / AX411 / Killer 1690 ([#24](https://github.com/zheznanohana/nekodrop/issues/24), [#25](https://github.com/zheznanohana/nekodrop/issues/25))**: the radio asks the firmware to append the FCS to received frames, while RxFrame promises awdld frames without it. These AX210-family cards do append it, and awdld (OWL) drops an AWDL action frame with 4 bytes after its last TLV before it records the MIF, so the iPhone stayed `not valid yet`: the PC never followed its sync and was never listed as a receiver. Sending was unaffected. The radio now removes the last 4 bytes only when they are the frame's valid CRC-32 (a frame without an FCS matches by chance with odds of 2^-32), which is safe on every card.
- **Fixed the second A-MPDU send failing and the iPhone vanishing after a successful first one ([#4](https://github.com/zheznanohana/nekodrop/issues/4), measured on an AX200)**: the Block Ack session stayed `agreed` after the send. Meanwhile the listener takes group frames only, so the iPhone's unicast DELBA was dropped by the firmware, and the next send still went out as A-MPDUs the iPhone no longer accepted: connection refused (`WinError 10061`), then `peer_gone` until the radio restarted. The PC now ends the session itself with a DELBA 1.5 s after a bulk send ends and its queue drains, restoring full receive; the next send negotiates again (~0.3 s).
- **Diagnostics**: the radio's `rx:` line adds `FCS removed`, `MIF bad tail` (MIFs awdld would still drop) and `MIF without version` (no version TLV, which awdld needs); the first 3 MIFs with a bad tail log their last 16 bytes.
- **Not yet verified on an AX211**; the reporters of #24 and #25 are asked to test it.

---

## v0.1.30 (Stable / 稳定版) — 2026-11-22

### 中文
- **🚀 传输速度十倍级跃升（AX201 并发模式实测验证）**：
  - **PC → iPhone 实测达到 `30.8 MB/s`**（104.8 MB 文件仅用 **3.4 秒**传完，对比此前单帧发送的 1–3 MB/s 提升 **10× 以上**）。开启实验性 A-MPDU 后 **0.304 秒**完成 Block Ack 握手，单次聚合高达 **63 帧**、确认率 **99.69%**（81,451 / 81,707），稳定跑满 **VHT 80 MHz 双流（2SS）MCS 9 + LDPC + SGI（866.7 Mbps）**。
  - **iPhone → PC 实测平均 `13.5 MB/s`（后半段峰值 ~30 MB/s）**，104.8 MB 安装包 **8.1 秒**收完落盘。
- **重点修复实验性 A-MPDU 协商与聚合路径（[#4](https://github.com/zheznanohana/nekodrop/issues/4)、[#9](https://github.com/zheznanohana/nekodrop/issues/9)）**：
  - 修复 0.1.28 导致 TX AddBA 一直停在 `asking`（`asks 80, agreed 0`）的回归：此前在 `asking` 阶段就切到组播过滤，把 iPhone 返回的单播 ADDBA Response 丢掉了。现在 `asking` 阶段保持混杂与管理帧接收，仅在 `agg_enable(true)` 拿到对端同意后原子切换到 ForbiddenItlwm 的 BA 监听状态（AWDL BSSID + 基础 ACK 速率 + 组播过滤），退出时立即恢复。
  - 修复 A-MPDU 响应路径（`handle_tx_resp`）在 `frame_count > 1` 时提前回收队列索引的隐患（改为仅由 `IWX_BA_NOTIF` 回收），并清零 26 字节 QoS 帧头后的 2 字节对齐填充。
  - 更激进的 A-MPDU 触发与协商：250 ms 内收到 24 帧大包即触发（此前为 1 s / 200 帧），每 100 ms 发一次 ADDBA（最多 20 次 / 5 s 超时即干净回退单帧，60 s 后允许再试），`asking` 期间不再开启发送门控（避免 `gate holds` 堆积与阻塞管理帧），回退时同步 `TXPATH_FLUSH` 清空聚合队列避免与单帧队列乱序竞争，同时开启 20/40/80 MHz SGI 与尊重对端协商窗口大小。
- **整体发送提速（A-MPDU 关/开均生效）**：Intel 无线发送窗口从 48 提到 64（`--tx-window 64`）；单帧速率阶梯改为 32 帧窗口（爬升快 2 倍）、上限新增 VHT 80 MHz 2SS MCS 8，并启用 VHT LDPC + SGI；主循环每轮读取管道帧数上限从 64 提到 128；`--fast-unlock` 在信道允许发送后立刻停掉剩余扫描。
- **诊断报告保留每次发送的 A-MPDU 快照（[#4](https://github.com/zheznanohana/nekodrop/issues/4)）**：在每次批量发送结束、回退或收到 DELBA 时立即写入一条 `aggregate:` 快照，诊断报告的 Bring-up 表优先展示活跃期（`asking`/`agreed`）记录而不再被末尾 `idle` 覆盖。

### English
- **🚀 10x+ Transfer Speed Leap (verified on AX201 Concurrent mode)**:
  - **PC → iPhone measured at `30.8 MB/s`** (104.8 MB transferred in **3.4 s**, up **10x+** from 1–3 MB/s single-frame TX). With experimental A-MPDU enabled, Block Ack negotiation completes in **0.304 s**, reaching **63-frame** aggregates with **99.69%** ACK rate (81,451 / 81,707) at full **VHT 80 MHz 2SS MCS 9 + LDPC + SGI (866.7 Mbps)**.
  - **iPhone → PC measured at `13.5 MB/s` average (~30 MB/s peak)**, receiving the 104.8 MB installer in **8.1 s**.
- **Major fix and tuning for experimental A-MPDU TX ([#4](https://github.com/zheznanohana/nekodrop/issues/4), [#9](https://github.com/zheznanohana/nekodrop/issues/9))**:
  - Fixed the 0.1.28 regression where TX AddBA stayed stuck in `asking` (`asks 80, agreed 0`): switching to group-only filtering during `asking` caused the firmware to drop unicast ADDBA Response management frames. Promiscuous/management receive now stays open throughout `asking`, switching atomically to ForbiddenItlwm's BA listener state (AWDL BSSID + basic ACK rates + group-only filter) inside `agg_enable(true)` only after the peer agrees, and restoring immediately on teardown.
  - Fixed `handle_tx_resp` for the aggregation queue so multi-frame A-MPDU responses (`frame_count > 1`) are reclaimed solely by `IWX_BA_NOTIF` (matching Linux iwlwifi / ForbiddenItlwm), and zeroed the 2-byte alignment padding after 26-byte QoS headers.
  - More aggressive A-MPDU trigger and bounded fallback: triggers at 24 payload frames in 250 ms (down from 200 in 1 s), asks every 100 ms up to 20 asks / 5 s before cleanly falling back with a 60 s cooldown, stops applying the send gate during `asking` (and never gates management frames), calls `TXPATH_FLUSH` on fallback to avoid reordering against the single-frame queue, enables 20/40/80 MHz SGI in TLC, and honors the peer's negotiated BA window.
- **Faster sending overall (with or without A-MPDU)**: Intel `--tx-window` raised from 48 to 64; single-frame rate ladder steps up over 32-frame windows (2x faster ramp), adds VHT 80 MHz 2SS MCS 8, and enables VHT LDPC + SGI; radio main loop drains up to 128 pipe frames per tick; `--fast-unlock` cancels remaining scans as soon as transmitting is allowed.
- **Per-send A-MPDU snapshots in diagnostics ([#4](https://github.com/zheznanohana/nekodrop/issues/4))**: logs an immediate `aggregate:` snapshot at the end of every bulk send, fallback, or DELBA, and `report.ps1` prefers the last active (`asking`/`agreed`) snapshot over a trailing `idle` line.

---

## v0.1.29 (Hourly / Pre-release) — 2026-11-22

### 中文
- **内置支持 Realtek RTL8852CE（`10EC:C852`）**：和 Intel 网卡一样按 PCI ID 自动识别、自动接管，不需要开关。用的是 winawdl 的 radio（基于 raph559/devourer 的 Windows PCIe 后端），打包为 `radio\nekodrop-radio-rtl.exe`。这张卡的驱动不支持同时连 Wi-Fi，所以按需模式运行。**还没有真机数据**。
- **自动识别失败时可以手动选择网卡**：*设置 → 网卡* 会列出本机的 Intel/Realtek 网卡和可选型号（Intel AX201/9560、AX200、AX210、AX211/AX411、Realtek RTL8852CE）。手动选择只对驱动包里列出的硬件 ID 有效。
- **网卡后端模块化**：服务端按 `IRadioBackend` 启动各个网卡的 radio；适配新网卡的接口和步骤写在 [docs/ADDING-A-CARD.md](docs/ADDING-A-CARD.md)，里面也列了下一批候选网卡和各自可以移植的驱动来源。

### English
- **Realtek RTL8852CE (`10EC:C852`) built in**: recognized and taken by its PCI ID like the Intel cards, no switch. It runs winawdl's radio on raph559/devourer's Windows PCIe backend, shipped as `radio\nekodrop-radio-rtl.exe`. Its driver has no station, so it runs on demand. **No hardware data yet**.
- **Manual card choice when detection fails**: *Settings > Wi-Fi card* lists the PC's Intel/Realtek network controllers and the models (Intel AX201/9560, AX200, AX210, AX211/AX411, Realtek RTL8852CE). Only for hardware IDs the driver package lists.
- **Pluggable radio backends**: the service starts each card's radio through `IRadioBackend`; the interfaces and checklist for the next card family are in [docs/ADDING-A-CARD.md](docs/ADDING-A-CARD.md), with the candidates and the drivers they could be ported from.

---

## v0.1.28 (Hourly / Pre-release) — 2026-11-22

### 中文
- **修复：连接 Wi-Fi 认证失败后网卡固件崩溃（[#21](https://github.com/zheznanohana/nekodrop/issues/21)）**：移除 station 前没有先取消会话保护、清空它的发送队列，结果移除命令超时，固件随之崩溃（LMAC 0x125F），#21 里连续发生了 3 次。现在按 OpenBSD/itlwm 的顺序断开：取消会话保护 → drain + TXPATH_FLUSH → 移除 station → binding → MAC → PHY。
- **实验性 A-MPDU：会话期间改用 ForbiddenItlwm 的监听配置（[#4](https://github.com/zheznanohana/nekodrop/issues/4)、[#9](https://github.com/zheznanohana/nekodrop/issues/9)）**：发送命令、QoS 帧、station、队列和速率控制都已经和 ForbiddenItlwm 逐字节一致，只剩会话期间的监听 MAC 不同。现在在 Block Ack 会话期间（混杂模式关闭时）改用 AWDL BSSID、基础 ACK 速率和只收组播的过滤器，和 ForbiddenItlwm 在 Mac → iPhone 跑到 23 MB/s 时的配置一样。发现阶段不变。A-MPDU 仍默认关闭，**还没有真机数据**。

### English
- **Fix: firmware crash after a failed Wi-Fi authentication ([#21](https://github.com/zheznanohana/nekodrop/issues/21))**: the station was removed without cancelling its session protection or flushing its queue; REMOVE_STA timed out and the firmware asserted (LMAC 0x125F), three times in a row in #21. Teardown now follows OpenBSD/itlwm: cancel session protection, drain + TXPATH_FLUSH, then station, binding, MAC, PHY.
- **Experimental A-MPDU: ForbiddenItlwm's listener state during the session ([#4](https://github.com/zheznanohana/nekodrop/issues/4), [#9](https://github.com/zheznanohana/nekodrop/issues/9))**: the TX command, QoS frame, peer station, queue and rate control already matched ForbiddenItlwm byte for byte; only the listener's MAC context differed. While the Block Ack session runs (promiscuous receive off) it now carries AWDL's BSSID, the basic ACK rates and a group-only filter, as ForbiddenItlwm does where Mac -> iPhone reaches 23 MB/s. Discovery is unchanged. A-MPDU stays off by default; **no hardware data yet**.

---

## v0.1.27 (Hourly / Pre-release) — 2026-11-22

### 中文
- **修复：传输途中被切信道打断（[#9](https://github.com/zheznanohana/nekodrop/issues/9)）**：iPhone 公告别的信道后，服务会等到 10 秒没有数据再切过去，但以前最多只等 120 秒，等满就不管有没有数据直接切。#9 里一个 537 MB 的发送在信道 44 上跑得好好的（1.8 MB/s），120 秒后被切到 149，在 37.7% 处以 TLS EOF 断开。现在只要还有数据就一直等；iPhone 又报回当前信道时取消切换；同时只保留一个切换任务。
- **修复：更新时 `libcrypto-3.dll` 复制失败（[#22](https://github.com/zheznanohana/nekodrop/issues/22)）**：安装脚本结束 `nekodrop.exe` 时，它启动的 AirDrop 引擎（`python.exe`）还占着文件。现在会等服务真正停下，结束所有从安装目录运行的进程，复制失败会重试，最后失败时会说明是哪个进程占着。
- **卸载不再静默留下残留（[#22](https://github.com/zheznanohana/nekodrop/issues/22)）**：每一步的失败都会记进 `%ProgramData%\nekodrop-uninstall.log`；会删除 `nekodrop` TAP 网卡（保留 TAP 驱动）和 `%LocalAppData%\nekodrop`；服务程序缺失时改用 `restore.ps1` 归还网卡。
- 构建时会用 PowerShell 5.1 解析所有安装器脚本，语法错误会让构建失败。

### English
- **Fix: sends cut by a channel move ([#9](https://github.com/zheznanohana/nekodrop/issues/9))**: after the iPhone named another channel, the service waited for 10 s without data but gave up after 120 s and moved anyway. In #9 a 537 MB send running at 1.8 MB/s on channel 44 was moved to 149 after 120 s and died with TLS EOF at 37.7%. Now it waits as long as data flows, cancels the move when the iPhone names our channel again, and runs one mover at a time.
- **Fix: updates failing to copy `libcrypto-3.dll` ([#22](https://github.com/zheznanohana/nekodrop/issues/22))**: the setup killed `nekodrop.exe` but not the AirDrop engine (`python.exe`) it starts, which kept the file open. Now it waits for the service to stop, stops everything running from the install folder, retries the copy and names what still holds it.
- **Uninstall no longer fails silently ([#22](https://github.com/zheznanohana/nekodrop/issues/22))**: each failed step goes to `%ProgramData%\nekodrop-uninstall.log`; it removes the `nekodrop` TAP adapter (not the TAP driver) and `%LocalAppData%\nekodrop`, and gives the card back with `restore.ps1` when the service exe is missing.
- The build parses every installer script under PowerShell 5.1 and fails on a syntax error.

---

## v0.1.26 (Hourly / Pre-release) — 2026-11-22

本版来自「nekodrop 自主进化」第 6 轮：Gemini 提案 → Claude 实现 → Codex QA 通过，风险等级 EXPERIMENTAL。 / From self-evolution round 6: proposal by Gemini, implementation by Claude, Codex QA passed, risk EXPERIMENTAL.

### 中文
- **新的实验性开关「更快解锁被动 5 GHz 信道」（默认关闭，[#11](https://github.com/zheznanohana/nekodrop/issues/11)）**：网卡不接受国家码时（#11 的 AX210 + TW），5 GHz AirDrop 信道一开始是被动的，在听到 iPhone 或附近路由器之前电脑一帧都不发，#11 里每次重启要等约 60 秒。打开后会在 1.5、12、30 秒各扫一次 11 个常用信道（原来是 10 秒扫一次全部信道），让网卡按自己的规则从附近路由器学到国家码。被拒绝的国家码会在 iPhone 报出同一国家时再问一次。不会在被动信道上强行发射。关闭时扫描时机和以前完全一样。**还没有真机数据**，有用户证明没问题后会改成始终打开。
- **发送统计改正**：`tx: N submitted` 只统计 AirDrop 的帧，发给路由器的帧移到 `wifi:` 那一行。以前在「同时联网」模式下 submitted 比 done 多出一大截，看上去像丢包，其实不是。
- **发现耗时诊断**：radio 日志新增「第一次收到 AWDL 帧 / MIF / 发给本机的帧用了多少秒」，诊断报告里有对应的时间表（[#4](https://github.com/zheznanohana/nekodrop/issues/4)、[#11](https://github.com/zheznanohana/nekodrop/issues/11)）。
- 诊断报告不再把「更快发送」显示成关闭（0.1.25 起它始终打开）。

### English
- **New experimental switch "Faster unlock of a passive 5 GHz channel" (off by default, [#11](https://github.com/zheznanohana/nekodrop/issues/11))**: where the card refuses the country (#11: AX210, TW), the 5 GHz AirDrop channel starts passive and the PC sends nothing until it hears an iPhone or a router; #11 waited ~60 s after each restart. With the switch on, the radio scans the eleven common channels at 1.5, 12 and 30 s (instead of all channels once at 10 s) so the card learns the country from nearby routers by its own rules, and asks again for a refused country when an iPhone names it. It never transmits on a passive channel. Off, the scan timing is exactly as before. **No hardware data yet**; it becomes always-on once users confirm it causes no problems.
- **TX counters fixed**: `tx: N submitted` counts AirDrop frames only; frames to the access point moved to the `wifi:` line. Under Concurrent the gap between submitted and done looked like loss and was not.
- **Discovery timing**: the radio logs how many seconds until the first AWDL frame, MIF and frame addressed to us, with a timing table in the diagnostic report ([#4](https://github.com/zheznanohana/nekodrop/issues/4), [#11](https://github.com/zheznanohana/nekodrop/issues/11)).
- The diagnostic report no longer shows faster sending as off (it is always on since 0.1.25).

---

## v0.1.25 (Hourly / Pre-release) — 2026-11-22

### 中文
- **「更快发送到 iPhone」改为始终打开，设置里的开关已删除**：同时在途的 AWDL 帧从 8 个提高到 48 个。这个开关从 0.1.9 起就有，至今没有任何网卡报告它让情况变差：AX211 上 537 MB 以 3.4 MB/s 完整传完、没有放弃的帧（[#9](https://github.com/zheznanohana/nekodrop/issues/9)），AX210 略快（[#14](https://github.com/zheznanohana/nekodrop/issues/14)），AX200 速度不变（[#4](https://github.com/zheznanohana/nekodrop/issues/4)）。之前没开过的用户升级后就会用上它。
- 「聚合发送（A-MPDU）」仍有问题（固件只发单帧），保留开关，默认关闭。

### English
- **"Faster sending to iPhone" is always on and its switch is gone**: up to 48 AWDL frames in flight instead of 8. It was a switch since 0.1.9 and no card reported it making things worse: AX211 sent 537 MB at 3.4 MB/s with no frames given up ([#9](https://github.com/zheznanohana/nekodrop/issues/9)), AX210 a little faster ([#14](https://github.com/zheznanohana/nekodrop/issues/14)), AX200 the same speed ([#4](https://github.com/zheznanohana/nekodrop/issues/4)). Users who never turned it on get it after updating.
- Aggregated sending (A-MPDU) still has problems (the firmware sends single frames only), so it keeps its switch, off by default.

---

## v0.1.24 (Hourly / Pre-release) — 2026-11-22

### 中文
- **修复：开启实验性 A-MPDU 后找不到 iPhone（0.1.22 起的回归，[#4](https://github.com/zheznanohana/nekodrop/issues/4)）**：0.1.22 在 A-MPDU 模式下把监听的 BSSID 改成了 AWDL BSSID 并加了 ACK 速率，0.1.23 又在会话期间把接收过滤收窄到只收组播。AX200 实测 0.1.23 开 A-MPDU 后 3 分半钟都没发现设备（PSF 计数停住、`0 addressed to us`），而 0.1.20 同样开着 A-MPDU 还能发现。现在 MAC 上下文恢复到 0.1.20 的样子。默认路径（A-MPDU 关闭）从没用过这两处改动，不受影响。
- **修复：「按需」模式下在 nekodrop 里连接 Wi-Fi 会白白重启无线（[#11](https://github.com/zheznanohana/nekodrop/issues/11)）**：只有「同时联网和 AirDrop」模式会在 AirDrop 旁边连 Wi-Fi。以前在其他模式下点连接，服务会重启 radio 但什么也连不上，每点一次 AirDrop 发现就从头开始。现在会直接提示需要切换模式。
- **设置窗口标题显示版本号**（[#11](https://github.com/zheznanohana/nekodrop/issues/11)）。
- 另外包含 0.1.23 之后合入的：AirDrop 引擎发送优化（64 KiB DVZip 块、自适应不压缩直传、2 MiB 套接字缓冲、`TCP_NODELAY`），安装器按厂商识别网卡、`restore.ps1` 不再假设 Intel。

### English
- **Fix: no devices found with experimental A-MPDU on (regression since 0.1.22, [#4](https://github.com/zheznanohana/nekodrop/issues/4))**: 0.1.22 gave the A-MPDU listener AWDL's BSSID and ACK rates, and 0.1.23 narrowed its receive filter to group frames during a session. On an AX200, 0.1.23 found nothing for 3.5 minutes with A-MPDU on (PSF count frozen, `0 addressed to us`) while 0.1.20 still found the iPhone. The MAC context is back to 0.1.20's. The default path (A-MPDU off) never used either change.
- **Fix: joining Wi-Fi in nekodrop outside Concurrent mode restarted the radio for nothing ([#11](https://github.com/zheznanohana/nekodrop/issues/11))**: only "Wi-Fi and AirDrop together" runs Wi-Fi beside AirDrop. In the other modes, choosing a network restarted the radio and joined nothing, resetting discovery each time. The Wi-Fi window now says which mode is needed.
- **The settings window title shows the version** ([#11](https://github.com/zheznanohana/nekodrop/issues/11)).
- Also includes what landed after 0.1.23: AirDrop engine send tuning (64 KiB DVZip chunks with an adaptive stored bypass, 2 MiB socket buffers, `TCP_NODELAY`), vendor-aware card classification in the installer and a vendor-agnostic `restore.ps1`.

---

## v0.1.23 (Hourly / Pre-release) — 2026-11-22

### 中文
- **底层传输提速（Intel 实验性 A-MPDU TX + 全网卡通用 Named Pipe 批处理）**：
  - 修复 `IwxDevice::transmit_agg()` 每帧同步敲门铃的问题：改为批量入队（`kick = false`）+ 整批单次 `kick_agg()`，消除每帧 20–40 µs 的同步 UMDF IOCTL 开销（避免饿死固件 SCD 聚合调度器）。
  - `agg_setup()` 补充 TLC `LDPC` 与双天线 `STBC` 标志（VHT 80 MHz MCS 9 必需），对齐上游 `ForbiddenItlwm`。
  - 开启 `--aggregate` 并关闭混杂模式时将监听过滤掩码严格收缩为 `IWX_MAC_FILTER_ACCEPT_GRP`，避免干扰硬件 Block Ack 记账（默认非聚合路径 100% 不变）。
  - 聚合队列在途上限提升至 `min(128, ring - 16)`（22000 族 128 帧 / AX210 族 112 帧），并在收到对端 DELBA 时先发 `IWX_TXPATH_FLUSH` 冲刷 `sta_id=3` 防止 SSN 错位。
  - `PipeServer` 新增 RX 帧批量合并写入（`queue_send` / `flush_send`，单批达 32 KiB 或每轮 `dev.poll()` 结束统一一次 `WriteFile`），并将管道缓冲扩至 256 KiB，大幅降低高吞吐下的系统调用开销（适用于所有网卡与模式）。
- **多厂商驱动层基础（`driver/regproxy`）**：
  - `regproxy.c` 升级为自动选择**最大的** Memory BAR（≥ 8 KiB），使单一驱动原生兼容 Intel（BAR0 8 KiB）、Realtek RTL8852CE/BE（BAR2 1 MiB）与 MediaTek MT7921/MT7922（BAR0 1 MiB），Intel 行为完全不变。
- **文档与兼容矩阵**：
  - 新增本更新日志 `CHANGELOG.md`，以及独立的中英双语跨品牌网卡兼容性文档 [`docs/COMPATIBILITY.zh-CN.md`](docs/COMPATIBILITY.zh-CN.md) / [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md)。

### English
- **Radio throughput improvements (Intel experimental A-MPDU TX + universal Named Pipe RX batching)**:
  - Batched A-MPDU doorbells (`transmit_agg(..., kick=false)` + one `kick_agg()` per batch), removing synchronous per-frame UMDF IOCTL overhead that could starve the firmware's SCD aggregator.
  - Enabled TLC `LDPC` (required for VHT 80 MHz MCS 9) and two-chain `STBC` flags in `agg_setup()`, matching upstream `ForbiddenItlwm`.
  - Narrowed non-promiscuous listener `filter_flags` to `IWX_MAC_FILTER_ACCEPT_GRP` when `--aggregate` is active (default non-aggregate path is 100% untouched).
  - Raised A-MPDU in-flight cap to `min(128, ring - 16)` and added `IWX_TXPATH_FLUSH` (`agg_flush()`) on DELBA before renegotiating to prevent SSN desync.
  - Added batched RX pipe writes (`PipeServer::queue_send` / `flush_send`, 32 KiB auto-flush + post-`dev.poll()` flush) and raised the Named Pipe buffer to 256 KiB for all cards and modes.
- **Multi-vendor register proxy (`driver/regproxy`)**:
  - Updated `regproxy.c` to select the largest memory BAR (≥ 8 KiB), supporting Intel (8 KiB BAR0), Realtek RTL8852CE/BE (1 MiB BAR2), and MediaTek MT7921/MT7922 (1 MiB BAR0) with zero change on Intel hardware.
- **Documentation**:
  - Added `CHANGELOG.md` and standalone multi-vendor compatibility matrices [`docs/COMPATIBILITY.md`](docs/COMPATIBILITY.md) / [`docs/COMPATIBILITY.zh-CN.md`](docs/COMPATIBILITY.zh-CN.md).

---

## v0.1.22 (Stable / 首个稳定版) — 2026-11-22

> SHA-256: `5d7545d61b8c546828bba2ad4fe13bd101cacfeaa7cf9b3853cf004eac9e77cf`

### 中文
- **客户端 UI（#11）**：修复 Windows 深色模式下主窗口与发送窗口附近设备名黑字看不清；修复对端 `model` 为空时 iPad / Mac 仍显示手机图标。
- **实验性 A-MPDU 发送（#4）**：开启 `--aggregate` 时为监听 MAC 设置 AWDL BSSID 与基础 ACK 速率；收紧 TLC 高 MCS 掩码；新增聚合单帧状态诊断日志及无 Block Ack 自动回退普通单帧队列保护（`iwx_selftest` 覆盖）。
- **模块化多网卡架构**：新增 `iwxradio/src/radio_backend.h` 后端能力契约与编译期校验，移除 `KernelDevice` 的 Intel 空桩，新增 `radio_backend_test.h` 离线测试。

### English
- **Client UI (#11)**: Fixed black device names in Windows dark mode in Main/Send windows; fixed iPad/Mac icon fallback when `model` is null.
- **Experimental A-MPDU sending (#4)**: Set AWDL BSSID and basic ACK rates on the `--aggregate` listener MAC; restricted TLC to high MCS; added per-single status/rate logging and automatic no-Block-Ack fallback (`iwx_selftest`).
- **Modular radio HAL**: Introduced `iwxradio/src/radio_backend.h` capability contract + compile-time checks, removed Intel stubs from `KernelDevice`, added `radio_backend_test.h`.

---

## v0.1.21 — 2026-11-22

> SHA-256: `58c72b9a0fe73dbe481edfbfbdf4a6a6b7ddbe1e49436cbec6e83801bdb1cd41`

- 新增 `app/service/CardCatalog.cs` 统一网卡档案与能力抽象，解耦 `Card.cs` / `Supervisor.cs` 的 Intel 硬编码，并将 `NekoDropService.exe --selftest` 接入每次 CI 构建。 / Introduced `CardCatalog.cs` multi-vendor adapter profiles & capability abstractions, decoupling `Card.cs` and `Supervisor.cs`, wired `NekoDropService.exe --selftest` into CI.

---

## v0.1.20 — 2026-11-22

> SHA-256: `16a19a5dd1790e7ef35601bb77fb4b6ff21caece31bc44c00e2ccbb5163c2e25`

- 修复 `#4` 代理环境下检查更新触发 GitHub API HTTP 403 限流（新增本地 `ETag` / `304` 缓存、15s 超时、`releases.atom` 回退及设置页「打开下载页」按钮）。 / Fixed #4 update check HTTP 403 rate-limit behind proxies (local `ETag` cache, 15s timeout, `releases.atom` fallback, Open Releases button).
- `installer/report.ps1` 新增全品牌无线网卡识别分类与 `-SelfTest`，新增 `docs/HARDWARE.md` 多网卡架构文档。 / Added multi-vendor Wi-Fi classification & `-SelfTest` to `installer/report.ps1`, plus `docs/HARDWARE.md`.

---

## v0.1.19 — 2026-11-22

> SHA-256: `6bba6037bb649b57c11aa3edeec5614a04864fbf798d3b28e647f3325ab12f4f`（已在 #11 AX210 真机验证）

- 修复 `#11`：`nekodrop` TAP 网卡跃点由 `75` 提升至 `500`，不再抢占笔记本有线以太网默认路由；修复被动信道 30 秒误判重启循环。 / Fixed #11: raised TAP metric `75 → 500` so Ethernet keeps routing priority; fixed 30s passive-channel bounce loop (hardware-verified on AX210).

---

## v0.1.18 — 2026-11-21

> SHA-256: `3c02105f7b4f7d3607e19cc46b9e217e0d08a4643f35852f8c7a3e7f3a1f58d1`（冷启动已在 #4 AX200 真机验证）

- 修复 `#4` 冷启动卡住 6 分钟问题（冷启动 `radio ready` 实测降至 2.1 秒）；修复 `#20` Win10 残留幽灵 `Not Present` TAP 适配器；确认 `#18` Win10 22H2 双向可用。 / Fixed #4 6-minute cold-boot stall (verified 2.1s on AX200); fixed #20 Win10 phantom TAP adapter; confirmed #18 Win10 22H2 working.

---

## v0.1.0 – v0.1.17 (2026-11-17 – 2026-11-21)

- 初始发布与早期实机验证里程碑：Intel AX201 / AX200 / AX210 / AX211 双向 AirDrop + Wi-Fi 共存、Windows 10 UMDF 2.31 兼容（#3/#12/#18）、AX210 族 PNVM DMA 预留与 `nic_config` 修复（#2/#7/#8）、128 项 TX 环与自动扩展 AWE 池（#11）、一键诊断报告 `report.ps1`（v0.1.7）、实验性高速发送 `--tx-window 48`（#9 实测提速 +41%）与实验性 A-MPDU 发送初版（v0.1.15）。
