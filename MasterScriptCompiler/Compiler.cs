using System.Diagnostics;
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
		public readonly HashSet<string> DefinedReferenceStructs = new();
		public readonly StringBuilder CSharpScript = new();
		public readonly List<string> Structs = new();

		public override string ToString()
		{
			return $@"
using System.Runtime.InteropServices;

using _type_int_ = System.Int32;
using _type_uint_ = System.UInt32;
using _type_long_ = System.Int64;
using _type_ulong_ = System.UInt64;
using _type_short_ = System.Int16;
using _type_ushort_ = System.UInt16;
using _type_byte_ = System.Byte;
using _type_sbyte_ = System.SByte;
using _type_float_ = System.Single;
using _type_double_ = System.Double;
using _type_bool_ = System.Boolean;
using _type_char_ = System.Char;

namespace MasterScript
{{
	public static unsafe class Program
	{{
		{string.Join('\n', Structs)}

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
		public readonly Script Script;
		public readonly Scope? Parent;
		
		private readonly Dictionary<string, Parser.StructDefineCommand> _structs = new();
		private readonly Dictionary<string, Parser.VariableDefineCommand> _variables = new();
		
		private readonly HashSet<string> _definedReferenceStructs = new();
		
		public Scope(Scope parent)
		{
			Script = parent.Script;
			Parent = parent;
		}
		
		public Scope(Script script)
		{
			Script = script;
			Parent = null;
		}
		
		public void DefineStruct(Parser.StructDefineCommand command)
		{
			if (_structs.ContainsKey(command.Name))
				throw new Exception($"Struct {command.Name} already defined in this scope");
			_structs[command.Name] = command;
		}
		
		public void DefineVariable(Parser.VariableDefineCommand command)
		{
			if (_variables.ContainsKey(command.Name))
				throw new Exception($"Variable {command.Name} already defined in this scope");
			_variables[command.Name] = command;
		}
		
		public Parser.StructDefineCommand GetStruct(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._structs.TryGetValue(name, out var command)) return command;
				scope = scope.Parent;
			}
			throw new Exception($"Struct {name} not found");
		}
		
		public Parser.VariableDefineCommand GetVariable(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._variables.TryGetValue(name, out var command)) return command;
				scope = scope.Parent;
			}
			throw new Exception($"Variable {name} not found");
		}
		
		public void DefineReferenceStruct(string name)
		{
			_definedReferenceStructs.Add(name);
		}
		
		public bool IsReferenceStructDefined(string name)
		{
			return _definedReferenceStructs.Contains(name);
		}
		
		public bool IsStructDefined(string name)
		{
			return _structs.ContainsKey(name);
		}
		
		public bool IsVariableDefined(string name)
		{
			return _variables.ContainsKey(name);
		}
		
		public bool IsTypeDefined(string name)
		{
			return IsStructDefined(name) || IsVariableDefined(name);
		}
		
		public bool IsPrimitiveType(string name)
		{
			return PrimitiveTypes.Contains(name);
		}
	}

	public static string Compile(string script)
	{
		var rootBlock = Parser.ParseScript(script);
		// TODO: Do the renaming on the parse result right here, so everything is named safely. Don't do the renaming while generating the C# code.
		var scope = new Scope(new Script());

		scope.Script.CSharpScript.Append(CompileBlock(rootBlock, scope));
		
		return scope.Script.ToString();
	}
	
	private static string CompileBlock(Parser.Block block, Scope scope)
	{
		var sb = new StringBuilder();
		sb.Append($"/** Block: {block.Name} **/");
		sb.Append("{");
		foreach (var command in block.Commands)
		{
			sb.Append(CompileCommand(command, scope));
			sb.Append(';');
		}
		sb.Append("}");
		return sb.ToString();
	}
	
	private static string CompileCommand(Parser.Command command, Scope scope)
	{
		switch (command)
		{
			case Parser.StructDefineCommand structDefineCommand:
				return CompileStruct(structDefineCommand, scope);
			case Parser.VariableDefineCommand variableDefineCommand:
				return CompileVariableDefinition(variableDefineCommand, scope, true);
			case Parser.VariableSetCommand variableSetCommand:
				return CompileVariableSet(variableSetCommand, scope);
			case Parser.VariableGetCommand variableGetCommand:
				return CompileVariableGet(variableGetCommand, scope);
			case Parser.NumberLiteralCommand numberLiteralCommand:
				return CompileNumberLiteral(numberLiteralCommand, scope);
			default:
				return "";
				throw new Exception($"Unknown command {command}");
		}
	}

	private static string CompileStruct(Parser.StructDefineCommand structDefineCommand, Scope scope)
	{
		scope.DefineStruct(structDefineCommand);
		
		var structBuilder = new StringBuilder();
		structBuilder.Append("[StructLayout(LayoutKind.Sequential)]");
		structBuilder.Append($"public struct {structDefineCommand.Name}");
		structBuilder.Append("{");
		var structScope = new Scope(scope);
		foreach (var field in structDefineCommand.Fields)
			structBuilder.Append($"public {CompileVariableDefinition(field, structScope, false)};");
		structBuilder.Append("}");
		
		scope.Script.Structs.Add(structBuilder.ToString());

		var defaultVariableBuilder = new StringBuilder();
		defaultVariableBuilder.Append($"{structDefineCommand.Name} {structDefineCommand.Name}_default = new {structDefineCommand.Name}");
		defaultVariableBuilder.Append("{");
		foreach (var field in structDefineCommand.Fields)
		{
			if (field.Value is { })
				defaultVariableBuilder.Append($"{field.Name} = {CompileCommand(field.Value, structScope)},");
		}
		defaultVariableBuilder.Append("}");

		return defaultVariableBuilder.ToString();
	}

	private static void CompileReferenceStruct(Parser.TypeName typeName, Scope scope)
	{
		if (!typeName.IsReference) throw new Exception("Type is not reference");
		if (scope.IsReferenceStructDefined(typeName.Name)) return;
		scope.DefineReferenceStruct(typeName.Name);
		
		var structBuilder = new StringBuilder();
		structBuilder.Append("[StructLayout(LayoutKind.Sequential)]");
		structBuilder.Append($"public struct _REF_{typeName.Name}");
		structBuilder.Append("{");
		structBuilder.Append($"public static readonly int Size = Marshal.SizeOf<{typeName.Name}>();");
		structBuilder.Append($"public {typeName.Name}* Pointer;");
		structBuilder.Append($"public _REF_{typeName.Name}({typeName.Name} initialValue)");
		structBuilder.Append("{");
		structBuilder.Append($"Pointer = ({typeName.Name}*)MasterScriptApi.Allocation.Allocate(Size);");
		structBuilder.Append($"*Pointer = initialValue;");
		structBuilder.Append("}");
		structBuilder.Append("}");

		scope.Script.Structs.Add(structBuilder.ToString());
	}
	
	private static string CompileVariableDefinition(Parser.VariableDefineCommand variableDefineCommand, Scope scope, bool compileSetter)
	{
		scope.DefineVariable(variableDefineCommand);

		if (variableDefineCommand.TypeName.IsReference)
		{
			CompileReferenceStruct(variableDefineCommand.TypeName, scope);
		}
		

		var compiled = $"{variableDefineCommand.TypeName.Name}{(variableDefineCommand.TypeName.IsReference ? "*" : "")} {variableDefineCommand.Name}";
	
		if (!compileSetter) return compiled;
		var defaultValue = scope.IsPrimitiveType(variableDefineCommand.TypeName.Name) ?
			"default" :
			$"{variableDefineCommand.TypeName.Name}_default";
		
		if (variableDefineCommand.TypeName.IsReference)
			compiled += $" = new _REF_{variableDefineCommand.TypeName.Name}({defaultValue}).Pointer";
		else
			compiled += $" = {defaultValue}";

		if (variableDefineCommand.Value is null) return compiled;
		var setCommand = CompileVariableSet(new Parser.VariableSetCommand
		{
			StartAt = variableDefineCommand.StartAt,
			Length = variableDefineCommand.Length,
			Name = variableDefineCommand.Name,
			Value = variableDefineCommand.Value
		}, scope);
		
		compiled += $"; {setCommand}";
		
		return compiled;
	}
		
	private static string CompileVariableSet(Parser.VariableSetCommand variableSetCommand, Scope scope)
	{
		if (!scope.IsVariableDefined(variableSetCommand.Name))
			throw new Exception($"Variable {variableSetCommand.Name} not defined");
		var definitionVariable = scope.GetVariable(variableSetCommand.Name);
		if (variableSetCommand.Value is Parser.VariableGetCommand variableGetCommand)
		{
			var setVariable = scope.GetVariable(variableGetCommand.Name);
			if (setVariable.TypeName.IsReference)
			{
				if (definitionVariable.TypeName.IsReference)
					return $"*{variableSetCommand.Name} = *{variableGetCommand.Name}";
				else
					return $"{variableSetCommand.Name} = *{variableGetCommand.Name}";
			}
			else
			{
				if (definitionVariable.TypeName.IsReference)
					return $"*{variableSetCommand.Name} = {variableGetCommand.Name}";
				else
					return $"{variableSetCommand.Name} = {variableGetCommand.Name}";
			}
		}
		else
		{
			if (definitionVariable.TypeName.IsReference)
				return $"*{variableSetCommand.Name} = {CompileCommand(variableSetCommand.Value, scope)}";
			else
				return $"{variableSetCommand.Name} = {CompileCommand(variableSetCommand.Value, scope)}";
		}
	}
	
	private static string CompileVariableGet(Parser.VariableGetCommand variableGetCommand, Scope scope)
	{
		if (!scope.IsVariableDefined(variableGetCommand.Name))
			throw new Exception($"Variable {variableGetCommand.Name} not defined");
		// var variable = scope.GetVariable(variableGetCommand.Name);
		return $"{variableGetCommand.Name}";
	}

	private static string CompileNumberLiteral(Parser.NumberLiteralCommand numberCommand, Scope scope)
	{
		/*var suffix = type switch
		{
			"float" => "f",
			"double" => "d",
			"long" => "L",
			"ulong" => "UL",
			"uint" => "U",
			"short" => "S",
			"ushort" => "US",
			"byte" => "B",
			"sbyte" => "SB",
			_ => ""
		};*/
		return $"{numberCommand.Value}";

	}
}