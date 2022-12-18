using MasterScriptCompiler;

const string exampleScript = @"
{
	struct int3
	{
		x: int = 1 
		y: int = 2
		z: int = 3
	}

	struct Ray
	{
		var origin: int3
		direction: int3
	}

	x: @double = alloc 1
	y: @double = alloc 2
	z: double = y
	number: int = 1 
	number2: float = 2.5

	a: @int3
	b: int3

	y = x

	ray: Ray

	struct
	{
		x: int
		y: int
	}

	test: {
		x: int = 123
		yz: {
			y: int = 1
			z: @int = alloc 12
		}
	}
";

File.WriteAllText(Path.Join(".", "compiled.cs"), Compiler.Compile(exampleScript));