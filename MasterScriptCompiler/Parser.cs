using System.Text;
using System.Text.RegularExpressions;

namespace MasterScriptCompiler;

public static class Parser
{
	public static Block ParseScript(string script)
	{
		const string endOfFile = "\0";
		// const string endOfLine = endOfFile + "\n;"; NO LINE END!!!!
		const string startBlock = "{(";
		const string endBlock = endOfFile + "})";

		var currentBlock = null! as Block;
		var i = 0;
		
		try
		{
			return ExpectBlock();
		}
		catch (Exception exception)
		{
			throw new Exception($"Syntax error: {exception.Message}. At {script[..i]}", exception);
		}
		
		Block ExpectBlock()
		{
			ExpectChar(startBlock);
			var previousBlock = currentBlock;
			var block = new Block(i.ToString());
			currentBlock = block;
			while (true)
			{
				if (endBlock.Contains(NextCharPeek())) break;
				block.Commands.Add(ExpectCommand());
			}
			currentBlock = previousBlock;
			ExpectChar(endBlock);

			return block;
		}

		Command ExpectCommand()
		{
			var wordStartsAt = i;
			var word = ExpectWord();
			switch (word)
			{
				case "var":
					return ExpectVariableDefineCommand();
				case "set":
					return ExpectVariableSetCommand();
				case "get":
					return ExpectVariableGetCommand();
				case "struct":
					return ExpectStructDefineCommand();
				case "alloc":
					return ExpectVariableAllocateCommand();
				default:
				{
					var c = NextChar();
					switch (c)
					{
						case ':':
						{
							i = wordStartsAt;
							return ExpectVariableDefineCommand();
						}
						case '=':
						{
							i = wordStartsAt;
							return ExpectVariableSetCommand();
						}
						default:
						{
							// Is Number
							if (Regex.IsMatch(word, @"^\d+$"))
							{
								i = wordStartsAt;
								return ExpectNumber();
							}

							i = wordStartsAt;
							return ExpectVariableGetCommand();
						}
					}
				}
			}
		}

		string ExpectWord()
		{
			var word = new StringBuilder();
			SkipWhitespace();
			for (; i < script.Length; i++)
			{
				var c = script[i];
				if (char.IsLetterOrDigit(c) || c == '@') word.Append(c);
					else break;
			}

			if (word.Length == 0)
				throw new Exception($"Expected word but got started with '{script[i]}'");
				
			return word.ToString();
		}

		char NextChar()
		{
			for(; i < script.Length; i++)
			{
				var c = script[i];
				if (char.IsWhiteSpace(c)) continue;
				i++;
				return c;
			} 
			return '\0';
		}
		
		char NextCharPeek()
		{
			for(; i < script.Length; i++)
			{
				var c = script[i];
				if (char.IsWhiteSpace(c)) continue;
				return c;
			} 
			return '\0';
		}
		
		void SkipWhitespace()
		{
			for (; i < script.Length; i++)
			{
				var c = script[i];
				if (char.IsWhiteSpace(c)) continue;
				break;
			}
		}

		char ExpectChar(string chars)
		{
			var c = NextChar();
			if (chars.Contains(c)) return c;
			throw new Exception($"Unexpected character '{c}' while expecting one of '{string.Join(", ", chars)}'");
		}

		VariableDefineCommand ExpectVariableDefineCommand()
		{
			var variableDefineCommand = new VariableDefineCommand
			{
				StartAt = i,
				Name = ExpectWord()
			};

			ExpectChar(":");
			if (NextCharPeek() == '{')
			{
				var structDefineCommand = ExpectStructDefineCommand();
				currentBlock.Commands.Add(structDefineCommand);
				var isReference = structDefineCommand.Name.StartsWith("@");
				variableDefineCommand.TypeName = new TypeName
				{
					Name = isReference ? structDefineCommand.Name[1..] : structDefineCommand.Name,
					IsReference = isReference
				};
			}
			else
			{
				var name = ExpectWord();
				var isReference = name.StartsWith("@");
				variableDefineCommand.TypeName = new TypeName
				{
					Name = isReference ? name[1..] : name,
					IsReference = isReference
				};
			}

			if (NextChar() == '=')
			{
				var command = ExpectCommand();
				if (command is not ValueCommand valueCommand) throw new Exception($"Expected value but got {command}");
				variableDefineCommand.Value = valueCommand;
			}
			else i--;

			variableDefineCommand.Length = i - variableDefineCommand.StartAt;
			return variableDefineCommand;
		}

		VariableSetCommand ExpectVariableSetCommand()
		{
			var variableSetCommand = new VariableSetCommand
			{
				StartAt = i,
				Name = ExpectWord()
			};

			ExpectChar("=");
			var command = ExpectCommand();
			if (command is not ValueCommand valueCommand) throw new Exception($"Expected value but got '{command}'");
			variableSetCommand.Value = valueCommand;
			
			variableSetCommand.Length = i - variableSetCommand.StartAt;
			return variableSetCommand;
		}

		VariableGetCommand ExpectVariableGetCommand()
		{
			var variableGetCommand = new VariableGetCommand
			{
				StartAt = i,
				Name = ExpectWord()
			};

			variableGetCommand.Length = i - variableGetCommand.StartAt;
			return variableGetCommand;
		}

		StructDefineCommand ExpectStructDefineCommand()
		{
			var structDefineCommand = new StructDefineCommand { StartAt = i };

			structDefineCommand.Name = NextCharPeek() == '{' ? $"_AnonymousStruct{structDefineCommand.StartAt}_" : ExpectWord();
			var block = ExpectBlock();
			foreach (var command in block.Commands)
			{
				switch (command)
				{
					case VariableDefineCommand variableDefineCommand:
						structDefineCommand.Fields.Add(variableDefineCommand);
						break;
					case StructDefineCommand innerStructDefineCommand:
						currentBlock.Commands.Add(innerStructDefineCommand);
						break;
					default:
						throw new Exception($"Unexpected command '{command}' in struct definition");
				}
			}

			structDefineCommand.Length = i - structDefineCommand.StartAt;
			return structDefineCommand;
		}

		NumberLiteralCommand ExpectNumber()
		{
			SkipWhitespace();
			var numberLiteralCommand = new NumberLiteralCommand { StartAt = i };
			
			var number = new StringBuilder();
			var isFloat = false;
			for (; i < script.Length; i++)
			{
				var c = script[i];
				switch (c)
				{
					case '0':
					case '1':
					case '2':
					case '3':
					case '4':
					case '5':
					case '6':
					case '7':
					case '8':
					case '9':
						number.Append(c);
						continue;
					case '.':
						if (isFloat) throw new Exception("Floating point number can only have one floating point");
						isFloat = true;
						number.Append(c);
						continue;
				}
				break;
			}

			if (number.Length == 0) throw new Exception($"Expected number but got nothing.");
			numberLiteralCommand.Value = number.ToString();
			numberLiteralCommand.IsFloat = isFloat;
			
			numberLiteralCommand.Length = i - numberLiteralCommand.StartAt;
			return numberLiteralCommand;
		}
		
		VariableAllocateCommand ExpectVariableAllocateCommand()
		{
			var variableAllocateCommand = new VariableAllocateCommand { StartAt = i };
			var command = ExpectCommand();
			if (command is not ValueCommand valueCommand) throw new Exception($"Expected value but got '{command}'");
			variableAllocateCommand.Value = valueCommand;
			variableAllocateCommand.Length = i - variableAllocateCommand.StartAt;
			return variableAllocateCommand;
		}

	}

	public class Block
	{
		public readonly string Name;
		public readonly List<Command> Commands;

		public Block(string name)
		{
			Name = name;
			Commands = new List<Command>();
		}
	}
	
	public class Command
	{
		public int StartAt;
		public int Length;
	}
	
	public class ValueCommand : Command
	{
	}
	
	public class LiteralCommand : ValueCommand
	{
	}
	
	public struct TypeName
	{
		public bool IsReference;
		public string Name;
	}

	public class VariableDefineCommand : Command
	{
		public string Name;
		public TypeName TypeName;
		public ValueCommand? Value;
	}

	public class VariableSetCommand : Command
	{
		public string Name;
		public ValueCommand Value;
	}
	
	public class VariableGetCommand : ValueCommand
	{
		public string Name;
	}

	public class StructDefineCommand : Command
	{
		public string Name;
		public List<VariableDefineCommand> Fields = new();
	}

	public class NumberLiteralCommand : LiteralCommand
	{
		public bool IsFloat;
		public string Value;
	}
	
	public class VariableAllocateCommand : ValueCommand
	{
		public ValueCommand Value;
	}

}