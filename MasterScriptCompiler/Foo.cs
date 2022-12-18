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
{
	public static unsafe class Program
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct int3
		{
			public int x;
			public int y;
			public int z;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Ray
		{
			public int3 origin;
			public int3 direction;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF_double
		{
			public static readonly int Size = Marshal.SizeOf<double>();
			public double* Pointer;

			public _REF_double(double initialValue)
			{
				Pointer = (double*)MasterScriptApi.Allocation.Allocate(Size);
				*Pointer = initialValue;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF_int3
		{
			public static readonly int Size = Marshal.SizeOf<int3>();
			public int3* Pointer;

			public _REF_int3(int3 initialValue)
			{
				Pointer = (int3*)MasterScriptApi.Allocation.Allocate(Size);
				*Pointer = initialValue;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _AnonymousStruct245_
		{
			public int x;
			public int y;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _AnonymousStruct301_
		{
			public int y;
			public int z;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _AnonymousStruct278_
		{
			public int x;
			public _AnonymousStruct301_ yz;
		}


		public static void Main()
		{
			int3 int3_default = new int3
			{
				x = 1,
				y = 2,
				z = 3,
			};
			;
			Ray Ray_default = new Ray
			{
			};
			;
			double* x = new _REF_double(default).Pointer;
			*x = 1;
			double* y = new _REF_double(default).Pointer;
			*y = 2;
			double z = default;
			z = *y;
			int number = default;
			number = 1;
			float number2 = default;
			number2 = 2.5f;
			int3* a = new _REF_int3(int3_default).Pointer;
			*y = *x;
			Ray ray = Ray_default;
			_AnonymousStruct245_ _AnonymousStruct245__default = new _AnonymousStruct245_
			{
			};
			;
			_AnonymousStruct301_ _AnonymousStruct301__default = new _AnonymousStruct301_
			{
				y = 1,
				z = 12,
			};
			;
			_AnonymousStruct278_ _AnonymousStruct278__default = new _AnonymousStruct278_
			{
				x = 123,
			};
			;
			_AnonymousStruct278_ test = _AnonymousStruct278__default;
		}
	}
}