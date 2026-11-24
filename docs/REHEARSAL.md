# Rehearsal: AetherDrop floating-card motion

## Native context-menu selection

Menu text is vertically centered against the full native row, independent of
ToolStrip's padded TextRectangle. A single rounded highlight follows measured
row bounds in menu coordinates over 140 ms with cubic ease-out. Each popup has
its own animation state/timer; retargeting begins at the current rectangle and
Closed stops the timer. Reduced motion snaps directly. Native item selection,
keyboard navigation, submenu actions and text stay unchanged.

Typography.Smoke captures `build/menu-highlight-motion/highlight-*.png` and
checks full-row centering, intermediate vertical travel and final alignment,
along with native rounded-window clipping. Sampled native frames were reviewed;
no browser illustration, browser film gate or continuous desktop video is claimed.

## Receive toggle

The existing native CheckBox input stays intact. Only the thumb's translation
and track brush animate (200/180 ms), using current visual values when reversed.
UiDebug.Smoke checks intermediate position/color, reversal, exact endpoints,
unchanged dimensions, matching receiving status, open tray and unchanged disk
settings. Frames under `build/receive-toggle-rehearsal` were inspected for on,
off and intermediate states; native text/layout remain stationary. No browser
film gate or continuous desktop video is claimed. OS reduced motion is honored.

## Interruption replacement

Failure now shares completion's title/footer replacement choreography and ring
fade. Device name and device glyph remain unchanged. Captured intermediate
native frames (40–460 ms) under `build/interruption-morph-rehearsal` were
inspected. FloatingUi.Smoke verifies the outgoing title, retained HWND and
coordinates throughout, unchanged device-label identity, final native text
transforms, fixed slots and absence of the success button/checkmark. No browser
gate or continuous desktop recording is claimed.

## Tray entry and exit

Native tray popup retains its existing dimensions, content and bottom anchor.
Opening starts at 78% size, settles without bounce in 320 ms and removes the
scale transform; closing shrinks and fades for 180 ms around the same anchor.
An interrupted entry closes from its current values without restarting size.

FloatingUi.Smoke captures entry at 25/125/245 ms, the settled state and exit at
55 ms under `build/tray-motion-rehearsal`. Native assertions verify anchored
entry, identity transforms/full opacity at rest, unchanged screen position,
exit before removal and safe mid-entry close. Existing desktop-only dismissal,
non-dismissal on deactivation and repeated-click behavior remain covered.
Native sampled frames were reviewed; no browser-film gate or continuous desktop
recording is claimed. Windows reduced-motion setting is respected.

## Completion replacement

The transfer's existing native window, card, title, secondary label, icon lane
and footer are retained. Completion is a 380-ms staggered replacement within
those slots, with a local checkmark draw and no card travel or resizing.
The native implementation follows the retained-track and dependent-timing
principles from Stageshow's choreography example; the browser kit is not used.

Captured: `build/completion-morph-rehearsal/contact-sheet.png`, including the
last transfer frame and sampled replacement frames from 40–460 ms. Inspection
confirmed a stable card border, legible final text, a continuous local icon
change, no overlapping old/new text and no movement after the final settle.

Validation: FloatingUi.Smoke verifies the same Window reference and HWND plus
unchanged Left/Top throughout seven intermediate samples, original TextBlock
identity, the outgoing animation, fixed final positions, identity transforms,
full opacity, one checkmark after duplicate events and the working folder
action. The existing completion hold/automatic exit also passes. No continuous
desktop recording or browser `film.mjs --gate` verdict is claimed.

Scope: the actual WPF application, retaining its fixed layout, icons, colors and
Microsoft YaHei text. No React illustration or substitute UI was introduced.
Stageshow's fast input feedback, whole-object arrival, soft landing and stable
text principles are applied to the native controls.

Beats: invitation arrives from the right; its button responds to press; release
returns to the same geometry; accept re-enters at the same anchor; decline or
dismiss accelerates off the right edge.

Captured: 1:1 native WPF frames at sampled flight positions from 20–510 ms and
button resting/pressed/released states. `build/motion-rehearsal/contact-sheet.png`
composes these rendered cards at their measured window positions; it is a test
contact sheet, not a desktop video. Optional capture is enabled with
`AETHERDROP_MOTION_CAPTURE_DIR` when running `tests/FloatingUi.Smoke`.

Validation: Debug build and FloatingUi.Smoke pass. Motion checks confirm
monotonic arrival without overshoot, exact resting coordinates, animated button
surface/tint, unchanged text transforms, release to original geometry, taskbar
clearance, unchanged fixed slots and completion/open-folder behavior.

Review: sampled frames were inspected for clipped text/controls, displaced
labels and landing changes. Text stays native size, the button label remains
stationary, and the card holds its final position. Arrival is opaque; there is
no empty-container phase, bounce or persistent text scaling.

Gate: the skill's browser `film.mjs --gate` is not applicable to the native WPF
window. Native motion assertions and frame inspection were used instead; no
browser rehearsal verdict is claimed.

Not checked: a continuous desktop recording, every monitor/DPI combination,
manual OS animation-setting changes or real-device radio transfers. The native
timelines consult the existing Windows reduced-motion setting; tests do not
change user system preferences.


## 保存成功提示：原生动效复核

- 参考 stageshow 的 `form-error` 保存反馈与 `motion.md` 的分层、交叠和阅读停留原则；沿用真实 WPF 控件，没有复制网页示例或添加伪界面。
- 节奏：不透明矢量背景 240ms 从中心展开；80ms 起对勾轻移入，100–400ms 描绘对勾；130ms 起文字单独上移 4 DIP，150ms 起显现；完整结果约停留两秒；2400ms 起文字先退场，背景延迟 60ms 后用 180ms 收拢。
- 所有文字保持原字体和字号，不进入背景的缩放轨道。背景自身和容器透明度始终为 1。
- 连续保存保留同一个卡片实例，从当前表面尺寸续接，新保存替代旧退出计时；关闭窗口会使待执行回调失效。
- 深浅色原生检查通过：`UiDebug.Smoke --settings-only`。采样 40、160、420、2320、2490ms 与退出中再次保存，图像置于忽略的 `build/save-motion-dark`、`build/save-motion-light`；深色接触表为 `contact-sheet.png`。
- 检查中间几何、完成端点、透明度、文字变换和过期计时；未改动正式设置和系统动画偏好。减少动画分支直接显示完整结果、到时关闭；本轮未切换 Windows 动画设置实测该分支。
- 网页 `film.mjs --gate` 不适用于该 WPF 窗口，本轮不声明网页 gate 通过，也未生成网页演示替代原生验证。未重新运行独立的传输模拟回归。


## 桌面布局与视觉规范统一

- 决策先行，完整规则见 `UI-DESIGN-SYSTEM.md`。保留现有 WPF / Forms 组件及功能，统一间距、字体层级、不透明表面、辅助标题栏与操作区。主窗口和设置圆角仍为 12 DIP，托盘与接收固定尺寸不变。
- 深浅色 `UiDebug.Smoke --layout-only` 通过：设置五页、460/520 宽度、中英文换行、主窗口短高断点、发送、Wi-Fi、登录／验证码、托盘、传输／完成／中断。截图在忽略的 `build/layout-system-*`。
- 深浅色 Typography.Smoke 通过：微软雅黑字形、原生菜单字号、选中提示、子菜单往返、圆角。
- FloatingUi.Smoke 检查中发现后台优先级的 100ms 采样实际在约 581ms 执行，早于采样的 280ms 退出已完成。测试改为检查点击后仍存在、实际关闭至少等待 240ms、下一邀请顺序；不改变产品动画。该套检查通过。该套旧测试需要真实窗口钩子，不能使用 UI-debug App；本轮页面布局与菜单测试使用内存设置入口。
- 视觉复核包括深浅色主窗口、中文紧凑设置、英文换行导航、网络页、Wi-Fi、发送、登录和接收完成；未新增网页，不使用网页工具描述原生验证。
- 未执行真实 iPhone 传输验收，未重新打包或上传安装器。


## 开关响应、主题色悬停与主标志

- 接收开关标题统一为“隔空投送”。关闭不再同步等待引擎退出，界面先完成更新，后台按顺序处理停止及快速反向开启。模拟进程收到退出指令后等待 700ms，测试确认调用即时返回、等待期间 Dispatcher 可响应、快速反向开启保留最后偏好。
- 移除多彩色块入口；旧版已保存的多彩偏好不因编辑无关设置而被覆盖。四个纯色色块悬停只叠加低透明白色，避免不透明 CardHover 覆盖原色。
- 主窗口原 PNG 仍使用原始 1024px 资源，指定 HighQuality 缩小并关闭图像像素吸附，修复默认低质量抽样的锯齿。
- 深浅色 `UiDebug.Smoke --settings-only` 通过，新增关闭响应与快速反向开启测试通过。截图在 `build/toggle-swatch-logo-*`，构建及差异检查通过；未修改或打包安装器。
