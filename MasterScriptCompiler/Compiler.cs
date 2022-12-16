using System.Text;

namespace MasterScriptCompiler;

public class Compiler
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

	public static readonly string[] PrimitiveTypes = new [] 
	{
		"bool",
		"char"
	}.Concat(PrimitiveNumericTypes).ToArray();

	private readonly Dictionary<string, Parser.StructDefineCommand> _structs = new();
	private readonly Dictionary<string, Parser.FunctionDefineCommand> _functions = new();
	private readonly Dictionary<string, Parser.VariableDefineCommand> _variables = new();
	private readonly HashSet<string> _setVariables = new();
	private readonly HashSet<string> _referenceStructs = new();

	private readonly HashSet<string> _blockStructs = new();
	private readonly HashSet<string> _blockFunctions = new();
	private readonly HashSet<string> _blockVariables = new();
	private readonly HashSet<string> _blockReferences = new();
	
	private readonly StringBuilder _cSharpScript = new();
	private readonly StringBuilder _cSharpStructs = new();

	public void Reset()
	{
		_structs.Clear();
		_functions.Clear();
		_variables.Clear();
		_setVariables.Clear();
		_referenceStructs.Clear();
		
		_blockStructs.Clear();
		_blockFunctions.Clear();
		_blockVariables.Clear();
		_blockReferences.Clear();
		
		_cSharpScript.Clear();
		_cSharpStructs.Clear();
	}

	public string Compile(Parser.Block root)
	{
		if (root.Parent != null) throw new Exception("Root block must not have a parent");
		
		string StructTypeName(Parser.StructDefineCommand structDefineCommand)
		{
			return $"{structDefineCommand.Name}_at_{structDefineCommand.Block.Name}";
		}
		
		string ReferenceStructTypeName(string name)
		{ 
			return $"_REF_{name[1..]}";
		}

		void DefineStruct(Parser.StructDefineCommand command)
		{
			if (_structs.ContainsKey(command.Name)) throw new Exception($"Struct {command.Name} already defined");
			_structs.Add(command.Name, command);
			_blockStructs.Add(command.Name);
		}

		void DefineFunction(Parser.FunctionDefineCommand command)
		{
			if (_functions.ContainsKey(command.Name)) throw new Exception($"Function {command.Name} already defined");
			_functions.Add(command.Name, command);
			_blockFunctions.Add(command.Name);
		}

		void DefineVariable(Parser.VariableDefineCommand command)
		{
			if (_variables.ContainsKey(command.Name)) throw new Exception($"Variable {command.Name} already defined");
			_variables.Add(command.Name, command);
			_blockVariables.Add(command.Name);
		}
		
		void DefineReference(Parser.VariableDefineCommand command)
		{
			_blockReferences.Add(command.Name);
		}

		string AssertFunctionName(string name)
		{
			if (_functions.ContainsKey(name)) return name;
			throw new Exception($"Function {name} not defined");
		}

		string AssertVariableName(string name)
		{
			if (_variables.ContainsKey(name)) return $"_{name}_";
			throw new Exception($"Variable {name} not defined");
		}

		string AssertTypeName(string name)
		{
			var isReference = name.StartsWith("@");
			var baseType = isReference ? name[1..] : name;
			
			if (PrimitiveTypes.Contains(baseType)) { }
			else if (_structs.ContainsKey(baseType)) baseType = StructTypeName(_structs[baseType]);
			else throw new Exception($"Type {name} not defined");
			
			if (!isReference) return baseType;
			
			AppendReferenceStructIfNotExist(name);
			return $"{baseType}*";
		}

		void AppendReferenceStructIfNotExist(string typeName)
		{
			if (!typeName.StartsWith("@")) throw new Exception($"Type {typeName} is not a reference");
			var baseTypeName = typeName[1..];
			if (_referenceStructs.Contains(baseTypeName)) return;
			_referenceStructs.Add(baseTypeName);
			var referenceStructTypeName = ReferenceStructTypeName(typeName);
			var referenceTypeName = AssertTypeName(typeName);
			_cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
			_cSharpStructs.Append($"public struct {referenceStructTypeName}");
			_cSharpStructs.Append("{");
			_cSharpStructs.Append($"public static readonly int Size = Marshal.SizeOf<{baseTypeName}>();");
			_cSharpStructs.Append($"public readonly {referenceTypeName} Pointer;");
			_cSharpStructs.Append($"public {referenceStructTypeName}({baseTypeName} initialValue)");
			_cSharpStructs.Append("{");
			_cSharpStructs.Append($"Pointer = ({referenceTypeName})MasterScriptApi.Allocation.Alloc(Size);");
			_cSharpStructs.Append($"*Pointer = initialValue;");
			_cSharpStructs.Append("}");
			_cSharpStructs.Append("}");
		}
		
		void AppendBlock(Parser.Block block)
		{
			_cSharpScript.AppendLine($"{{ // Block: {block.Name}");
			foreach (var command in block.Commands)
			{
				AppendCommand(command);
				_cSharpScript.AppendLine(";");
			}
			foreach (var variableDefineCommand in _blockReferences.Select(referenceName => _variables[referenceName]))
			{
				var definitionName = AssertVariableName(variableDefineCommand.Name);
				_cSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");
			}
			_cSharpScript.Append('}');

			foreach (var structName in _blockStructs) _structs.Remove(structName);
			foreach (var functionName in _blockFunctions) _functions.Remove(functionName);
			foreach (var referenceName in _blockReferences) _referenceStructs.Remove(referenceName);
			foreach (var variableName in _blockVariables)
			{
				_setVariables.Remove(variableName);
				_variables.Remove(variableName);
			}

			_blockStructs.Clear();
			_blockFunctions.Clear();
			_blockVariables.Clear();
			_blockReferences.Clear();
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

					_cSharpScript.Append($"{type} {name}");
					if (variableDefineCommand.Value != null)
					{
						_cSharpScript.Append(';');
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
					var definitionVariable = _variables[variableSetCommand.Name];
					var definitionTypeName = AssertTypeName(definitionVariable.Type);
					var isReference = definitionVariable.Type.StartsWith("@");
					
					if (_setVariables.Contains(definitionName))
					{
						_cSharpScript.Append($"MasterScriptApi.Allocation.RemoveRef({definitionName});");
					}
					
					_setVariables.Add(definitionName);
					_cSharpScript.Append($"{definitionName} = ");
					if (isReference)
					{
						DefineReference(definitionVariable);
						if (variableSetCommand.Value is Parser.VariableGetCommand variableGetCommand)
						{
							var valueName = AssertVariableName(variableGetCommand.Name);
							_cSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef({valueName})");
						}
						else
						{
							var referenceStructType = ReferenceStructTypeName(definitionVariable.Type);
							_cSharpScript.Append($"({definitionTypeName})MasterScriptApi.Allocation.AddRef(new {referenceStructType}(");
							AppendCommand(variableSetCommand.Value, variableSetCommand);
							_cSharpScript.Append(").Pointer)");
						}
					}
					else AppendCommand(variableSetCommand.Value, variableSetCommand);
					break;
				}
				case Parser.VariableGetCommand variableGetCommand:
				{
					var name = AssertVariableName(variableGetCommand.Name);
					_cSharpScript.Append($"{name}");
					break;
				}
				case Parser.StructDefineCommand structDefineCommand:
				{
					DefineStruct(structDefineCommand);
					var structType = StructTypeName(structDefineCommand);
					_cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
					_cSharpStructs.Append($"public struct {structType}");
					_cSharpStructs.Append("{");
					foreach (var variableDefineCommand in structDefineCommand.Variables)
					{
						var type = AssertTypeName(variableDefineCommand.Type);
						_cSharpStructs.Append($"public {type} {variableDefineCommand.Name};");
					}
					_cSharpStructs.Append("}");
					
					_cSharpScript.AppendLine($"// Struct: {structType}");
					_cSharpScript.Append("// ");
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
					var type = _variables[name].Type.TrimStart('@');


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
					_cSharpScript.Append($"{numberLiteralCommand.Value}{suffix}");

					break;
				}
				default: throw new Exception($"Unexpected command type '{command.GetType()}'");
			}
		}

		AppendBlock(root);

		var code = $@"
using System;
using System.Runtime.InteropServices;
using MasterScriptApi;

namespace MasterScript
{{
	public static unsafe class Program
	{{
		{_cSharpStructs}

		public static void Main()
		{{
			{_cSharpScript}
		}}
	}}
}}
".Trim();
		Reset();
		return code;
	}
}