namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Enums;

public partial class Executor
{
    public void ExecuteArithmetic(in Instruction instruction)
    {
        int a = _cpu.GetRegisterValue(instruction.Register1);
        int b = _cpu.GetRegisterValue(instruction.Register2);

        switch (instruction.Opcode)
        {
            case Opcode.ADD:
            {
                int result = unchecked(a + b);
                bool carry = (ulong)(uint)a + (uint)b > uint.MaxValue;
                bool overflow = ((a ^ result) & (b ^ result)) < 0;
                Store(instruction.Register1, result, carry, overflow);
                break;
            }
            
            case Opcode.SUB:
            {
                Subtract(a, b, out int result, out bool carry, out bool overflow);
                Store(instruction.Register1, result, carry, overflow);
                break;
            }

            case Opcode.MUL:
            {
                long product = (long) a * b;
                int result = unchecked((int)product);
                bool overflow = product != result;
                Store(instruction.Register1, result, overflow, overflow);
                break;
            }

            case Opcode.DIV:
                Divide(instruction, a, b);
                break;

            case Opcode.CMP:
            {
                Subtract(a, b, out int result, out bool carry, out bool overflow);
                SetFlags(result, carry, overflow);
                break;
            }

            default:
                throw new ArgumentException($"{instruction.Opcode} is not an arithmetic instruction");
        }
    }

    private static void Subtract(int a, int b, out int result, out bool carry, out bool overflow)
    {
        result = unchecked(a - b);
        carry = (uint)a < (uint)b;
        overflow = ((a ^ b) & (a ^ result)) < 0;
    }

    private void Divide(in Instruction instruction, int a, int b)
    {
        if (b == 0)
        {
            _cpu.PI = (byte)ProgramInterrupt.DivisionByZero;
            return;
        }

        if (a == int.MinValue && b == -1)
        {
            SetFlags(int.MinValue, carry: false, overflow: true);
            _cpu.PI = (byte)ProgramInterrupt.Overflow;
            return;
        }

        int quotient = a / b;
        int remainder = a % b;

        SetFlags(quotient, carry: false, overflow: false);
        _cpu.SetRegisterValue(instruction.Register1, quotient);
        _cpu.SetRegisterValue(instruction.Register2, remainder);
    }

    private void Store(int register, int result, bool carry, bool overflow)
    {
        SetFlags(result, carry, overflow);
        if (overflow)
        {
            _cpu.PI = (byte)ProgramInterrupt.Overflow;
            return;
        }
        _cpu.SetRegisterValue(register, result);
    }

    private void SetFlags(int result, bool carry, bool overflow)
    {
        _cpu.Zero = result == 0;
        _cpu.Sign = result < 0;
        _cpu.Carry = carry;
        _cpu.Overflow = overflow;
    }
}