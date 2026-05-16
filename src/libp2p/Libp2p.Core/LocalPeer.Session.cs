// SPDX-FileCopyrightText: 2025 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using Multiformats.Address;
using Nethermind.Libp2p.Core.Exceptions;
using Nethermind.Libp2p.Core.Metrics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Nethermind.Libp2p.Core;

public partial class LocalPeer
{
    public class Session : ISession
    {
        private static int SessionIdCounter;
        private readonly ConnectionStateMachine _stateMachine;
        private readonly LocalPeer _peer;

        public Session(LocalPeer peer)
        {
            _peer = peer;
            _stateMachine = new ConnectionStateMachine();
            _stateMachine.StateChanged += OnStateChanged;
        }

        public string Id { get; } = Interlocked.Increment(ref SessionIdCounter).ToString();
        public State State { get; } = new();
        public Activity? Activity { get; }

        public ConnectionState ConnectionState => _stateMachine.State;
        public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

        public Multiaddress RemoteAddress => State.RemoteAddress ?? throw new Libp2pException("Session contains uninitialized remote address.");

        private readonly BlockingCollection<UpgradeOptions> SubDialRequests = [];

        public async Task DialAsync<TProtocol>(CancellationToken token = default) where TProtocol : ISessionProtocol
        {
            TaskCompletionSource<object?> tcs = new();
            SubDialRequests.Add(new UpgradeOptions() { CompletionSource = tcs!, SelectedProtocol = _peer.GetProtocolInstance<TProtocol>() }, token);
            await tcs.Task;
            MarkAsConnected();
        }

        public async Task DialAsync(ISessionProtocol protocol, CancellationToken token = default)
        {
            TaskCompletionSource<object?> tcs = new();
            SubDialRequests.Add(new UpgradeOptions() { CompletionSource = tcs, SelectedProtocol = protocol }, token);
            await tcs.Task;
            MarkAsConnected();
        }

        public async Task<TResponse> DialAsync<TProtocol, TRequest, TResponse>(TRequest request, CancellationToken token = default) where TProtocol : ISessionProtocol<TRequest, TResponse>
        {
            TaskCompletionSource<object?> tcs = new();
            SubDialRequests.Add(new UpgradeOptions() { CompletionSource = tcs, SelectedProtocol = _peer.GetProtocolInstance<TProtocol>(), Argument = request }, token);
            await tcs.Task;
            MarkAsConnected();
            return (TResponse)tcs.Task.Result!;
        }

        private CancellationTokenSource connectionTokenSource = new();

        public Task DisconnectAsync()
        {
            _stateMachine.TryTransitionSafe(ConnectionState.Closed);
            connectionTokenSource.Cancel();
            _peer.RemoveSession(this);
            return Task.CompletedTask;
        }

        public CancellationToken ConnectionToken => connectionTokenSource.Token;

        public TaskCompletionSource ConnectedTcs = new();
        public Task Connected => ConnectedTcs.Task;

        internal void MarkAsConnected()
        {
            _stateMachine.TryTransitionSafe(ConnectionState.Connected);
            ConnectedTcs?.TrySetResult();
        }

        internal bool TryTransition(ConnectionState target, Exception? error = null)
        {
            return _stateMachine.TryTransition(target, error);
        }

        internal bool TryTransitionSafe(ConnectionState target, Exception? error = null)
        {
            return _stateMachine.TryTransitionSafe(target, error);
        }

        internal IEnumerable<UpgradeOptions> GetRequestQueue() => SubDialRequests.GetConsumingEnumerable(ConnectionToken);

        private void OnStateChanged(object? sender, ConnectionStateChangedEventArgs e)
        {
            // Publish/forward the event to outer listeners
            ConnectionStateChanged?.Invoke(this, e);

            // Increment transition counter
            Libp2pMetrics.ConnectionStateTransitions.Add(1);

            // Log transitions using source-generated loggers
            if (e.Current == ConnectionState.Failed)
            {
                _peer._logger?.ConnectionFailed(Id, e.Error?.Message ?? "Unknown failure", e.Error);
            }
            else
            {
                _peer._logger?.ConnectionStateChanged(Id, e.Previous.ToString(), e.Current.ToString());
            }
        }
    }

    private void RemoveSession(Session session)
    {
        lock (Sessions)
        {
            Sessions.Remove(session);
        }
        if (session.ConnectionState == ConnectionState.Connected)
        {
            session.TryTransitionSafe(ConnectionState.Reconnecting);
            Libp2pMetrics.ReconnectAttempts.Add(1);
            session.TryTransitionSafe(ConnectionState.Failed, new Libp2pException("Connection lost due to transport failure"));
        }
        Libp2pMetrics.SessionsClosed.Add(1);
        Libp2pMetrics.SessionsActive.Add(-1);
        Libp2pMetrics.ConnectionsActive.Add(-1);
    }
}
