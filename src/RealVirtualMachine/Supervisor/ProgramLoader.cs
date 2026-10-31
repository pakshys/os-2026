namespace RealVirtualMachine.Supervisor;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;

//loads program from HDD into new VM. all transfers go through channel device
public sealed class ProgramLoader
{
    //page tables use lowest supervisor blocks (max 4 VMs -> 0-3), so last two are free for buffers
    public const int InputBufferBlock = 15;
    public const int StagingBufferBlock = 14;

    private readonly Cpu _realCpu;
    private readonly Memory _supervisorMemory;
    private readonly Hdd _hdd;
    private readonly ChannelDevice _channel;
    private readonly VirtualMachineManager _manager;

    //realCpu = cpu whose SB DB ST DT registers channel device uses
    public ProgramLoader(Cpu realCpu, Memory supervisorMemory, Hdd hdd, ChannelDevice channel, VirtualMachineManager manager)
    {
        ArgumentNullException.ThrowIfNull(realCpu);
        ArgumentNullException.ThrowIfNull(supervisorMemory);
        ArgumentNullException.ThrowIfNull(hdd);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(manager);

        _realCpu = realCpu;
        _supervisorMemory = supervisorMemory;
        _hdd = hdd;
        _channel = channel;
        _manager = manager;
    }

    //VM is created only after program is validated -> failed load doesnt leave allocated memory
    public VirtualMachine Load(string name)
    {
        var entry = _hdd.FindProgram(name)
            ?? throw new KeyNotFoundException($"Program '{name}' was not found.");
        if (entry.WordCount > ProgramParser.MaximumProgramWords)
            throw new FormatException($"Program '{name}' has {entry.WordCount} words, at most {ProgramParser.MaximumProgramWords} are allowed.");

        var program = ProgramParser.Parse(ReadFromHdd(entry));

        var vm = _manager.CreateVirtualMachine();
        try
        {
            WriteSection(vm, firstVirtualBlock: 0, program.Data);
            WriteSection(vm, firstVirtualBlock: ProgramParser.CodeStartAddress / Memory.WordsPerMemoryBlock, program.Code);
        }
        catch
        {
            _manager.DestroyVirtualMachine(vm);
            throw;
        }

        vm.Cpu.IC = ProgramParser.CodeStartAddress;
        return vm;
    }

    //HDD -> supervisor input buffer, block by block
    private uint[] ReadFromHdd(HddProgram entry)
    {
        var words = new List<uint>(entry.WordCount);
        int blocks = (entry.WordCount + Memory.WordsPerMemoryBlock - 1) / Memory.WordsPerMemoryBlock;

        for (int b = 0; b < blocks; b++)
        {
            Transfer(ChannelTypeBasedOnDevice.HDD, entry.StartBlock + b, ChannelTypeBasedOnDevice.SupervisorMemory, InputBufferBlock);
            int take = Math.Min(Memory.WordsPerMemoryBlock, entry.WordCount - words.Count);
            words.AddRange(_supervisorMemory.ReadBlock(InputBufferBlock).Take(take));
        }

        _supervisorMemory.WriteBlock(InputBufferBlock, new uint[Memory.WordsPerMemoryBlock]);
        return words.ToArray();
    }

    //copies section into consecutive VM blocks, rest of last block stays 0
    private void WriteSection(VirtualMachine vm, int firstVirtualBlock, uint[] words)
    {
        for (int offset = 0; offset < words.Length; offset += Memory.WordsPerMemoryBlock)
        {
            var block = new uint[Memory.WordsPerMemoryBlock];
            Array.Copy(words, offset, block, 0, Math.Min(Memory.WordsPerMemoryBlock, words.Length - offset));

            int virtualBlock = firstVirtualBlock + offset / Memory.WordsPerMemoryBlock;
            _supervisorMemory.WriteBlock(StagingBufferBlock, block);
            Transfer(ChannelTypeBasedOnDevice.SupervisorMemory, StagingBufferBlock,
                ChannelTypeBasedOnDevice.UserMemory, vm.UserMemoryBlocks[virtualBlock]);
        }

        _supervisorMemory.WriteBlock(StagingBufferBlock, new uint[Memory.WordsPerMemoryBlock]);
    }

    private void Transfer(ChannelTypeBasedOnDevice sourceType, int sourceBlock, ChannelTypeBasedOnDevice destinationType, int destinationBlock)
    {
        _realCpu.ST = sourceType;
        _realCpu.SB = sourceBlock;
        _realCpu.DT = destinationType;
        _realCpu.DB = destinationBlock;
        _channel.Xchg();
    }
}
