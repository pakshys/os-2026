namespace RealVirtualMachine.Hardware;

public class Memory
{
    public const int WordsPerMemoryBlock = 16;

    private readonly uint[] _words;

    public int BlockCount { get; }
    public int WordCount => BlockCount * WordsPerMemoryBlock;

    public Memory(int blockCount)
    {
        if (blockCount <= 0) 
            throw new ArgumentOutOfRangeException(nameof(blockCount), "Invalid block count");

        BlockCount = blockCount;
        _words = new uint[blockCount * WordsPerMemoryBlock];
    }

    public uint ReadWord(int address)
    {
        CheckAddress(address);
        return _words[address];
    }

    public void WriteWord(int address, uint word)
    {
        CheckAddress(address);
        _words[address] = word;
    }

    public uint[] ReadBlock(int block)
    {
        CheckBlock(block);
        var result = new uint[WordsPerMemoryBlock];
        Array.Copy(_words, block * WordsPerMemoryBlock, result, 0, WordsPerMemoryBlock);
        return result;
    }

    public void WriteBlock(int block, uint[] words)
    {
        CheckBlock(block);
        if (words == null)
            throw new ArgumentNullException(nameof(words), "Invalid words");

        if (words.Length != WordsPerMemoryBlock)
            throw new ArgumentException($"A block has {WordsPerMemoryBlock} words", nameof(words));

        Array.Copy(words, 0, _words, block * WordsPerMemoryBlock, WordsPerMemoryBlock);
    }

    public void Clear() => Array.Clear(_words);

    private void CheckAddress(int address)
    {
        if (address < 0 || address >= WordCount)
            throw new ArgumentOutOfRangeException(nameof(address), "Invalid address");
    }

    private void CheckBlock(int block)
    {
        if (block < 0 || block >= BlockCount)
            throw new ArgumentOutOfRangeException(nameof(block), "Invalid block");
    }
}