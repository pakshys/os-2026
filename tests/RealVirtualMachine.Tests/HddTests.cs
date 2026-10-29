using RealVirtualMachine.Hardware;

namespace RealVirtualMachine.Tests;

public sealed class HddTests : IDisposable
{
    // Each test gets its own disk image, keeping tests independent of hdd.txt
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
    // xUnit disposes the fixture after each test, including failed tests.
    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    // Exercise the last valid address, persisted formatting, OOR coordinates.
    [Fact]
    public void RawWrite_PersistsAtSpecifiedLine()
    {
        var disk = new Hdd(_path);
        disk.WriteWord(255, 15, 0xABCDEF12);
        var lines = File.ReadAllLines(_path);
        Assert.Equal(4096, lines.Length);
        Assert.All(lines, line => Assert.Matches("^[0-9A-F]{8}$", line));
        Assert.Equal("ABCDEF12", lines[4095]);
        Assert.Equal(0xABCDEF12u, new Hdd(_path).ReadWord(255, 15));
        Assert.Throws<ArgumentOutOfRangeException>(() => disk.WriteWord(256, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => disk.ReadWord(0, 16));
    }

    [Fact]
    public void Programs_RoundTripAcrossBlocks_WithDirectoryAndPadding()
    {
        var disk = new Hdd(_path);
        var program = Enumerable.Range(1, 17).Select(i => (uint)i).ToArray();
        disk.WriteProgram("DEMO", program);
        disk.WriteProgram("B", [0xFFFFFFFF]);
        var reopened = new Hdd(_path);
        Assert.Equal(new HddProgram("DEMO", 1, 17), reopened.FindProgram("DEMO"));
        Assert.Equal(program, reopened.ReadProgram("DEMO"));
        Assert.Equal(3, reopened.FindProgram("B")!.StartBlock);
        Assert.Equal(0u, reopened.ReadWord(2, 1));
        // Check the actual file encoding
        Assert.Equal("44454D4F", File.ReadAllLines(_path)[0]);
        Assert.Equal("00010011", File.ReadAllLines(_path)[1]);
        Assert.Null(reopened.FindProgram("NONE"));
        Assert.Throws<KeyNotFoundException>(() => reopened.ReadProgram("NONE"));
    }

    // Rejected imports should preserve existing data and dir entries.
    [Fact]
    public void FullDirectoryAndDuplicate_LeaveFileUnchanged()
    {
        var disk = new Hdd(_path);
        for (int i = 0; i < 8; i++) disk.WriteProgram($"P{i}", [(uint)i]);
        var before = File.ReadAllText(_path);
        Assert.Throws<InvalidOperationException>(() => disk.WriteProgram("P0", [99]));
        Assert.Throws<InvalidOperationException>(() => disk.WriteProgram("NEW", [99]));
        Assert.Equal(before, File.ReadAllText(_path));
    }

    // The 255 data blocks hold 4080 words; directory slots dont mean free space.
    [Fact]
    public void FullDisk_RejectsAnotherProgram()
    {
        var disk = new Hdd(_path);
        disk.WriteProgram("FULL", new uint[4080]);
        Assert.Throws<InvalidOperationException>(() => disk.WriteProgram("NEXT", [1]));
        Assert.Equal(4080, new Hdd(_path).ReadProgram("FULL").Length);
    }

    // Reject incorrect line count and invalid hex in full-sized image.
    [Fact]
    public void MalformedFile_IsRejectedWithoutOverwriting()
    {
        File.WriteAllText(_path, "bad");
        Assert.Throws<FormatException>(() => new Hdd(_path));
        Assert.Equal("bad", File.ReadAllText(_path));
        var lines = Enumerable.Repeat("00000000", 4096).ToArray();
        lines[42] = "0000000Z";
        File.WriteAllLines(_path, lines);
        Assert.Throws<FormatException>(() => new Hdd(_path));
    }
}
