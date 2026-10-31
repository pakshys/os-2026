namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Instructions;
using Xunit;

public class ExecutorArithmeticTests : ExecutorTestBase
{
    private void Run(Opcode opcode, int a, int b)
    {
        _cpu.AX = a;
        _cpu.BX = b;
        _executor.ExecuteArithmetic(new Instruction(opcode, Register1: (int)Register.AX, Register2: (int)Register.BX));
    }

    [Theory]
    [InlineData(Opcode.ADD, 2, 3, 5, false, false)]
    [InlineData(Opcode.ADD, -2, -3, -5, false, true)]
    [InlineData(Opcode.ADD, 5, -5, 0, true, false)]
    [InlineData(Opcode.SUB, 5, 3, 2, false, false)]
    [InlineData(Opcode.SUB, 3, 5, -2, false, true)]
    [InlineData(Opcode.SUB, 5, 5, 0, true, false)]
    [InlineData(Opcode.MUL, 4, 3, 12, false, false)]
    [InlineData(Opcode.MUL, -4, 3, -12, false, true)]
    [InlineData(Opcode.MUL, 0, 7, 0, true, false)]
    public void ExecuteArithmetic_AddSubMulNormalCases_StoreResultAndSetZeroSign(
        Opcode opcode, int a, int b, int expected, bool zero, bool sign)
    {
        Run(opcode, a, b);

        Assert.Equal(expected, _cpu.AX);
        Assert.Equal(b, _cpu.BX);
        Assert.Equal(zero, _cpu.Zero);
        Assert.Equal(sign, _cpu.Sign);
        Assert.False(_cpu.Overflow);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Theory]
    [InlineData(Opcode.ADD, int.MaxValue, 1)]
    [InlineData(Opcode.ADD, int.MinValue, -1)]
    [InlineData(Opcode.SUB, int.MinValue, 1)]
    [InlineData(Opcode.SUB, int.MaxValue, -1)]
    [InlineData(Opcode.MUL, 0x10000, 0x10000)]
    [InlineData(Opcode.MUL, int.MinValue, -1)]
    public void ExecuteArithmetic_AddSubMulOverflow_SetsPi4AndOverflowFlagAndKeepsRegister(Opcode opcode, int a, int b)
    {
        Run(opcode, a, b);

        Assert.Equal(Pi(ProgramInterrupt.Overflow), _cpu.PI);
        Assert.True(_cpu.Overflow);
        Assert.Equal(a, _cpu.AX);
        Assert.Equal(b, _cpu.BX);
    }

    [Fact]
    public void ExecuteArithmetic_MulOverflow_SetsCarryTooButNormalMulClearsIt()
    {
        Run(Opcode.MUL, 0x10000, 0x10000);
        Assert.True(_cpu.Carry);

        _cpu.PI = 0;
        Run(Opcode.MUL, 3, 4);
        Assert.False(_cpu.Carry);
    }

    [Fact]
    public void ExecuteArithmetic_AddUnsignedCarry_SetsCarryWithoutOverflow()
    {
        Run(Opcode.ADD, -1, 1);   // 0xFFFFFFFF + 1

        Assert.Equal(0, _cpu.AX);
        Assert.True(_cpu.Carry);
        Assert.False(_cpu.Overflow);
        Assert.True(_cpu.Zero);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void ExecuteArithmetic_SubBorrow_SetsCarry()
    {
        Run(Opcode.SUB, 0, 1);

        Assert.Equal(-1, _cpu.AX);
        Assert.True(_cpu.Carry);
        Assert.True(_cpu.Sign);
        Assert.False(_cpu.Overflow);
    }

    [Fact]
    public void ExecuteArithmetic_SubNoBorrow_ClearsCarry()
    {
        _cpu.Carry = true;

        Run(Opcode.SUB, 5, 3);

        Assert.False(_cpu.Carry);
    }

    [Fact]
    public void ExecuteArithmetic_AddSameRegisterBothOperands_Doubles()
    {
        _cpu.AX = 21;

        _executor.ExecuteArithmetic(new Instruction(Opcode.ADD, Register1: 0, Register2: 0));

        Assert.Equal(42, _cpu.AX);
    }

    [Fact]
    public void ExecuteArithmetic_SubSameRegisterBothOperands_GivesZero()
    {
        _cpu.AX = 21;

        _executor.ExecuteArithmetic(new Instruction(Opcode.SUB, Register1: 0, Register2: 0));

        Assert.Equal(0, _cpu.AX);
        Assert.True(_cpu.Zero);
    }

    [Fact]
    public void ExecuteArithmetic_Add_UsesRegisterCodesFromInstruction()
    {
        _cpu.CX = 10;
        _cpu.DX = 20;

        _executor.ExecuteArithmetic(new Instruction(Opcode.ADD, Register1: 2, Register2: 3));

        Assert.Equal(30, _cpu.CX);
        Assert.Equal(20, _cpu.DX);
        Assert.Equal(0, _cpu.AX);
    }

    [Fact]
    public void ExecuteArithmetic_CmpEqual_SetsZeroAndKeepsRegisters()
    {
        Run(Opcode.CMP, 5, 5);

        Assert.True(_cpu.Zero);
        Assert.False(_cpu.Sign);
        Assert.Equal(5, _cpu.AX);
        Assert.Equal(5, _cpu.BX);
    }

    [Fact]
    public void ExecuteArithmetic_CmpFirstSmaller_SignDiffersFromOverflow()
    {
        Run(Opcode.CMP, 3, 5);   // JL condition: SIGN != OVERFLOW

        Assert.False(_cpu.Zero);
        Assert.NotEqual(_cpu.Sign, _cpu.Overflow);
    }

    [Fact]
    public void ExecuteArithmetic_CmpFirstGreater_NotZeroAndSignEqualsOverflow()
    {
        Run(Opcode.CMP, 5, 3);   // JG condition: ZERO = 0 and SIGN = OVERFLOW

        Assert.False(_cpu.Zero);
        Assert.Equal(_cpu.Sign, _cpu.Overflow);
    }

    [Fact]
    public void ExecuteArithmetic_CmpOverflowingDifference_DoesNotRaisePiAndStillGivesCorrectOrdering()
    {
        Run(Opcode.CMP, int.MaxValue, -1);   // MaxValue > -1, but the difference overflows

        Assert.Equal((byte)0, _cpu.PI);
        Assert.True(_cpu.Overflow);
        Assert.True(_cpu.Sign);
        Assert.False(_cpu.Zero);
        // JG condition holds: ZERO = 0 and SIGN = OVERFLOW
        Assert.Equal(_cpu.Sign, _cpu.Overflow);
        Assert.Equal(int.MaxValue, _cpu.AX);
        Assert.Equal(-1, _cpu.BX);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(int.MinValue, 1)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void ExecuteArithmetic_Cmp_NeverChangesRegistersOrRaisesPi(int a, int b)
    {
        Run(Opcode.CMP, a, b);

        Assert.Equal(a, _cpu.AX);
        Assert.Equal(b, _cpu.BX);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Theory]
    [InlineData(7, 2, 3, 1)]
    [InlineData(-7, 2, -3, -1)]
    [InlineData(7, -2, -3, 1)]
    [InlineData(-7, -2, 3, -1)]
    [InlineData(6, 3, 2, 0)]
    public void ExecuteArithmetic_Div_StoresQuotientInOp1AndRemainderInOp2(int a, int b, int quotient, int remainder)
    {
        Run(Opcode.DIV, a, b);

        Assert.Equal(quotient, _cpu.AX);
        Assert.Equal(remainder, _cpu.BX);
        Assert.Equal((byte)0, _cpu.PI);
    }

    [Fact]
    public void ExecuteArithmetic_DivQuotientZero_SetsZeroFlag()
    {
        Run(Opcode.DIV, 1, 2);

        Assert.Equal(0, _cpu.AX);
        Assert.Equal(1, _cpu.BX);
        Assert.True(_cpu.Zero);
    }

    [Fact]
    public void ExecuteArithmetic_DivNegativeQuotient_SetsSignFlag()
    {
        Run(Opcode.DIV, -9, 2);

        Assert.True(_cpu.Sign);
        Assert.False(_cpu.Zero);
    }

    [Fact]
    public void ExecuteArithmetic_Div_ClearsCarryAndOverflow()
    {
        _cpu.Carry = true;
        _cpu.Overflow = true;

        Run(Opcode.DIV, 10, 3);

        Assert.False(_cpu.Carry);
        Assert.False(_cpu.Overflow);
    }

    [Fact]
    public void ExecuteArithmetic_DivByZero_SetsPi3AndChangesNothing()
    {
        _cpu.Carry = true;
        _cpu.Sign = true;
        byte flagsBefore = _cpu.Flags;

        Run(Opcode.DIV, 10, 0);

        Assert.Equal(Pi(ProgramInterrupt.DivisionByZero), _cpu.PI);
        Assert.Equal(10, _cpu.AX);
        Assert.Equal(0, _cpu.BX);
        Assert.Equal(flagsBefore, _cpu.Flags);
    }

    [Fact]
    public void ExecuteArithmetic_DivMinValueByMinusOne_SetsPi4WithoutThrowing()
    {
        var exception = Record.Exception(() => Run(Opcode.DIV, int.MinValue, -1));

        Assert.Null(exception);
        Assert.Equal(Pi(ProgramInterrupt.Overflow), _cpu.PI);
        Assert.Equal(int.MinValue, _cpu.AX);
        Assert.Equal(-1, _cpu.BX);
        Assert.True(_cpu.Overflow);
    }

    [Fact]
    public void ExecuteArithmetic_DivSameRegisterBothOperands_RemainderWins()
    {
        _cpu.AX = 5;

        _executor.ExecuteArithmetic(new Instruction(Opcode.DIV, Register1: 0, Register2: 0));

        Assert.Equal(0, _cpu.AX);   // quotient 1 written first, then remainder 0
    }

    [Theory]
    [InlineData(Opcode.MOV)]
    [InlineData(Opcode.LW)]
    [InlineData(Opcode.JMP)]
    [InlineData(Opcode.HALT)]
    public void ExecuteArithmetic_WrongOpcode_ThrowsArgumentException(Opcode opcode)
    {
        Assert.Throws<ArgumentException>(() => _executor.ExecuteArithmetic(new Instruction(opcode)));
    }
}