// SPDX-License-Identifier: GPL-2.0-only
/*
 * What Windows is told the adapter can do. WiFiCx takes these during
 * PrepareHardware (it no longer asks with WDI_GET_ADAPTER_CAPABILITIES).
 *
 * AX201: 2x2, 2.4 and 5 GHz, 802.11ax silicon; the station side of this
 * driver negotiates up to VHT (802.11ac), so that is what is reported.
 * Security: open, WPA2 (personal and enterprise) and WPA3-Personal (SAE, run
 * by Windows through WDI_SET_SAE_AUTH_PARAMS) with 802.11w; Windows runs the
 * 4-way handshake and 802.1X over the data path and hands down the keys.
 */
#include "precomp.h"

static const WDI_CHANNEL_MAPPING_ENTRY kChannels24[] = {
    {1, 2412}, {2, 2417}, {3, 2422}, {4, 2427}, {5, 2432}, {6, 2437}, {7, 2442},
    {8, 2447}, {9, 2452}, {10, 2457}, {11, 2462}, {12, 2467}, {13, 2472},
};
static const WDI_CHANNEL_MAPPING_ENTRY kChannels5[] = {
    {36, 5180},  {40, 5200},  {44, 5220},  {48, 5240},  {52, 5260},  {56, 5280},  {60, 5300},
    {64, 5320},  {100, 5500}, {104, 5520}, {108, 5540}, {112, 5560}, {116, 5580}, {120, 5600},
    {124, 5620}, {128, 5640}, {132, 5660}, {136, 5680}, {140, 5700}, {144, 5720}, {149, 5745},
    {153, 5765}, {157, 5785}, {161, 5805}, {165, 5825},
};

NTSTATUS NekoSetCapabilities(NEKO_DEVICE *dev) {
  WIFI_DEVICE_CAPABILITIES device;
  WIFI_DEVICE_CAPABILITIES_INIT(&device);
  device.HardwareRadioState = TRUE;
  device.SoftwareRadioState = TRUE;
  RtlCopyMemory(device.FirmwareVersion, "iwlwifi-QuZ 68 (AetherDrop)", sizeof("iwlwifi-QuZ 68 (AetherDrop)"));
  device.ActionFramesSupported = FALSE;
  device.NumRxStreams = 2;
  device.NumTxStreams = 2;
  device.BluetoothCoexistenceSupport = WDI_BLUETOOTH_COEXISTENCE_PERFORMANCE_MAINTAINED;
  device.SAEAuthenticationSupported = TRUE;
  device.NumRadios = 1;
  NTSTATUS st = WifiDeviceSetDeviceCapabilities(dev->Device, &device);
  if (!NT_SUCCESS(st)) return st;

  static const DOT11_AUTH_CIPHER_PAIR unicast[] = {
      {DOT11_AUTH_ALGO_80211_OPEN, DOT11_CIPHER_ALGO_NONE},
      {DOT11_AUTH_ALGO_RSNA_PSK, DOT11_CIPHER_ALGO_CCMP},
      {DOT11_AUTH_ALGO_RSNA, DOT11_CIPHER_ALGO_CCMP},
      {DOT11_AUTH_ALGO_WPA3_SAE, DOT11_CIPHER_ALGO_CCMP},
  };
  static const DOT11_AUTH_CIPHER_PAIR multicastMgmt[] = {
      {DOT11_AUTH_ALGO_80211_OPEN, DOT11_CIPHER_ALGO_NONE},
      {DOT11_AUTH_ALGO_RSNA_PSK, DOT11_CIPHER_ALGO_BIP},
      {DOT11_AUTH_ALGO_RSNA, DOT11_CIPHER_ALGO_BIP},
      {DOT11_AUTH_ALGO_WPA3_SAE, DOT11_CIPHER_ALGO_BIP},
  };
  static RSNA_AKM_SUITE akms[] = {rsna_akm_psk, rsna_akm_1x, rsna_akm_sae_pmk256};

  WIFI_STATION_CAPABILITIES sta;
  WIFI_STATION_CAPABILITIES_INIT(&sta);
  sta.ScanSSIDListSize = 4;
  sta.DesiredSSIDListSize = 1;
  sta.PrivacyExemptionListSize = 1;
  sta.KeyMappingTableSize = 4;
  sta.DefaultKeyTableSize = 4;
  sta.WEPKeyValueMaxLength = 0;
  sta.MaxNumPerSTA = 4;
  sta.SupportedQOSFlags = 0;
  sta.MFPCapable = TRUE;
  sta.NumSupportedUnicastAlgorithms = ARRAYSIZE(unicast);
  sta.UnicastAlgorithmsList = const_cast<PDOT11_AUTH_CIPHER_PAIR>(unicast);
  sta.NumSupportedMulticastDataAlgorithms = ARRAYSIZE(unicast);
  sta.MulticastDataAlgorithmsList = const_cast<PDOT11_AUTH_CIPHER_PAIR>(unicast);
  sta.NumSupportedMulticastMgmtAlgorithms = ARRAYSIZE(multicastMgmt);
  sta.MulticastMgmtAlgorithmsList = const_cast<PDOT11_AUTH_CIPHER_PAIR>(multicastMgmt);
  sta.NumAkmsSupported = ARRAYSIZE(akms);
  sta.AkmsList = akms;
  st = WifiDeviceSetStationCapabilities(dev->Device, &sta);
  if (!NT_SUCCESS(st)) return st;

  static WDI_PHY_TYPE phy24[] = {WDI_PHY_TYPE_HRDSSS, WDI_PHY_TYPE_ERP, WDI_PHY_TYPE_HT};
  static WDI_PHY_TYPE phy5[] = {WDI_PHY_TYPE_OFDM, WDI_PHY_TYPE_HT, WDI_PHY_TYPE_VHT};
  static UINT32 widths24[] = {20, 40};
  static UINT32 widths5[] = {20, 40, 80};
  WIFI_BAND_INFO bands[2] = {};
  bands[0].BandID = WDI_BAND_ID_2400;
  bands[0].BandState = TRUE;
  bands[0].NumValidPhyTypes = ARRAYSIZE(phy24);
  bands[0].ValidPhyTypeList = phy24;
  bands[0].NumValidChannelTypes = ARRAYSIZE(kChannels24);
  bands[0].ValidChannelTypes = const_cast<WDI_CHANNEL_MAPPING_ENTRY *>(kChannels24);
  bands[0].NumChannelWidths = ARRAYSIZE(widths24);
  bands[0].ChannelWidthList = widths24;
  bands[1].BandID = WDI_BAND_ID_5000;
  bands[1].BandState = TRUE;
  bands[1].NumValidPhyTypes = ARRAYSIZE(phy5);
  bands[1].ValidPhyTypeList = phy5;
  bands[1].NumValidChannelTypes = ARRAYSIZE(kChannels5);
  bands[1].ValidChannelTypes = const_cast<WDI_CHANNEL_MAPPING_ENTRY *>(kChannels5);
  bands[1].NumChannelWidths = ARRAYSIZE(widths5);
  bands[1].ChannelWidthList = widths5;
  WIFI_BAND_CAPABILITIES bandCaps;
  WIFI_BAND_CAPABILITIES_INIT(&bandCaps);
  bandCaps.NumBands = ARRAYSIZE(bands);
  bandCaps.BandInfoList = bands;
  st = WifiDeviceSetBandCapabilities(dev->Device, &bandCaps);
  if (!NT_SUCCESS(st)) return st;

  /* Rates in 500 kb/s units. */
  WIFI_PHY_INFO phys[5] = {};
  const UINT8 both = WDI_DATA_RATE_RX_RATE | WDI_DATA_RATE_TX_RATE;
  const UINT16 cck[] = {2, 4, 11, 22};
  const UINT16 ofdm[] = {12, 18, 24, 36, 48, 72, 96, 108};
  phys[0].PhyType = WDI_PHY_TYPE_HRDSSS;
  for (UINT16 r : cck) phys[0].DataRateList[phys[0].NumberDataRateEntries++] = {both, r};
  phys[1].PhyType = WDI_PHY_TYPE_ERP;
  for (UINT16 r : cck) phys[1].DataRateList[phys[1].NumberDataRateEntries++] = {both, r};
  for (UINT16 r : ofdm) phys[1].DataRateList[phys[1].NumberDataRateEntries++] = {both, r};
  phys[2].PhyType = WDI_PHY_TYPE_OFDM;
  for (UINT16 r : ofdm) phys[2].DataRateList[phys[2].NumberDataRateEntries++] = {both, r};
  phys[3].PhyType = WDI_PHY_TYPE_HT; /* up to MCS 15, 40 MHz, short GI: 300 Mb/s */
  phys[3].DataRateList[phys[3].NumberDataRateEntries++] = {both, 13};
  phys[3].DataRateList[phys[3].NumberDataRateEntries++] = {both, 600};
  phys[4].PhyType = WDI_PHY_TYPE_VHT; /* 2SS MCS 9, 80 MHz, short GI: 866.7 Mb/s */
  phys[4].DataRateList[phys[4].NumberDataRateEntries++] = {both, 13};
  phys[4].DataRateList[phys[4].NumberDataRateEntries++] = {both, 1733};
  WIFI_PHY_CAPABILITIES phyCaps;
  WIFI_PHY_CAPABILITIES_INIT(&phyCaps);
  phyCaps.NumPhyTypes = ARRAYSIZE(phys);
  phyCaps.PhyInfoList = phys;
  return WifiDeviceSetPhyCapabilities(dev->Device, &phyCaps);
}
