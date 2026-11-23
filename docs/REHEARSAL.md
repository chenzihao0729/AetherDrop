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
