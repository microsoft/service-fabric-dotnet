// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using Xunit;

namespace Microsoft.ServiceFabric.Diagnostics.Metrics.Implementation;

/// <summary>
/// Dedicated threads that record concurrently in race tests.
/// </summary>
static class RecorderThreads
{
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Starts the threads, returns once all are running, and collects anything they throw.
    /// </summary>
    internal static Thread[] Start(int count, ConcurrentQueue<Exception> errors, Action body)
    {
        CountdownEvent started = new(count);
        Thread[] threads = [.. Enumerable.Range(0, count).Select(_ => new Thread(() =>
        {
            started.Signal();
            try
            {
                body();
            }
            catch (Exception e)
            {
                errors.Enqueue(e);
            }
        }) { IsBackground = true })];

        foreach (Thread thread in threads)
            thread.Start();

        Assert.True(started.Wait(Timeout), "The recorder threads did not start.");
        return threads;
    }

    internal static void JoinAll(Thread[] threads)
    {
        foreach (Thread thread in threads)
            Assert.True(thread.Join(Timeout), "A recorder thread did not finish.");
    }
}
