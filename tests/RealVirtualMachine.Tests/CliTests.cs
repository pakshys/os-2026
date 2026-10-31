namespace RealVirtualMachine.Tests;

using RealVirtualMachine;

public sealed class CliTests : IDisposable
{
    //each test gets its own disk image
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
    //test programs are copied next to the test dll
    private static readonly string Programs = Path.Combine(AppContext.BaseDirectory, "programs");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    private (int Code, string Output, string Error) Run(string input, params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int code = Cli.Run(args, new StringReader(input), output, error, _path);
        return (code, output.ToString(), error.ToString());
    }

    private void Import(string name, string file) =>
        Assert.Equal(0, Run("", "hdd", "import", name, Path.Combine(Programs, file)).Code);

    private static string Lines(params string[] lines) =>
        string.Concat(lines.Select(line => line + Environment.NewLine));

    [Theory]
    [InlineData("HELO", "hello.txt", "HELLO, WORLD!")]
    [InlineData("ADD", "add.txt", "OK")]
    [InlineData("DIV0", "div0.txt", "DIV0: division by zero")]
    [InlineData("OVFL", "overflow.txt", "OVFL: overflow")]
    public void Run_TestProgram_PrintsExpectedOutput(string name, string file, string expected)
    {
        Import(name, file);

        var result = Run("", "run", name);

        Assert.Equal(0, result.Code);
        Assert.Equal(Lines(expected), result.Output);
    }

    [Fact]
    public void Run_Echo_PrintsKeyboardLine()
    {
        Import("ECHO", "echo.txt");

        var result = Run("labas\n", "run", "ECHO");

        Assert.Equal(Lines("labas" + new string(' ', 59)), result.Output);
    }

    //LOOP never halts, timer still lets HELO finish
    [Fact]
    public void Run_LoopWithOtherProgram_StopsAtLimit()
    {
        Import("LOOP", "loop.txt");
        Import("HELO", "hello.txt");

        var result = Run("", "run", "LOOP", "HELO");

        Assert.Equal(0, result.Code);
        Assert.Equal(Lines("HELLO, WORLD!", $"Stopped after {Cli.InstructionLimit} instructions."), result.Output);
    }

    [Fact]
    public void Import_SkipsCommentsAndEmptyLines()
    {
        var file = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(file, ["; comment", "", "  0000F000 ; HALT", "00000001"]);
            Assert.Equal([0xF000u, 1u], Cli.ReadProgramFile(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void List_ShowsImportedPrograms()
    {
        Import("HELO", "hello.txt");
        Import("ECHO", "echo.txt");

        var result = Run("", "hdd", "list");

        Assert.Equal(Lines("HELO: block 01, 11 words", "ECHO: block 02, 8 words"), result.Output);
    }

    [Fact]
    public void Errors_ReturnExitCodeOne()
    {
        Assert.Equal(1, Run("", "run", "NONE").Code);
        Assert.Equal(1, Run("", "hdd", "import", "X", Path.Combine(Programs, "missing.txt")).Code);

        Import("HELO", "hello.txt");
        var tooMany = Run("", "run", "HELO", "HELO", "HELO", "HELO", "HELO");
        Assert.Equal(1, tooMany.Code);
        Assert.NotEmpty(tooMany.Error);
    }

    [Fact]
    public void UnknownCommand_PrintsUsage()
    {
        var result = Run("", "hello");

        Assert.Equal(0, result.Code);
        Assert.Equal(Lines(Cli.Usage), result.Output);
    }
}
