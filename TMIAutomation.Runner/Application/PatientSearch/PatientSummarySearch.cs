using System.Collections.Generic;
using System.Linq;
using VMS.TPS.Common.Model.API;

namespace TMIAutomation.Runner
{
    internal class PatientSummarySearch
    {
        private readonly IEnumerable<PatientSummary> patients;
        private readonly PatientSearch patientSearch;

        /// <summary>
        /// Initializes a new instance of the PatientSummarySearch class.
        /// </summary>
        /// <param name="patients">The list of patients to search.</param>
        /// <param name="maxResults">The maximum number of results to return.</param>
        public PatientSummarySearch(IEnumerable<PatientSummary> patients, int maxResults)
        {
            // Need to convert patients to an array,
            // or the IEnumerable will be iterated multiple times
            this.patients = patients.ToArray();

            // Adapt each PatientSummary to a SearchPatient object
            // to make PatientSearch testable without referencing ESAPI
            IEnumerable<PatientSummaryShort> patientSummaryShort = CreatePatientSummaryShort();
            patientSearch = new PatientSearch(patientSummaryShort, maxResults);
        }

        /// <summary>
        /// Finds the patients that match the given search string.
        /// </summary>
        /// <param name="searchText">The search string.</param>
        /// <returns>The patients that match the given search string.</returns>
        /// <remarks>
        /// If the search string is a single word, a patient matches it
        /// if the patient's ID, first name, or last name starts with it.
        /// If the search string contains two or more words, a patient matches it
        /// if the patient's first name or last name starts with any of the first
        /// two words in the search string (the remaining words are ignored).
        /// The words in a search string may be separated by a space, a comma, or
        /// a semicolon.
        /// </remarks>
        public IEnumerable<PatientSummary> FindMatches(string searchText)
        {
            // Need to convert matches to an array,
            // or the IEnumerable will be iterated multiple times
            IEnumerable<PatientSummaryShort> matches = patientSearch.FindMatches(searchText);
            return GetPatientSummaries(matches.ToArray());
        }

        private IEnumerable<PatientSummaryShort> CreatePatientSummaryShort()
        {
            return patients.Select(CreateSearchPatient);
        }

        private PatientSummaryShort CreateSearchPatient(PatientSummary patientSummary)
        {
            return new PatientSummaryShort
            {
                Id = patientSummary.Id,
                FirstName = patientSummary.FirstName,
                LastName = patientSummary.LastName,
                CreationDateTime = patientSummary.CreationDateTime
            };
        }

        private IEnumerable<PatientSummary> GetPatientSummaries(IEnumerable<PatientSummaryShort> patients)
        {
            return this.patients.Where(ps => ContainsById(ps, patients));
        }

        private bool ContainsById(PatientSummary patientSummary, IEnumerable<PatientSummaryShort> patients)
        {
            return patients.FirstOrDefault(p => p.Id == patientSummary.Id) != null;
        }
    }
}
