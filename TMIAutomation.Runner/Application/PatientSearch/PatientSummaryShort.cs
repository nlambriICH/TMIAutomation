using System;

namespace TMIAutomation.Runner
{
    internal class PatientSummaryShort
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime? CreationDateTime { get; set; }
    }
}