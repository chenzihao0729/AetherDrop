# Floating UI

Receive cards share one fixed 78-DIP content template for invitation, progress,
failure and completion. The title anchors to its top and the current footer
(buttons, speed, interruption or saved location) anchors to its bottom, giving
equal top and bottom layout insets from the card border. The device name is
centered in a fixed middle slot, independent of the measured footer height.
The three slots are 22, 28 and 28 DIPs tall. State changes
only replace content and visibility; they never rebuild or resize the template.
The speed uses 22-DIP YaHei Bold in the shared 28-DIP footer slot, without
scaling or baseline transforms. Completion reuses the 13-DIP device-name slot
for "Saved locally" and the footer slot for a blue "Open folder" button styled
like Accept. Paths remain internal and are never shown in the card or tooltips;
the button opens the actual stored-event folder. In UI debug it opens Documents.
Large rates use whole MB/s or GB/s so the text fits without losing the unit.
FooterButton defines a 72-DIP width unit: Accept and Decline use one unit each;
Open folder uses two units (144 DIPs). The 6-DIP gap between separate buttons
is independent of that width unit. All share the same height, style and motion.

Floating-card buttons share native hover/press motion: a 160-ms tint transition,
a 70-ms press to 97% size on the background only, then a 160-ms return. The text
never scales, and a new input retargets from the current visual state. Receive
cards start off screen before Show, arrive with a 460-ms quintic ease-out, and
exit with a 280-ms cubic ease-in. Their native window moves without scaling.
SystemParameters.ClientAreaAnimation disables these timelines when Windows
animations are off. See `REHEARSAL.md` for native motion checks and captures.

The tray opens from its bottom anchor at 78% size with a 320-ms quintic ease-out
and a short opacity arrival; it restores an identity transform at rest. Closing
shrinks back to that same anchor with a 180-ms ease-in and fade. A close during
entry continues from the current size/opacity, and both timelines are removed
on Closed. Repeated tray clicks and internal interactions retain the open card.

ReceiveToggle retains CheckBox keyboard/accessibility semantics and the original
39-by-20-DIP track. Its 16-DIP thumb moves between 0 and 19 DIPs over 200 ms;
the track fades between neutral gray and blue over 180 ms. Both ease out and
retarget from their current values on rapid input. Initial state and reduced
motion are applied immediately; the receiving action is never delayed.

Transfer completion retains the same ReceiveWindow, card, fixed slots, title
and secondary TextBlocks. The title, secondary label and shared footer replace
their content in place, staggered by 50 ms: 100-ms fade/lift out, then 180-ms
fade/settle in over four DIPs, without text scaling. The ring and device glyph
fade away as the pre-mounted checkmark draws in. The native window never flies
in again. Duplicate completion events are ignored, and the four-second hold
starts after the replacement has settled. Offscreen snapshots and reduced
motion use the final state immediately.

Interruption uses the same Finish/ReplaceSlot path, title/footer timing and
ring fade as successful completion. The existing device label and device glyph
remain stationary, while the speed becomes the interruption message. It keeps
the same native window and screen anchor, with a five-second hold after the
transition; completion-only controls and checkmark are never shown.



The tray card is 244 × 208 DIPs; receive/progress cards are 302 × 142 DIPs,

about 15% larger than the previous compact version. Font sizes, icons, spacing

and geometry are laid out individually at their final dimensions. There is no

Viewbox, permanent scaling transform or effect on a text ancestor. The shadow

is a separate background sibling; layout rounding and display text hinting are

enabled. The temporary tray entry transform is removed after the animation.



The left tray click opens a light AetherDrop card anchored above the clicked

tray area. Its receive switch stays in the card. Repeated tray clicks,

deactivation and interactions with the card do not dismiss it. Only the close

button and an empty desktop click dismiss it; desktop icons are excluded.

The original right-click tray menu remains available for settings and tools.



Incoming invitations use a custom card rather than the Windows request toast.

The card enters from the right, inside the monitor work area and above the

taskbar. Accepting flies the progress card in from the same edge to the same

final coordinates. Declining or closing an unanswered invitation rejects it and

animates it out. Closing a progress card hides it without cancelling the file

transfer. Completed cards leave after four seconds; failed cards after five.



Progress displays a circular percentage and smoothed decimal MB/s or KB/s based

on incoming byte counts and elapsed time. Multiple invitations are queued to

avoid covering one another. Expired requests and engine restarts clean up old

cards. Motion respects Windows' client-area animation preference.



For render-only previews without starting the engine or registering the app:



```powershell

build/stage/AetherDrop.exe --snapshot build/ui-previews --lang zh

```



The output includes `tray.png`, `receive.png`, `progress.png` and `complete.png` in addition to

the existing windows. Preview transfer data is synthetic. Actual incoming

requests and progress are wired to the AirDrop engine events.



Offline interaction smoke checks (briefly shows synthetic topmost cards, without

starting the radio, registering the app, or saving settings):



```powershell

. ./scripts/dev-env.ps1

dotnet run --project tests/FloatingUi.Smoke/FloatingUi.Smoke.csproj -c Release

```



These checks exercise event-driven accept/decline, progress, queuing, expiry,

completion and hidden-progress behavior, and verify taskbar clearance on the

current monitor. Other monitor/DPI combinations and real-device transfers

should also be checked on the target machine.


Clicking Open folder opens the saved directory and immediately closes the card, cancelling its auto-dismiss timer.
