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
{
	public static unsafe class Program
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct _int3_at_0e9c5f242397_
		{
			public _int_ x;
			public _int_ y;
			public _int_ z;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _Ray_at_0e9c5f242397_
		{
			public _int3_at_0e9c5f242397_ origin;
			public _int3_at_0e9c5f242397_ direction;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF_double_
		{
			public static readonly int Size = Marshal.SizeOf<_double_>();
			public readonly _double_* Pointer;

			public _REF_double_(_double_ initialValue)
			{
				Pointer = (_double_*)MasterScriptApi.Allocation.Alloc(Size);
				*Pointer = initialValue;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF_int3_at_0e9c5f242397_
		{
			public static readonly int Size = Marshal.SizeOf<_int3_at_0e9c5f242397_>();
			public readonly _int3_at_0e9c5f242397_* Pointer;

			public _REF_int3_at_0e9c5f242397_(_int3_at_0e9c5f242397_ initialValue)
			{
				Pointer = (_int3_at_0e9c5f242397_*)MasterScriptApi.Allocation.Alloc(Size);
				*Pointer = initialValue;
			}
		}

		public static void Main()
		{
			{
				// Block: 0e9c5f242397
// Struct: _int3_at_0e9c5f242397_
// ;
// Struct: _Ray_at_0e9c5f242397_
// ;
				_double_* _x_;
				_x_ = (_double_*)MasterScriptApi.Allocation.AddRef(new _REF_double_(1d).Pointer);
				_double_* _y_;
				_y_ = (_double_*)MasterScriptApi.Allocation.AddRef(new _REF_double_(2d).Pointer);
				_int3_at_0e9c5f242397_* _z_;
				_int_ _number_;
				_number_ = 1;
				_float_ _number2_;
				_number2_ = 2.5f;
				MasterScriptApi.Allocation.RemoveRef(_y_);
				_y_ = (_double_*)MasterScriptApi.Allocation.AddRef(_x_);
				_Ray_at_0e9c5f242397_ _ray_;
				MasterScriptApi.Allocation.RemoveRef(_x_);
				MasterScriptApi.Allocation.RemoveRef(_y_);
			}
		}
	}
}