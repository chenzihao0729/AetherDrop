// SPDX-License-Identifier: GPL-2.0-only
/*
 * AetherDrop-wlan: the AX201 as a Windows Wi-Fi adapter (WiFiCx) that also runs
 * AWDL for AirDrop. See docs/KERNEL.md.
 *
 * This file: driver and device creation. WiFiCx owns the NetAdapter and the
 * WDI message flow; the driver answers WDI commands (wdi.cpp) and drives the
 * hardware (hw.cpp, the kernel twin of iwxradio).
 */
#include "precomp.h"
#include "kplat_glue.h"

extern "C" DRIVER_INITIALIZE DriverEntry;
static EVT_WDF_DRIVER_DEVICE_ADD EvtDeviceAdd;
static EVT_WDF_DEVICE_PREPARE_HARDWARE EvtPrepareHardware;
static EVT_WDF_DEVICE_RELEASE_HARDWARE EvtReleaseHardware;
static EVT_WIFI_DEVICE_CREATE_ADAPTER EvtCreateAdapter;
static EVT_WIFI_DEVICE_CREATE_WIFIDIRECTDEVICE EvtCreateWifiDirectDevice;

extern "C" NTSTATUS DriverEntry(_In_ PDRIVER_OBJECT driver, _In_ PUNICODE_STRING registry) {
  NekoGlueSetDriverPath(driver);
  WDF_DRIVER_CONFIG config;
  WDF_DRIVER_CONFIG_INIT(&config, EvtDeviceAdd);
  config.DriverPoolTag = NEKO_TAG;
  NTSTATUS status = WdfDriverCreate(driver, registry, WDF_NO_OBJECT_ATTRIBUTES, &config, WDF_NO_HANDLE);
  NEKO_LOG("DriverEntry: 0x%08x", status);
  return status;
}

static NTSTATUS EvtDeviceAdd(_In_ WDFDRIVER, _Inout_ PWDFDEVICE_INIT init) {
  NTSTATUS status = NetDeviceInitConfig(init);
  if (!NT_SUCCESS(status)) return status;
  status = WifiDeviceInitConfig(init);
  if (!NT_SUCCESS(status)) return status;

  WDF_PNPPOWER_EVENT_CALLBACKS pnp;
  WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnp);
  pnp.EvtDevicePrepareHardware = EvtPrepareHardware;
  pnp.EvtDeviceReleaseHardware = EvtReleaseHardware;
  WdfDeviceInitSetPnpPowerEventCallbacks(init, &pnp);

  WDF_OBJECT_ATTRIBUTES attrs;
  WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attrs, NEKO_DEVICE);
  WDFDEVICE device;
  status = WdfDeviceCreate(&init, &attrs, &device);
  if (!NT_SUCCESS(status)) return status;
  NEKO_DEVICE *ctx = NekoGetDevice(device);
  ctx->Device = device;
  NekoInitQueues(ctx);
  /* Without an interrupt the worker simply polls. */
  const NTSTATUS irq = NekoIrqCreate(ctx);
  if (!NT_SUCCESS(irq)) NEKO_LOG("no interrupt: 0x%08x (polling)", irq);

  WIFI_DEVICE_CONFIG wifi;
  WIFI_DEVICE_CONFIG_INIT(&wifi, WDI_VERSION_LATEST, NekoWdiSendCommand, EvtCreateAdapter,
                          EvtCreateWifiDirectDevice);
  status = WifiDeviceInitialize(device, &wifi);
  NEKO_LOG("device added: 0x%08x", status);
  return status;
}

static NTSTATUS EvtPrepareHardware(_In_ WDFDEVICE device, _In_ WDFCMRESLIST, _In_ WDFCMRESLIST translated) {
  return NekoHwPrepare(NekoGetDevice(device), translated);
}

static NTSTATUS EvtReleaseHardware(_In_ WDFDEVICE device, _In_ WDFCMRESLIST) {
  NekoHwRelease(NekoGetDevice(device));
  return STATUS_SUCCESS;
}

static NTSTATUS EvtCreateAdapter(_In_ WDFDEVICE device, _Inout_ NETADAPTER_INIT *init) {
  return NekoAdapterCreate(NekoGetDevice(device), init);
}

static NTSTATUS EvtCreateWifiDirectDevice(_In_ WDFDEVICE, _Inout_ WIFIDIRECT_DEVICE_INIT *) {
  return STATUS_NOT_SUPPORTED; /* no Wi-Fi Direct; AirDrop uses AWDL */
}
