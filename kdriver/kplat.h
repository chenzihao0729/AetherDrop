// SPDX-License-Identifier: GPL-2.0-only
/* Kernel side of plat.h: what the shared hardware sources see when built
 * into AetherDrop-wlan.sys (NEKO_KERNEL). */
#pragma once

#include <ntddk.h>
#include <wdf.h>
#include <errno.h>

/* The card the driver is bound to; set before IwxDevice::open(). */
struct NekoCard {
  volatile UCHAR *bar;
  ULONG bar_len;
  USHORT vendor, device, subsystem;
  WDFDMAENABLER dma;
};
extern NekoCard g_neko_card;

/* Where the driver keeps the iwlwifi firmware files. */
#define NEKO_FIRMWARE_DIR "\\SystemRoot\\System32\\drivers\\AetherDrop"
