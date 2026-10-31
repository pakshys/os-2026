namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;

public sealed class VirtualMachineManagerTests
{
    private static VirtualMachineManager CreateManager(
        out Memory userMemory,
        out Memory supervisorMemory,
        int seed = 1234)
    {
        userMemory = new Memory(64);
        supervisorMemory = new Memory(16);
        return new VirtualMachineManager(userMemory, supervisorMemory, new Random(seed));
    }

    [Fact]
    public void CreateVirtualMachine_AllocatesAndMapsSixteenDistinctUserBlocks()
    {
        var manager = CreateManager(out var userMemory, out var supervisorMemory);

        var vm = manager.CreateVirtualMachine();

        Assert.Equal(16, vm.UserMemoryBlocks.Count);
        Assert.Equal(16, vm.UserMemoryBlocks.Distinct().Count());
        Assert.All(vm.UserMemoryBlocks, block => Assert.InRange(block, 0, 63));
        Assert.Equal((uint)vm.PageTableBlock, vm.Cpu.PTR);
        Assert.Equal((uint)CpuMode.Supervisor, vm.Cpu.Mode);
        Assert.Equal(0, vm.Cpu.AX);
        Assert.Equal(0, vm.Cpu.BX);
        Assert.Equal(0, vm.Cpu.CX);
        Assert.Equal(0, vm.Cpu.DX);
        Assert.Equal(48, manager.FreeUserBlockCount);
        Assert.Equal(15, manager.FreeSupervisorBlockCount);

        for (int virtualBlock = 0; virtualBlock < 16; virtualBlock++)
        {
            Assert.Equal(
                (uint)vm.UserMemoryBlocks[virtualBlock],
                supervisorMemory.ReadWord(vm.PageTableBlock * 16 + virtualBlock));
            Assert.True(vm.Mmu.TryTranslate(virtualBlock * 16 + 7, out int realAddress));
            Assert.Equal(vm.UserMemoryBlocks[virtualBlock] * 16 + 7, realAddress);
        }

        Assert.Equal(64, userMemory.BlockCount);
    }

    [Fact]
    public void CreateVirtualMachine_UsesSeededRandom_Reproducibly()
    {
        var firstManager = CreateManager(out _, out _);
        var secondManager = CreateManager(out _, out _);

        var firstAllocations = Enumerable.Range(0, 4)
            .Select(_ => firstManager.CreateVirtualMachine().UserMemoryBlocks.ToArray())
            .ToArray();
        var secondAllocations = Enumerable.Range(0, 4)
            .Select(_ => secondManager.CreateVirtualMachine().UserMemoryBlocks.ToArray())
            .ToArray();

        Assert.Equal(firstAllocations, secondAllocations);
        Assert.Equal(64, firstAllocations.SelectMany(blocks => blocks).Distinct().Count());
    }

    [Fact]
    public void CreateVirtualMachine_FifthVmIsRejectedWithoutChangingAllocations()
    {
        var manager = CreateManager(out _, out _);
        var vms = Enumerable.Range(0, 4)
            .Select(_ => manager.CreateVirtualMachine())
            .ToArray();

        Assert.Equal(4, manager.ActiveVirtualMachineCount);
        Assert.Equal(0, manager.FreeUserBlockCount);
        Assert.Equal(12, manager.FreeSupervisorBlockCount);
        Assert.Throws<InvalidOperationException>(() => manager.CreateVirtualMachine());
        Assert.Equal(4, manager.ActiveVirtualMachineCount);

        foreach (var vm in vms)
            manager.DestroyVirtualMachine(vm);
    }

    [Fact]
    public void DestroyVirtualMachine_ClearsAndReturnsBlocksForReuse()
    {
        var manager = CreateManager(out var userMemory, out var supervisorMemory);
        var vm = manager.CreateVirtualMachine();
        int[] allocatedBlocks = vm.UserMemoryBlocks.ToArray();
        int pageTableBlock = vm.PageTableBlock;
        userMemory.WriteWord(allocatedBlocks[0] * 16, 0xDEADBEEF);
        supervisorMemory.WriteWord(pageTableBlock * 16, 0xFFFFFFFF);

        manager.DestroyVirtualMachine(vm);

        Assert.Equal(0, manager.ActiveVirtualMachineCount);
        Assert.Equal(64, manager.FreeUserBlockCount);
        Assert.Equal(16, manager.FreeSupervisorBlockCount);
        Assert.All(allocatedBlocks, block => Assert.All(userMemory.ReadBlock(block), word => Assert.Equal(0u, word)));
        Assert.All(supervisorMemory.ReadBlock(pageTableBlock), word => Assert.Equal(0u, word));
    }

    [Fact]
    public void DestroyVirtualMachine_RejectsVmNotOwnedByManager()
    {
        var firstManager = CreateManager(out _, out _);
        var secondManager = CreateManager(out _, out _, seed: 5678);
        var vm = firstManager.CreateVirtualMachine();

        Assert.Throws<InvalidOperationException>(() => secondManager.DestroyVirtualMachine(vm));
        Assert.Equal(1, firstManager.ActiveVirtualMachineCount);
        Assert.Equal(0, secondManager.ActiveVirtualMachineCount);
    }

    [Fact]
    public void Constructor_RequiresSpecifiedPhysicalMemorySizes()
    {
        Assert.Throws<ArgumentException>(() =>
            new VirtualMachineManager(new Memory(63), new Memory(16), new Random(1)));
        Assert.Throws<ArgumentException>(() =>
            new VirtualMachineManager(new Memory(64), new Memory(15), new Random(1)));
    }
}
