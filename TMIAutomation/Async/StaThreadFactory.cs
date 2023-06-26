using System;
using System.Threading;

namespace TMIAutomation.Async
{
    public class StaThreadFactory
    {
        public static Thread StartNew(Action a)
        {
            var thread = new Thread(() => a());
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return thread;
        }
    }
}