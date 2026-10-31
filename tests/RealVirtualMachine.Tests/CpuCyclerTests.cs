namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using Xunit;

public class CpuCyclerTests : ExecutorTestBase
{
    private const ushort CodeStart = 0x80;

    private readonly CpuCycler _cycler;

    public CpuCyclerTests()
    {
        _cycler = new CpuCycler(_cpu, _mmu, _executor);
    }

    private void Load(params uint[] words)
    {
        for (int i = 0; i < words.Length; i++)
            _userMemory.WriteWord(RealAddress(CodeStart + i), words[i]);

        _cpu.IC = CodeStart;
    }

    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new CpuCycler(null!, _mmu, _executor));
        Assert.Throws<ArgumentNullException>(() => new CpuCycler(_cpu, null!, _executor));
        Assert.Throws<ArgumentNullException>(() => new CpuCycler(_cpu, _mmu, null!));
    }

    [Fact]
    public void Step_Mov_ExecutesAndAdvancesIc()
    {
        Load(0x1005);   // MOV AX, 5

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Continue, result);
        Assert.Equal(5, _cpu.AX);
        Assert.Equal(CodeStart + 1, _cpu.IC);
        Assert.Equal((byte)0, _cpu.PI);
        Assert.Equal((byte)0, _cpu.SI);
    }

    [Fact]
    public void Step_ShortProgram_ComputesSum()
    {
        Load(0x1002, 0x1103, 0x4010);   // MOV AX,2; MOV BX,3; ADD AX,BX

        _cycler.Step();
        _cycler.Step();
        _cycler.Step();

        Assert.Equal(5, _cpu.AX);
        Assert.Equal(CodeStart + 3, _cpu.IC);
    }

    [Fact]
    public void Step_LwAndSw_WorkThroughPageTable()
    {
        _userMemory.WriteWord(RealAddress(0x05), 42);
        Load(0x2005, 0x3106);   // LW AX, 05; SW 06, BX
        _cpu.BX = 7;

        _cycler.Step();
        _cycler.Step();

        Assert.Equal(42, _cpu.AX);
        Assert.Equal(7u, _userMemory.ReadWord(RealAddress(0x06)));
    }

    [Fact]
    public void Step_JmpTaken_SetsIcToTarget()
    {
        Load(0x9090);   // JMP 90

        _cycler.Step();

        Assert.Equal(0x90, _cpu.IC);
    }

    [Fact]
    public void Step_LoopWithCmpAndJl_RunsUntilHalt()
    {
        Load(
            0x1000,   // 80: MOV AX, 0
            0x1103,   // 81: MOV BX, 3
            0x1201,   // 82: MOV CX, 1
            0x4020,   // 83: ADD AX, CX
            0x8010,   // 84: CMP AX, BX
            0xC083,   // 85: JL 83
            0xF000);  // 86: HALT

        int steps = 0;
        while (_cpu.SI != (byte)SupervisorInterrupt.Stop && steps < 100)
        {
            _cycler.Step();
            steps++;
        }

        Assert.Equal((byte)SupervisorInterrupt.Stop, _cpu.SI);
        Assert.Equal(3, _cpu.AX);
        Assert.Equal(3 + 3 * 3 + 1, steps);   // 3 MOV + 3 loops of (ADD, CMP, JL) + HALT
    }

    [Fact]
    public void Step_InvalidOpcode_SetsPi2AndInterrupts()
    {
        Load(0x0000);

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Interrupt, result);
        Assert.Equal((byte)ProgramInterrupt.InvalidOpcode, _cpu.PI);
        Assert.Equal((byte)CpuMode.Supervisor, _cpu.Mode);
        Assert.Equal(CodeStart + 1, _cpu.IC);   // IC already incremented: faulting instruction is IC - 1
    }

    [Fact]
    public void Step_InvalidRegisterCode_SetsPi2()
    {
        Load(0x1405);   // MOV with register 4

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)ProgramInterrupt.InvalidOpcode, _cpu.PI);
    }

    [Fact]
    public void Step_FetchFromInvalidMapping_SetsPi1AndDoesNotAdvanceIc()
    {
        _supervisorMemory.WriteWord(8, 64);   // VM block 8, invalid real block
        _cpu.IC = CodeStart;

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Interrupt, result);
        Assert.Equal((byte)ProgramInterrupt.InvalidAddress, _cpu.PI);
        Assert.Equal(CodeStart, _cpu.IC);
    }

    [Fact]
    public void Step_IcPastLastAddress_SetsPi1()
    {
        _cpu.IC = 0x100;

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)ProgramInterrupt.InvalidAddress, _cpu.PI);
    }

    [Fact]
    public void Step_ExecutingLastAddress_NextStepFailsWithPi1()
    {
        _userMemory.WriteWord(RealAddress(0xFF), 0x1001);   // MOV AX, 1
        _cpu.IC = 0xFF;

        Assert.Equal(CpuStepResult.Continue, _cycler.Step());
        Assert.Equal(0x100, _cpu.IC);

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)ProgramInterrupt.InvalidAddress, _cpu.PI);
    }

    [Fact]
    public void Step_DivByZero_SetsPi3AndKeepsRegisters()
    {
        Load(0x7010);   // DIV AX, BX
        _cpu.AX = 10;
        _cpu.BX = 0;

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Interrupt, result);
        Assert.Equal((byte)ProgramInterrupt.DivisionByZero, _cpu.PI);
        Assert.Equal(10, _cpu.AX);
        Assert.Equal(0, _cpu.BX);
    }

    [Fact]
    public void Step_ArithmeticOverflow_SetsPi4()
    {
        Load(0x4010);   // ADD AX, BX
        _cpu.AX = int.MaxValue;
        _cpu.BX = 1;

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)ProgramInterrupt.Overflow, _cpu.PI);
    }

    [Fact]
    public void Step_LwWithInvalidMapping_SetsPi1()
    {
        _supervisorMemory.WriteWord(3, 64);
        Load(0x2030);   // LW AX, 30

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)ProgramInterrupt.InvalidAddress, _cpu.PI);
    }

    [Theory]
    [InlineData(0xD010u, SupervisorInterrupt.Input)]
    [InlineData(0xE010u, SupervisorInterrupt.Output)]
    [InlineData(0xF000u, SupervisorInterrupt.Stop)]
    public void Step_GdPdHalt_SetSiAndSwitchToSupervisor(uint word, SupervisorInterrupt expected)
    {
        Load(word);
        _cpu.Mode = (byte)CpuMode.User;

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Interrupt, result);
        Assert.Equal((byte)expected, _cpu.SI);
        Assert.Equal((byte)0, _cpu.PI);
        Assert.Equal((byte)CpuMode.Supervisor, _cpu.Mode);
        Assert.Equal(CodeStart + 1, _cpu.IC);   // already past the instruction
    }

    [Fact]
    public void Step_ClearsPiAndSiFromPreviousStep()
    {
        Load(0xF000, 0x1001);   // HALT; MOV AX, 1

        _cycler.Step();
        Assert.Equal((byte)SupervisorInterrupt.Stop, _cpu.SI);

        var result = _cycler.Step();

        Assert.Equal(CpuStepResult.Continue, result);
        Assert.Equal((byte)0, _cpu.SI);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void Step_TimerReachesZero_InterruptsWithoutPiOrSi()
    {
        Load(0x1001, 0x1002, 0x1003);
        _cpu.TI = 2;
        _cpu.Mode = (byte)CpuMode.User;

        Assert.Equal(CpuStepResult.Continue, _cycler.Step());
        Assert.Equal((byte)1, _cpu.TI);
        Assert.Equal((byte)CpuMode.User, _cpu.Mode);

        Assert.Equal(CpuStepResult.Interrupt, _cycler.Step());
        Assert.Equal((byte)0, _cpu.TI);
        Assert.Equal((byte)0, _cpu.PI);
        Assert.Equal((byte)0, _cpu.SI);
        Assert.Equal((byte)CpuMode.Supervisor, _cpu.Mode);
    }

    [Fact]
    public void Step_TimerZero_NeverInterrupts()
    {
        Load(0x1001, 0x1002, 0x1003);
        _cpu.TI = 0;

        Assert.Equal(CpuStepResult.Continue, _cycler.Step());
        Assert.Equal(CpuStepResult.Continue, _cycler.Step());
        Assert.Equal(CpuStepResult.Continue, _cycler.Step());
        Assert.Equal((byte)0, _cpu.TI);
    }

    [Fact]
    public void Step_NoInterrupt_LeavesModeUnchanged()
    {
        Load(0x1001);
        _cpu.Mode = (byte)CpuMode.User;

        _cycler.Step();

        Assert.Equal((byte)CpuMode.User, _cpu.Mode);
    }
}