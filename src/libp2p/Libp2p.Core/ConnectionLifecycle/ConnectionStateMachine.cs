// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System;
using System.Threading;

namespace Nethermind.Libp2p.Core;

/// <summary>
/// A thread-safe, lock-free state machine for managing a connection lifecycle.
/// </summary>
internal sealed class ConnectionStateMachine
{
    private int _state; // Stores the current state as an integer representation of ConnectionState

    /// <summary>
    /// Gets the current state of the connection.
    /// </summary>
    public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

    /// <summary>
    /// Occurs when the connection state has transitioned successfully.
    /// </summary>
    public event EventHandler<ConnectionStateChangedEventArgs>? StateChanged;

    public ConnectionStateMachine(ConnectionState initialState = ConnectionState.Disconnected)
    {
        _state = (int)initialState;
    }

    /// <summary>
    /// Attempts to transition to the specified target state.
    /// Throws an InvalidOperationException if the transition is illegal.
    /// </summary>
    public bool TryTransition(ConnectionState to, Exception? error = null)
    {
        while (true)
        {
            ConnectionState current = State;

            if (current == to)
            {
                return false; // Already in the target state
            }

            if (!IsValidTransition(current, to))
            {
                throw new InvalidOperationException($"Invalid state transition from '{current}' to '{to}'.");
            }

            if (Interlocked.CompareExchange(ref _state, (int)to, (int)current) == (int)current)
            {
                OnStateChanged(current, to, error);
                return true;
            }
        }
    }

    /// <summary>
    /// Attempts to transition to the specified target state.
    /// Does not throw on illegal transition; instead returns false.
    /// </summary>
    public bool TryTransitionSafe(ConnectionState to, Exception? error = null)
    {
        try
        {
            return TryTransition(to, error);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resets a Failed state back to Disconnected to allow retry/redial attempts.
    /// </summary>
    public bool ResetToDisconnected()
    {
        return TryTransitionSafe(ConnectionState.Disconnected);
    }

    private static bool IsValidTransition(ConnectionState from, ConnectionState to)
    {
        return (from, to) switch
        {
            // From Disconnected
            (ConnectionState.Disconnected, ConnectionState.Connecting) => true,
            (ConnectionState.Disconnected, ConnectionState.Connected) => true, // Inbound connections direct to Connected
            (ConnectionState.Disconnected, ConnectionState.Closed) => true,

            // From Connecting
            (ConnectionState.Connecting, ConnectionState.Connected) => true,
            (ConnectionState.Connecting, ConnectionState.Failed) => true,
            (ConnectionState.Connecting, ConnectionState.Closed) => true,

            // From Connected
            (ConnectionState.Connected, ConnectionState.Reconnecting) => true,
            (ConnectionState.Connected, ConnectionState.Closed) => true,
            (ConnectionState.Connected, ConnectionState.Failed) => true,

            // From Reconnecting
            (ConnectionState.Reconnecting, ConnectionState.Connected) => true,
            (ConnectionState.Reconnecting, ConnectionState.Failed) => true,
            (ConnectionState.Reconnecting, ConnectionState.Closed) => true,

            // From Failed (allow reset back to Disconnected to redial/retry)
            (ConnectionState.Failed, ConnectionState.Disconnected) => true,
            (ConnectionState.Failed, ConnectionState.Closed) => true,

            // From Closed (terminal state, no transitions allowed out of Closed)
            _ => false
        };
    }

    private void OnStateChanged(ConnectionState previous, ConnectionState current, Exception? error)
    {
        StateChanged?.Invoke(this, new ConnectionStateChangedEventArgs(previous, current, error));
    }
}
