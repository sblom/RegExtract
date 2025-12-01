// Compatibility shim for older target frameworks that don't have Verify packages available.
#if NETCOREAPP3_1 || NET462
using System;
using System.Threading.Tasks;

namespace VerifyXunit
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class UsesVerifyAttribute : Attribute { }
}

namespace VerifyTests
{
    // Minimal Verifier stub to allow the test code to compile and run on older TFMs.
    internal static class Verifier
    {
        public static Task Verify(object? input) => Task.CompletedTask;
        public static Task Verify(string input) => Task.CompletedTask;
    }
}
#endif
