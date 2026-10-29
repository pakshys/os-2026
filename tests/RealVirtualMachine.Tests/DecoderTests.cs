namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Instructions;
using Xunit;

public class DecoderTests
{
    [Theory]
    [InlineData(0x1005u, Register.AX, 5)]
    [InlineData(0x11FFu, Register.BX, -1)]
    [InlineData(0x1280u, Register.CX, -128)]
    [InlineData(0x137Fu, Register.DX, 127)]
    public void TryDecode_Mov_DecodesRegisterAndConstant(uint word, Register reg, int constant)
    {
        Assert.True(Decoder.TryDecode(word, out var instruction));
        Assert.Equal(Opcode.MOV, instruction.Opcode);
        Assert.Equal((int)reg, instruction.Register1);
        Assert.Equal(constant, instruction.Constant);
    }

    [Theory]
    [InlineData(0x2125u, Opcode.LW, Register.BX, 0x25)]
    [InlineData(0x3300u, Opcode.SW, Register.DX, 0x00)]
    [InlineData(0x20FFu, Opcode.LW, Register.AX, 0xFF)]
    public void TryDecode_LwSw_DecodesRegisterAndAddress(uint word, Opcode op, Register reg, int address)
    {
        Assert.True(Decoder.TryDecode(word, out var instruction));
        Assert.Equal(op, instruction.Opcode);
        Assert.Equal((int)reg, instruction.Register1);
        Assert.Equal(address, instruction.Address);
    }

    [Theory]
    [InlineData(0x4010u, Opcode.ADD, Register.AX, Register.BX)]
    [InlineData(0x5230u, Opcode.SUB, Register.CX, Register.DX)]
    [InlineData(0x6020u, Opcode.MUL, Register.AX, Register.CX)]
    [InlineData(0x7010u, Opcode.DIV, Register.AX, Register.BX)]
    [InlineData(0x8330u, Opcode.CMP, Register.DX, Register.DX)]
    public void TryDecode_Arithmetic_DecodesBothRegisters(uint word, Opcode op, Register reg1, Register reg2)
    {
        Assert.True(Decoder.TryDecode(word, out var instruction));
        Assert.Equal(op, instruction.Opcode);
        Assert.Equal((int)reg1, instruction.Register1);
        Assert.Equal((int)reg2, instruction.Register2);
    }

    [Theory]
    [InlineData(0x9080u, Opcode.JMP, 0x80)]
    [InlineData(0xA09Fu, Opcode.JE, 0x9F)]
    [InlineData(0xB090u, Opcode.JG, 0x90)]
    [InlineData(0xC085u, Opcode.JL, 0x85)]
    [InlineData(0xD010u, Opcode.GD, 0x10)]
    [InlineData(0xE010u, Opcode.PD, 0x10)]
    public void TryDecode_JumpsAndIO_DecodesAddress(uint word, Opcode op, int address)
    {
        Assert.True(Decoder.TryDecode(word, out var instruction));
        Assert.Equal(op, instruction.Opcode);
        Assert.Equal(address, instruction.Address);
    }

    [Fact]
    public void TryDecode_Halt_Decodes()
    {
        Assert.True(Decoder.TryDecode(0xF000u, out var instruction));
        Assert.Equal(Opcode.HALT, instruction.Opcode);
    }

    [Theory]
    [InlineData(0x0000u)]
    [InlineData(0x0005u)]
    [InlineData(0x1405u)]
    [InlineData(0x2405u)]
    [InlineData(0x4040u)]
    [InlineData(0x4004u)]
    [InlineData(0x4011u)]
    [InlineData(0x9180u)]
    [InlineData(0xD110u)]
    [InlineData(0xF001u)]
    [InlineData(0xF010u)]
    [InlineData(0xF100u)]
    [InlineData(0x10001005u)]
    [InlineData(0xFFFFFFFFu)]
    public void TryDecode_InvalidWord_ReturnsFalseAndDefaultInstruction(uint word)
    {
        Assert.False(Decoder.TryDecode(word, out var instruction));
        Assert.Equal(default, instruction);
    }
}