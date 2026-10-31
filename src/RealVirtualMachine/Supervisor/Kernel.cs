namespace RealVirtualMachine.Supervisor;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using RealVirtualMachine.Instructions;

//how VM ended: halted normally or stopped with error message
public sealed record ProcessResult(string Name, bool Halted, string? Error);

//supervisor: runs loaded VMs round robin and handles their interrupts
public sealed class Kernel
{
    public const byte TimeQuantum = 10;

    private sealed record Process(string Name, VirtualMachine Vm, CpuCycler Cycler);

    private readonly Cpu _realCpu;
    private readonly ChannelDevice _channel;
    private readonly VirtualMachineManager _manager;
    private readonly ProgramLoader _loader;
    private readonly TextWriter _console;
    private readonly Queue<Process> _ready = new();
    private readonly List<ProcessResult> _finished = new();

    //console = same writer as channel device screen, used for error messages and newline after PD
    public Kernel(Cpu realCpu, ChannelDevice channel, VirtualMachineManager manager, ProgramLoader loader, TextWriter? console = null)
    {
        ArgumentNullException.ThrowIfNull(realCpu);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(loader);

        _realCpu = realCpu;
        _channel = channel;
        _manager = manager;
        _loader = loader;
        _console = console ?? Console.Out;
    }

    public int ReadyCount => _ready.Count;
    public IReadOnlyList<ProcessResult> Finished => _finished;

    //loads program from HDD and adds its VM to ready queue
    public void Load(string name)
    {
        var vm = _loader.Load(name);
        var cycler = new CpuCycler(vm.Cpu, vm.Mmu, new Executor(vm.Cpu, vm.Mmu));
        _ready.Enqueue(new Process(name, vm, cycler));
    }

    //runs until all VMs stop -> true, or until instruction limit is reached -> false
    public bool Run(int instructionLimit = int.MaxValue)
    {
        int executed = 0;
        while (_ready.Count > 0)
        {
            if (executed >= instructionLimit)
                return false;

            var process = _ready.Peek();
            var cpu = process.Vm.Cpu;

            //new time slice
            if (cpu.TI == 0)
                cpu.TI = TimeQuantum;

            cpu.Mode = (byte)CpuMode.User;
            var result = process.Cycler.Step();
            executed++;

            if (result == CpuStepResult.Continue)
                continue;

            if (!HandleInterrupt(process))
            {
                _ready.Dequeue();
                continue;
            }

            //timer ran out -> next VM (with one VM it just gets a new slice)
            if (cpu.TI == 0)
                _ready.Enqueue(_ready.Dequeue());
        }
        return true;
    }

    //returns false if VM was stopped. PI is checked before SI
    private bool HandleInterrupt(Process process)
    {
        var cpu = process.Vm.Cpu;

        if (cpu.PI != 0)
        {
            Stop(process, halted: false, Describe((ProgramInterrupt)cpu.PI));
            return false;
        }

        try
        {
            switch ((SupervisorInterrupt)cpu.SI)
            {
                case SupervisorInterrupt.Input:
                    Transfer(ChannelTypeBasedOnDevice.Keyboard, 0, ChannelTypeBasedOnDevice.UserMemory, IoBlock(process));
                    break;

                case SupervisorInterrupt.Output:
                    Transfer(ChannelTypeBasedOnDevice.UserMemory, IoBlock(process), ChannelTypeBasedOnDevice.Screen, 0);
                    _console.WriteLine();
                    break;

                case SupervisorInterrupt.Stop:
                    Stop(process, halted: true, error: null);
                    return false;
            }
        }
        catch (InvalidDataException ex)
        {
            //non ASCII keyboard input or screen data
            Stop(process, halted: false, ex.Message);
            return false;
        }

        return true;
    }

    //GD xy / PD xy use VM block x, y is ignored. IC already points after the instruction
    private static int IoBlock(Process process)
    {
        var vm = process.Vm;
        vm.Mmu.TryReadWord(vm.Cpu.IC - 1, out uint word);
        Decoder.TryDecode(word, out var instruction);
        return vm.UserMemoryBlocks[instruction.Address / Memory.WordsPerMemoryBlock];
    }

    //frees VM memory and remembers how it ended
    private void Stop(Process process, bool halted, string? error)
    {
        _manager.DestroyVirtualMachine(process.Vm);
        if (error != null)
            _console.WriteLine($"{process.Name}: {error}");
        _finished.Add(new ProcessResult(process.Name, halted, error));
    }

    private void Transfer(ChannelTypeBasedOnDevice sourceType, int sourceBlock, ChannelTypeBasedOnDevice destinationType, int destinationBlock)
    {
        _realCpu.ST = sourceType;
        _realCpu.SB = sourceBlock;
        _realCpu.DT = destinationType;
        _realCpu.DB = destinationBlock;
        _channel.Xchg();
    }

    private static string Describe(ProgramInterrupt interrupt) => interrupt switch
    {
        ProgramInterrupt.InvalidAddress => "invalid address",
        ProgramInterrupt.InvalidOpcode => "invalid instruction",
        ProgramInterrupt.DivisionByZero => "division by zero",
        ProgramInterrupt.Overflow => "overflow",
        _ => $"program interrupt {(int)interrupt}"
    };
}
