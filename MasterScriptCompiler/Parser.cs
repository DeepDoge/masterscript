using System.Text;
using System.Text.RegularExpressions;

namespace MasterScriptCompiler;

public static class Parser
{
	public class Command
	{
		public int StartAt;
		public int Length;
		public Block Block;
	}

	public class VariableDefineCommand : Command
	{
		public string Name;
		public string Type;
		public Command? Value;
	}

	public class VariableSetCommand : Command
	{
		public string Name;
		public Command Value;
	}

	public class VariableGetCommand : Command
	{
		public string Name;
	}

	public class StructDefineCommand : Command
	{
		public string Name;
		public List<VariableDefineCommand> Variables = new();
	}
	
	public class NumberLiteralCommand : Command
	{
		public bool IsFloat;
		public string Value;
	}

	public class FunctionDefineCommand : Command
	{
		public string Name;
		public string ReturnType;
		public List<VariableDefineCommand> Parameters;
		public Command[] Commands;
	}

	public class Block
	{
		public readonly Block? Parent;
		public readonly string Name;
		public readonly List<Command> Commands;

		public Block(Block? parent)
		{
			Name = $"{string.Join("", Guid.NewGuid().ToString().Split('-').Take(2))}";
			Parent = parent;
			Commands = new List<Command>();
		}
	}

	public static Block ParseScript(string script)
	{
		const string endOfFile = "\0";
		const string endOfLine = endOfFile + "\n;";
		const string startBlock = "{(";
		const string endBlock = endOfFile + "})";

		var currentBlock = null as Block;

		var i = 0;

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
			var variableDefineCommand = new VariableDefineCommand { StartAt = i, Block = currentBlock };
			
			variableDefineCommand.Name = ExpectWord();
			ExpectChar(":");
			variableDefineCommand.Type = ExpectWord();
			if (NextChar() == '=') variableDefineCommand.Value = ExpectCommand();
			else i--;
			
			variableDefineCommand.Length = i - variableDefineCommand.StartAt;
			return variableDefineCommand;
		}

		VariableSetCommand ExpectVariableSetCommand()
		{
			var variableSetCommand = new VariableSetCommand { StartAt = i, Block = currentBlock };
			
			variableSetCommand.Name = ExpectWord();
			ExpectChar("=");
			variableSetCommand.Value = ExpectCommand();
			
			variableSetCommand.Length = i - variableSetCommand.StartAt;
			return variableSetCommand;
		}

		VariableGetCommand ExpectVariableGetCommand()
		{
			var variableGetCommand = new VariableGetCommand { StartAt = i, Block = currentBlock };
			
			variableGetCommand.Name = ExpectWord();
			
			variableGetCommand.Length = i - variableGetCommand.StartAt;
			return variableGetCommand;
		}

		StructDefineCommand ExpectStructDefineCommand()
		{
			var structDefineCommand = new StructDefineCommand { StartAt = i, Block = currentBlock };
			
			structDefineCommand.Name = ExpectWord();
			ExpectChar(startBlock);
			while (!endBlock.Contains(NextChar()))
			{
				i--;
				structDefineCommand.Variables.Add(ExpectVariableDefineCommand());
			}
			i--;
			ExpectChar(endBlock);
			
			structDefineCommand.Length = i - structDefineCommand.StartAt;
			return structDefineCommand;
		}

		NumberLiteralCommand ExpectNumber()
		{
			SkipWhitespace();
			var numberLiteralCommand = new NumberLiteralCommand { StartAt = i, Block = currentBlock };
			
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

		Block ExpectBlock<T>(Func<T> expect) where T : Command
		{
			ExpectChar(startBlock);
			var block = new Block(currentBlock);
			currentBlock = block;
			while (true)
			{
				if (endBlock.Contains(NextChar())) break;
				i--;
				block.Commands.Add(expect());
			}

			ExpectChar(endBlock);
			currentBlock = block.Parent;

			return block;
		}
		
		try
		{
			return ExpectBlock(ExpectCommand);
		}
		catch (Exception exception)
		{
			throw new Exception($"Error: {exception.Message}. at {script[..i]}", exception);
		}
	}
}