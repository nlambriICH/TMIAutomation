using System;
using System.Reflection;

namespace TMIAutomation.Tests
{
    public static class TestReporter
    {
        public static int TotalTests { get; set; }
        public static int PassedTests { get; set; }
        public static int FailedTests { get; set; }

        public static void PrintReport()
        {
            Console.WriteLine($"Total tests: {TotalTests} (Passed: {PassedTests}, Failed: {FailedTests})");
        }

        public static void ReportFailedTest(object testInstance, MethodInfo testMethod, Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed {testInstance.GetType()}:{testMethod.Name}");
            Console.ResetColor();
            Console.WriteLine(e.InnerException.Message);
            FailedTests++;
        }
    }
}
