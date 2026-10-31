using System.Globalization;
using System.Text;

namespace RealVirtualMachine.Hardware;

public sealed record HddProgram(string Name, int StartBlock, int WordCount);

public sealed class Hdd
{
    public const int WordsPerMemoryBlock = 16;
    public const int BlockCount = 256;
    public const int WordCount = 4096;
    private readonly string _path;

    // Create an empty disk if missing / validate the file format.
    public Hdd(string path = "hdd.txt")
    {
        _path = Path.GetFullPath(path);
        if (!File.Exists(_path)) Save(new uint[WordCount]);
        Load();
    }

    // Accept eight hex digits, with no prefix or surrounding whitespace.
    public static uint ParseWord(string text)
    {
        if (text.Length != 8 || text.Any(c => !Uri.IsHexDigit(c)))
            throw new FormatException("Each word must contain exactly 8 hex digits.");
        return uint.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    // Read a fresh snapshot so  operations see saved changes.
    private uint[] Load()
    {
        var lines = File.ReadAllLines(_path);
        if (lines.Length != WordCount) throw new FormatException("HDD must contain exactly 4096 lines.");
        return lines.Select(ParseWord).ToArray();
    }

    // Finish writing a temporary file before replacing the disk image.
    private void Save(uint[] words)
    {
        var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllLines(temporary, words.Select(w => w.ToString("X8")), new UTF8Encoding(false));
            File.Move(temporary, _path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    // Convert zero-based block/word coordinates to the corresponding line index.
    private static int GetAddress(int block, int word)
    {
        if (block < 0 || block >= BlockCount) throw new ArgumentOutOfRangeException(nameof(block));
        if (word < 0 || word >= WordsPerMemoryBlock) throw new ArgumentOutOfRangeException(nameof(word));
        return block * WordsPerMemoryBlock + word;
    }

    // Raw access includes block 00, which contains the directory.
    public uint ReadWord(int block, int word) => Load()[GetAddress(block, word)];

    // Persist one raw word; callers writing block 00 must preserve valid metadata.
    public void WriteWord(int block, int word, uint value)
    {
        int address = GetAddress(block, word);
        var words = Load();
        words[address] = value;
        Save(words);
    }

    // Read a complete 16-word block from its zero-based block number.
    public uint[] ReadBlock(int block)
    {
        int start = GetAddress(block, 0);
        return Load().Skip(start).Take(WordsPerMemoryBlock).ToArray();
    }

    // Replace a complete 16-word block and persist the disk image once.
    public void WriteBlock(int block, uint[] words)
    {
        int start = GetAddress(block, 0);
        ArgumentNullException.ThrowIfNull(words);
        if (words.Length != WordsPerMemoryBlock)
            throw new ArgumentException($"A block has {WordsPerMemoryBlock} words", nameof(words));

        var diskWords = Load();
        Array.Copy(words, 0, diskWords, start, WordsPerMemoryBlock);
        Save(diskWords);
    }

    // Pack ASCII characters fromhighest byte down, trailing zero bytes.
    private static uint EncodeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length is < 1 or > 4 || name.Any(c => c < '!' || c > '~'))
            throw new ArgumentException("Names must be 1-4 printable ASCII characters without spaces.", nameof(name));
        uint value = 0;
        for (int i = 0; i < 4; i++) value = (value << 8) | (i < name.Length ? name[i] : 0u);
        return value;
    }

    // Decode the eight entries in block 00 and reject invalid or overlapping allocations.
    private static List<HddProgram> ReadDirectory(uint[] words)
    {
        var result = new List<HddProgram>();
        var occupied = new bool[BlockCount];
        for (int i = 0; i < 16; i += 2)
        {
            uint nameWord = words[i], location = words[i + 1];
            if (nameWord == 0 && location == 0) continue;
            var name = new string(Enumerable.Range(0, 4).Select(n => (char)((nameWord >> (24 - n * 8)) & 255)).ToArray()).TrimEnd('\0');
  
            int start = (int)(location >> 16), count = (int)(location & 65535);
            int blocks = (count + 15) / 16;
            if (name.Length == 0 || name.Any(c => c < '!' || c > '~') || start < 1 || count == 0 || start + blocks > BlockCount || result.Any(p => p.Name == name))
                throw new FormatException("Invalid HDD directory entry.");
            for (int b = start; b < start + blocks; b++)
            {
                if (occupied[b]) throw new FormatException("Overlapping HDD programs.");
                occupied[b] = true;
            }
            result.Add(new HddProgram(name, start, count));
        }
        return result;
    }

    // Return occupied directory entries in their on-disk order.
    public IReadOnlyList<HddProgram> ListPrograms() => ReadDirectory(Load());

    // Names are case-sensitive; a valid but absent name returns null.
    public HddProgram? FindProgram(string name)
    {
        EncodeName(name);
        return ListPrograms().FirstOrDefault(p => p.Name == name);
    }

    // Return program words without block padding; fail if the name is absent.
    public uint[] ReadProgram(string name)
    {
        EncodeName(name);
        var words = Load();
        var program = ReadDirectory(words).FirstOrDefault(p => p.Name == name)
            ?? throw new KeyNotFoundException($"Program '{name}' was not found.");
        return words.Skip(program.StartBlock * 16).Take(program.WordCount).ToArray();
    }

    // Add a new program; duplicate names are rejected rather than overwritten.
    public void WriteProgram(string name, uint[] program)
    {
        uint encodedName = EncodeName(name);
        ArgumentNullException.ThrowIfNull(program);
        if (program.Length is < 1 or > 4080) throw new ArgumentException("Programs must contain 1-4080 words", nameof(program));
        var words = Load();
        var directory = ReadDirectory(words);
        if (directory.Any(p => p.Name == name)) throw new InvalidOperationException("This name already exists");
        if (directory.Count == 8) throw new InvalidOperationException("Directory full");
 
        int blocks = (program.Length + 15) / 16;
        int start = Enumerable.Range(1, 255).FirstOrDefault(b => b + blocks <= BlockCount && directory.All(p => b + blocks <= p.StartBlock || b >= p.StartBlock + (p.WordCount + 15) / 16));
        if (start == 0) throw new InvalidOperationException("Not enough HDD space");
        int slot = Enumerable.Range(0, 8).First(i => words[i * 2] == 0) * 2;
    
        Array.Clear(words, start * 16, blocks * 16);
        Array.Copy(program, 0, words, start * 16, program.Length);
        words[slot] = encodedName;
        words[slot + 1] = ((uint)start << 16) | (uint)program.Length;
        Save(words);
    }
}
