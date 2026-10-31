namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Hardware;

public partial class Executor
{
    private readonly Cpu _cpu;
    private readonly Mmu _mmu;

    public Executor(Cpu cpu, Mmu mmu)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(mmu);

        _cpu = cpu;
        _mmu = mmu;
    }
}