namespace RealVirtualMachine.Hardware;

///allocates and releases memory used by VM.
public sealed class VirtualMachineManager
{
    public const int MaximumVirtualMachines = 4;
    public const int UserBlocksPerVirtualMachine = 16;

    private readonly Memory _userMemory;
    private readonly Memory _supervisorMemory;
    private readonly Random _random;
    private readonly SortedSet<int> _freeUserBlocks;
    private readonly SortedSet<int> _freeSupervisorBlocks;
    private readonly Dictionary<VirtualMachine, int[]> _allocations = new();

    public VirtualMachineManager(
        Memory userMemory,
        Memory supervisorMemory,
        Random? random = null)
    {
        ArgumentNullException.ThrowIfNull(userMemory);
        ArgumentNullException.ThrowIfNull(supervisorMemory);

        if (userMemory.BlockCount != 64)
            throw new ArgumentException("User memory must contain 64 blocks.", nameof(userMemory));
        if (supervisorMemory.BlockCount != 16)
            throw new ArgumentException("Supervisor memory must contain 16 blocks.", nameof(supervisorMemory));

        _userMemory = userMemory;
        _supervisorMemory = supervisorMemory;
        _random = random ?? Random.Shared;
        _freeUserBlocks = new SortedSet<int>(Enumerable.Range(0, userMemory.BlockCount));
        _freeSupervisorBlocks = new SortedSet<int>(Enumerable.Range(0, supervisorMemory.BlockCount));
    }

    public int ActiveVirtualMachineCount => _allocations.Count;
    public int FreeUserBlockCount => _freeUserBlocks.Count;
    public int FreeSupervisorBlockCount => _freeSupervisorBlocks.Count;

    /// creates VM with one page table and 16 user blocks
    public VirtualMachine CreateVirtualMachine()
    {
        if (_allocations.Count >= MaximumVirtualMachines)
            throw new InvalidOperationException("The maximum number of virtual machines has been reached.");
        if (_freeSupervisorBlocks.Count == 0 || _freeUserBlocks.Count < UserBlocksPerVirtualMachine)
            throw new InvalidOperationException("Not enough free memory to create a virtual machine.");


        int pageTableBlock = _freeSupervisorBlocks.Min;
        int[] userBlocks = ChooseRandomUserBlocks();
        var cpu = new Cpu { PTR = (uint)pageTableBlock };
        var mmu = new Mmu(cpu, _supervisorMemory, _userMemory);
        var vm = new VirtualMachine(cpu, mmu, pageTableBlock, userBlocks);

        try
        {
            _freeSupervisorBlocks.Remove(pageTableBlock);
            foreach (int block in userBlocks)
                _freeUserBlocks.Remove(block);


            _supervisorMemory.WriteBlock(pageTableBlock, new uint[Memory.WordsPerMemoryBlock]);
            for (int virtualBlock = 0; virtualBlock < UserBlocksPerVirtualMachine; virtualBlock++)
            {
                int physicalBlock = userBlocks[virtualBlock];
                _userMemory.WriteBlock(physicalBlock, new uint[Memory.WordsPerMemoryBlock]);
                _supervisorMemory.WriteWord(
                    pageTableBlock * Memory.WordsPerMemoryBlock + virtualBlock,
                    (uint)physicalBlock);
            }

            _allocations.Add(vm, userBlocks);
            return vm;
        }
        catch
        {

            _supervisorMemory.WriteBlock(pageTableBlock, new uint[Memory.WordsPerMemoryBlock]);
            _freeSupervisorBlocks.Add(pageTableBlock);
            foreach (int block in userBlocks)
            {
                _userMemory.WriteBlock(block, new uint[Memory.WordsPerMemoryBlock]);
                _freeUserBlocks.Add(block);
            }
            throw;
        }
    }

    /// clears a VMs memory and returns all of its blocks to lists
    public void DestroyVirtualMachine(VirtualMachine vm)
    {
        ArgumentNullException.ThrowIfNull(vm);
        if (!_allocations.Remove(vm, out int[]? userBlocks))
            throw new InvalidOperationException("The virtual machine is not active in this manager.");

        _supervisorMemory.WriteBlock(vm.PageTableBlock, new uint[Memory.WordsPerMemoryBlock]);
        _freeSupervisorBlocks.Add(vm.PageTableBlock);
        foreach (int block in userBlocks)
        {
            _userMemory.WriteBlock(block, new uint[Memory.WordsPerMemoryBlock]);
            _freeUserBlocks.Add(block);
        }
    }

    private int[] ChooseRandomUserBlocks()
    {
    //shuffles only needed prefix so seeds have some alloc sequence    
        var candidates = _freeUserBlocks.ToArray();
        for (int i = 0; i < UserBlocksPerVirtualMachine; i++)
        {
            int selected = _random.Next(i, candidates.Length);
            (candidates[i], candidates[selected]) = (candidates[selected], candidates[i]);
        }
        return candidates.Take(UserBlocksPerVirtualMachine).ToArray();
    }
}
