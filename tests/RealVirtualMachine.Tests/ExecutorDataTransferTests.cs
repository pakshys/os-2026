namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using RealVirtualMachine.Instructions;
using Xunit;

public class ExecutorDataTransferTests : ExecutorTestBase
{
    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Executor(null!, _mmu));
        Assert.Throws<ArgumentNullException>(() => new Executor(_cpu, null!));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(1, -1)]
    [InlineData(2, -128)]
    [InlineData(3, 127)]
    [InlineData(0, 0)]
    public void ExecuteDataTransfer_Mov_SetsRegisterToConstant(int register, int constant)
    {
        _executor.ExecuteDataTransfer(new Instruction(Opcode.MOV, Register1: register, Constant: constant));

        Assert.Equal(constant, _cpu.GetRegisterValue(register));
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void ExecuteDataTransfer_Mov_DoesNotChangeOtherRegisters()
    {
        _cpu.BX = 9;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.MOV, Register1: 0, Constant: 5));

        Assert.Equal(9, _cpu.BX);
    }

    [Fact]
    public void ExecuteDataTransfer_Mov_DoesNotChangeFlags()
    {
        _cpu.Zero = true;
        _cpu.Overflow = true;
        byte before = _cpu.Flags;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.MOV, Register1: 0, Constant: 5));

        Assert.Equal(before, _cpu.Flags);
    }

    [Fact]
    public void ExecuteDataTransfer_Lw_ReadsWordThroughPageTable()
    {
        _userMemory.WriteWord(RealAddress(0x25), 1234);

        _executor.ExecuteDataTransfer(new Instruction(Opcode.LW, Register1: 1, Address: 0x25));

        Assert.Equal(1234, _cpu.BX);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void ExecuteDataTransfer_LwWordWithTopBitSet_BecomesNegative()
    {
        _userMemory.WriteWord(RealAddress(0x10), 0xFFFFFFFF);

        _executor.ExecuteDataTransfer(new Instruction(Opcode.LW, Register1: 0, Address: 0x10));

        Assert.Equal(-1, _cpu.AX);
    }

    [Fact]
    public void ExecuteDataTransfer_Lw_DoesNotChangeFlags()
    {
        _userMemory.WriteWord(RealAddress(0x10), 0);
        _cpu.Sign = true;
        byte before = _cpu.Flags;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.LW, Register1: 0, Address: 0x10));

        Assert.Equal(before, _cpu.Flags);
    }

    [Fact]
    public void ExecuteDataTransfer_LwInvalidMapping_SetsPiAndLeavesRegisterUnchanged()
    {
        _supervisorMemory.WriteWord(3, 64);   // VM block 3 -> invalid real block
        _cpu.AX = 77;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.LW, Register1: 0, Address: 0x30));

        Assert.Equal(Pi(ProgramInterrupt.InvalidAddress), _cpu.PI);
        Assert.Equal(77, _cpu.AX);
    }

    [Fact]
    public void ExecuteDataTransfer_Sw_WritesRegisterToTranslatedAddress()
    {
        _cpu.CX = 4321;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.SW, Register1: 2, Address: 0x25));

        Assert.Equal(4321u, _userMemory.ReadWord(RealAddress(0x25)));
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void ExecuteDataTransfer_SwNegativeValue_StoredAsTwosComplementWord()
    {
        _cpu.AX = -1;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.SW, Register1: 0, Address: 0x00));

        Assert.Equal(0xFFFFFFFFu, _userMemory.ReadWord(RealAddress(0x00)));
    }

    [Fact]
    public void ExecuteDataTransfer_SwThenLw_RoundTripsNegativeValue()
    {
        _cpu.AX = int.MinValue;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.SW, Register1: 0, Address: 0x40));
        _executor.ExecuteDataTransfer(new Instruction(Opcode.LW, Register1: 1, Address: 0x40));

        Assert.Equal(int.MinValue, _cpu.BX);
    }

    [Fact]
    public void ExecuteDataTransfer_Sw_DoesNotChangeRegisterOrFlags()
    {
        _cpu.AX = 5;
        _cpu.Carry = true;
        byte before = _cpu.Flags;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.SW, Register1: 0, Address: 0x10));

        Assert.Equal(5, _cpu.AX);
        Assert.Equal(before, _cpu.Flags);
    }

    [Fact]
    public void ExecuteDataTransfer_SwInvalidMapping_SetsPiAndWritesNothing()
    {
        _supervisorMemory.WriteWord(3, 64);
        _cpu.AX = 99;

        _executor.ExecuteDataTransfer(new Instruction(Opcode.SW, Register1: 0, Address: 0x30));

        Assert.Equal(Pi(ProgramInterrupt.InvalidAddress), _cpu.PI);
        for (int i = 0; i < _userMemory.WordCount; i++)
            Assert.Equal(0u, _userMemory.ReadWord(i));
    }

    [Theory]
    [InlineData(Opcode.ADD)]
    [InlineData(Opcode.JMP)]
    [InlineData(Opcode.HALT)]
    public void ExecuteDataTransfer_WrongOpcode_ThrowsArgumentException(Opcode opcode)
    {
        Assert.Throws<ArgumentException>(() => _executor.ExecuteDataTransfer(new Instruction(opcode)));
    }
}