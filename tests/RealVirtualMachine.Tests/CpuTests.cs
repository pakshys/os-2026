namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using Xunit;

public class CpuTests
{
    [Fact]
    public void Cpu_AfterCreation_ModeIsSupervisorEverythingElseZero()
    {
        var cpu = new Cpu();

        Assert.Equal((byte)CpuMode.Supervisor, cpu.Mode);
        Assert.Equal(0, cpu.AX);
        Assert.Equal(0, cpu.BX);
        Assert.Equal(0, cpu.CX);
        Assert.Equal(0, cpu.DX);
        Assert.Equal(0, cpu.Flags);
        Assert.Equal(0u, cpu.PTR);
        Assert.Equal(0, cpu.IC);
        Assert.Equal(0, cpu.SI);
        Assert.Equal(0, cpu.PI);
        Assert.Equal(0, cpu.TI);
    }

    [Fact]
    public void GetRegisterValue_ValidRegisterCode_ReturnsValue()
    {
        var cpu = new Cpu();

        cpu.AX = 17;

        Assert.Equal(17, cpu.GetRegisterValue((int)Register.AX));
    }

    [Fact]
    public void GetRegisterValue_InvalidRegisterCode_ThrowsArgumentOutOfRangeException()
    {
        var cpu = new Cpu();

        Assert.Throws<ArgumentOutOfRangeException>(() => cpu.GetRegisterValue(5));
    }

    [Fact]
    public void SetRegisterValue_ValidRegisterCode_SetsValue()
    {
        var cpu = new Cpu();

        cpu.SetRegisterValue((int)Register.DX, 156);

        Assert.Equal(156, cpu.GetRegisterValue((int)Register.DX));
    }

    [Fact]
    public void SetRegisterValue_InvalidRegisterCode_ThrowsArgumentOutOfRangeException()
    {
        var cpu = new Cpu();

        Assert.Throws<ArgumentOutOfRangeException>(() => cpu.SetRegisterValue(-1, 120));
    }

    [Fact]
    public void ResetRegisters_WithSetValues_ResetsToSupervisorModeAndEverythingElseZero()
    {
        var cpu = new Cpu();
        cpu.AX = 6;
        cpu.Overflow = true;
        cpu.PTR = 7;
        cpu.IC = 0x80;
        cpu.SI = 1;
        cpu.PI = 2;
        cpu.Mode = (byte)CpuMode.User;

        cpu.ResetRegisters();

        Assert.Equal(0, cpu.AX);
        Assert.Equal(0, cpu.Flags);
        Assert.Equal(0u, cpu.PTR);
        Assert.Equal(0, cpu.IC);
        Assert.Equal(0, cpu.SI);
        Assert.Equal(0, cpu.PI);
        Assert.Equal((byte)CpuMode.Supervisor, cpu.Mode);
    }

    [Fact]
    public void SetFlag_OneFlag_DoesNotChangeOthers()
    {
        var cpu = new Cpu();

        cpu.Zero = true;

        Assert.True(cpu.Zero);
        Assert.False(cpu.Sign);
        Assert.False(cpu.Carry);
        Assert.False(cpu.Overflow);
        Assert.Equal(0b0001, cpu.Flags);
    }

    [Fact]
    public void SetFlag_MultipleFlags_RetainsExpectedBitPattern()
    {
        var cpu = new Cpu();

        cpu.Zero = true;
        cpu.Overflow = true;

        Assert.Equal(0b1001, cpu.Flags);
    }

    [Fact]
    public void SetFlag_DifferentValue_ChangesValue()
    {
        var cpu = new Cpu();

        cpu.Zero = true;
        cpu.Sign = true;

        cpu.Zero = false;

        Assert.False(cpu.Zero);
        Assert.True(cpu.Sign);
        Assert.Equal(0b0010, cpu.Flags);
    }

    [Fact]
    public void ResetFlags_WithValues_ResetsToZero()
    {
        var cpu = new Cpu();

        cpu.Zero = true;
        cpu.Sign = true;

        cpu.ResetFlags();

        Assert.False(cpu.Zero);
        Assert.False(cpu.Sign);
        Assert.Equal(0, cpu.Flags);
    }
}