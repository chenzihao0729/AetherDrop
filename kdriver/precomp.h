// SPDX-License-Identifier: GPL-2.0-only
#pragma once

#include <ntddk.h>
#include <wdf.h>
#include <netadaptercx.h>
#include <wificx.h>
#include <TlvGeneratorParser.hpp>

#include "khal.h"

/* Placement new for the TLV containers (no C++ library in the glue). */
inline void *operator new(size_t, void *p) noexcept { return p; }
inline void operator delete(void *, void *) noexcept {}

#define NEKO_TAG 'okeN'
#define NEKO_LOG(fmt, ...) \
  DbgPrintEx(DPFLTR_IHVNETWORK_ID, DPFLTR_ERROR_LEVEL, "AetherDrop: " fmt "\n", __VA_ARGS__)

/* Raw frames: 802.11 header first, no FCS. */
struct NEKO_FRAME {
  LIST_ENTRY Link;
  ULONG Length;
  BOOLEAN Encrypt;
  UCHAR Data[1];
};
NEKO_FRAME *NekoFrameAlloc(ULONG length);
void NekoFrameFree(NEKO_FRAME *f);

/* A WDI request waiting for the worker. */
struct NEKO_JOB {
  LIST_ENTRY Link;
  WIFIREQUEST Request;
  UINT16 MessageId;
  PVOID Buffer;
  UINT InLength, OutLength;
  WDFREQUEST IoRequest;    /* a AetherDropAwdl control request instead of WDI */
  ULONG IoCode;
};

/* Scan results kept between scans (the BSS list Windows shows). */
#define NEKO_MAX_BSS 64
struct NEKO_BSS {
  BOOLEAN Valid;
  UCHAR Bssid[6];
  UCHAR Channel;
  CHAR Rssi;
  BOOLEAN Probe;           /* body is a probe response, not a beacon */
  ULONGLONG SeenUs;
  USHORT BodyLength;       /* frame body: timestamp, interval, capability, IEs */
  UCHAR Body[1024];
};

enum NEKO_CONN_STATE { ConnIdle, ConnSae, ConnAuth, ConnAssoc, ConnUp };

struct NEKO_DEVICE {
  WDFDEVICE Device;
  NETADAPTER Adapter;
  WDFDMAENABLER Dma;
  volatile UCHAR *Bar;
  SIZE_T BarLength;
  TLV_CONTEXT Tlv;
  UCHAR Address[6];        /* the station's MAC: the card's permanent address */
  ULONG DataFormat;        /* 0: 802.11 frames on the data path, 1: Ethernet */

  /* Worker thread: every hardware access happens on it. */
  PKTHREAD Thread;
  KEVENT Wake;
  volatile LONG Stop;
  KSPIN_LOCK Lock;
  LIST_ENTRY Jobs;         /* NEKO_JOB */
  LIST_ENTRY TxFrames;     /* NEKO_FRAME from the OS */
  LIST_ENTRY RxFrames;     /* NEKO_FRAME for the OS */
  ULONG RxCount;
  BOOLEAN HwOpen;
  BOOLEAN RadioOn;
  UCHAR HomeChannel;       /* the listener's channel between scans */

  /* Scan task */
  BOOLEAN Scanning;
  BOOLEAN ScanFirmware;     /* the firmware runs this scan (UMAC) */
  BOOLEAN NoFirmwareScan;   /* the firmware refused once: visit channels */
  WDI_MESSAGE_HEADER ScanHeader;
  UCHAR ScanChannels[64];
  ULONG ScanCount, ScanIndex;
  ULONGLONG ScanDwellEndUs;
  UCHAR ScanSsid[33];
  ULONG ScanSsidLength;
  NEKO_BSS Bss[NEKO_MAX_BSS];

  /* Connect task */
  NEKO_CONN_STATE Conn;
  WDI_MESSAGE_HEADER ConnHeader;
  BOOLEAN ConnPending;     /* M4 not sent yet */
  UCHAR ConnBssid[6];
  UCHAR ConnChannel;
  UCHAR ConnSsid[33];
  ULONG ConnSsidLength;
  BOOLEAN ConnRsn, ConnHt, ConnVht, ConnWmm;
  USHORT ConnBeaconInt, ConnCapability;
  UCHAR ConnBeacon[1024];  /* the BSS's beacon/probe body */
  USHORT ConnBeaconLength;
  UCHAR AssocReq[512];     /* body we sent, for the association result */
  USHORT AssocReqLength;
  UCHAR AssocResp[512];
  USHORT AssocRespLength;
  ULONG ConnTries;
  ULONGLONG ConnDeadlineUs;
  USHORT MgmtSeq;
  BOOLEAN PairwiseKey;
  BOOLEAN ConnSaeAuth;      /* WPA3-Personal: SAE instead of open auth */
  BOOLEAN ConnMfp;          /* 802.11w negotiated */
  BOOLEAN ConnMfpRequired;  /* the AP requires it (or SAE does) */
  ULONGLONG LinkReportUs;
  CHAR LastRssi;

  /* Receive interrupts (irq.cpp) */
  WDFINTERRUPT Interrupt;
  BOOLEAN IrqEnabled;
  volatile LONG IrqCount;

  /* AWDL engine session (awdl.cpp) */
  WDFDEVICE AwdlControl;
  WDFQUEUE AwdlReadQueue;
  BOOLEAN AwdlAttached;
  volatile LONG AwdlCloseRequested;
  LIST_ENTRY AwdlEvents;
  ULONG AwdlEventCount;
  ULONGLONG AwdlDropped;

  /* Data path */
  NETPACKETQUEUE TxQueue, RxQueue;
  NET_EXTENSION TxVa, RxVa;
};
WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(NEKO_DEVICE, NekoGetDevice);

/* device.cpp */
void NekoInitQueues(NEKO_DEVICE *dev);
NTSTATUS NekoHwPrepare(NEKO_DEVICE *dev, WDFCMRESLIST translated);
void NekoHwRelease(NEKO_DEVICE *dev);
NTSTATUS NekoSetCapabilities(NEKO_DEVICE *dev);
void NekoKick(NEKO_DEVICE *dev);
void NekoQueueTx(NEKO_DEVICE *dev, NEKO_FRAME *f);
void NekoQueueRx(NEKO_DEVICE *dev, NEKO_FRAME *f);

/* wdi.cpp */
EVT_WIFI_DEVICE_SEND_COMMAND NekoWdiSendCommand;
void NekoWdiRun(NEKO_DEVICE *dev, NEKO_JOB *job);
void NekoIndicate(NEKO_DEVICE *dev, const WDI_MESSAGE_HEADER *orig, UINT16 id, UINT32 transaction,
                  NTSTATUS status, const UCHAR *tlv, ULONG tlvLength);

/* scan.cpp */
void NekoScanStart(NEKO_DEVICE *dev, const WDI_MESSAGE_HEADER *hdr, const WDI_SCAN_PARAMETERS *p);
void NekoScanTick(NEKO_DEVICE *dev);
void NekoScanAbort(NEKO_DEVICE *dev);
void NekoScanOnFrame(NEKO_DEVICE *dev, const KhalRx *f);
NEKO_BSS *NekoFindBss(NEKO_DEVICE *dev, const UCHAR bssid[6]);
const UCHAR *NekoFindIe(const UCHAR *ies, ULONG len, UCHAR id, UCHAR *outLen);

/* connect.cpp */
NTSTATUS NekoConnectStart(NEKO_DEVICE *dev, const WDI_MESSAGE_HEADER *hdr, const WDI_TASK_CONNECT_PARAMETERS *p);
void NekoConnectTick(NEKO_DEVICE *dev);
void NekoConnectOnFrame(NEKO_DEVICE *dev, const KhalRx *f);
void NekoDisconnect(NEKO_DEVICE *dev, const WDI_MESSAGE_HEADER *hdr, BOOLEAN byHost, USHORT reason);
NTSTATUS NekoAddKeys(NEKO_DEVICE *dev, const WDI_SET_ADD_CIPHER_KEYS_PARAMETERS *p);
NTSTATUS NekoSaeParams(NEKO_DEVICE *dev, const WDI_MESSAGE_HEADER *hdr, const WDI_SET_SAE_AUTH_PARAMS_COMMAND *p);

/* datapath.cpp */
NTSTATUS NekoAdapterCreate(NEKO_DEVICE *dev, NETADAPTER_INIT *init);
void NekoDataOnFrame(NEKO_DEVICE *dev, const KhalRx *f);
void NekoDataTransmit(NEKO_DEVICE *dev, NEKO_FRAME *f);
void NekoRxNotify(NEKO_DEVICE *dev);

/* awdl.cpp */
NTSTATUS NekoAwdlCreate(NEKO_DEVICE *dev);
void NekoAwdlDestroy(NEKO_DEVICE *dev);
void NekoAwdlRun(NEKO_DEVICE *dev, NEKO_JOB *job);
void NekoAwdlTick(NEKO_DEVICE *dev);
void NekoAwdlOnFrame(NEKO_DEVICE *dev, const KhalRx *f);

/* irq.cpp */
NTSTATUS NekoIrqCreate(NEKO_DEVICE *dev);
void NekoIrqEnable(NEKO_DEVICE *dev);
void NekoIrqDisable(NEKO_DEVICE *dev);
