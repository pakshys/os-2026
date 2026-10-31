# os-2026

## Team members

- Gediminas Pakšys
- Danielius Starkutis
- Augustas Nevardauskas

## Starting the project

Run the command:

`dotnet run --project src/RealVirtualMachine`

## Running tests

Run the command:

`dotnet test`

## Commands

`dotnet run --project src/RealVirtualMachine -- <command>`

- `hdd import <name> <file>` - import a program (one 8 hex digit word per line, text after the word and lines starting with `;` are comments)
- `hdd list` - list programs on the HDD
- `hdd read <name>` - print program words
- `run <name> [<name>...]` - run up to 4 programs, stops after 100000 instructions

## Test programs

Test programs are in the `programs` folder:

| File | Name | What it does |
|---|---|---|
| `hello.txt` | `HELO` | prints `HELLO, WORLD!` |
| `add.txt` | `ADD` | 5 + 10, prints `OK` if result is 15 |
| `echo.txt` | `ECHO` | reads a line and prints it |
| `div0.txt` | `DIV0` | division by zero error |
| `overflow.txt` | `OVFL` | overflow error |
| `loop.txt` | `LOOP` | never halts, tests the timer |

Example:

```
dotnet run --project src/RealVirtualMachine -- hdd import HELO programs/hello.txt
dotnet run --project src/RealVirtualMachine -- run HELO
```
