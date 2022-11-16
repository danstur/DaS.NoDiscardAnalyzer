using DaS.NoDiscardAnalyzer.Tests.Verifiers;
using System.Threading.Tasks;
using Xunit;
using Verify = DaS.NoDiscardAnalyzer.Tests.Verifiers.CSharpAnalyzerVerifier<DaS.NoDiscardAnalyzer.NoDiscardAnalyzer>;

namespace DaS.NoDiscardAnalyzer.Tests;

public sealed partial class NoDiscardAnalyzerTests
{
    [Fact]
    public async Task Empty_code_causes_no_diagnostic()
    {
        var test = "";
        await Verify.VerifyAnalyzerAsync(test);
    }

    [Fact]
    public async Task Unused_attributed_method_causes_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

public static class Foo 
{
    [NoDiscard] public static int Bar() => 0xdead;

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Unused_return_attributed_method_causes_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

public static class Foo 
{
    [return: NoDiscard] public static int Bar() => 0xdead;

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Unused_attributed_reference_type_causes_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

[NoDiscard]
public sealed class Result{}

public static class Foo 
{
    public static Result Bar() => new();

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Unused_attributed_struct_type_causes_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

[NoDiscard]
public struct Result{}

public static class Foo 
{
    public static Result Bar() => new();

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Unused_attributed_enum_type_causes_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

[NoDiscard]
public enum Result { None, Something }

public static class Foo 
{
    public static Result Bar() => new();

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Custom_NoDiscard_Attribute_Causes_Diagnostic()
    {
        var test = """
namespace Baz 
{
    using System;

    [AttributeUsage(AttributeTargets.Method |
                    AttributeTargets.ReturnValue |
                    AttributeTargets.Class | 
                    AttributeTargets.Struct | 
                    AttributeTargets.Enum, AllowMultiple = false, Inherited = true)]
    public sealed class MyCustomDiscardAttribute : Attribute
    {
        public string Explanation { get; }

        public MyCustomDiscardAttribute(string explanation = null)
        {
            Explanation = explanation;
        }
    }
}

namespace Bar 
{
    using Baz;

    public static class Foo 
    {
        [MyCustomDiscard]
        public static int Bar() => 0xdead;

        public static void Usage() 
        {
            Foo.{|#0:Bar|}();
        }
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, new VerificationOptions()
        {
            CustomAttributeName = "Baz.MyCustomDiscardAttribute"
        }, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Type_configured_in_file_causes_diagnostic()
    {
        var test = """
namespace Baz;

public sealed class Result {}

public static class Foo 
{
    public static Result Bar() => new();

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, new VerificationOptions()
        {
            AdditionalForbiddenDiscardTypesFileContent = """
            Baz.Result
            """
        }, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Type_configured_in_file_ignores_whitespace()
    {
        var test = """
namespace Baz;

public sealed class Result {}

public static class Foo 
{
    public static Result Bar() => new();

    public static void Usage() 
    {
        Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test, new VerificationOptions()
        {
            AdditionalForbiddenDiscardTypesFileContent = """
              Baz.Result  
            """
        }, Verify.Diagnostic(NoDiscardAnalyzer.DoNotDiscardResultRule)
            .WithLocation(0));
    }

    [Fact]
    public async Task Used_attributed_method_causes_no_diagnostic()
    {
        var test = """
using DaS.NoDiscardAnalyzer.Attributes;

public static class Foo 
{
    [NoDiscard] public static int Bar() => 0xdead;

    public static void Usage() 
    {
        _ = Foo.{|#0:Bar|}();
    }
}
""";
        await Verify.VerifyAnalyzerAsync(test);
    }
}