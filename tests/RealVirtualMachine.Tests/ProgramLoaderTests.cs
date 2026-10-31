namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using RealVirtualMachine.Instructions;
using RealVirtualMachine.Supervisor;
using static RealVirtualMachine.Supervisor.ProgramParser;

public sealed class ProgramLoaderTests : IDisposable
{
    //each test gets its own disk image
    private readonly string _path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
    private readonly Cpu _realCpu = new();
    private readonly Memory _userMemory = new(64);
    private readonly Memory _supervisorMemory = new(16);
    private readonly Hdd _hdd;
    private readonly VirtualMachineManager _manager;
    private readonly ProgramLoader _loader;

    public ProgramLoaderTests()
    {
        _hdd = new Hdd(_path);
        _manager = new VirtualMachineManager(_userMemory, _supervisorMemory, new Random(1234));
        var channel = new ChannelDevice(_realCpu, _userMemory, _supervisorMemory, _hdd, TextReader.Null, TextWriter.Null);
        _loader = new ProgramLoader(_realCpu, _supervisorMemory, _hdd, channel, _manager);
    }

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    private static uint[] Program(uint[] data, uint[] code) =>
        [AmjTag, 0x50415631, DatTag, .. data, CodTag, .. code, EndTag];

    private static uint ReadVm(VirtualMachine vm, int virtualAddress)
    {
        Assert.True(vm.Mmu.TryReadWord(virtualAddress, out uint word));
        return word;
    }

    //data from 00, code from 80, IC = 80
    [Fact]
    public void Load_PlacesDataAndCodeAndSetsIc()
    {
        _hdd.WriteProgram("ADD", Program([5, 10], [0x2000, 0x2101, 0x4010, 0x3002, 0xF000]));

        var vm = _loader.Load("ADD");

        Assert.Equal(5u, ReadVm(vm, 0x00));
        Assert.Equal(10u, ReadVm(vm, 0x01));
        Assert.Equal(0u, ReadVm(vm, 0x02));
        Assert.Equal(0x2000u, ReadVm(vm, 0x80));
        Assert.Equal(0xF000u, ReadVm(vm, 0x84));
        Assert.Equal(0u, ReadVm(vm, 0x85));
        Assert.Equal(0x80, vm.Cpu.IC);
        Assert.Equal(1, _manager.ActiveVirtualMachineCount);
    }

    //program bigger than one HDD block
    [Fact]
    public void Load_MultiBlockProgram_FillsConsecutiveVmBlocks()
    {
        var data = Enumerable.Range(1, 40).Select(i => (uint)i).ToArray();
        var code = Enumerable.Range(0, 20).Select(_ => 0x9080u).Append(0xF000u).ToArray();
        _hdd.WriteProgram("BIG", Program(data, code));

        var vm = _loader.Load("BIG");

        for (int i = 0; i < data.Length; i++)
            Assert.Equal(data[i], ReadVm(vm, i));
        for (int i = 0; i < code.Length; i++)
            Assert.Equal(code[i], ReadVm(vm, 0x80 + i));
        Assert.Equal(0u, ReadVm(vm, 40));
        Assert.Equal(0u, ReadVm(vm, 0x80 + code.Length));
    }

    //buffers dont overlap page tables even with max VMs
    [Fact]
    public void Load_FourVms_DoNotOverlapAndBuffersAreCleared()
    {
        _hdd.WriteProgram("P", Program([7], [0xF000]));

        var vms = Enumerable.Range(0, 4).Select(_ => _loader.Load("P")).ToArray();

        Assert.All(vms, vm => Assert.InRange(vm.PageTableBlock, 0, ProgramLoader.StagingBufferBlock - 1));
        Assert.All(vms, vm => Assert.Equal(7u, ReadVm(vm, 0x00)));
        Assert.All(_supervisorMemory.ReadBlock(ProgramLoader.InputBufferBlock), w => Assert.Equal(0u, w));
        Assert.All(_supervisorMemory.ReadBlock(ProgramLoader.StagingBufferBlock), w => Assert.Equal(0u, w));
    }

    [Fact]
    public void Load_InvalidProgram_ThrowsAndAllocatesNothing()
    {
        _hdd.WriteProgram("BAD", [AmjTag, 0, DatTag, 1, 2, 3]);

        Assert.Throws<FormatException>(() => _loader.Load("BAD"));

        Assert.Equal(0, _manager.ActiveVirtualMachineCount);
        Assert.Equal(64, _manager.FreeUserBlockCount);
        Assert.Equal(16, _manager.FreeSupervisorBlockCount);
    }

    [Fact]
    public void Load_TooLongProgram_IsRejectedBeforeReading()
    {
        _hdd.WriteProgram("LONG", new uint[MaximumProgramWords + 1]);

        Assert.Throws<FormatException>(() => _loader.Load("LONG"));
        Assert.Equal(0, _manager.ActiveVirtualMachineCount);
    }

    [Fact]
    public void Load_MissingProgram_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => _loader.Load("NONE"));
    }

    //load, run until HALT, check result
    [Fact]
    public void LoadedProgram_RunsToHalt()
    {
        // LW AX,00; LW BX,01; ADD AX,BX; SW AX,02; HALT
        _hdd.WriteProgram("ADD", Program([5, 10], [0x2000, 0x2101, 0x4010, 0x3002, 0xF000]));
        var vm = _loader.Load("ADD");
        vm.Cpu.Mode = (byte)CpuMode.User;
        var cycler = new CpuCycler(vm.Cpu, vm.Mmu, new Executor(vm.Cpu, vm.Mmu));

        int steps = 0;
        while (cycler.Step() == CpuStepResult.Continue)
            Assert.True(++steps < 100, "Program did not halt");

        Assert.Equal((byte)SupervisorInterrupt.Stop, vm.Cpu.SI);
        Assert.Equal(0, vm.Cpu.PI);
        Assert.Equal(15u, ReadVm(vm, 0x02));
    }
}
