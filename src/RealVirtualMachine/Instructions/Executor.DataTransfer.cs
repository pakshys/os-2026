namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Enums;

public partial class Executor
{
    public void ExecuteDataTransfer(in Instruction instruction)
    {
        switch (instruction.Opcode)
        {
            case Opcode.MOV:
                _cpu.SetRegisterValue(instruction.Register1, instruction.Constant);
                break;
            
            case Opcode.LW:
                if (!_mmu.TryReadWord(instruction.Address, out uint word))
                {
                    _cpu.PI = (byte)ProgramInterrupt.InvalidAddress;
                    return;
                }
                _cpu.SetRegisterValue(instruction.Register1, unchecked((int)word));
                break;
            
            case Opcode.SW:
                uint value = unchecked((uint)_cpu.GetRegisterValue(instruction.Register1));
                if (!_mmu.TryWriteWord(instruction.Address, value))
                    _cpu.PI = (byte)ProgramInterrupt.InvalidAddress;
                break;

            default:
                throw new ArgumentException($"{instruction.Opcode} is not a data transfer instruction");
        }
    }
}