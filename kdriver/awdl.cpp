// SPDX-License-Identifier: GPL-2.0-only
/*
 * \\.\AetherDropAwdl: the AWDL engine's access to the listener (MAC 0) while
 * Windows uses the station. A control device (SYSTEM and Administrators
 * only) rather than the NetAdapter's own device, so its I/O never mixes with
 * NDIS. Requests run on the worker thread like everything else that touches
 * the card; received frames and transmit statuses wait in a bounded event
 * list until the engine's pended IOCTL_NEKO_READ takes them. Closing the
 * handle ends the session.
 */
#include "precomp.h"

#include <wdmsec.h>
#include "aetherdrop_ioctl.h"

struct NEKO_CTRL {
  NEKO_DEVICE *Dev;
};
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(NEKO_CTRL, NekoGetCtrl);

struct NEKO_EVT {
  LIST_ENTRY Link;
  NEKO_EVENT Event;
  UCHAR Data[1];
};

static const ULONG kMaxEvents = 4096;

static void Push(NEKO_DEVICE *dev, const NEKO_EVENT &e, const UCHAR *data) {
  auto *x = static_cast<NEKO_EVT *>(
      ExAllocatePool2(POOL_FLAG_NON_PAGED, FIELD_OFFSET(NEKO_EVT, Data) + e.length, NEKO_TAG));
  if (!x) {
    dev->AwdlDropped++;
    return;
  }
  x->Event = e;
  if (e.length) RtlCopyMemory(x->Data, data, e.length);
  if (dev->AwdlEventCount >= kMaxEvents) { /* the engine fell behind: drop the oldest */
    ExFreePoolWithTag(CONTAINING_RECORD(RemoveHeadList(&dev->AwdlEvents), NEKO_EVT, Link), NEKO_TAG);
    dev->AwdlEventCount--;
    dev->AwdlDropped++;
  }
  InsertTailList(&dev->AwdlEvents, &x->Link);
  dev->AwdlEventCount++;
}

void NekoAwdlOnFrame(NEKO_DEVICE *dev, const KhalRx *f) {
  if (!dev->AwdlAttached || f->len > 4000) return;
  NEKO_EVENT e = {};
  e.type = NEKO_EVENT_RX;
  e.length = static_cast<unsigned short>(f->len);
  e.dbm = f->dbm;
  e.channel = f->channel;
  e.status = f->status;
  Push(dev, e, f->data);
}

static void OnTxStatus(void *ctx, unsigned slot, bool acked) {
  auto *dev = static_cast<NEKO_DEVICE *>(ctx);
  if (!dev->AwdlAttached) return;
  NEKO_EVENT e = {};
  e.type = NEKO_EVENT_TX_STATUS;
  e.status = acked ? 1 : 0;
  e.slot = slot;
  Push(dev, e, nullptr);
}

static void Flush(NEKO_DEVICE *dev) {
  while (!IsListEmpty(&dev->AwdlEvents)) {
    ExFreePoolWithTag(CONTAINING_RECORD(RemoveHeadList(&dev->AwdlEvents), NEKO_EVT, Link), NEKO_TAG);
  }
  dev->AwdlEventCount = 0;
}

/* Worker: hands queued events to pended reads. */
void NekoAwdlDeliver(NEKO_DEVICE *dev) {
  if (!dev->AwdlReadQueue) return;
  while (!IsListEmpty(&dev->AwdlEvents)) {
    WDFREQUEST req;
    if (!NT_SUCCESS(WdfIoQueueRetrieveNextRequest(dev->AwdlReadQueue, &req))) return;
    PUCHAR out;
    size_t cap;
    if (!NT_SUCCESS(WdfRequestRetrieveOutputBuffer(req, sizeof(NEKO_EVENT), reinterpret_cast<PVOID *>(&out), &cap))) {
      WdfRequestComplete(req, STATUS_BUFFER_TOO_SMALL);
      continue;
    }
    size_t used = 0;
    while (!IsListEmpty(&dev->AwdlEvents)) {
      auto *x = CONTAINING_RECORD(dev->AwdlEvents.Flink, NEKO_EVT, Link);
      const size_t need = (sizeof(NEKO_EVENT) + x->Event.length + 3) & ~size_t(3);
      if (used + need > cap) break;
      RtlCopyMemory(out + used, &x->Event, sizeof(NEKO_EVENT));
      RtlCopyMemory(out + used + sizeof(NEKO_EVENT), x->Data, x->Event.length);
      used += need;
      RemoveHeadList(&dev->AwdlEvents);
      dev->AwdlEventCount--;
      ExFreePoolWithTag(x, NEKO_TAG);
    }
    WdfRequestCompleteWithInformation(req, used ? STATUS_SUCCESS : STATUS_BUFFER_TOO_SMALL, used);
  }
}

static void Detach(NEKO_DEVICE *dev) {
  if (!dev->AwdlAttached) return;
  dev->AwdlAttached = FALSE;
  khal_set_tx_status(nullptr, nullptr);
  for (unsigned t = 0; t < 8; t++)
    if (khal_rx_ba_tids() & (1u << t)) khal_remove_rx_ba(t);
  khal_set_promiscuous(true);
  Flush(dev);
  NEKO_LOG("AWDL session ended");
}

/* Worker: one control request. */
void NekoAwdlRun(NEKO_DEVICE *dev, NEKO_JOB *job) {
  WDFREQUEST req = job->IoRequest;
  PVOID in = nullptr, out = nullptr;
  size_t inLength = 0, outLength = 0;
  WdfRequestRetrieveInputBuffer(req, 0, &in, &inLength);
  WdfRequestRetrieveOutputBuffer(req, 0, &out, &outLength);
  NTSTATUS st = STATUS_SUCCESS;
  ULONG_PTR info = 0;
  const auto *b = static_cast<const UCHAR *>(in);
  switch (job->IoCode) {
  case IOCTL_NEKO_ATTACH: {
    if (inLength < sizeof(NEKO_ATTACH) || outLength < sizeof(NEKO_INFO)) { st = STATUS_INVALID_PARAMETER; break; }
    const auto *a = static_cast<const NEKO_ATTACH *>(in);
    if (!dev->HwOpen) { st = STATUS_DEVICE_NOT_READY; break; }
    NekoScanAbort(dev);
    Flush(dev);
    dev->HomeChannel = a->channel;
    khal_set_channel(a->channel, a->width ? a->width : (a->channel > 14 ? 80 : 20));
    khal_set_listener_address(a->address);
    khal_set_promiscuous(true);
    khal_set_tx_status(OnTxStatus, dev);
    dev->AwdlAttached = TRUE;
    auto *o = static_cast<NEKO_INFO *>(out);
    RtlCopyMemory(o->hw_address, dev->Address, 6);
    o->channel = khal_channel();
    o->width = static_cast<UCHAR>(khal_width());
    info = sizeof(NEKO_INFO);
    NEKO_LOG("AWDL session on channel %u", o->channel);
    break;
  }
  case IOCTL_NEKO_DETACH:
    Detach(dev);
    break;
  case IOCTL_NEKO_TX: {
    if (inLength < sizeof(NEKO_TX) || outLength < sizeof(LONG)) { st = STATUS_INVALID_PARAMETER; break; }
    const auto *t = static_cast<const NEKO_TX *>(in);
    if (sizeof(NEKO_TX) + t->length > inLength || t->length < 10) { st = STATUS_INVALID_PARAMETER; break; }
    *static_cast<LONG *>(out) = dev->AwdlAttached
        ? khal_listener_transmit_rate(b + sizeof(NEKO_TX), t->length, t->kind, t->mbps_x2, t->mcs, t->nss, t->wide80 != 0)
        : -1;
    info = sizeof(LONG);
    break;
  }
  case IOCTL_NEKO_SET_CHANNEL:
    if (inLength < 2) { st = STATUS_INVALID_PARAMETER; break; }
    dev->HomeChannel = b[0];
    if (!dev->Scanning && !khal_set_channel(b[0], b[1] ? b[1] : (b[0] > 14 ? 80 : 20))) st = STATUS_UNSUCCESSFUL;
    break;
  case IOCTL_NEKO_SET_ADDRESS:
    if (inLength < 6 || !khal_set_listener_address(b)) st = STATUS_INVALID_PARAMETER;
    break;
  case IOCTL_NEKO_SET_PROMISC:
    if (inLength < 1 || !khal_set_promiscuous(b[0] != 0)) st = STATUS_INVALID_PARAMETER;
    break;
  case IOCTL_NEKO_ADD_RX_BA: {
    if (inLength < sizeof(NEKO_BA) || outLength < 1) { st = STATUS_INVALID_PARAMETER; break; }
    const auto *a = static_cast<const NEKO_BA *>(in);
    *static_cast<UCHAR *>(out) = khal_add_rx_ba(a->peer, a->tid, a->ssn, a->window, a->token) ? 1 : 0;
    info = 1;
    break;
  }
  case IOCTL_NEKO_REMOVE_RX_BA:
    if (inLength < 1) { st = STATUS_INVALID_PARAMETER; break; }
    khal_remove_rx_ba(b[0]);
    break;
  case IOCTL_NEKO_SET_COUNTRY: {
    if (inLength < 2 || outLength < 1) { st = STATUS_INVALID_PARAMETER; break; }
    const char a2[3] = {static_cast<char>(b[0]), static_cast<char>(b[1]), 0};
    *static_cast<UCHAR *>(out) = khal_set_country(a2) ? 1 : 0;
    info = 1;
    break;
  }
  case IOCTL_NEKO_QUERY: {
    if (outLength < sizeof(NEKO_STATUS)) { st = STATUS_BUFFER_TOO_SMALL; break; }
    auto *s = static_cast<NEKO_STATUS *>(out);
    RtlZeroMemory(s, sizeof(*s));
    s->healthy = khal_healthy();
    s->promiscuous = khal_promiscuous();
    s->channel = khal_channel();
    s->width = static_cast<UCHAR>(khal_width());
    s->ba_tids = khal_rx_ba_tids();
    const UCHAR chans[4] = {6, 36, 44, 149};
    for (int i = 0; i < 4; i++) s->channel_flags[i] = khal_channel_flags(chans[i]);
    KhalTxStats t;
    khal_tx_stats(&t);
    s->submitted = t.submitted;
    s->completed = t.completed;
    s->acked = t.acked;
    s->failed = t.failed;
    s->retries = t.retries;
    s->dropped_full = t.dropped_full;
    s->events_dropped = dev->AwdlDropped;
    info = sizeof(NEKO_STATUS);
    break;
  }
  default:
    st = STATUS_INVALID_DEVICE_REQUEST;
  }
  WdfRequestCompleteWithInformation(req, st, info);
}

/* ------------------------------------------------------- control device */

static void EvtIoControl(WDFQUEUE queue, WDFREQUEST req, size_t, size_t, ULONG code) {
  NEKO_DEVICE *dev = NekoGetCtrl(WdfIoQueueGetDevice(queue))->Dev;
  if (code == IOCTL_NEKO_READ) {
    if (!NT_SUCCESS(WdfRequestForwardToIoQueue(req, dev->AwdlReadQueue))) WdfRequestComplete(req, STATUS_UNSUCCESSFUL);
    else NekoKick(dev);
    return;
  }
  auto *job = static_cast<NEKO_JOB *>(ExAllocatePool2(POOL_FLAG_NON_PAGED, sizeof(NEKO_JOB), NEKO_TAG));
  if (!job) {
    WdfRequestComplete(req, STATUS_INSUFFICIENT_RESOURCES);
    return;
  }
  job->IoRequest = req;
  job->IoCode = code;
  KIRQL irql;
  KeAcquireSpinLock(&dev->Lock, &irql);
  InsertTailList(&dev->Jobs, &job->Link);
  KeReleaseSpinLock(&dev->Lock, irql);
  NekoKick(dev);
}

static void EvtFileCleanup(WDFFILEOBJECT file) {
  NEKO_DEVICE *dev = NekoGetCtrl(WdfFileObjectGetDevice(file))->Dev;
  /* The engine went away: end the session from the worker. */
  InterlockedExchange(&dev->AwdlCloseRequested, 1);
  NekoKick(dev);
}

void NekoAwdlTick(NEKO_DEVICE *dev) {
  if (InterlockedExchange(&dev->AwdlCloseRequested, 0)) Detach(dev);
  NekoAwdlDeliver(dev);
}

NTSTATUS NekoAwdlCreate(NEKO_DEVICE *dev) {
  PWDFDEVICE_INIT init = WdfControlDeviceInitAllocate(WdfDeviceGetDriver(dev->Device), &SDDL_DEVOBJ_SYS_ALL_ADM_ALL);
  if (!init) return STATUS_INSUFFICIENT_RESOURCES;
  DECLARE_CONST_UNICODE_STRING(name, NEKO_AWDL_DEVICE_NAME);
  NTSTATUS st = WdfDeviceInitAssignName(init, &name);
  if (NT_SUCCESS(st)) {
    WDF_FILEOBJECT_CONFIG fileConfig;
    WDF_FILEOBJECT_CONFIG_INIT(&fileConfig, WDF_NO_EVENT_CALLBACK, WDF_NO_EVENT_CALLBACK, EvtFileCleanup);
    WdfDeviceInitSetFileObjectConfig(init, &fileConfig, WDF_NO_OBJECT_ATTRIBUTES);
    WDF_OBJECT_ATTRIBUTES attrs;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&attrs, NEKO_CTRL);
    WDFDEVICE ctrl;
    st = WdfDeviceCreate(&init, &attrs, &ctrl);
    if (NT_SUCCESS(st)) {
      NekoGetCtrl(ctrl)->Dev = dev;
      DECLARE_CONST_UNICODE_STRING(link, NEKO_AWDL_DOS_NAME);
      st = WdfDeviceCreateSymbolicLink(ctrl, &link);
      WDF_IO_QUEUE_CONFIG q;
      if (NT_SUCCESS(st)) {
        WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&q, WdfIoQueueDispatchParallel);
        q.EvtIoDeviceControl = EvtIoControl;
        st = WdfIoQueueCreate(ctrl, &q, WDF_NO_OBJECT_ATTRIBUTES, WDF_NO_HANDLE);
      }
      if (NT_SUCCESS(st)) {
        WDF_IO_QUEUE_CONFIG_INIT(&q, WdfIoQueueDispatchManual);
        st = WdfIoQueueCreate(ctrl, &q, WDF_NO_OBJECT_ATTRIBUTES, &dev->AwdlReadQueue);
      }
      if (NT_SUCCESS(st)) {
        WdfControlFinishInitializing(ctrl);
        dev->AwdlControl = ctrl;
        return STATUS_SUCCESS;
      }
      WdfObjectDelete(ctrl);
      dev->AwdlReadQueue = nullptr;
      return st;
    }
  }
  if (init) WdfDeviceInitFree(init);
  return st;
}

void NekoAwdlDestroy(NEKO_DEVICE *dev) {
  Detach(dev);
  if (dev->AwdlControl) {
    WdfObjectDelete(dev->AwdlControl);
    dev->AwdlControl = nullptr;
    dev->AwdlReadQueue = nullptr;
  }
  Flush(dev);
}
