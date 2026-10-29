namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Enums;

public static class Decoder
{
    public static bool TryDecode(uint word, out Instruction instruction)
    {
        instruction = null;

        if ((word >> 16) != 0)
            return false;

        // instruction format: [operation][nibble2][nibble3][nibble4]
        int op = (int)((word >> 12) & 0xF);
        int n2 = (int)((word >> 8) & 0xF);
        int n3 = (int)((word >> 4) & 0xF);
        int n4 = (int)(word & 0xF);
        byte low = (byte)(word & 0xFF) // [n3][n4]

        if (!Enum.IsDefined(typeof(Opcode), (byte)op))
            return false;

        var opcode = (Opcode)op;
        switch (opcode)
        {
            case Opcode.MOV:
                if(!Cpu.IsValidRegisterCode(n2))
                    return false;
                
                instruction = new Instruction(opcode, Register1: n2, Constant: (sbyte)low);
                return true;

            case Opcode.LW:
            case Opcode.SW:
                if(!Cpu.IsValidRegisterCode(n2))
                    return false;

                instruction = new Instruction(opcode, Register1: n2, Address: low);
                return true;

            case Opcode.ADD:
            case Opcode.SUB:
            case Opcode.MUL:
            case Opcode.DIV:
            case Opcode.CMP:
                if (!Cpu.IsValidRegisterCode(n2) || !Cpu.IsValidRegisterCode(n3) || n4 != 0)
                    return false;
                
                instruction = new Instruction(opcode, Register1: n2, Register2: n3);
                return true;
            
            case Opcode.JMP:
            case Opcode.JE:
            case Opcode.JG:
            case Opcode.JL:
            case Opcode.GD:
            case Opcode.PD:
                if (n2 != 0)
                    return false;
                
                instruction = new Instruction(opcode, Address: low);
                return true;

            case Opcode.HALT:
                if (n2 != 0 || n3 != 0 || n4 != 0)
                    return false;

                instruction = new Instruction(opcode);
                return true;
            
            default:
                return false;
        }
    }
}