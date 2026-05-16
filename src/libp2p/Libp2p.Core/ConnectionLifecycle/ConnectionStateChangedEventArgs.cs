// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System;

namespace Nethermind.Libp2p.Core;

/// <summary>
/// Provides event data for connection state changes.
/// </summary>
public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the state before the transition.
    /// </summary>
    public ConnectionState Previous { get; }

    /// <summary>
    /// Gets the state after the transition.
    /// </summary>
    public ConnectionState Current { get; }

    /// <summary>
    /// Gets the exception that caused the transition to Failed, if any.
    /// </summary>
    public Exception? Error { get; }

    /// <summary>
    /// Gets the timestamp when the state transition occurred.
    /// </summary>
    public DateTimeOffset ChangedAt { get; }

    public ConnectionStateChangedEventArgs(ConnectionState previous, ConnectionState current, Exception? error = null)
    {
        Previous = previous;
        Current = current;
        Error = error;
        ChangedAt = DateTimeOffset.UtcNow;
    }
}
