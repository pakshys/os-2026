namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Hardware;
using RealVirtualMachine.Supervisor;
using static RealVirtualMachine.Supervisor.ProgramParser;

public sealed class KernelTests : IDisposable
{
    //each test gets its own disk image
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
    private readonly Memory _userMemory = new(64);
    private readonly Memory _supervisorMemory = new(16);
    private readonly StringWriter _screen = new();
    private readonly Hdd _hdd;
    private readonly VirtualMachineManager _manager;

    public KernelTests()
    {
        _hdd = new Hdd(_path);
        _manager = new VirtualMachineManager(_userMemory, _supervisorMemory, new Random(1234));
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    private Kernel CreateKernel(string keyboard = "")
    {
        var realCpu = new Cpu();
        var channel = new ChannelDevice(realCpu, _userMemory, _supervisorMemory, _hdd, new StringReader(keyboard), _screen);
        var loader = new ProgramLoader(realCpu, _supervisorMemory, _hdd, channel, _manager);
        return new Kernel(realCpu, channel, _manager, loader, _screen);
    }

    private void Write(string name, uint[] data, uint[] code) =>
        _hdd.WriteProgram(name, [AmjTag, 0x50415631, DatTag, .. data, CodTag, .. code, EndTag]);

    private void AssertAllMemoryFree()
    {
        Assert.Equal(0, _manager.ActiveVirtualMachineCount);
        Assert.Equal(64, _manager.FreeUserBlockCount);
        Assert.Equal(16, _manager.FreeSupervisorBlockCount);
    }

    [Fact]
    public void Halt_StopsVmAndFreesMemory()
    {
        Write("H", [], [0xF000]);
        var kernel = CreateKernel();
        kernel.Load("H");

        Assert.True(kernel.Run());

        Assert.Equal([new ProcessResult("H", true, null)], kernel.Finished);
        Assert.Equal(0, kernel.ReadyCount);
        AssertAllMemoryFree();
    }

    //PD 00; HALT
    [Fact]
    public void Output_PrintsDataBlockAndNewLine()
    {
        Write("HI", [0x48454C4C, 0x4F000000], [0xE000, 0xF000]);
        var kernel = CreateKernel();
        kernel.Load("HI");

        kernel.Run();

        Assert.Equal("HELLO" + Environment.NewLine, _screen.ToString());
    }

    //GD 1F; PD 10; HALT -> y is ignored, both use VM block 1
    [Fact]
    public void Input_ThenOutput_EchoesLine()
    {
        Write("ECHO", [], [0xD01F, 0xE010, 0xF000]);
        var kernel = CreateKernel("abc\n");
        kernel.Load("ECHO");

        Assert.True(kernel.Run());

        Assert.Equal("abc" + new string(' ', 61) + Environment.NewLine, _screen.ToString());
        Assert.True(kernel.Finished.Single().Halted);
    }

    //MOV AX,1; MOV BX,0; DIV AX,BX; HALT
    [Fact]
    public void DivisionByZero_StopsVmWithError()
    {
        Write("DIV0", [], [0x1001, 0x1100, 0x7010, 0xF000]);
        var kernel = CreateKernel();
        kernel.Load("DIV0");

        Assert.True(kernel.Run());

        Assert.Equal([new ProcessResult("DIV0", false, "division by zero")], kernel.Finished);
        Assert.Equal("DIV0: division by zero" + Environment.NewLine, _screen.ToString());
        AssertAllMemoryFree();
    }

    [Fact]
    public void InvalidInstruction_StopsVmWithError()
    {
        Write("BAD", [], [0x0000]);
        var kernel = CreateKernel();
        kernel.Load("BAD");

        kernel.Run();

        Assert.Equal("invalid instruction", kernel.Finished.Single().Error);
        AssertAllMemoryFree();
    }

    [Fact]
    public void NonAsciiKeyboardInput_StopsVmWithError()
    {
        Write("IN", [], [0xD000, 0xF000]);
        var kernel = CreateKernel("ąčę\n");
        kernel.Load("IN");

        kernel.Run();

        Assert.False(kernel.Finished.Single().Halted);
        AssertAllMemoryFree();
    }

    //JMP 80 forever: run stops at the limit, VM stays ready
    [Fact]
    public void InfiniteLoop_StopsAtInstructionLimit()
    {
        Write("LOOP", [], [0x9080]);
        var kernel = CreateKernel();
        kernel.Load("LOOP");

        Assert.False(kernel.Run(instructionLimit: 100));

        Assert.Equal(1, kernel.ReadyCount);
        Assert.Empty(kernel.Finished);
    }

    //timer switches VMs, so the second program finishes even though the first one loops
    [Fact]
    public void Timer_SwitchesBetweenVms()
    {
        Write("LOOP", [], [0x9080]);
        Write("HI", [0x48490000], [0xE000, 0xF000]);
        var kernel = CreateKernel();
        kernel.Load("LOOP");
        kernel.Load("HI");

        Assert.False(kernel.Run(instructionLimit: 1000));

        Assert.Equal("HI" + Environment.NewLine, _screen.ToString());
        Assert.Equal("HI", kernel.Finished.Single().Name);
        Assert.Equal(1, kernel.ReadyCount);
    }

    //first VM gets one full quantum before the second one starts
    [Fact]
    public void Timer_GivesEachVmOneQuantum()
    {
        var code = Enumerable.Repeat(0x1001u, 20).Append(0xF000u).ToArray(); // 20x MOV AX,1; HALT
        Write("A", [], code);
        Write("B", [], [0xF000]);
        var kernel = CreateKernel();
        kernel.Load("A");
        kernel.Load("B");

        Assert.False(kernel.Run(instructionLimit: Kernel.TimeQuantum + 1));

        Assert.Equal("B", kernel.Finished.Single().Name);
        Assert.True(kernel.Run());
        Assert.Equal(["B", "A"], kernel.Finished.Select(r => r.Name));
        AssertAllMemoryFree();
    }

    [Fact]
    public void FourPrograms_AllFinish()
    {
        Write("P", [0x4F4B0000], [0xE000, 0xF000]);
        var kernel = CreateKernel();
        for (int i = 0; i < 4; i++) kernel.Load("P");

        Assert.True(kernel.Run());

        Assert.Equal(4, kernel.Finished.Count(r => r.Halted));
        Assert.Equal(string.Concat(Enumerable.Repeat("OK" + Environment.NewLine, 4)), _screen.ToString());
        AssertAllMemoryFree();
    }

    [Fact]
    public void Load_MissingProgram_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => CreateKernel().Load("NONE"));
    }
}
