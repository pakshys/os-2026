namespace RealVirtualMachine.Instructions;

using RealVirtualMachine.Enums;

public record struct Instruction(
    Opcode Opcode,
    int Register1 = 0,
    int Register2 = 0,
    int Constant = 0,
    byte Address = 0);