using MasterScriptCompiler;

const string exampleScript = @"
{
	struct int3
	{
		x: int y: int z: int
	}

	struct Ray
	{
		origin: int3
		direction: int3
	}

	x: @double = 1
	y: @double = 2
	z: @int3
	number: int = 1 
	number2: float = 2.5

	y = x

	ray: Ray
}
";

File.WriteAllText(Path.Join(".", "compiled.cs"), Compiler.Compile(exampleScript));