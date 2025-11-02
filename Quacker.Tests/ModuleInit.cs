using System.Runtime.CompilerServices;

namespace Quacker.Tests;

public static class ModuleInit
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
