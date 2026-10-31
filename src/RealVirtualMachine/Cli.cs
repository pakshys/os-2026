namespace RealVirtualMachine;

using RealVirtualMachine.Hardware;
using RealVirtualMachine.Supervisor;

//command line: hdd commands and running programs
public static class Cli
{
    public const int InstructionLimit = 100_000;
    public const string Usage = "Usage: hdd import <name> <hex-file> | hdd list | hdd read <name> | run <name> [<name>...]";

    //returns exit code: 0 ok, 1 error
    public static int Run(string[] args, TextReader input, TextWriter output, TextWriter error, string hddPath = "hdd.txt")
    {
        try
        {
            switch (args)
            {
                case ["hdd", "import", var name, var source]:
                    var program = ReadProgramFile(source);
                    new Hdd(hddPath).WriteProgram(name, program);
                    output.WriteLine($"Imported {name}: {program.Length} words.");
                    break;
                case ["hdd", "list"]:
                    foreach (var entry in new Hdd(hddPath).ListPrograms())
                        output.WriteLine($"{entry.Name}: block {entry.StartBlock:X2}, {entry.WordCount} words");
                    break;
                case ["hdd", "read", var name]:
                    foreach (uint word in new Hdd(hddPath).ReadProgram(name))
                        output.WriteLine($"{word:X8}");
                    break;
                case ["run", .. var names] when names.Length > 0:
                    RunPrograms(names, input, output, hddPath);
                    break;
                default:
                    output.WriteLine(Usage);
                    break;
            }
            return 0;
        }
        catch (Exception ex) when (ex is IOException or FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    //one word per line, text after the word is a comment, empty lines and lines starting with ; are skipped
    public static uint[] ReadProgramFile(string path) =>
        File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .Select(line => Hdd.ParseWord(line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0]))
            .ToArray();

    //builds real machine, loads programs (max 4) and runs them until all stop
    private static void RunPrograms(string[] names, TextReader input, TextWriter output, string hddPath)
    {
        var hdd = new Hdd(hddPath);
        var userMemory = new Memory(64);
        var supervisorMemory = new Memory(16);
        var realCpu = new Cpu();
        var channel = new ChannelDevice(realCpu, userMemory, supervisorMemory, hdd, input, output);
        var manager = new VirtualMachineManager(userMemory, supervisorMemory);
        var loader = new ProgramLoader(realCpu, supervisorMemory, hdd, channel, manager);
        var kernel = new Kernel(realCpu, channel, manager, loader, output);

        foreach (var name in names)
            kernel.Load(name);

        if (!kernel.Run(InstructionLimit))
            output.WriteLine($"Stopped after {InstructionLimit} instructions.");
    }
}
