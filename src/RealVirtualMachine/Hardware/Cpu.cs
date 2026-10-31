namespace RealVirtualMachine.Hardware;

using RealVirtualMachine.Enums;

public class Cpu
{
    private readonly int[] _generalPurposeRegisters = new int[4];

    public int AX {get => GetRegisterValue((int)Register.AX); set => SetRegisterValue((int)Register.AX, value); }
    public int BX {get => GetRegisterValue((int)Register.BX); set => SetRegisterValue((int)Register.BX, value); }
    public int CX {get => GetRegisterValue((int)Register.CX); set => SetRegisterValue((int)Register.CX, value); }
    public int DX {get => GetRegisterValue((int)Register.DX); set => SetRegisterValue((int)Register.DX, value); }

    public byte Flags { get; private set; }
    public bool Zero { get => GetFlag((int)Flag.Zero); set => SetFlag((int)Flag.Zero, value); }
    public bool Sign { get => GetFlag((int)Flag.Sign); set => SetFlag((int)Flag.Sign, value); }
    public bool Carry { get => GetFlag((int)Flag.Carry); set => SetFlag((int)Flag.Carry, value); }
    public bool Overflow { get => GetFlag((int)Flag.Overflow); set => SetFlag((int)Flag.Overflow, value); }

    public uint PTR { get; set; }
    public byte Mode { get; set; }
    public ushort IC { get; set; }
    public byte SI { get; set; } 
    public byte PI { get; set; } 
    public byte TI { get; set; }

//extra registers for source and destination. SB ST soursce, DB DT destination.
    public int SB { get; set; }
    public int DB { get; set; }
    public ChannelTypeBasedOnDevice ST { get; set; }
    public ChannelTypeBasedOnDevice DT { get; set; }

    public Cpu()
    {
        ResetRegisters();
    }

    public static bool IsValidRegisterCode(int registerCode) => Enum.IsDefined(typeof(Register), registerCode);

    public int GetRegisterValue(int registerCode)
    {
        if (!IsValidRegisterCode(registerCode))
            throw new ArgumentOutOfRangeException(nameof(registerCode), "Invalid register code");

        return _generalPurposeRegisters[registerCode];
    }

    public void SetRegisterValue(int registerCode, int value)
    {
        if (!IsValidRegisterCode(registerCode))
            throw new ArgumentOutOfRangeException(nameof(registerCode), "Invalid register code");

        _generalPurposeRegisters[registerCode] = value;
    }

    public void ResetRegisters()
    {
        Array.Clear(_generalPurposeRegisters);
        Flags = 0;
        PTR = 0;
        IC = 0;
        SI = 0;
        PI = 0;
        TI = 0;
        //SB DB for for user memory defaults
        SB = 0;
        DB = 0;
        //device type
        ST = ChannelTypeBasedOnDevice.UserMemory;
        DT = ChannelTypeBasedOnDevice.UserMemory;
        
        Mode = (byte)CpuMode.Supervisor;
    }

    private bool GetFlag(int flagBit) => (Flags & (1 << flagBit)) != 0;
    
    private void SetFlag(int flagBit, bool value)
    {
        if (value) Flags |= (byte)(1 << flagBit);
        else Flags &= (byte)~(1 << flagBit);
    }

    public void ResetFlags() => Flags = 0;
}
