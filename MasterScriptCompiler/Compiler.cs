using System.Text;

namespace MasterScriptCompiler;

public static class Compiler
{
	public struct Definition
	{
		public string Type;
		public Parser.Command Command;
	}

	public static readonly string[] PrimitiveTypes =
	{
		"byte",
		"short",
		"ushort",
		"int",
		"uint",
		"long",
		"ulong",
		"float",
		"double",
		"bool",
		"char"
	};

	public static string Stringify(Parser.Block root)
	{
		if (root.Parent != null) throw new Exception("Root block must not have a parent");
		var cSharpStructs = new StringBuilder();
		var cSharpScript = new StringBuilder();

		var structs = new Dictionary<string, Parser.StructDefineCommand>();
		var functions = new Dictionary<string, Parser.FunctionDefineCommand>();
		var variables = new Dictionary<string, Parser.VariableDefineCommand>();
		var references = new Dictionary<string, Parser.VariableDefineCommand>();

		var blockStructs = new List<string>();
		var blockFunctions = new List<string>();
		var blockVariables = new List<string>();
		var blockReferences = new List<string>();

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

		void UseType(string type)
		{
			if (PrimitiveTypes.Contains(type)) return;
			if (structs.ContainsKey(type)) return;
			throw new Exception($"Type {type} not defined");
		}

		void UseFunction(string name)
		{
			if (functions.ContainsKey(name)) return;
			throw new Exception($"Function {name} not defined");
		}

		void UseVariable(string name)
		{
			if (variables.ContainsKey(name)) return;
			throw new Exception($"Variable {name} not defined");
		}
		
		string StructName(Parser.StructDefineCommand command, Parser.Block block)
		{
			return $"{block.Name}_{command.Name}_struct";
		}
		
		string StructReferenceName(Parser.StructDefineCommand command, Parser.Block block)
		{
			return $"{block.Name}_{command.Name}_reference";
		}

		void AppendBlock(Parser.Block block)
		{
			cSharpScript.Append('{');
			foreach (var command in block.Commands) AppendCommand(command, block);
			cSharpScript.Append('}');
			
			foreach (var structName in blockStructs) structs.Remove(structName);
			foreach (var functionName in blockFunctions) functions.Remove(functionName);
			foreach (var variableName in blockVariables) variables.Remove(variableName);
		}

		void AppendCommand(Parser.Command command, Parser.Block block)
		{
			switch (command)
			{
				case Parser.VariableDefineCommand variableDefineCommand:
				{
					DefineVariable(variableDefineCommand);
					var type = variableDefineCommand.Type;

					var isReference = variableDefineCommand.Type.StartsWith('@');
					if (isReference) type = type[1..];

					UseType(type);
					if (isReference) type = StructReferenceName(structs[type], block);
					else if (structs.ContainsKey(type)) type = StructName(structs[type], block);

					cSharpScript.Append($"{type} _{variableDefineCommand.Name}_");
					if (variableDefineCommand.Value != null)
					{
						cSharpScript.Append($" = ");
						AppendCommand(variableDefineCommand.Value, block);
					}

					cSharpScript.Append(';');
					break;
				}
				case Parser.VariableSetCommand variableSetCommand:
				{
					cSharpScript.Append($"_{variableSetCommand.Name}_ = ");
					AppendCommand(variableSetCommand.Value, block);
					cSharpScript.Append(";");
					break;
				}
				case Parser.VariableGetCommand variableGetCommand:
				{
					UseVariable(variableGetCommand.Name);
					cSharpScript.Append($"_{variableGetCommand.Name}_;");
					break;
				}
				case Parser.StructDefineCommand structDefineCommand:
				{
					DefineStruct(structDefineCommand);
					var structName = StructName(structDefineCommand, block);
					var referenceName = StructReferenceName(structDefineCommand, block);
					cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
					cSharpStructs.Append($"public struct {structName}");
					cSharpStructs.Append("{");
					foreach (var variableDefineCommand in structDefineCommand.Variables)
					{
						UseType(variableDefineCommand.Type);
						cSharpStructs.Append($"public {variableDefineCommand.Type} {variableDefineCommand.Name};");
					}
					cSharpStructs.Append("}");

					cSharpStructs.Append("[StructLayout(LayoutKind.Sequential)]");
					cSharpStructs.Append($"public struct {referenceName}");
					cSharpStructs.Append("{");
					cSharpStructs.Append("public readonly int Size;");
					cSharpStructs.Append($"public readonly {structName}* Pointer;");
					cSharpStructs.Append($"public {referenceName}()");
					cSharpStructs.Append("{");
					cSharpStructs.Append($"Size = Marshal.SizeOf<{structName}>();");
					cSharpStructs.Append($"Pointer = ({structName}*)MasterScriptApi.Allocation.Allocate(Size);");
					cSharpStructs.Append("}");
					cSharpStructs.Append("}");
					break;
				}
				case Parser.NumberLiteralCommand numberLiteralCommand:
				{
					cSharpScript.Append($"{numberLiteralCommand.Value}");
					break;
				}
				default:
				{
					throw new Exception($"Unexpected command type '{command.GetType()}'");
				}
			}
		}

		AppendBlock(root);

		return $@"
using System;
using System.Runtime.InteropServices;
using MasterScriptApi;

namespace MasterScript
{{
	{cSharpStructs}

	public static class Program
	{{
		public static void Main()
		{{
			{cSharpScript}
		}}
	}}
}}
".Trim();
	}

}