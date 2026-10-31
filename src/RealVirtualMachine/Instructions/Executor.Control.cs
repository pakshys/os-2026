namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Enums;

public partial class Executor
{
    public void ExecuteControl(in Instruction instruction)
    {
        bool jump = instruction.Opcode switch
        {
            Opcode.JMP => true,
            Opcode.JE => _cpu.Zero,
            Opcode.JL => _cpu.Sign != _cpu.Overflow,
            Opcode.JG => !_cpu.Zero && _cpu.Sign == _cpu.Overflow,
            _ => throw new ArgumentException($"{instruction.Opcode} is not a control instruction")
        };

        if (jump)
            _cpu.IC = instruction.Address;
    }
}