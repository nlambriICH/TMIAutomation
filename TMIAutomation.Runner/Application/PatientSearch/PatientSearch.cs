using System;
using System.Collections.Generic;
using System.Linq;

namespace TMIAutomation.Runner
{
    internal class PatientSearch
    {
        private readonly IEnumerable<PatientSummaryShort> patients;
        private readonly int maxResults;

        public PatientSearch(IEnumerable<PatientSummaryShort> patients, int maxResults)
        {
            this.patients = patients;
            this.maxResults = maxResults;
        }

        public IEnumerable<PatientSummaryShort> FindMatches(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return Enumerable.Empty<PatientSummaryShort>();
            }

            string[] searchTerms = GetSearchTerms(searchText);
            return patients
                .Where(p => IsMatch(p, searchTerms))
                .OrderByDescending(p => p.CreationDateTime)
                .Take(maxResults);
        }

        private bool IsMatch(PatientSummaryShort patient, string[] searchTerms)
        {
            switch (searchTerms.Length)
            {
                case 0:
                    return false;
                case 1:
                    return IsMatchWithOneSearchTerm(patient, searchTerms[0]);
                default:
                    return IsMatchWithTwoSearchTerms(patient, searchTerms[0], searchTerms[1]);
            }
        }

        private string[] GetSearchTerms(string searchText)
        {
            return searchText.Split(' ', ',').ToArray();
        }

        private bool IsMatchWithOneSearchTerm(PatientSummaryShort patient, string term)
        {
            return IsSubstring(term, patient.Id)
                || IsSubstring(term, patient.LastName)
                || IsSubstring(term, patient.FirstName);
        }

        private bool IsMatchWithTwoSearchTerms(PatientSummaryShort patient, string term1, string term2)
        {
            return IsMatchWithLastThenFirstName(patient, term1, term2)
                || IsMatchWithLastThenFirstName(patient, term2, term1);
        }

        private bool IsMatchWithLastThenFirstName(PatientSummaryShort patient, string lastName, string firstName)
        {
            return IsSubstring(lastName, patient.LastName) && IsSubstring(firstName, patient.FirstName);
        }

        private bool IsSubstring(string small, string large)
        {
            return large.StartsWith(small, StringComparison.OrdinalIgnoreCase);
        }
    }
}