﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace TMIJunction.Async
{
    /// <summary>
    /// Represents an object that runs operations asynchronously
    /// on the thread on which it was instantiated.
    /// </summary>
    public class CurrentThreadWorker
    {
        private TaskScheduler _taskScheduler;

        public CurrentThreadWorker()
        {
            // Create the task scheduler using the dispatcher to
            // ensure that the proper synchronization context exists
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                _taskScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            });
        }

        public Task RunAsync(Action action)
        {
            return Task.Factory.StartNew(action,
                CancellationToken.None,
                TaskCreationOptions.None,
                _taskScheduler);
        }

        public Task<T> RunAsync<T>(Func<T> function)
        {
            return Task.Factory.StartNew(function,
                CancellationToken.None,
                TaskCreationOptions.None,
                _taskScheduler);
        }
    }
}