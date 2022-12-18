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
		public readonly List<string> After = new();
		
		private readonly Dictionary<string, Parser.StructDefineCommand> _structs = new();
		private readonly Dictionary<string, Parser.VariableDefineCommand> _variables = new();
		
		private readonly HashSet<string> _definedReferences = new();
		private readonly HashSet<string> _allocatedReferences = new();

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
			_definedReferences.Add(name);
		}
		
		public void AllocateReference(string name)
		{
			_allocatedReferences.Add(name);
		}
		
		public bool IsReferenceStructDefined(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._definedReferences.Contains(name)) return true;
				scope = scope.Parent;
			}
			return false;
		}
		
		public bool IsReferenceStructAllocated(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._allocatedReferences.Contains(name)) return true;
				scope = scope.Parent;
			}
			return false;
		}
		
		public bool IsReferenceStructDefinedInThisScope(string name)
		{
			return _definedReferences.Contains(name);
		}
		
		public bool IsStructDefined(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._structs.ContainsKey(name)) return true;
				scope = scope.Parent;
			}
			return false;
		}
		
		public bool IsPrimitiveType(string name)
		{
			return PrimitiveTypes.Contains(name) || PrimitiveTypes.Select(x => $"_type_{x}_").Contains(name);
		}
		
		public bool IsTypeDefined(string name)
		{
			return IsStructDefined(name) || IsPrimitiveType(name);
		}
		
		public bool IsVariableDefined(string name)
		{
			var scope = this;
			while (scope != null)
			{
				if (scope._variables.ContainsKey(name)) return true;
				scope = scope.Parent;
			}
			return false;
		}
		
		public IEnumerable<string> ListAllocatedReferencesInThisScope()
		{
			return _allocatedReferences;
		}
	}

	public static string Compile(string script)
	{
		var rootBlock = Parser.ParseScript(script);
		var scriptObject = new Script();
		scriptObject.CSharpScript.Append(CompileBlock(new Scope(scriptObject), rootBlock));
		return scriptObject.ToString();
	}

	private static string CompileBlock(Scope scope, Parser.Block block)
	{
		var sb = new StringBuilder();
		sb.Append($"/* Block: {block.Name} */");
		sb.Append("{");
		foreach (var command in block.Commands)
		{
			switch (command)
			{
				case Parser.StructDefineCommand structDefineCommand:
					structDefineCommand.Name = $"_type_{structDefineCommand.Name}_at_{block.Name}";
					foreach (var field in structDefineCommand.Fields)
					{
						if (scope.IsPrimitiveType(field.TypeName.Name))
							field.TypeName.Name = $"_type_{field.TypeName.Name}_";
						else
							field.TypeName.Name = $"_type_{field.TypeName.Name}_at_{block.Name}";
						
						field.Name = $"_var_{field.Name}_";
					}
					break;
				case Parser.VariableDefineCommand variableDefineCommand:
				{
					if (scope.IsPrimitiveType(variableDefineCommand.TypeName.Name))
						variableDefineCommand.TypeName.Name = $"_type_{variableDefineCommand.TypeName.Name}_";
					else
						variableDefineCommand.TypeName.Name = $"_type_{variableDefineCommand.TypeName.Name}_at_{block.Name}";
					variableDefineCommand.Name = $"_var_{variableDefineCommand.Name}_";
					if (variableDefineCommand.Value is Parser.VariableGetCommand defaultValue)
						defaultValue.Name = $"_var_{defaultValue.Name}_";
					break;
				}
				case Parser.VariableSetCommand variableSetCommand:
					variableSetCommand.Name = $"_var_{variableSetCommand.Name}_";
					if (variableSetCommand.Value is Parser.VariableGetCommand variableReferenceCommand)
						variableReferenceCommand.Name = $"_var_{variableReferenceCommand.Name}_";
					break;
				case Parser.VariableGetCommand variableGetCommand:
					variableGetCommand.Name = $"_var_{variableGetCommand.Name}_";
					break;
			}
			sb.Append(CompileCommand(scope, command, null));
			sb.Append(';');
		}
		sb.Append(string.Join("", scope.After));
		sb.Append("}");
		return sb.ToString();
	}

	private static string CompileCommand(Scope scope, Parser.Command command, Parser.Command? previousCommand)
	{
		switch (command)
		{
			case Parser.StructDefineCommand structDefineCommand:
				return CompileStruct(scope, structDefineCommand);
			case Parser.VariableDefineCommand variableDefineCommand:
				return $"{CompileVariableDefinition(scope, variableDefineCommand)};{CompileVariableInitialization(scope, variableDefineCommand)}";
			case Parser.VariableSetCommand variableSetCommand:
				return CompileVariableSet(scope, variableSetCommand);
			case Parser.VariableGetCommand variableGetCommand:
			{
				if (previousCommand is not Parser.VariableSetCommand variableSetCommand)
					throw new Exception("Variable get command must be preceded by variable set command");
				return CompileVariableGet(scope, variableGetCommand, variableSetCommand);
			}
			case Parser.NumberLiteralCommand numberLiteralCommand:
			{
				if (previousCommand is not Parser.VariableSetCommand variableSetCommand)
					throw new Exception("Number literal can be used only as variable value");
				return CompileNumberLiteral(scope, numberLiteralCommand, variableSetCommand);
			}
			case Parser.VariableAllocateCommand variableAllocateCommand:
			{
				if (previousCommand is not Parser.VariableSetCommand variableSetCommand)
					throw new Exception("Variable allocation can be used only as variable value");
				return CompileVariableAllocate(scope, variableAllocateCommand, variableSetCommand);
			}
			default:
				throw new Exception($"Unknown command {command}");
		}
	}

	private static string CompileStruct(Scope scope, Parser.StructDefineCommand structDefineCommand)
	{
		scope.DefineStruct(structDefineCommand);
		
		var structBuilder = new StringBuilder();
		structBuilder.Append("[StructLayout(LayoutKind.Sequential)]");
		structBuilder.Append($"public struct {structDefineCommand.Name}");
		structBuilder.Append("{");
		var structScope = new Scope(scope);
		foreach (var field in structDefineCommand.Fields)
			structBuilder.Append($"public {CompileVariableDefinition(structScope, field)};");
		structBuilder.Append("}");
		
		scope.Script.Structs.Add(structBuilder.ToString());

		var defaultVariableBuilder = new StringBuilder();
		defaultVariableBuilder.Append($"{structDefineCommand.Name} {structDefineCommand.Name}_default = new {structDefineCommand.Name}");
		defaultVariableBuilder.Append("{");
		foreach (var field in structDefineCommand.Fields)
		{
			if (field.Value is { })
				defaultVariableBuilder.Append(CompileVariableSet(structScope, new Parser.VariableSetCommand
				{
					StartAt = field.StartAt,
					Length = field.Length,
					Name = field.Name,
					Value = field.Value
				}));
			else
				defaultVariableBuilder.Append(CompileVariableInitialization(structScope, field));
			defaultVariableBuilder.Append(',');
		}
		defaultVariableBuilder.Append("}");

		return defaultVariableBuilder.ToString();
	}

	private static void CompileReferenceStruct(Scope scope, Parser.TypeName typeName)
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
		structBuilder.Append($"public {typeName.Name}* Allocate({typeName.Name} initialValue)");
		structBuilder.Append("{");
		structBuilder.Append($"Pointer = ({typeName.Name}*)MasterScriptApi.Allocation.Allocate(Size);");
		structBuilder.Append("*Pointer = initialValue;");
		structBuilder.Append("return Pointer;");
		structBuilder.Append("}");
		structBuilder.Append($"public static {typeName.Name}* AddRef({typeName.Name}* pointer)");
		structBuilder.Append("{");
		structBuilder.Append("MasterScriptApi.Allocation.AddRef(pointer);");
		structBuilder.Append("return pointer;");
		structBuilder.Append("}");
		structBuilder.Append($"public static void RemoveRef({typeName.Name}* pointer)");
		structBuilder.Append("{");
		structBuilder.Append("MasterScriptApi.Allocation.RemoveRef(pointer);");
		structBuilder.Append("}");
		structBuilder.Append("}");

		scope.Script.Structs.Add(structBuilder.ToString());
	}
	
	private static string CompileVariableDefinition(Scope scope, Parser.VariableDefineCommand variableDefineCommand)
	{
		scope.DefineVariable(variableDefineCommand);
		if (!scope.IsTypeDefined(variableDefineCommand.TypeName.Name))
			throw new Exception($"Type {variableDefineCommand.TypeName.Name} is not defined");

		if (variableDefineCommand.TypeName.IsReference)
			CompileReferenceStruct(scope, variableDefineCommand.TypeName);

		var compiled = $"{variableDefineCommand.TypeName.Name}{(variableDefineCommand.TypeName.IsReference ? "*" : "")} {variableDefineCommand.Name}";

		return compiled;
	}

	private static string CompileVariableInitialization(Scope scope, Parser.VariableDefineCommand variableDefineCommand)
	{
		if (variableDefineCommand.Value is { })
		{
			return CompileVariableSet(scope, new Parser.VariableSetCommand
			{
				StartAt = variableDefineCommand.StartAt,
				Length = variableDefineCommand.Length,
				Name = variableDefineCommand.Name,
				Value = variableDefineCommand.Value
			});
		}
		
		if (variableDefineCommand.TypeName.IsReference) return "";

		var defaultValue = scope.IsStructDefined(variableDefineCommand.TypeName.Name)
			? $"{variableDefineCommand.TypeName.Name}_default"
			: "default";
		return $"{variableDefineCommand.Name} = {defaultValue}";
	}
		
	private static string CompileVariableSet(Scope scope, Parser.VariableSetCommand variableSetCommand)
	{
		if (!scope.IsVariableDefined(variableSetCommand.Name))
			throw new Exception($"Variable {variableSetCommand.Name} not defined");
		
		// If value is a reference and being allocated, set it as allocated on the scope
		if (variableSetCommand.Value is Parser.VariableAllocateCommand)
			scope.AllocateReference(variableSetCommand.Name);
		
		return $"{variableSetCommand.Name} = {CompileCommand(scope, variableSetCommand.Value, variableSetCommand)}";
	}

	private static string CompileVariableGet(Scope scope, Parser.VariableGetCommand variableGetCommand, Parser.VariableSetCommand variableSetCommand, bool noDefinitionCheck = false)
	{
		if (!noDefinitionCheck && !scope.IsVariableDefined(variableGetCommand.Name))
			throw new Exception($"Variable {variableGetCommand.Name} not defined");
		var getVariable = scope.GetVariable(variableGetCommand.Name);
		var setVariable = scope.GetVariable(variableSetCommand.Name);

		return getVariable.TypeName.IsReference switch
		{
			true when !scope.IsReferenceStructAllocated(variableGetCommand.Name) => throw new Exception($"Reference struct {variableGetCommand.Name} is not allocated"),
			true when !setVariable.TypeName.IsReference => $"*{variableGetCommand.Name}",
			_ => variableGetCommand.Name
		};
	}
	
	private static string CompileVariableAllocate(Scope scope, Parser.VariableAllocateCommand variableAllocateCommand, Parser.VariableSetCommand previousCommand)
	{
		var typeName = scope.GetVariable(previousCommand.Name).TypeName;
		if (!typeName.IsReference) throw new Exception("Variable allocate must be used in reference variable");
		var define = $"new _REF_{typeName.Name}().Allocate({CompileCommand(scope, variableAllocateCommand.Value, previousCommand)})";
		scope.After.Add($"_REF_{typeName.Name}.RemoveRef({previousCommand.Name});");
		return define;
	}

	private static string CompileNumberLiteral(Scope scope, Parser.NumberLiteralCommand numberCommand, Parser.VariableSetCommand previousCommand)
	{
		var type = scope.GetVariable(previousCommand.Name).TypeName.Name;
		var suffix = type switch
		{
			"_type_float_" => "f",
			"_type_double_" => "d",
			"_type_uint_" => "u",
			"_type_long_" => "l",
			"_type_ulong_" => "ul",
			_ => ""
		};
		return $"{numberCommand.Value}{suffix}";

	}
}