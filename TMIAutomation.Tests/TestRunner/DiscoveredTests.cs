using System;
using System.Collections.Generic;
using System.Reflection;

namespace TMIAutomation.Tests
{
    public static class DiscoveredTests
    {
        private static readonly Dictionary<object, List<MethodInfo>> factTests = new Dictionary<object, List<MethodInfo>> { };
        private static readonly Dictionary<object, List<MethodInfo>> theoryTests = new Dictionary<object, List<MethodInfo>> { };
        public static Dictionary<object, List<MethodInfo>> FactTests { get => factTests; private set { } }
        public static Dictionary<object, List<MethodInfo>> TheoryTests { get => theoryTests; private set { } }
    }
}
