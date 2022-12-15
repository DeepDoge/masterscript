using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using MasterScriptApi;

// Rules
// Never use classes or any reference type, only use structs
// Line ends after ; or /n
// Functions defined on compiled time but can be referenced and used as a variable on runtime

var ExampleScript = @"
	var number: int = 1
	var number2: int = 2

	struct int3
	{
		x: int
		y: int
		z: int
	}
	add(this int3 a, int3 b): int3
	{
		return int3
		{
			x = a.x + b.x
			y = a.y + b.y
			z = a.z + b.z
		}
	}

	struct ray
	{
		origin: int3
		direction: int3
	}

	test(): int3@
	{
		var int3Allocation: int3@ = alloc int3(number, number2, number + number2)
		return int3Allocation
	}

	test2(int3@ value): int3@
	{
		console.log(value)
	}

	int3@ value = test()
	console.log(123)
	test2(value)
";

public static unsafe class ExampleScriptCompiled
{
	public static class Structs
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct root_struct_int3
		{
			public const int Size = sizeof(int) + sizeof(int) + sizeof(int);
			public int x;
			public int y;
			public int z;
		}
		[StructLayout(LayoutKind.Sequential)]
		public struct root_ptr_int3
		{
			public readonly root_struct_int3* ptr;
			public root_ptr_int3()
			{
				ptr = (root_struct_int3*)MasterScriptApi.Allocation.Alloc(Structs.root_struct_int3.Size);
			}
		}
		
		
		[StructLayout(LayoutKind.Sequential)]
		public struct root_struct_ray
		{
			public const int Size = sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int) + sizeof(int);
			public Structs.root_struct_int3 origin;
			public Structs.root_struct_int3 direction;
		}
	}

	public static void Main()
	{
		int number = 1;
		int number2 = 2;
		
		Structs.root_ptr_int3 test()
		{
			Structs.root_ptr_int3 int3Allocation = new Structs.root_ptr_int3();
			// Adds reference because int3Allocation allocated
			MasterScriptApi.Allocation.AddRef(int3Allocation.ptr);
			int3Allocation.ptr->x = number;
			int3Allocation.ptr->y = number2;
			int3Allocation.ptr->z = number + number2;
			
			// Adds reference again because there is return
			MasterScriptApi.Allocation.AddRef(int3Allocation.ptr);
			// Removing reference because int3Allocation's scope is over
			MasterScriptApi.Allocation.Dereference(int3Allocation.ptr);
			return int3Allocation;
		}
		
		void test2(Structs.root_ptr_int3 value)
		{
			// Adds reference because value is passed as a parameter
			MasterScriptApi.Allocation.AddRef(value.ptr);
			Console.WriteLine(value.ptr->ToString());
			// Removing reference because value's scope is over
			MasterScriptApi.Allocation.Dereference(value.ptr);
		}
		
		Structs.root_ptr_int3 value = test();
		Console.WriteLine(123);
		test2(value);
		// Removing reference because value's scope is over
		MasterScriptApi.Allocation.Dereference(value.ptr);
	}
}

public static class Compiler
{
	public class Command
	{
	}

	public class VariableDefineCommand : Command
	{
		public StringBuilder Name = new();
		public StringBuilder Type = new();
	}
	
	public class VariableSetCommand : Command
	{
		public StringBuilder Name = new();
		public StringBuilder Value = new();
	}
	
	public class VariableGetCommand : Command
	{
		public StringBuilder Name = new();
	}
	
	public class StructDefineCommand : Command
	{
		public StringBuilder Name = new();
		public List<VariableDefineCommand> Variables = new();
	}
	
	public class FunctionDefineCommand : Command
	{
		public StringBuilder Name = new();
		public StringBuilder ReturnType = new();
		public List<VariableDefineCommand> Parameters = new();
		public List<Command> Body = new();
	}
	
	public class FunctionCallCommand : Command
	{
		public StringBuilder Name = new();
		public List<VariableGetCommand> Parameters = new();
	}
	
	public class ReturnCommand : Command
	{
		public VariableGetCommand Value = new();
	}

	public class Block
	{
		public Block? Parent = null;
		public List<Command> Commands = new ();
	}

	public enum State
	{
		Out,
		Word,
		
		VariableName,
		VariableType,
		VariableTypeAfter,
		VariableValue
	}

	public static string Compile(Block root)
	{
		if (root.Parent != null) throw new Exception("Root block must not have a parent");
		var cSharp = new StringBuilder();

		foreach (var command in root.Commands)
		{
			switch (command)
			{
				case VariableDefineCommand variableDefineCommand:
					cSharp.Append($"{variableDefineCommand.Type} {variableDefineCommand.Name};");
					break;
				case VariableSetCommand variableSetCommand:
					cSharp.Append($"{variableSetCommand.Name} = {Compile(ParseBlock(variableSetCommand.Value.ToString())l)};");
					break;
				case VariableGetCommand variableGetCommand:
					cSharp.Append($"{variableGetCommand.Name};");
					break;
			}
		}
	}

	public static Block ParseBlock(string script)
	{
		var root = new Block();
		var currentBlock = root;
		
		var state = State.Out;
		var word = new StringBuilder();

		void Out()
		{
			state = State.Out;
			word.Clear();
		}
		
		for (var i = 0; i < script.Length; i++)
		{
			var c = script[i];
			var command = currentBlock.Commands[^1];

			switch (state)
			{
				case State.Out:
					if (char.IsLetter(c))
					{
						word.Append(c);
						state = State.Word;
					}
					break;
				case State.Word:
					if (char.IsLetter(c))
						word.Append(c);
					else if (char.IsWhiteSpace(c))
					{
						if (word.Equals("var"))
						{
							state = State.VariableName;
							currentBlock.Commands.Add(new VariableDefineCommand());
						}
						else if (word.Equals("struct"))
						{

						}
						else throw new Exception("Unknown word");
					}
					else throw new Exception("Unexpected character");
					break;
				case State.VariableName:
				{
					if (command is not VariableDefineCommand variableDefineCommand) throw new Exception("Unexpected command");

					if (char.IsLetter(c))
						variableDefineCommand.Name.Append(c);
					else if (c == ':')
						state = State.VariableType;
					else throw new Exception("Unexpected character");
					break;
				}
				case State.VariableType:
				{
					if (command is not VariableDefineCommand variableDefineCommand) throw new Exception("Unexpected command");

					if (char.IsLetter(c))
						variableDefineCommand.Type.Append(c);
					else if (variableDefineCommand.Type.Length > 0)
					{
						switch (c)
						{
							case '=':
								state = State.VariableValue;
								break;
							case ';' or '\n':
								Out();
								break;
							default:
								if (!char.IsWhiteSpace(c)) throw new Exception("Unexpected character");
								state = State.VariableTypeAfter;
								break;
						}
					}
					else if (!char.IsWhiteSpace(c)) throw new Exception("Unexpected character");
					break;
				}
				case State.VariableTypeAfter:
					switch (c)
					{
						case '=':
							state = State.VariableValue;
							currentBlock.Commands.Add(new VariableSetCommand());
							break;
						case ';' or '\n':
							Out();
							break;
						default:
							if (!char.IsWhiteSpace(c)) throw new Exception("Unexpected character");
							break;
					}
					break;
				case State.VariableValue:
				{
					if (command is not VariableSetCommand variableSetCommand) throw new Exception("Unexpected command");

					if (char.IsLetter(c))
						variableSetCommand.Value.Append(c);
					else if (c is ';' or '\n')
					{
						if (variableSetCommand.Value.Length > 0)
							Out();
						else throw new Exception("Unexpected character");
					}
					else throw new Exception("Unexpected character");
					break;
				}
			}
		}
	}
} 