namespace RealVirtualMachine.Hardware;


/// class for general virtual processorand addr translator for virtual machine manager class
/// generally path work for supervisor or cli solution +-
public sealed class VirtualMachine
{
    internal VirtualMachine(Cpu cpu, Mmu mmu, int pageTableBlock, int[] userMemoryBlocks)
    {
        Cpu = cpu;
        Mmu = mmu;
        PageTableBlock = pageTableBlock;
        UserMemoryBlocks = Array.AsReadOnly(userMemoryBlocks);
    }

    public Cpu Cpu { get; }
    public Mmu Mmu { get; }

    /// supervisor-memory block containing VM page table. and mem blocks by VM
    public int PageTableBlock { get; }


    public IReadOnlyList<int> UserMemoryBlocks { get; }
}
