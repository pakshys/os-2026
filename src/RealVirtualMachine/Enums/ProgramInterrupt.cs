namespace RealVirtualMachine.Enums;

public enum ProgramInterrupt
{
    None = 0,
    InvalidAddress = 1,
    InvalidOpcode = 2,
    DivisionByZero = 3,
    Overflow = 4
}