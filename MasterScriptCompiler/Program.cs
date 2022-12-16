using MasterScriptCompiler;

const string exampleScript = @"
{
	struct int3
	{
		x: int
		y: int
		z: int
	}

	struct Ray
	{
		origin: int3
		direction: int3
	}

	number: int = 1
	number2: float = 2.5

	hello: int = struct error
	{
	}

	ray: Ray
}
";

File.WriteAllText(Path.Join(".", "compiled.cs"), Compiler.Stringify(Parser.ParseScript(exampleScript)));