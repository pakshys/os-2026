namespace RealVirtualMachine.Hardware;

public class Mmu
{
    public const int VirtualAddressCount = 256; // 0-ff

    private readonly Cpu _cpu;
    private readonly Memory _supervisorMemory;
    private readonly Memory _userMemory;

    public Mmu(Cpu cpu, Memory supervisorMemory, Memory userMemory)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(supervisorMemory);
        ArgumentNullException.ThrowIfNull(userMemory);

        _cpu = cpu;
        _supervisorMemory = supervisorMemory;
        _userMemory = userMemory;
    }

    public bool TryTranslate(int virtualAddress, out int realAddress) 
    {
        realAddress = 0;

        if (virtualAddress < 0 || virtualAddress >= VirtualAddressCount)
            return false;

        if (_cpu.PTR >= (uint)_supervisorMemory.BlockCount)
            return false;

        int x = virtualAddress / Memory.WordsPerMemoryBlock;
        int y = virtualAddress % Memory.WordsPerMemoryBlock;

        uint realBlock = _supervisorMemory.ReadWord((int)_cpu.PTR * Memory.WordsPerMemoryBlock + x);

        if (realBlock >=  (uint)_userMemory.BlockCount)
            return false;

        realAddress = (int)realBlock * Memory.WordsPerMemoryBlock + y;
        return true;
    }

    public bool TryReadWord(int virtualAddress, out uint word)
    {
        word = 0;

        if (!TryTranslate(virtualAddress, out int real))
            return false;

        word = _userMemory.ReadWord(real);
        return true;
    }

    public bool TryWriteWord(int virtualAddress, uint word)
    {
        if (!TryTranslate(virtualAddress, out int real))
            return false;
        
        _userMemory.WriteWord(real, word);
        return true;
    }
}