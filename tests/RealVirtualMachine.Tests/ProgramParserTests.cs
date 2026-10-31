namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Supervisor;
using static RealVirtualMachine.Supervisor.ProgramParser;

public sealed class ProgramParserTests
{
    private const uint Name = 0x50415631; // PAV1

    private static uint[] Program(uint[] data, uint[] code) =>
        [AmjTag, Name, DatTag, .. data, CodTag, .. code, EndTag];

    [Fact]
    public void Parse_SplitsNameDataAndCode()
    {
        var program = Parse(Program([5, 10], [0x2000, 0x2101, 0xF000]));

        Assert.Equal(Name, program.Name);
        Assert.Equal([5u, 10u], program.Data);
        Assert.Equal([0x2000u, 0x2101u, 0xF000u], program.Code);
    }

    [Fact]
    public void Parse_EmptyDataSectionIsAllowed()
    {
        var program = Parse(Program([], [0xF000]));

        Assert.Empty(program.Data);
        Assert.Single(program.Code);
    }

    [Fact]
    public void Tags_AreAsciiEncoded()
    {
        static uint Ascii(string s) => s.Aggregate(0u, (w, c) => (w << 8) | c);

        Assert.Equal(AmjTag, Ascii("$AMJ"));
        Assert.Equal(DatTag, Ascii("$DAT"));
        Assert.Equal(CodTag, Ascii("$COD"));
        Assert.Equal(EndTag, Ascii("$END"));
    }

    [Fact]
    public void Parse_FullSections_AreAccepted()
    {
        var program = Parse(Program(new uint[128], Enumerable.Repeat(0xF000u, 128).ToArray()));

        Assert.Equal(128, program.Data.Length);
        Assert.Equal(128, program.Code.Length);
    }

    [Fact]
    public void Parse_TooManyDataWords_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse(Program(new uint[129], [0xF000])));
    }

    [Fact]
    public void Parse_TooManyCodeWords_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse(Program([], Enumerable.Repeat(0xF000u, 129).ToArray())));
    }

    [Fact]
    public void Parse_EmptyCodeSection_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse(Program([1], [])));
    }

    [Theory]
    [InlineData(0)] // $AMJ
    [InlineData(2)] // $DAT
    public void Parse_WrongHeader_IsRejected(int index)
    {
        var words = Program([1], [0xF000]);
        words[index] = 0;

        Assert.Throws<FormatException>(() => Parse(words));
    }

    [Fact]
    public void Parse_MissingCod_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse([AmjTag, Name, DatTag, 1, 2, EndTag]));
    }

    [Fact]
    public void Parse_MissingEnd_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse([AmjTag, Name, DatTag, 1, CodTag, 0xF000]));
    }

    [Fact]
    public void Parse_WordsAfterEnd_AreRejected()
    {
        Assert.Throws<FormatException>(() => Parse([.. Program([1], [0xF000]), 0]));
    }

    [Fact]
    public void Parse_TooShortOrTooLong_IsRejected()
    {
        Assert.Throws<FormatException>(() => Parse([AmjTag, Name]));
        Assert.Throws<FormatException>(() => Parse(new uint[MaximumProgramWords + 1]));
    }
}
