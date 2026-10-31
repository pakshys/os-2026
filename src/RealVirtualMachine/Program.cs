using RealVirtualMachine.Hardware;

try
{
    switch (args)
    {
        case ["hdd", "import", var name, var source]:
            var program = File.ReadAllLines(source).Select(Hdd.ParseWord).ToArray();
            new Hdd().WriteProgram(name, program);
            Console.WriteLine($"Imported {name}: {program.Length} words.");
            break;
        case ["hdd", "list"]:
            foreach (var entry in new Hdd().ListPrograms())
                Console.WriteLine($"{entry.Name}: block {entry.StartBlock:X2}, {entry.WordCount} words");
            break;
        case ["hdd", "read", var name]:
            foreach (uint word in new Hdd().ReadProgram(name)) Console.WriteLine($"{word:X8}");
            break;
        default:
            Console.WriteLine("Usage: hdd import <name> <hex-file> | hdd list | hdd read <name>");
            break;
    }
}
catch (Exception ex) when (ex is IOException or FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
