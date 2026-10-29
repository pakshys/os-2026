namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Hardware;
using Xunit;

public class MemoryTests
{
    private const int UserBlocks = 64;
    private const int SupervisorBlocks = 16;

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_InvalidBlockCount_ThrowsArgumentOutOfRangeException(int blocks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Memory(blocks));
    }

    [Theory]
    [InlineData(UserBlocks, 1024)]
    [InlineData(SupervisorBlocks, 256)]
    public void Constructor_ValidBlockAndWordCount_SetsBlockAndWordCount(int blocks, int words)
    {
        var memory = new Memory(blocks);

        Assert.Equal(blocks, memory.BlockCount);
        Assert.Equal(words, memory.WordCount);
    }

    [Fact]
    public void Constructor_ValidArguments_NewMemoryIsAllZeros()
    {
        var memory = new Memory(UserBlocks);

        Assert.Equal(0u, memory.ReadWord(0));
        Assert.Equal(0u, memory.ReadWord(memory.WordCount - 1));
    }

    [Theory]
    [InlineData(0, 0u)]
    [InlineData(5, 0x12345678u)]
    [InlineData(0x3FF, 0xFFFFFFFFu)]
    public void WriteWord_ThenReadWord_ReturnsSameValue(int address, uint value)
    {
        var memory = new Memory(UserBlocks);

        memory.WriteWord(address, value);

        Assert.Equal(value, memory.ReadWord(address));
    }

    [Theory]
    [InlineData(UserBlocks, 0x400)]
    [InlineData(UserBlocks, -1)]
    [InlineData(SupervisorBlocks, 0x100)]
    [InlineData(SupervisorBlocks, -1)]
    public void MemoryAccess_OutOfRange_ThrowsArgumentOutOfRangeException(int blocks, int address)
    {
        var memory = new Memory(blocks);

        Assert.Throws<ArgumentOutOfRangeException>(() => memory.ReadWord(address));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.WriteWord(address, 1));
    }

    [Fact]
    public void WriteBlock_ThenReadBlock_ReturnsSameWords()
    {
        var memory = new Memory(UserBlocks);
        var block = Enumerable.Range(1, Memory.WordsPerMemoryBlock).Select(i => (uint)i).ToArray();

        memory.WriteBlock(3, block);

        Assert.Equal(block, memory.ReadBlock(3));
    }

    [Theory]
    [InlineData(UserBlocks, 64)]
    [InlineData(UserBlocks, -1)]
    [InlineData(SupervisorBlocks, 16)]
    [InlineData(SupervisorBlocks, -1)]
    public void BlockAccess_OutOfRange_ThrowsArgumentOutOfRangeException(int blocks, int block)
    {
        var memory = new Memory(blocks);
        var data = new uint[Memory.WordsPerMemoryBlock];

        Assert.Throws<ArgumentOutOfRangeException>(() => memory.ReadBlock(block));
        Assert.Throws<ArgumentOutOfRangeException>(() => memory.WriteBlock(block, data));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(17)]
    public void WriteBlock_WrongLength_ThrowsArgumentException(int length)
    {
        var memory = new Memory(UserBlocks);

        Assert.Throws<ArgumentException>(() => memory.WriteBlock(0, new uint[length]));
    }

    [Fact]
    public void WriteBlock_Null_ThrowsArgumentNullException()
    {
        var memory = new Memory(UserBlocks);

        Assert.Throws<ArgumentNullException>(() => memory.WriteBlock(0, null));
    }

    [Fact]
    public void Clear_WithData_ZeroesEverything()
    {
        var memory = new Memory(UserBlocks);
        memory.WriteWord(0, 1);
        memory.WriteWord(memory.WordCount - 1, 2);

        memory.Clear();

        Assert.Equal(0u, memory.ReadWord(0));
        Assert.Equal(0u, memory.ReadWord(memory.WordCount - 1));
    }
}