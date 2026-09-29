using System;
using System.Linq;
using NUnit.Framework;

namespace UnityIsekaiGame.Tests
{
    internal static class TestTypeResolver
    {
        public static Type RequiredType(string fullName)
        {
            Type type = AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(found => found != null);

            Assert.That(type, Is.Not.Null, $"Expected runtime type {fullName} to exist in loaded project assemblies.");
            return type;
        }

        public static Type OptionalType(string fullName)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly => assembly.GetType(fullName))
                .FirstOrDefault(found => found != null);
        }
    }
}
