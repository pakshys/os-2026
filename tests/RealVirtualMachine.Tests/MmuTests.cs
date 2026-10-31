namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Hardware;
using Xunit;

public class MmuTests
{
    private const int UserBlocks = 64;
    private const int SupervisorBlocks = 16;

    private readonly Cpu _cpu = new();
    private readonly Memory _supervisorMemory = new(SupervisorBlocks);
    private readonly Memory _userMemory = new(UserBlocks);
    private readonly Mmu _mmu;

    public MmuTests()
    {
        _mmu = new Mmu(_cpu, _supervisorMemory, _userMemory);
    }

    private void SetPageTableEntry(uint ptr, int x, uint realBlock)
    {
        _supervisorMemory.WriteWord((int)ptr * Memory.WordsPerMemoryBlock + x, realBlock);
    }

    [Fact]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new Mmu(null!, _supervisorMemory, _userMemory));
        Assert.Throws<ArgumentNullException>(() => new Mmu(_cpu, null!, _userMemory));
        Assert.Throws<ArgumentNullException>(() => new Mmu(_cpu, _supervisorMemory, null!));
    }

    [Fact]
    public void TryTranslate_SpecExample_ReturnsExpectedRealAddress()
    {
        _cpu.PTR = 3;
        SetPageTableEntry(3, 2, 0x1A);

        Assert.True(_mmu.TryTranslate(0x25, out int real));
        Assert.Equal(0x1A5, real);
    }

    [Theory]
    [InlineData(0x30, 0x2A * 16)]
    [InlineData(0x3F, 0x2A * 16 + 15)]
    public void TryTranslate_OffsetWithinBlock_IsPreserved(int virtualAddress, int expected)
    {
        _cpu.PTR = 1;
        SetPageTableEntry(1, 3, 0x2A);

        Assert.True(_mmu.TryTranslate(virtualAddress, out int real));
        Assert.Equal(expected, real);
    }

    [Fact]
    public void TryTranslate_AllVmBlocks_MapIndependently()
    {
        _cpu.PTR = 2;
        for (int x = 0; x < 16; x++)
            SetPageTableEntry(2, x, (uint)(63 - x));

        for (int x = 0; x < 16; x++)
        {
            Assert.True(_mmu.TryTranslate(x * 16 + 7, out int real));
            Assert.Equal((63 - x) * 16 + 7, real);
        }
    }

    [Fact]
    public void TryTranslate_DifferentPtr_UsesDifferentPageTables()
    {
        SetPageTableEntry(0, 5, 10);
        SetPageTableEntry(15, 5, 20);

        _cpu.PTR = 0;
        Assert.True(_mmu.TryTranslate(0x50, out int first));
        Assert.Equal(10 * 16, first);

        _cpu.PTR = 15;
        Assert.True(_mmu.TryTranslate(0x50, out int second));
        Assert.Equal(20 * 16, second);
    }

    [Theory]
    [InlineData(0x00)]
    [InlineData(0xFF)]
    public void TryTranslate_ValidVirtualAddressBounds_ReturnsTrue(int virtualAddress)
    {
        _cpu.PTR = 0;
        for (int x = 0; x < 16; x++)
            SetPageTableEntry(0, x, (uint)x);

        Assert.True(_mmu.TryTranslate(virtualAddress, out _));
    }

    [Theory]
    [InlineData(0x100)]
    [InlineData(0x1000)]
    [InlineData(-1)]
    public void TryTranslate_VirtualAddressOutOfRange_ReturnsFalse(int virtualAddress)
    {
        Assert.False(_mmu.TryTranslate(virtualAddress, out int real));
        Assert.Equal(0, real);
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(63u)]
    public void TryTranslate_ValidRealBlock_ReturnsTrue(uint realBlock)
    {
        _cpu.PTR = 0;
        SetPageTableEntry(0, 0, realBlock);

        Assert.True(_mmu.TryTranslate(0x01, out int real));
        Assert.Equal((int)realBlock * 16 + 1, real);
    }

    [Theory]
    [InlineData(64u)]
    [InlineData(1000u)]
    [InlineData(0xFFFFFFFFu)]
    public void TryTranslate_InvalidRealBlock_ReturnsFalse(uint realBlock)
    {
        _cpu.PTR = 0;
        SetPageTableEntry(0, 0, realBlock);

        Assert.False(_mmu.TryTranslate(0x00, out _));
    }

    [Theory]
    [InlineData(16u)]
    [InlineData(1000u)]
    [InlineData(0xFFFFFFFFu)]
    public void TryTranslate_PtrOutOfRange_ReturnsFalseWithoutThrowing(uint ptr)
    {
        _cpu.PTR = ptr;

        Assert.False(_mmu.TryTranslate(0x00, out _));
    }

    [Fact]
    public void TryWriteWord_ThenTryReadWord_ReturnsSameValue()
    {
        _cpu.PTR = 4;
        SetPageTableEntry(4, 2, 0x1A);

        Assert.True(_mmu.TryWriteWord(0x25, 0xDEADBEEF));

        Assert.True(_mmu.TryReadWord(0x25, out uint word));
        Assert.Equal(0xDEADBEEFu, word);
    }

    [Fact]
    public void TryWriteWord_WritesToTranslatedRealAddress()
    {
        _cpu.PTR = 3;
        SetPageTableEntry(3, 2, 0x1A);

        _mmu.TryWriteWord(0x25, 42);

        Assert.Equal(42u, _userMemory.ReadWord(0x1A5));
    }

    [Fact]
    public void TryReadWord_ReadsFromTranslatedRealAddress()
    {
        _cpu.PTR = 3;
        SetPageTableEntry(3, 2, 0x1A);
        _userMemory.WriteWord(0x1A5, 77);

        Assert.True(_mmu.TryReadWord(0x25, out uint word));
        Assert.Equal(77u, word);
    }

    [Fact]
    public void TryReadWord_InvalidAddress_ReturnsFalseAndZero()
    {
        Assert.False(_mmu.TryReadWord(0x100, out uint word));
        Assert.Equal(0u, word);
    }

    [Fact]
    public void TryWriteWord_InvalidAddress_ReturnsFalseAndChangesNothing()
    {
        _cpu.PTR = 0;
        SetPageTableEntry(0, 0, 64); // invalid real block

        Assert.False(_mmu.TryWriteWord(0x00, 99));

        for (int i = 0; i < _userMemory.WordCount; i++)
            Assert.Equal(0u, _userMemory.ReadWord(i));
    }
}