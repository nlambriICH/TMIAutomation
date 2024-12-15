using System;
using System.Windows.Threading;

namespace TMIAutomation.Async
{
    /// <summary>
    /// Provides a static method to run a given action concurrently.
    /// </summary>
    public class ConcurrentStaThreadRunner
    {
        /// <summary>
        /// Runs the given action on a new STA thread while keeping the
        /// calling thread responsive (i.e., runs the action concurrently).
        /// </summary>
        /// <param name="a">The action to run on the new STA thread.</param>
        public static void Run(Action a)
        {
            // This new message loop (DispatcherFrame) will prevent the
            // current thread from exiting until the action is done
            DispatcherFrame frame = new DispatcherFrame();

            StaThreadFactory.StartNew(() =>
            {
                a();

                // End the message loop so that the original thread can exit
                frame.Continue = false;
            });

            // Start the new message loop
            Dispatcher.PushFrame(frame);
        }
    }
}