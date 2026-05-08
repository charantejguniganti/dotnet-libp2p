// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using Multiformats.Address;

namespace Nethermind.Libp2p.Core.Tests;

[TestFixture]
public class WebRtcMultiaddressExtensionsTests
{
    // --- IsWebRtcAddress ---

    [Test]
    public void IsWebRtcAddress_WithWebRtcProtocol_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.True);
    }

    [Test]
    public void IsWebRtcAddress_WithWebRtcDirectProtocol_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc-direct/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.True);
    }

    [Test]
    public void IsWebRtcAddress_WithTcpAddress_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/tcp/4001/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsWebRtcAddress_WithQuicAddress_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/quic-v1/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsWebRtcAddress_WithDeprecatedP2pWebrtcStar_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/tcp/9090/p2p-webrtc-star/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsWebRtcAddress_WithDeprecatedP2pWebrtcDirect_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/tcp/9090/p2p-webrtc-direct/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.False);
    }

    // --- IsValidWebRtcAddress ---

    [Test]
    public void IsValidWebRtcAddress_WithCorrectStructure_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.True);
    }

    [Test]
    public void IsValidWebRtcAddress_WebRtcDirect_WithUdp_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc-direct/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.True);
    }

    [Test]
    public void IsValidWebRtcAddress_WithTcpBeforeWebRtc_ReturnsFalse()
    {
        // TCP is not a valid transport base for WebRTC
        Multiaddress addr = "/ip4/192.168.1.1/tcp/9090/udp/9091/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsValidWebRtcAddress_WithoutUdp_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsValidWebRtcAddress_NonWebRtcAddress_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/tcp/4001/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.False);
    }

    [Test]
    public void IsValidWebRtcAddress_DeprecatedVariant_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/tcp/9090/p2p-webrtc-star/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.False);
    }

    // --- IsWebRtcSignaled / IsWebRtcDirect ---

    [Test]
    public void IsWebRtcSignaled_WithWebRtc_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcSignaled(), Is.True);
    }

    [Test]
    public void IsWebRtcSignaled_WithWebRtcDirect_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc-direct/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcSignaled(), Is.False);
    }

    [Test]
    public void IsWebRtcDirect_WithWebRtcDirect_ReturnsTrue()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc-direct/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcDirect(), Is.True);
    }

    [Test]
    public void IsWebRtcDirect_WithWebRtc_ReturnsFalse()
    {
        Multiaddress addr = "/ip4/192.168.1.1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcDirect(), Is.False);
    }

    // --- IPv6 ---

    [Test]
    public void IsWebRtcAddress_WithIPv6_ReturnsTrue()
    {
        Multiaddress addr = "/ip6/::1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsWebRtcAddress(), Is.True);
    }

    [Test]
    public void IsValidWebRtcAddress_WithIPv6_ReturnsTrue()
    {
        Multiaddress addr = "/ip6/::1/udp/9090/webrtc/p2p/QmTest123456789012345678901234567890123456789012";
        Assert.That(addr.IsValidWebRtcAddress(), Is.True);
    }
}
