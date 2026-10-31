namespace RealVirtualMachine.Hardware;

using RealVirtualMachine.Enums;
using RealVirtualMachine.Instructions;

public class CpuCycler
{
    private readonly Cpu _cpu;
    private readonly Mmu _mmu;
    private readonly Executor _executor;

    public CpuCycler(Cpu cpu, Mmu mmu, Executor executor)
    {
        ArgumentNullException.ThrowIfNull(cpu);
        ArgumentNullException.ThrowIfNull(mmu);
        ArgumentNullException.ThrowIfNull(executor);

        _cpu = cpu;
        _mmu = mmu;
        _executor = executor;
    }

    public CpuStepResult Step()
    {
        _cpu.PI = 0;
        _cpu.SI = 0;

        ExecuteOne();

        bool timerExpired = false;
        if (_cpu.TI > 0)
        {
            _cpu.TI--;
            timerExpired = _cpu.TI == 0;
        }

        if (_cpu.PI != 0 || _cpu.SI != 0 || timerExpired)
        {
            _cpu.Mode = (byte)CpuMode.Supervisor;
            return CpuStepResult.Interrupt;
        }
        return CpuStepResult.Continue;
    }

    private void ExecuteOne()
    {
        if (!_mmu.TryReadWord(_cpu.IC, out uint word))
        {
            _cpu.PI = (byte)ProgramInterrupt.InvalidAddress;
            return;
        }

        _cpu.IC++;

        if (!Decoder.TryDecode(word, out var instruction))
        {
            _cpu.PI = (byte)ProgramInterrupt.InvalidOpcode;
            return;
        }

        switch (instruction.Opcode)
        {
            case Opcode.MOV:
            case Opcode.LW:
            case Opcode.SW:
                _executor.ExecuteDataTransfer(instruction);
                break;

            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.MUL:
            case Opcode.DIV:
            case Opcode.CMP:
                _executor.ExecuteArithmetic(instruction);
                break;

            case Opcode.JMP:
            case Opcode.JE:
            case Opcode.JG:
            case Opcode.JL:
                _executor.ExecuteControl(instruction);
                break;
            
            case Opcode.GD:
                _cpu.SI = (byte)SupervisorInterrupt.Input;
                break;

            case Opcode.PD:
                _cpu.SI = (byte)SupervisorInterrupt.Output;
                break;

            case Opcode.HALT:
                _cpu.SI = (byte)SupervisorInterrupt.Stop;
                break;
        }
    }
}