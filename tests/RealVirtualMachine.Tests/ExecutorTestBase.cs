namespace RealVirtualMachine.Tests;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Hardware;
using RealVirtualMachine.Instructions;

public abstract class ExecutorTestBase
{
    protected readonly Cpu _cpu = new();
    protected readonly Memory _supervisorMemory = new(16);
    protected readonly Memory _userMemory = new(64);
    protected readonly Mmu _mmu;
    protected readonly Executor _executor;

    protected ExecutorTestBase() 
    {
        _mmu = new Mmu(_cpu, _supervisorMemory, _userMemory);
        _executor = new Executor(_cpu, _mmu);

        _cpu.PTR = 0;
        for (int x = 0; x < 16; x++)
            _supervisorMemory.WriteWord(x, (uint)(x + 10));
    }

    protected static int RealAddress(int virtualAddress) => (virtualAddress / 16 + 10) * 16 + virtualAddress % 16;

    protected byte Pi(ProgramInterrupt interrupt) => (byte)interrupt;
}