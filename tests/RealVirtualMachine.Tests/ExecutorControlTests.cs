namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Instructions;
using Xunit;

public class ExecutorControlTests : ExecutorTestBase
{
    private const ushort StartIc = 0x81;
    private const byte Target = 0x90;

    private void Run(Opcode opcode, bool zero = false, bool sign = false, bool overflow = false)
    {
        _cpu.IC = StartIc;
        _cpu.Zero = zero;
        _cpu.Sign = sign;
        _cpu.Overflow = overflow;
        _executor.ExecuteControl(new Instruction(opcode, Address: Target));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public void ExecuteControl_Jmp_AlwaysJumps(bool zero, bool sign, bool overflow)
    {
        Run(Opcode.JMP, zero, sign, overflow);

        Assert.Equal(Target, _cpu.IC);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]
    public void ExecuteControl_Je_JumpsOnlyWhenZero(bool zero, bool sign, bool overflow, bool jumps)
    {
        Run(Opcode.JE, zero, sign, overflow);

        Assert.Equal(jumps ? Target : StartIc, _cpu.IC);
    }

    [Theory]
    [InlineData(false, true, false, true)]    // plain negative result
    [InlineData(false, false, true, true)]    // overflowed positive-looking result
    [InlineData(false, false, false, false)]
    [InlineData(false, true, true, false)]    // overflow flipped the sign: not less
    public void ExecuteControl_Jl_JumpsWhenSignDiffersFromOverflow(bool zero, bool sign, bool overflow, bool jumps)
    {
        Run(Opcode.JL, zero, sign, overflow);

        Assert.Equal(jumps ? Target : StartIc, _cpu.IC);
    }

    [Theory]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, true, true)]     // MaxValue vs -1 case
    [InlineData(true, false, false, false)]   // equal: not greater
    [InlineData(false, true, false, false)]
    [InlineData(false, false, true, false)]
    public void ExecuteControl_Jg_JumpsWhenNotZeroAndSignEqualsOverflow(bool zero, bool sign, bool overflow, bool jumps)
    {
        Run(Opcode.JG, zero, sign, overflow);

        Assert.Equal(jumps ? Target : StartIc, _cpu.IC);
    }

    [Theory]
    [InlineData(Opcode.JMP)]
    [InlineData(Opcode.JE)]
    [InlineData(Opcode.JL)]
    [InlineData(Opcode.JG)]
    public void ExecuteControl_Jumps_DoNotChangeFlagsRegistersOrPi(Opcode opcode)
    {
        _cpu.AX = 5;
        _cpu.Zero = true;
        _cpu.Carry = true;
        byte flagsBefore = _cpu.Flags;

        _executor.ExecuteControl(new Instruction(opcode, Address: Target));

        Assert.Equal(flagsBefore, _cpu.Flags);
        Assert.Equal(5, _cpu.AX);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Theory]
    [InlineData(0x80)]
    [InlineData(0xFF)]
    [InlineData(0x00)]
    public void ExecuteControl_Jmp_SetsIcToAnyAddress(int address)
    {
        _executor.ExecuteControl(new Instruction(Opcode.JMP, Address: (byte)address));

        Assert.Equal(address, _cpu.IC);
    }

    [Theory]
    [InlineData(Opcode.MOV)]
    [InlineData(Opcode.ADD)]
    [InlineData(Opcode.GD)]
    [InlineData(Opcode.HALT)]
    public void ExecuteControl_WrongOpcode_ThrowsArgumentException(Opcode opcode)
    {
        Assert.Throws<ArgumentException>(() => _executor.ExecuteControl(new Instruction(opcode)));
    }

    [Theory]
    [InlineData(3, 5, Opcode.JL, true)]
    [InlineData(5, 3, Opcode.JL, false)]
    [InlineData(5, 3, Opcode.JG, true)]
    [InlineData(3, 5, Opcode.JG, false)]
    [InlineData(4, 4, Opcode.JE, true)]
    [InlineData(4, 4, Opcode.JG, false)]
    [InlineData(4, 4, Opcode.JL, false)]
    [InlineData(int.MaxValue, -1, Opcode.JG, true)]
    [InlineData(int.MaxValue, -1, Opcode.JL, false)]
    [InlineData(int.MinValue, 1, Opcode.JL, true)]
    [InlineData(int.MinValue, 1, Opcode.JG, false)]
    public void ExecuteControl_AfterCmp_FollowsSignedComparison(int a, int b, Opcode jump, bool taken)
    {
        _cpu.AX = a;
        _cpu.BX = b;
        _cpu.IC = StartIc;

        _executor.ExecuteArithmetic(new Instruction(Opcode.CMP, Register1: 0, Register2: 1));
        _executor.ExecuteControl(new Instruction(jump, Address: Target));

        Assert.Equal(taken ? Target : StartIc, _cpu.IC);
    }
}