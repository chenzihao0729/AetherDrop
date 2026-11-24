# AetherDrop kernel driver (WiFiCx)

Why: with the user-mode radio, Windows loses its Wi-Fi adapter the moment the
card leaves Intel's driver (no Wi-Fi switch, no network list; internet only
through a TAP "Ethernet"). Windows lists Wi-Fi only from a WLAN miniport. So
AetherDrop becomes one: a KMDF + NetAdapterCx + **WiFiCx** driver for the AX201
that serves Windows' Wi-Fi (scan, connect, WPA2 through WlanSvc, data path) and,
beside it, the AWDL interface that awdld and the AirDrop engine already use.
This is ForbiddenItlwm's model on macOS: one driver does both.

## What is reused

| Source | Becomes |
|---|---|
| `iwxradio/` (hardware-verified on this AX201: firmware boot, monitor/listener MAC, AWDL TX/RX, station MAC 1, keys, TLC, Block Ack, 0.6.0 policies) | the hardware layer, with `RegProxy` replaced by mapped BAR0 (MmMapIoSpaceEx) and `DmaPool` by WDF common buffers |
| itlwm `hal_iwx` / OpenBSD net80211 | what iwxradio lacks for Windows: UMAC scan (`iwx_umac_scan`), scan result parsing, interrupts (MSI-X causes, `iwx_intr_msix`) |
| `wifi_sta.cpp` | MLME for WDI connect (auth, assoc, deauth); the 4-way handshake moves to Windows (EAPOL goes up the data path, keys come down as WDI_SET_ADD_CIPHER_KEYS) |
| awdld + AirDrop engine + client | unchanged; the radio pipe protocol is carried by a device IOCTL instead of a named pipe |

## Layout

```
kdriver/
  driver.cpp      DriverEntry, EvtDeviceAdd (WifiDeviceInitConfig/Initialize), PnP/power
  hw.cpp          BAR0, interrupts, DMA (kernel twin of iwxradio's proxy + dma)
  wdi.cpp         EvtWifiDeviceSendCommand: WDI task/property dispatch (TlvGeneratorParser)
  adapter.cpp     NetAdapter, TX/RX rings <-> firmware queues
  awdl_ioctl.cpp  private interface for awdld (frames, channel, events)
  AetherDrop-wlan.inf
```

## WDI messages, in order of need

1. Open/init: `WDI_TASK_OPEN`, `WDI_GET_ADAPTER_CAPABILITIES` (interface, station, band, PHY), `WDI_TASK_CREATE_PORT`.
2. Scan: `WDI_TASK_SCAN` -> UMAC scan -> `WDI_INDICATION_BSS_ENTRY_LIST`, `WDI_INDICATION_SCAN_COMPLETE`.
3. Connect: `WDI_TASK_CONNECT` -> auth/assoc -> `WDI_INDICATION_ASSOCIATION_RESULT`, `WDI_INDICATION_CONNECT_COMPLETE`; EAPOL through the data path; `WDI_SET_ADD_CIPHER_KEYS`; `WDI_TASK_DISCONNECT`, `WDI_INDICATION_DISASSOCIATION`.
4. Data: NetAdapter TX/RX queues, 802.11 <-> Ethernet in the driver (WiFiCx data path is Ethernet frames).
5. Power, roaming, link quality: later.

## Concurrency with AWDL

Station on MAC 1 / its own PHY and binding (hardware-proven 2026-11-17), the
AWDL listener on MAC 0 following the peer channel (6 / 44 / 149) as upstream.

## Signing

Development builds are test-signed. Secure Boot is off on the target PC; the
user enables test signing once (`bcdedit /set testsigning on`, reboot). A
release needs attestation signing through the Microsoft partner portal.

## How AirDrop runs on it

`AetherDrop-radio --kernel` (started by AetherDropService when the card is bound
to AetherDrop-wlan.sys) opens `\\.\AetherDropAwdl` and runs the same AWDL engine
as the user-mode radio: upstream 0.6.0's channel following, country learning,
receive Block Ack, BA filter, rate ladder and unicast resend all stay in user
mode; the driver only executes listener primitives (transmit at a rate, TX
status, channel, address, promiscuous, Block Ack session, country, status).
awdld and the AirDrop engine talk to the radio pipe exactly as before.

## Testing on the PC

1. `bcdedit /set testsigning on` (administrator), reboot. Secure Boot is off on
   this PC, so test-signed drivers load.
2. `scripts\build-kdriver.ps1` (already built: `build\kdriver\package`).
3. `scripts\kdriver-install.ps1` (administrator): signs the package with a
   certificate made on the PC, adds it to the driver store, binds it to the
   AX201 (installed copy: `C:\Program Files\AetherDrop\kdriver\kdriver-install.ps1`).
   Log: `%ProgramData%\AetherDrop\kdriver-install.log`.
4. Windows Wi-Fi: the network list should fill; connect to the usual network.
   Kernel log: DebugView (Capture Kernel) shows lines starting `AetherDrop:`.
5. AirDrop: start AetherDropService (or `AetherDrop-radio 6 --kernel` + awdld).
6. Undo at any point: `scripts\kdriver-restore.ps1` gives the card back to
   Intel's driver and removes the package.

## Status (2026-11-17: built and statically checked, not yet loaded)

- [x] builds: KMDF 1.33 + NetAdapterCx 2.4 + WiFiCx 1.2, 230 KB; imports only
      ntoskrnl, HAL and WDFLDR; INF passes InfVerif; catalog generated
- [x] hardware layer shared with the user-mode radio (hardware-proven code)
- [x] capabilities, radio state, reset, BSS list
- [x] scan: firmware UMAC scan (v14, adaptive dwell, between the station's and
      the listener's slots); channel visiting as a fallback if the firmware
      refuses it
- [x] connect: auth, assoc (RSN/HT/VHT/WMM), association result, link state,
      deauth/disassoc, disconnect; CCMP keys from Windows (WPA2 personal and
      enterprise)
- [x] WPA3-Personal: SAE run by Windows (WDI_SET_SAE_AUTH_PARAMS), commit and
      confirm frames sent and reported by the driver, H2E status 126
- [x] 802.11w: MFPC/MFPR in the RSN element, MFP on the firmware station, IGTK
      (BIP-CMAC-128) through MGMT_MCAST_KEY, protected deauthentication
- [x] data path: Native 802.11 frames (DataFormat=1 switches to Ethernet)
- [x] MSI receive interrupts wake the worker (1 ms polling stays as a fallback
      and relaxes to 10 ms once interrupts arrive)
- [x] AWDL control device, `--kernel` engine backend, service integration
- [x] `--pcap` capture of everything the listener receives
- [ ] loaded on hardware (needs test signing on)
- [ ] other Intel generations: hal_iwm (7260/8260/9260/9560), hal_iwn. This is
      a second hardware layer (older firmware API, different TX/RX rings), not
      a configuration; it can only be checked on such a card.
