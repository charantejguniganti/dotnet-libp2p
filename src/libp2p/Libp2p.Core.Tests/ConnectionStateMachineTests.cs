// SPDX-FileCopyrightText: 2024 Demerzel Solutions Limited
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Nethermind.Libp2p.Core.Tests;

[TestFixture]
public class ConnectionStateMachineTests
{
    [Test]
    public void Test_InitialState_IsDisconnected()
    {
        var machine = new ConnectionStateMachine();
        Assert.That(machine.State, Is.EqualTo(ConnectionState.Disconnected));
    }

    [Test]
    public void Test_NormalOutboundDialLifecycle_TransitionsSuccessfully()
    {
        var machine = new ConnectionStateMachine();
        var changes = new List<ConnectionStateChangedEventArgs>();
        machine.StateChanged += (s, e) => changes.Add(e);

        // Disconnected -> Connecting
        bool step1 = machine.TryTransition(ConnectionState.Connecting);
        Assert.Multiple(() =>
        {
            Assert.That(step1, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Connecting));
        });

        // Connecting -> Connected
        bool step2 = machine.TryTransition(ConnectionState.Connected);
        Assert.Multiple(() =>
        {
            Assert.That(step2, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Connected));
        });

        // Connected -> Closed
        bool step3 = machine.TryTransition(ConnectionState.Closed);
        Assert.Multiple(() =>
        {
            Assert.That(step3, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Closed));
        });

        Assert.Multiple(() =>
        {
            Assert.That(changes, Has.Count.EqualTo(3));
            Assert.That(changes[0].Previous, Is.EqualTo(ConnectionState.Disconnected));
            Assert.That(changes[0].Current, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(changes[1].Previous, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(changes[1].Current, Is.EqualTo(ConnectionState.Connected));
            Assert.That(changes[2].Previous, Is.EqualTo(ConnectionState.Connected));
            Assert.That(changes[2].Current, Is.EqualTo(ConnectionState.Closed));
        });
    }

    [Test]
    public void Test_InboundLifecycle_TransitionsDirectlyToConnected()
    {
        var machine = new ConnectionStateMachine();
        bool step1 = machine.TryTransition(ConnectionState.Connected);
        Assert.Multiple(() =>
        {
            Assert.That(step1, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Connected));
        });
    }

    [Test]
    public void Test_DialFailure_TransitionsToFailedWithException()
    {
        var machine = new ConnectionStateMachine();
        var changes = new List<ConnectionStateChangedEventArgs>();
        machine.StateChanged += (s, e) => changes.Add(e);

        machine.TryTransition(ConnectionState.Connecting);
        var originalException = new Exception("Connection refused");
        bool step2 = machine.TryTransition(ConnectionState.Failed, originalException);

        Assert.Multiple(() =>
        {
            Assert.That(step2, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Failed));
            Assert.That(changes, Has.Count.EqualTo(2));
            Assert.That(changes[1].Current, Is.EqualTo(ConnectionState.Failed));
            Assert.That(changes[1].Error, Is.SameAs(originalException));
        });
    }

    [Test]
    public void Test_IllegalTransition_ThrowsInvalidOperationException()
    {
        var machine = new ConnectionStateMachine();
        machine.TryTransition(ConnectionState.Connecting);
        
        // Cannot go from Connecting directly to Reconnecting
        Assert.Throws<InvalidOperationException>(() => machine.TryTransition(ConnectionState.Reconnecting));
    }

    [Test]
    public void Test_TryTransitionSafe_DoesNotThrowOnIllegalTransition()
    {
        var machine = new ConnectionStateMachine();
        machine.TryTransition(ConnectionState.Connecting);

        bool result = machine.TryTransitionSafe(ConnectionState.Reconnecting);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Connecting));
        });
    }

    [Test]
    public void Test_FailedState_CanBeResetToDisconnected()
    {
        var machine = new ConnectionStateMachine();
        machine.TryTransition(ConnectionState.Connecting);
        machine.TryTransition(ConnectionState.Failed);

        bool resetResult = machine.ResetToDisconnected();
        Assert.Multiple(() =>
        {
            Assert.That(resetResult, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Disconnected));
        });
    }

    [Test]
    public void Test_ClosedIsTerminal_DoesNotAllowAnyFurtherTransitions()
    {
        var machine = new ConnectionStateMachine();
        machine.TryTransition(ConnectionState.Connected);
        machine.TryTransition(ConnectionState.Closed);

        // Try transitioning Closed -> Reconnecting or Closed -> Disconnected
        Assert.Throws<InvalidOperationException>(() => machine.TryTransition(ConnectionState.Reconnecting));
        Assert.Throws<InvalidOperationException>(() => machine.TryTransition(ConnectionState.Disconnected));
    }

    [Test]
    public void Test_ReconnectingFlows_TransitionCorrectly()
    {
        var machine = new ConnectionStateMachine();
        machine.TryTransition(ConnectionState.Connected);

        // Transition Connected -> Reconnecting
        bool step1 = machine.TryTransition(ConnectionState.Reconnecting);
        Assert.Multiple(() =>
        {
            Assert.That(step1, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Reconnecting));
        });

        // Reconnecting -> Connected (Success recovery)
        bool step2 = machine.TryTransition(ConnectionState.Connected);
        Assert.Multiple(() =>
        {
            Assert.That(step2, Is.True);
            Assert.That(machine.State, Is.EqualTo(ConnectionState.Connected));
        });
    }

    [Test]
    public async Task Test_ConcurrentTimeTransitions_EnforcesStrictAtomicity()
    {
        var machine = new ConnectionStateMachine();
        int successfulTransitions = 0;
        int transitionAttempts = 100;
        var tasks = new List<Task>();

        // Set to Connecting first
        machine.TryTransition(ConnectionState.Connecting);

        for (int i = 0; i < transitionAttempts; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                if (machine.TryTransitionSafe(ConnectionState.Connected))
                {
                    Interlocked.Increment(ref successfulTransitions);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Since CAS guarantees thread-safety and atomicity, only exactly ONE thread
        // must successfully perform the state transition. All others must return false.
        Assert.That(successfulTransitions, Is.EqualTo(1));
    }
}
