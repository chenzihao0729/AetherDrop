/* SPDX-License-Identifier: GPL-2.0-only */
/*
 * The AWDL interface of AetherDrop-wlan.sys, shared by the driver and the
 * user-mode AWDL engine (AetherDrop-radio --kernel). The driver owns the card
 * and serves Windows' Wi-Fi; the engine runs upstream 0.6.0's AWDL policies
 * on the listener (MAC 0) through these calls, as it did on the user-mode
 * hardware layer.
 *
 * Device: \\.\AetherDropAwdl (SYSTEM and Administrators only). All IOCTLs are
 * METHOD_BUFFERED. IOCTL_NEKO_READ pends until events are available and
 * returns as many as fit: each a NEKO_EVENT followed by `length` bytes,
 * padded to 4.
 */
#pragma once

#define NEKO_AWDL_DEVICE_NAME L"\\Device\\AetherDropAwdl"
#define NEKO_AWDL_DOS_NAME L"\\DosDevices\\AetherDropAwdl"
#define NEKO_AWDL_USER_PATH "\\\\.\\AetherDropAwdl"

#define NEKO_IOCTL(n) CTL_CODE(FILE_DEVICE_NETWORK, 0x900 + (n), METHOD_BUFFERED, FILE_ANY_ACCESS)
#define IOCTL_NEKO_ATTACH NEKO_IOCTL(0)        /* in NEKO_ATTACH, out NEKO_INFO */
#define IOCTL_NEKO_DETACH NEKO_IOCTL(1)
#define IOCTL_NEKO_TX NEKO_IOCTL(2)            /* in NEKO_TX + frame, out LONG slot (-1 failed) */
#define IOCTL_NEKO_READ NEKO_IOCTL(3)          /* out events */
#define IOCTL_NEKO_SET_CHANNEL NEKO_IOCTL(4)   /* in UCHAR[2]: channel, width */
#define IOCTL_NEKO_SET_ADDRESS NEKO_IOCTL(5)   /* in UCHAR[6] */
#define IOCTL_NEKO_SET_PROMISC NEKO_IOCTL(6)   /* in UCHAR */
#define IOCTL_NEKO_ADD_RX_BA NEKO_IOCTL(7)     /* in NEKO_BA, out UCHAR ok */
#define IOCTL_NEKO_REMOVE_RX_BA NEKO_IOCTL(8)  /* in UCHAR tid */
#define IOCTL_NEKO_SET_COUNTRY NEKO_IOCTL(9)   /* in CHAR[2], out UCHAR taken */
#define IOCTL_NEKO_QUERY NEKO_IOCTL(10)        /* out NEKO_STATUS */

#pragma pack(push, 1)
typedef struct NEKO_ATTACH {
  unsigned char channel, width;
  unsigned char address[6];
} NEKO_ATTACH;

typedef struct NEKO_INFO {
  unsigned char hw_address[6];
  unsigned char channel, width;
} NEKO_INFO;

typedef struct NEKO_TX {
  unsigned char kind; /* 0 legacy, 1 HT, 2 VHT, 3 firmware rate control */
  unsigned char mcs, nss, wide80;
  unsigned short mbps_x2;
  unsigned short length; /* frame bytes after this header */
} NEKO_TX;

typedef struct NEKO_BA {
  unsigned char peer[6];
  unsigned char tid, token;
  unsigned short ssn, window;
} NEKO_BA;

#define NEKO_EVENT_RX 1
#define NEKO_EVENT_TX_STATUS 2
typedef struct NEKO_EVENT {
  unsigned short type, length;
  signed char dbm;
  unsigned char channel;
  unsigned short status;   /* RX: decryption status; TX: 1 acknowledged */
  unsigned int rate;       /* RX: rate_n_flags */
  unsigned int slot;       /* TX: the slot IOCTL_NEKO_TX returned */
} NEKO_EVENT;

typedef struct NEKO_STATUS {
  unsigned char healthy, promiscuous, channel, width;
  unsigned char ba_tids;   /* receive BA sessions open, bit per TID */
  unsigned char reserved[3];
  unsigned int channel_flags[4]; /* IWX_NVM_CHANNEL_* for 6, 36, 44, 149 */
  unsigned long long submitted, completed, acked, failed, retries, dropped_full;
  unsigned long long events_dropped;
} NEKO_STATUS;
#pragma pack(pop)
