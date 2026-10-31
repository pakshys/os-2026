namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;

public sealed class ChannelDeviceTests : IDisposable
{
    private readonly string _hddPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");

    public void Dispose()
    {
        if (File.Exists(_hddPath)) File.Delete(_hddPath);
    }

    private ChannelDevice CreateDevice(
        Cpu cpu,
        Memory userMemory,
        Memory supervisorMemory,
        TextReader? keyboard = null,
        TextWriter? screen = null)
        => new(cpu, userMemory, supervisorMemory, new Hdd(_hddPath), keyboard, screen);

    [Fact]
    public void Xchg_UserMemoryToSupervisorMemory_CopiesWholeBlock()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.UserMemory, SB = 3, DT = ChannelTypeBasedOnDevice.SupervisorMemory, DB = 7 };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        var words = Enumerable.Range(0, Memory.WordsPerMemoryBlock).Select(i => (uint)(i + 1)).ToArray();
        userMemory.WriteBlock(3, words);
        var channel = CreateDevice(cpu, userMemory, supervisorMemory);

        channel.Xchg();

        Assert.Equal(words, supervisorMemory.ReadBlock(7));
    }

    [Fact]
    public void Xchg_HddToUserMemory_CopiesWholeBlock()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.HDD, SB = 5, DT = ChannelTypeBasedOnDevice.UserMemory, DB = 9 };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        var disk = new Hdd(_hddPath);
        var words = Enumerable.Range(10, Memory.WordsPerMemoryBlock).Select(i => (uint)i).ToArray();
        disk.WriteBlock(5, words);
        var channel = new ChannelDevice(cpu, userMemory, supervisorMemory, disk);

        channel.Xchg();

        Assert.Equal(words, userMemory.ReadBlock(9));
    }

    [Fact]
    public void Xchg_KeyboardToHdd_PacksAsciiAndPadsWithSpaces()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.Keyboard, DT = ChannelTypeBasedOnDevice.HDD, DB = 2 };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        var disk = new Hdd(_hddPath);
        var channel = new ChannelDevice(cpu, userMemory, supervisorMemory, disk, new StringReader("Hi\n"));

        channel.Xchg();

        var block = disk.ReadBlock(2);
        Assert.Equal(0x48692020u, block[0]);
        Assert.Equal(0x20202020u, block[^1]);
    }

    [Fact]
    public void Xchg_UserMemoryToScreen_StopsAtFirstZeroByte()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.UserMemory, SB = 1, DT = ChannelTypeBasedOnDevice.Screen };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        userMemory.WriteBlock(1, [0x41420043, .. Enumerable.Repeat(0x44444444u, 15)]);
        using var output = new StringWriter();
        var channel = CreateDevice(cpu, userMemory, supervisorMemory, screen: output);

        channel.Xchg();

        Assert.Equal("AB", output.ToString());
    }

    [Fact]
    public void Xchg_RejectsScreenAsSourceAndDoesNotModifyDestination()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.Screen, DT = ChannelTypeBasedOnDevice.UserMemory, DB = 0 };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        var channel = CreateDevice(cpu, userMemory, supervisorMemory);

        Assert.Throws<InvalidOperationException>(() => channel.Xchg());
        Assert.All(userMemory.ReadBlock(0), word => Assert.Equal(0u, word));
    }

    [Fact]
    public void Xchg_InvalidDestinationBlockIsRejectedBeforeKeyboardIsRead()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.Keyboard, DT = ChannelTypeBasedOnDevice.UserMemory, DB = 64 };
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        using var input = new TrackingReader("line");
        var channel = CreateDevice(cpu, userMemory, supervisorMemory, keyboard: input);

        Assert.Throws<ArgumentOutOfRangeException>(() => channel.Xchg());
        Assert.False(input.WasRead);
    }

    [Fact]
    public void Xchg_KeyboardLineLongerThanBlock_IsTruncatedTo64Characters()
    {
        var cpu = new Cpu { ST = ChannelTypeBasedOnDevice.Keyboard, DT = ChannelTypeBasedOnDevice.UserMemory, DB = 0 };
        var userMemory = new Memory(64);
        var channel = CreateDevice(cpu, userMemory, new Memory(16), new StringReader(new string('A', 65)));

        channel.Xchg();

        Assert.All(userMemory.ReadBlock(0), word => Assert.Equal(0x41414141u, word));
    }

    private sealed class TrackingReader(string text) : StringReader(text)
    {
        public bool WasRead { get; private set; }

        public override string? ReadLine()
        {
            WasRead = true;
            return base.ReadLine();
        }
    }
}
