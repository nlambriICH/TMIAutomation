using System.Collections.Generic;

namespace TMIAutomation.Runner.Persistence
{
    internal class Data
    {
        public Data()
        {
            Recents = new List<RecentEntry>();
        }

        public List<RecentEntry> Recents { get; set; }
    }
}