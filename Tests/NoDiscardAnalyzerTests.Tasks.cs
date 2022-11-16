using System.Threading.Tasks;
using Xunit;
using Verify = DaS.NoDiscardAnalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<DaS.NoDiscardAnalyzer.NoDiscardAnalyzer>;

namespace DaS.NoDiscardAnalyzer.Tests;

public sealed partial class NoDiscardAnalyzerTests
{
    [Fact]
    public async Task Awaited_Task_discarding_value_shows_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;
using System.Threading.Tasks;

public static class Foo 
{
    [NoDiscard] public static Task<int> BarAsync() => Task.FromResult(0xdead);

    public static async Task Usage() 
    {
        await Foo.{|#0:BarAsync|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task NonAwaited_Task_discarding_value_shows_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;
using System.Threading.Tasks;

public static class Foo 
{
    [NoDiscard] public static Task<int> BarAsync() => Task.FromResult(0xdead);

    public static void Usage() 
    {
        Foo.{|#0:BarAsync|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }
    
    // ValueTask is only available starting with .NET Core 1.0
#if NETCOREAPP
    [Fact]
    public async Task Awaited_ValueTask_discarding_value_shows_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;
using System.Threading.Tasks;

public static class Foo 
{
    [NoDiscard] public static ValueTask<int> BarAsync() => new(0xdead);

    public static async Task Usage() 
    {
        await Foo.{|#0:BarAsync|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task NonAwaited_ValueTask_discarding_value_shows_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;
using System.Threading.Tasks;

public static class Foo 
{
    [NoDiscard] public static ValueTask<int> BarAsync() => new(0xdead);

    public static void Usage() 
    {
        Foo.{|#0:BarAsync|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }
#endif
}