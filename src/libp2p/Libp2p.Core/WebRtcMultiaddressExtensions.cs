// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using Multiformats.Address;

namespace Nethermind.Libp2p.Core;

/// <summary>
/// Extension methods for WebRTC multiaddr validation and transport address matching.
/// Supports both /webrtc (0x0119) and /webrtc-direct (0x0118) protocol components,
/// and explicitly rejects deprecated p2p-webrtc-star (0x0113) / p2p-webrtc-direct (0x0114).
/// </summary>
public static class WebRtcMultiaddressExtensions
{
    // Protocol string components as they appear in multiaddr string representation
    private const string WebRtcProtocol = "/webrtc";
    private const string WebRtcDirectProtocol = "/webrtc-direct";
    private const string UdpProtocol = "/udp/";
    private const string TcpProtocol = "/tcp/";
    private const string CerthashProtocol = "/certhash/";

    // Deprecated protocol strings — must be rejected
    private const string DeprecatedP2pWebrtcStar = "/p2p-webrtc-star";
    private const string DeprecatedP2pWebrtcDirect = "/p2p-webrtc-direct";

    /// <summary>
    /// Determines whether the given multiaddress represents a WebRTC or WebRTC-Direct transport address.
    /// This is the predicate intended for use by a future <c>ITransportProtocol.IsAddressMatch</c> implementation.
    /// Rejects deprecated p2p-webrtc-star and p2p-webrtc-direct variants.
    /// </summary>
    public static bool IsWebRtcAddress(this Multiaddress addr)
    {
        string addrStr = addr.ToString();

        if (ContainsDeprecatedWebRtcProtocol(addrStr))
        {
            return false;
        }

        return ContainsWebRtc(addrStr) || ContainsWebRtcDirect(addrStr);
    }

    /// <summary>
    /// Validates that a WebRTC multiaddress has the correct structural ordering:
    /// the address must contain a /udp/ segment (not /tcp/) before the /webrtc or /webrtc-direct component.
    /// Returns false for non-WebRTC addresses or addresses with invalid transport base.
    /// </summary>
    public static bool IsValidWebRtcAddress(this Multiaddress addr)
    {
        string addrStr = addr.ToString();

        if (ContainsDeprecatedWebRtcProtocol(addrStr))
        {
            return false;
        }

        if (!ContainsWebRtc(addrStr) && !ContainsWebRtcDirect(addrStr))
        {
            return false;
        }

        // WebRTC requires UDP as the underlying transport, not TCP
        if (!addrStr.Contains(UdpProtocol, StringComparison.Ordinal))
        {
            return false;
        }

        // Reject if TCP appears before the webrtc component (invalid transport base)
        int webrtcIndex = GetWebRtcProtocolIndex(addrStr);
        int tcpIndex = addrStr.IndexOf(TcpProtocol, StringComparison.Ordinal);
        if (tcpIndex >= 0 && tcpIndex < webrtcIndex)
        {
            return false;
        }

        // Ensure UDP appears before the webrtc component
        int udpIndex = addrStr.IndexOf(UdpProtocol, StringComparison.Ordinal);
        if (udpIndex >= webrtcIndex)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Returns true if the multiaddress is a /webrtc address (w3c spec-based, with signaling).
    /// Returns false for /webrtc-direct.
    /// </summary>
    public static bool IsWebRtcSignaled(this Multiaddress addr)
    {
        string addrStr = addr.ToString();
        return ContainsWebRtc(addrStr) && !ContainsWebRtcDirect(addrStr);
    }

    /// <summary>
    /// Returns true if the multiaddress is a /webrtc-direct address (ICE-lite, no signaling server).
    /// </summary>
    public static bool IsWebRtcDirect(this Multiaddress addr)
    {
        string addrStr = addr.ToString();
        return ContainsWebRtcDirect(addrStr);
    }

    // --- Private helpers ---

    private static bool ContainsWebRtc(string addrStr)
    {
        // Match /webrtc but not /webrtc-direct and not deprecated /p2p-webrtc-*
        int idx = 0;
        while (true)
        {
            idx = addrStr.IndexOf(WebRtcProtocol, idx, StringComparison.Ordinal);
            if (idx < 0)
            {
                return false;
            }

            // Reject if this is part of a longer token (e.g. /webrtc-direct, /p2p-webrtc-star)
            int afterIdx = idx + WebRtcProtocol.Length;
            bool isExactComponent = afterIdx >= addrStr.Length ||
                                    addrStr[afterIdx] == '/';

            // Reject if preceded by /p2p- (deprecated variants)
            bool isPrefixedByP2p = idx >= 4 && addrStr[(idx - 4)..idx] == "p2p-";

            if (isExactComponent && !isPrefixedByP2p)
            {
                return true;
            }

            idx = afterIdx;
        }
    }

    private static bool ContainsWebRtcDirect(string addrStr) =>
        addrStr.Contains(WebRtcDirectProtocol, StringComparison.Ordinal) &&
        !addrStr.Contains(DeprecatedP2pWebrtcDirect, StringComparison.Ordinal);

    private static bool ContainsDeprecatedWebRtcProtocol(string addrStr) =>
        addrStr.Contains(DeprecatedP2pWebrtcStar, StringComparison.Ordinal) ||
        addrStr.Contains(DeprecatedP2pWebrtcDirect, StringComparison.Ordinal);

    private static int GetWebRtcProtocolIndex(string addrStr)
    {
        int directIdx = addrStr.IndexOf(WebRtcDirectProtocol, StringComparison.Ordinal);
        if (directIdx >= 0 && !addrStr.Contains(DeprecatedP2pWebrtcDirect, StringComparison.Ordinal))
        {
            return directIdx;
        }

        // Find /webrtc that is not /webrtc-direct
        int idx = 0;
        while (true)
        {
            idx = addrStr.IndexOf(WebRtcProtocol, idx, StringComparison.Ordinal);
            if (idx < 0)
            {
                return -1;
            }

            int afterIdx = idx + WebRtcProtocol.Length;
            bool isExact = afterIdx >= addrStr.Length || addrStr[afterIdx] == '/';
            bool isPrefixed = idx >= 4 && addrStr[(idx - 4)..idx] == "p2p-";

            if (isExact && !isPrefixed)
            {
                return idx;
            }

            idx = afterIdx;
        }
    }
}
