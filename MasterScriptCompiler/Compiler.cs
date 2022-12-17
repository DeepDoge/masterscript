using System.Text;

namespace MasterScriptCompiler;

/*
 * Need to refactor this later.
 */
public static class Compiler
{
	public static readonly string[] PrimitiveIntegerTypes =
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

	public static readonly string[] PrimitiveFloatTypes =
	{
		"float",
		"double"
	};

	public static readonly string[] PrimitiveNumericTypes = PrimitiveIntegerTypes.Concat(PrimitiveFloatTypes).ToArray();

	public static readonly string[] PrimitiveTypes = new[]
	{
		"bool",
		"char"
	}.Concat(PrimitiveNumericTypes).ToArray();

	public static string Compile(string script)
	{
		var root = Parser.ParseScript(script);

		var structs = new Dictionary<string, Parser.StructDefineCommand>();
		var functions = new Dictionary<string, Parser.FunctionDefineCommand>();
		var variables = new Dictionary<string, Parser.VariableDefineCommand>();
		var setVariables = new HashSet<string>();
		var referenceStructs = new HashSet<string>();

		var cSharpScript = new StringBuilder();
		var cSharpStructs = new StringBuilder();

		AppendBlock(root);

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
		{cSharpStructs}

		public static void Main()
		{{
			{cSharpScript}
		}}
	}}
}}
".Trim();

		void AppendBlock(Parser.Block block)
		{
			var blockStructs = new HashSet<string>();
			var blockFunctions = new HashSet<string>();
			var blockVariables = new HashSet<string>();
			var blockReferences = new HashSet<string>();
			
			cSharpScript.AppendLine($"{{ // Block: {block.Name}");
			
			foreach (var command in block.Commands)
			{
				try
				{
					AppendCommand(command);
					cSharpScript.AppendLine(";");
				}
				catch (Exception exception)
				{
					throw new Exception($"Compiler error: {exception.Message} at {script[..command.StartAt]}{script.Substring(command.StartAt, command.Length)}", exception);
				}
			}

			foreach (var variableDefineCommand in blockReferences.Select(referenceName => variables[referenceName]))
			{
				var definitionName = AssertVariableName(variableDefineCommand.Name);
				cSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");
			}

			cSharpScript.Append('}');

			foreach (var structName in blockStructs) structs.Remove(structName);
			foreach (var functionName in blockFunctions) functions.Remove(functionName);
			foreach (var referenceName in blockReferences) referenceStructs.Remove(referenceName);
			foreach (var variableName in blockVariables)
			{
				setVariables.Remove(variableName);
				variables.Remove(variableName);
			}

			#region Methods

			string BaseTypeName(string name)
			{
				if (name.StartsWith("@")) name = name[1..];
				if (PrimitiveTypes.Contains(name))
				{
				}
				else if (structs.ContainsKey(name))
				{
					var structDefineCommand = structs[name];
					name = $"{structDefineCommand.Name}_at_{structDefineCommand.Block.Name}";
				}
				else throw new Exception($"Unknown type: {name}");

				return $"_{name}_";
			}

			string ReferenceStructTypeName(string name)
			{
				if (!name.StartsWith("@")) throw new Exception("Reference struct name must start with @");
				return $"_REF{BaseTypeName(name)}";
			}

			void DefineStruct(Parser.StructDefineCommand command)
			{
				if (structs.ContainsKey(command.Name)) throw new Exception($"Struct {command.Name} already defined");
				structs.Add(command.Name, command);
				blockStructs.Add(command.Name);
			}

			void DefineFunction(Parser.FunctionDefineCommand command)
			{
				if (functions.ContainsKey(command.Name)) throw new Exception($"Function {command.Name} already defined");
				functions.Add(command.Name, command);
				blockFunctions.Add(command.Name);
			}

			void DefineVariable(Parser.VariableDefineCommand command)
			{
				if (variables.ContainsKey(command.Name)) throw new Exception($"Variable {command.Name} already defined");
				variables.Add(command.Name, command);
				blockVariables.Add(command.Name);
			}

			void DefineReference(Parser.VariableDefineCommand command)
			{
				blockReferences.Add(command.Name);
			}

			string AssertFunctionName(string name)
			{
				if (functions.ContainsKey(name)) return name;
				throw new Exception($"Function {name} not defined");
			}

			string AssertVariableName(string name)
			{
				if (variables.ContainsKey(name)) return $"_{name}_";
				throw new Exception($"Variable {name} not defined");
			}

			string AssertTypeName(string name)
			{
				var isReference = name.StartsWith("@");
				var baseType = BaseTypeName(name);

				if (!isReference) return baseType;

				AppendReferenceStructIfNotExist(name);
				return $"{baseType}*";
			}

			void AppendReferenceStructIfNotExist(string typeName)
			{
				if (!typeName.StartsWith("@")) throw new Exception($"Type {typeName} is not a reference");
				if (referenceStructs.Contains(typeName)) return;
				referenceStructs.Add(typeName);
				var referenceName = AssertTypeName(typeName);
				var referenceStructTypeName = ReferenceStructTypeName(typeName);
				var baseTypeName = BaseTypeName(typeName);
				cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
				cSharpStructs.Append($"public struct {referenceStructTypeName}");
				cSharpStructs.Append("{");
				cSharpStructs.Append($"public static readonly int Size = Marshal.SizeOf<{baseTypeName}>();");
				cSharpStructs.Append($"public readonly {referenceName} Pointer;");
				cSharpStructs.Append($"public {referenceStructTypeName}({baseTypeName} initialValue)");
				cSharpStructs.Append("{");
				cSharpStructs.Append($"Pointer = ({referenceName})MasterScriptApi.Allocation.Alloc(Size);");
				cSharpStructs.Append($"*Pointer = initialValue;");
				cSharpStructs.Append("}");
				cSharpStructs.Append("}");
			}

			void AppendCommand(Parser.Command command, Parser.Command? previousCommand = null)
			{
				switch (command)
				{
					case Parser.VariableDefineCommand variableDefineCommand:
					{
						DefineVariable(variableDefineCommand);
						var type = AssertTypeName(variableDefineCommand.Type);
						var name = AssertVariableName(variableDefineCommand.Name);

						cSharpScript.Append($"{type} {name}");
						if (variableDefineCommand.Value != null)
						{
							cSharpScript.Append(';');
							var variableSetCommand = new Parser.VariableSetCommand
							{
								Name = variableDefineCommand.Name,
								Value = variableDefineCommand.Value
							};
							AppendCommand(variableSetCommand, variableDefineCommand);
						}

						break;
					}
					case Parser.VariableSetCommand variableSetCommand:
					{
						var definitionName = AssertVariableName(variableSetCommand.Name);
						var definitionVariable = variables[variableSetCommand.Name];
						var definitionTypeName = AssertTypeName(definitionVariable.Type);
						var isReference = definitionVariable.Type.StartsWith("@");

						if (setVariables.Contains(definitionName))
						{
							cSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");
						}

						setVariables.Add(definitionName);
						cSharpScript.Append($"{definitionName} = ");
						if (isReference)
						{
							DefineReference(definitionVariable);
							if (variableSetCommand.Value is Parser.VariableGetCommand variableGetCommand)
							{
								var valueName = AssertVariableName(variableGetCommand.Name);
								cSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef({valueName})");
							}
							else
							{
								var referenceStructType = ReferenceStructTypeName(definitionVariable.Type);
								cSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef(new {referenceStructType}(");
								AppendCommand(variableSetCommand.Value, variableSetCommand);
								cSharpScript.Append(").Pointer)");
							}
						}
						else AppendCommand(variableSetCommand.Value, variableSetCommand);

						break;
					}
					case Parser.VariableGetCommand variableGetCommand:
					{
						var name = AssertVariableName(variableGetCommand.Name);
						cSharpScript.Append($"{name}");
						break;
					}
					case Parser.StructDefineCommand structDefineCommand:
					{
						DefineStruct(structDefineCommand);
						var baseTypeName = BaseTypeName(structDefineCommand.Name);
						cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
						cSharpStructs.Append($"public struct {baseTypeName}");
						cSharpStructs.Append("{");
						foreach (var variableDefineCommand in structDefineCommand.Variables)
						{
							var type = AssertTypeName(variableDefineCommand.Type);
							cSharpStructs.Append($"public {type} {variableDefineCommand.Name};");
						}

						cSharpStructs.Append("}");

						cSharpScript.AppendLine($"// Struct: {baseTypeName}");
						cSharpScript.Append("// ");
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
						var type = variables[name].Type.TrimStart('@');


						if (PrimitiveIntegerTypes.Contains(type))
						{
							if (numberLiteralCommand.IsFloat)
								throw new Exception($"Cannot assign float to {type}");
						}
						else if (!PrimitiveFloatTypes.Contains(type))
							throw new Exception($"Cannot assign {type} to number");

						var suffix = type switch
						{
							"byte" => "",
							"sbyte" => "",
							"short" => "",
							"ushort" => "",
							"int" => "",
							"uint" => "u",
							"long" => "l",
							"ulong" => "ul",
							"float" => "f",
							"double" => "d",
							_ => throw new Exception($"Unknown numeric type {type}")
						};
						cSharpScript.Append($"{numberLiteralCommand.Value}{suffix}");

						break;
					}
					default: throw new Exception($"Unexpected command type '{command.GetType()}'");
				}
			}
			
			#endregion
		}
	}
}