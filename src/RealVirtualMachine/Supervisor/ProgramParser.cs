namespace RealVirtualMachine.Supervisor;

using RealVirtualMachine.Hardware;

public sealed record ParsedProgram(uint Name, uint[] Data, uint[] Code);

//splits program into data and code: $AMJ, name, $DAT, data, $COD, code, $END
public static class ProgramParser
{
    //tags are 4 ASCII chars, first char in highest byte
    public const uint AmjTag = 0x24414D4A; // $AMJ
    public const uint DatTag = 0x24444154; // $DAT
    public const uint CodTag = 0x24434F44; // $COD
    public const uint EndTag = 0x24454E44; // $END

    //VM blocks 0-7 for data, 8-F for code
    public const int MaximumSectionWords = 8 * Memory.WordsPerMemoryBlock; // 128
    public const int CodeStartAddress = 0x80;

    //5 tags/name words + both sections
    public const int MaximumProgramWords = 5 + 2 * MaximumSectionWords; // 261

    //throws FormatException if program is invalid
    public static ParsedProgram Parse(IReadOnlyList<uint> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        if (words.Count > MaximumProgramWords)
            throw new FormatException($"Program has {words.Count} words, at most {MaximumProgramWords} are allowed.");
        if (words.Count < 5 || words[0] != AmjTag)
            throw new FormatException("Program must start with $AMJ and a name.");
        if (words[2] != DatTag)
            throw new FormatException("Expected $DAT after the program name.");

        int cod = IndexOf(words, CodTag, 3);
        if (cod < 0)
            throw new FormatException("Missing $COD.");

        int end = IndexOf(words, EndTag, cod + 1);
        if (end < 0)
            throw new FormatException("Missing $END.");
        if (end != words.Count - 1)
            throw new FormatException("$END must be the last word of the program.");

        uint[] data = Slice(words, 3, cod);
        uint[] code = Slice(words, cod + 1, end);

        if (data.Length > MaximumSectionWords)
            throw new FormatException($"Data section has {data.Length} words, at most {MaximumSectionWords} are allowed.");
        if (code.Length > MaximumSectionWords)
            throw new FormatException($"Code section has {code.Length} words, at most {MaximumSectionWords} are allowed.");
        if (code.Length == 0)
            throw new FormatException("Code section is empty.");

        return new ParsedProgram(words[1], data, code);
    }

    private static int IndexOf(IReadOnlyList<uint> words, uint tag, int start)
    {
        for (int i = start; i < words.Count; i++)
            if (words[i] == tag) return i;
        return -1;
    }

    private static uint[] Slice(IReadOnlyList<uint> words, int from, int to) =>
        Enumerable.Range(from, to - from).Select(i => words[i]).ToArray();
}
