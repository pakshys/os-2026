namespace RealVirtualMachine.Hardware;

using System.Text;
using RealVirtualMachine.Enums;

//transfers block between physical memory, HDD, keyboard, screen
public sealed class ChannelDevice
{
    private const int MaximumTextLength = Memory.WordsPerMemoryBlock * 4;

    private readonly Cpu _cpu;
    private readonly Memory _userMemory;
    private readonly Memory _supervisorMemory;
    private readonly Hdd _hdd;
    private readonly TextReader _keyboard;
    private readonly TextWriter _screen;

    public ChannelDevice(
        Cpu cpu,
        Memory userMemory,
        Memory supervisorMemory,
        Hdd hdd,
        TextReader? keyboard = null,
        TextWriter? screen = null)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(userMemory);
        ArgumentNullException.ThrowIfNull(supervisorMemory);
        ArgumentNullException.ThrowIfNull(hdd);

        _cpu = cpu;
        _userMemory = userMemory;
        _supervisorMemory = supervisorMemory;
        _hdd = hdd;
        _keyboard = keyboard ?? Console.In;
        _screen = screen ?? Console.Out;
    }

    //exchanges one 16-word block using ST/SB as source and DT/DB as destination. added in Cpu.cs
    public void Xchg()
    {
        ValidateSource(_cpu.ST, _cpu.SB);
        ValidateDestination(_cpu.DT, _cpu.DB);

        // buffers complete source first -> devices receive blocks not respond based on one word at a time writes
        uint[] words = ReadSource(_cpu.ST, _cpu.SB);
        WriteDestination(_cpu.DT, _cpu.DB, words);
    }

    private void ValidateSource(ChannelTypeBasedOnDevice type, int block)
    {
        switch (type)
        {
            case ChannelTypeBasedOnDevice.UserMemory:
                CheckBlock(block, _userMemory.BlockCount, nameof(_cpu.SB));
                break;
            case ChannelTypeBasedOnDevice.SupervisorMemory:
                CheckBlock(block, _supervisorMemory.BlockCount, nameof(_cpu.SB));
                break;
            case ChannelTypeBasedOnDevice.HDD:
                CheckBlock(block, Hdd.BlockCount, nameof(_cpu.SB));
                break;
            case ChannelTypeBasedOnDevice.Keyboard:
                break;
            case ChannelTypeBasedOnDevice.Screen:
                throw new InvalidOperationException("The screen can only be an XCHG destination.");
            default:
                throw new ArgumentOutOfRangeException(nameof(type), "Unknown source device type.");
        }
    }

    private void ValidateDestination(ChannelTypeBasedOnDevice type, int block)
    {
        switch (type)
        {
            case ChannelTypeBasedOnDevice.UserMemory:
                CheckBlock(block, _userMemory.BlockCount, nameof(_cpu.DB));
                break;
            case ChannelTypeBasedOnDevice.SupervisorMemory:
                CheckBlock(block, _supervisorMemory.BlockCount, nameof(_cpu.DB));
                break;
            case ChannelTypeBasedOnDevice.HDD:
                CheckBlock(block, Hdd.BlockCount, nameof(_cpu.DB));
                break;
            case ChannelTypeBasedOnDevice.Keyboard:
                throw new InvalidOperationException("The keyboard can only be an XCHG source.");
            case ChannelTypeBasedOnDevice.Screen:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), "Unknown destination device type.");
        }
    }

    private uint[] ReadSource(ChannelTypeBasedOnDevice type, int block) => type switch
    {
        ChannelTypeBasedOnDevice.UserMemory => _userMemory.ReadBlock(block),
        ChannelTypeBasedOnDevice.SupervisorMemory => _supervisorMemory.ReadBlock(block),
        ChannelTypeBasedOnDevice.HDD => _hdd.ReadBlock(block),
        ChannelTypeBasedOnDevice.Keyboard => ReadKeyboardBlock(),
        _ => throw new InvalidOperationException("Unsupported XCHG source device.")
    };

    private void WriteDestination(ChannelTypeBasedOnDevice type, int block, uint[] words)
    {
        switch (type)
        {
            case ChannelTypeBasedOnDevice.UserMemory:
                _userMemory.WriteBlock(block, words);
                break;
            case ChannelTypeBasedOnDevice.SupervisorMemory:
                _supervisorMemory.WriteBlock(block, words);
                break;
            case ChannelTypeBasedOnDevice.HDD:
                _hdd.WriteBlock(block, words);
                break;
            case ChannelTypeBasedOnDevice.Screen:
                _screen.Write(DecodeText(words));
                break;
            default:
                throw new InvalidOperationException("Unsupported XCHG destination.");
        }
    }

    private uint[] ReadKeyboardBlock()
    {
        string input = _keyboard.ReadLine() ?? string.Empty;
        if (input.Length > MaximumTextLength)
            input = input[..MaximumTextLength];

        var words = new uint[Memory.WordsPerMemoryBlock];
        for (int i = 0; i < MaximumTextLength; i += 4)
        {
            uint word = 0;
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            {
                int charIndex = i + byteIndex;
                char value = charIndex < input.Length ? input[charIndex] : ' ';
                if (value > 0x7F)
                    throw new InvalidDataException("Keyboard input must contain ASCII characters.");
                word = (word << 8) | value;
            }
            words[i / 4] = word;
        }
        return words;
    }

    private static string DecodeText(uint[] words)
    {
        var output = new StringBuilder(MaximumTextLength);
        foreach (uint word in words)
        {
            for (int shift = 24; shift >= 0; shift -= 8)
            {
                int value = (int)((word >> shift) & 0xFF);
                if (value == 0)
                    return output.ToString();
                if (value > 0x7F)
                    throw new InvalidDataException("Screen data contains a non-ASCII byte.");
                output.Append((char)value);
            }
        }
        return output.ToString();
    }

    private static void CheckBlock(int block, int blockCount, string parameterName)
    {
        if (block < 0 || block >= blockCount)
            throw new ArgumentOutOfRangeException(parameterName, block, "Block is outside the selected device.");
    }
}
