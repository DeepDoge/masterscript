using System.Text;

namespace MasterScriptCompiler;

/*
 * Need to refactor this later.
 */
public static class Compiler
{
	private static readonly string[] PrimitiveIntegerTypes =
	{
		"int",
		"uint",
		"long",
		"ulong",
		"short",
		"ushort",
		"byte",
		"sbyte"
	};

	private static readonly string[] PrimitiveFloatTypes =
	{
		"float",
		"double"
	};

	private static readonly string[] PrimitiveNumericTypes = PrimitiveIntegerTypes.Concat(PrimitiveFloatTypes).ToArray();

	private static readonly string[] PrimitiveTypes = new[]
	{
		"bool",
		"char"
	}.Concat(PrimitiveNumericTypes).ToArray();

	private class Script
	{
		public readonly HashSet<string> ReferenceStructs = new();
		public readonly StringBuilder CSharpScript = new();
		public readonly List<string> OuterScript = new();

		public override string ToString()
		{
			return $@"
using System.Runtime.InteropServices;

using _int_ = System.Int32;
using _uint_ = System.UInt32;
using _float_ = System.Single;
using _double_ = System.Double;
using _bool_ = System.Boolean;
using _char_ = System.Char;
using _byte_ = System.Byte;
using _sbyte_ = System.SByte;
using _short_ = System.Int16;
using _ushort_ = System.UInt16;
using _long_ = System.Int64;
using _ulong_ = System.UInt64;

namespace MasterScript
{{
	public static unsafe class Program
	{{
		{string.Join('\n', OuterScript)}

		public static void Main()
		{{
			{CSharpScript}
		}}
	}}
}}
".Trim();
		}
	}

	private class Scope
	{
		public readonly Parser.Block Block;
		private readonly Script _script;
		private readonly Scope? _parent;

		private readonly Dictionary<string, Parser.VariableDefineCommand> _variables = new();
		private readonly Dictionary<string, Parser.StructDefineCommand> _structs = new();

		public Scope(Script script, Scope? parent, Parser.Block block)
		{
			_script = script;
			_parent = parent;
			Block = block;
		}

		public void DefineVariable(Parser.VariableDefineCommand command)
		{
			if (_variables.ContainsKey(command.Name)) throw new Exception($"Variable {command.Name} already defined in this scope");
			_variables.Add(command.Name, command);
		}

		public void DefineStruct(Parser.StructDefineCommand command)
		{
			if (_structs.ContainsKey(command.Name)) throw new Exception($"Struct {command.Name} already defined in this scope");
			_structs.Add(command.Name, command);
		}

		public Parser.VariableDefineCommand GetVariable(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._variables.TryGetValue(name, out var variable)) return variable;
				scope = scope._parent;
			}

			throw new Exception($"Variable {name} not defined in this scope");
		}
		
		public Parser.StructDefineCommand GetStruct(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._structs.TryGetValue(name, out var @struct)) return @struct;
				scope = scope._parent;
			}

			throw new Exception($"Struct {name} not defined in this scope");
		}

		public bool IsVariableDefined(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._variables.ContainsKey(name))
				{
					return true;
				}

				scope = scope._parent;
			}

			return false;
		}

		public bool IsStructDefined(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._structs.ContainsKey(name))
				{
					return true;
				}

				scope = scope._parent;
			}

			return false;
		}

		public bool IsTypeDefined(string name)
		{
			return IsStructDefined(name) || PrimitiveTypes.Contains(name);
		}

		public IEnumerable<string> GetVariableNames()
		{
			return _variables.Keys;
		}
		
		public IEnumerable<string> GetStructNames()
		{
			return _structs.Keys;
		}

		public IEnumerable<string> GetTypeNames()
		{
			return GetStructNames().Concat(PrimitiveTypes);
		}

		public string BaseTypeName(string name)
		{
			if (name.StartsWith("@")) name = name[1..];
			if (PrimitiveTypes.Contains(name))
			{
			}
			else if (IsStructDefined(name))
			{
				var structDefinition = GetStruct(name);
				name = $"{structDefinition.Name}_at_{structDefinition.Block.Name}";
			}
			else throw new Exception($"Unknown type: {name}");

			return $"_{name}_";
		}

		public string ReferenceStructTypeName(string name)
		{
			if (!name.StartsWith("@")) throw new Exception("Reference struct name must start with @");
			return $"_REF{BaseTypeName(name)}";
		}
		
		public string AssertVariableName(string name)
		{
			if (IsVariableDefined(name)) return $"_{name}_";
			throw new Exception($"Variable {name} not defined");
		}

		public string AssertTypeName(string name)
		{
			var isReference = name.StartsWith("@");
			var baseType = BaseTypeName(name);

			if (!isReference) return baseType;

			AppendReferenceStructIfNotExist(name);
			return $"{baseType}*";
		}

		public void AppendReferenceStructIfNotExist(string typeName)
		{
			if (!typeName.StartsWith("@")) throw new Exception($"Type {typeName} is not a reference");
			if (_script.ReferenceStructs.Contains(typeName)) return;
			_script.ReferenceStructs.Add(typeName);
			var referenceName = AssertTypeName(typeName);
			var referenceStructTypeName = ReferenceStructTypeName(typeName);
			var baseTypeName = BaseTypeName(typeName);

			var structScript = new StringBuilder();
			
			structScript.Append("[StructLayout(LayoutKind.Sequential)]");
			structScript.Append($"public struct {referenceStructTypeName}");
			structScript.Append("{");
			structScript.Append($"public static readonly int Size = Marshal.SizeOf<{baseTypeName}>();");
			structScript.Append($"public readonly {referenceName} Pointer;");
			structScript.Append($"public {referenceStructTypeName}({baseTypeName} initialValue)");
			structScript.Append("{");
			structScript.Append($"Pointer = ({referenceName})MasterScriptApi.Allocation.Alloc(Size);");
			structScript.Append("*Pointer = initialValue;");
			structScript.Append("}");
			structScript.Append("}");
			
			_script.OuterScript.Add(structScript.ToString());
		}
	}

	public static string Compile(string scriptText)
	{
		var script = new Script();
		var root = Parser.ParseScript(scriptText);

		AppendScope(new Scope(script, null, root));

		return script.ToString();

		void AppendScope(Scope scope)
		{
			script.CSharpScript.AppendLine($"{{ // Block: {scope.Block.Name}");

			foreach (var command in scope.Block.Commands)
			{
				try
				{
					AppendCommand(scope, command);
				}
				catch (Exception exception)
				{
					throw new Exception($"Compiler error: {exception.Message}. At {scriptText[..command.StartAt]}{scriptText.Substring(command.StartAt, command.Length)}", exception);
				}
			}

			foreach (var variableName in scope.GetVariableNames())
			{
				var variable = scope.GetVariable(variableName);
				if (!variable.Type.StartsWith("@")) continue;
				var definitionName = scope.AssertVariableName(variable.Name);
				script.CSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");
			}

			script.CSharpScript.Append('}');
		}

		void AppendCommand(Scope scope, Parser.Command command, Parser.Command? previousCommand = null)
		{
			switch (command)
			{
				case Parser.VariableDefineCommand variableDefineCommand:
				{
					scope.DefineVariable(variableDefineCommand);
					var type = scope.AssertTypeName(variableDefineCommand.Type);
					var name = scope.AssertVariableName(variableDefineCommand.Name);

					script.CSharpScript.Append($"{type} {name}");
					if (variableDefineCommand.Value != null)
					{
						script.CSharpScript.Append(';');
						var variableSetCommand = new Parser.VariableSetCommand
						{
							Name = variableDefineCommand.Name,
							Value = variableDefineCommand.Value
						};
						AppendCommand(scope, variableSetCommand, variableDefineCommand);
					}
					else
					{
						if (scope.IsStructDefined(variableDefineCommand.Type))
						{
							var structDefinition = scope.GetStruct(variableDefineCommand.Type);
							
							script.CSharpScript.Append($" = new {type}{{");
							foreach (var field in structDefinition.Block.Commands)
							{
								if (field is not Parser.VariableDefineCommand variableDefineFieldCommand) continue;
								if (variableDefineFieldCommand.Value == null) continue;
								AppendCommand(scope, new Parser.VariableSetCommand
								{
									Name = variableDefineFieldCommand.Name,
									Value = variableDefineFieldCommand.Value,
								}, variableDefineFieldCommand);
								script.CSharpScript.Append(',');
							}
							script.CSharpScript.Append("};");
						}
						else
							script.CSharpScript.Append(" = default");
					}

					break;
				}
				case Parser.VariableSetCommand variableSetCommand:
				{
					var definitionName = scope.AssertVariableName(variableSetCommand.Name);
					var definitionVariable = scope.GetVariable(variableSetCommand.Name);
					var definitionTypeName = scope.AssertTypeName(definitionVariable.Type);
					var isReference = definitionVariable.Type.StartsWith("@");

					if (previousCommand is not Parser.VariableDefineCommand)
						script.CSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");

					script.CSharpScript.Append($"{definitionName} = ");
					if (isReference)
					{
						if (variableSetCommand.Value is Parser.VariableGetCommand variableGetCommand)
						{
							var valueName = scope.AssertVariableName(variableGetCommand.Name);
							script.CSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef({valueName})");
						}
						else
						{
							var referenceStructType = scope.ReferenceStructTypeName(definitionVariable.Type);
							script.CSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef(new {referenceStructType}(");
							AppendCommand(scope, variableSetCommand.Value, variableSetCommand);
							script.CSharpScript.Append(").Pointer)");
						}
					}
					else AppendCommand(scope, variableSetCommand.Value, variableSetCommand);

					break;
				}
				case Parser.VariableGetCommand variableGetCommand:
				{
					var name = scope.AssertVariableName(variableGetCommand.Name);
					var type = scope.GetVariable(variableGetCommand.Name).Type;
					script.CSharpScript.Append(type.StartsWith("@") ? $"*{name}" : name);
					break;
				}
				case Parser.StructDefineCommand structDefineCommand:
				{
					scope.DefineStruct(structDefineCommand);
					var baseTypeName = scope.BaseTypeName(structDefineCommand.Name);

					var structScope = new Scope(script, scope, structDefineCommand.Block);

					var definitions = new List<Parser.VariableDefineCommand>();
					foreach (var structCommand in structDefineCommand.Block.Commands)
					{
						switch (structCommand)
						{
							case Parser.VariableDefineCommand variableDefineCommand:
								definitions.Add(variableDefineCommand);
								structScope.DefineVariable(variableDefineCommand);
								break;
							case Parser.StructDefineCommand variableDefineCommand:
								AppendCommand(scope, variableDefineCommand);
								break;
							default:
								throw new Exception($"Unexpected command in struct definition: {structCommand}");
						}
					}

					var structScript = new StringBuilder();
					structScript.Append($"public struct {baseTypeName}");
					structScript.Append(" { ");
					foreach (var definition in definitions)
					{
						var type = structScope.AssertTypeName(definition.Type);
						var name = structScope.AssertVariableName(definition.Name);
						structScript.Append($"public {type} {name}; ");
					}
					structScript.Append('}');
					script.OuterScript.Add(structScript.ToString());

					break;
				}
				case Parser.NumberLiteralCommand numberLiteralCommand:
				{
					var name = previousCommand switch
					{
						Parser.VariableSetCommand variableSetCommand => variableSetCommand.Name,
						Parser.VariableDefineCommand variableDefineCommand => variableDefineCommand.Name,
						_ => throw new Exception("Number literal must be assigned to variable")
					};
					var type = scope.GetVariable(name).Type.TrimStart('@');


					if (PrimitiveIntegerTypes.Contains(type))
					{
						if (numberLiteralCommand.IsFloat)
							throw new Exception($"Cannot assign float to {type}");
					}
					else if (!PrimitiveFloatTypes.Contains(type))
						throw new Exception($"Cannot assign {type} to number");

					var suffix = type switch
					{
						"uint" => "u",
						"long" => "l",
						"ulong" => "ul",
						"float" => "f",
						"double" => "d",
						_ => ""
					};
					script.CSharpScript.Append($"{numberLiteralCommand.Value}{suffix}");

					break;
				}
				default: throw new Exception($"Unexpected command type '{command.GetType()}'");
			}

			if (previousCommand == null) script.CSharpScript.AppendLine(";");
		}
	}
}