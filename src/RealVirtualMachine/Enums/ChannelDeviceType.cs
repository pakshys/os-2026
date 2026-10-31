namespace RealVirtualMachine.Enums;
//devices  selected as an XCHG source or destination
public enum ChannelTypeBasedOnDevice : byte
{
    UserMemory = 0,
    SupervisorMemory = 1,
    HDD = 2,
    Keyboard = 3,
    Screen = 4
}
