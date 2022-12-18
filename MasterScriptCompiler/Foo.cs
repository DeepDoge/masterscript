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
		public struct _type_int3_at_2
		{
			public _type_int_ _var_x_;
			public _type_int_ _var_y_;
			public _type_int_ _var_z_;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _type_Ray_at_2
		{
			public _type_int3_at_2 _var_origin_;
			public _type_int3_at_2 _var_direction_;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF__type_double_
		{
			public static readonly int Size = Marshal.SizeOf<_type_double_>();
			public _type_double_* Pointer;

			public _REF__type_double_(_type_double_ initialValue)
			{
				Pointer = (_type_double_*)MasterScriptApi.Allocation.Allocate(Size);
				*Pointer = initialValue;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF__type_int3_at_2
		{
			public static readonly int Size = Marshal.SizeOf<_type_int3_at_2>();
			public _type_int3_at_2* Pointer;

			public _REF__type_int3_at_2(_type_int3_at_2 initialValue)
			{
				Pointer = (_type_int3_at_2*)MasterScriptApi.Allocation.Allocate(Size);
				*Pointer = initialValue;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _type__AnonymousStruct254__at_2
		{
			public _type_int_ _var_x_;
			public _type_int_ _var_y_;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _type__AnonymousStruct310__at_2
		{
			public _type_int_ _var_y_;
			public _type_int_ _var_z_;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _type__AnonymousStruct287__at_2
		{
			public _type_int_ _var_x_;
			public _type__AnonymousStruct310__at_2 _var_yz_;
		}

		public static void Main()
		{
			/** Block: 2 **/
			{
				_type_int3_at_2 _type_int3_at_2_default = new _type_int3_at_2 { _var_x_ = 1, _var_y_ = 2, _var_z_ = 3, };
				_type_Ray_at_2 _type_Ray_at_2_default = new _type_Ray_at_2 { };
				_type_double_* _var_x_ = new _REF__type_double_(default).Pointer;
				*_var_x_ = 1;
				_type_double_* _var_y_ = new _REF__type_double_(default).Pointer;
				*_var_y_ = 2;
				_type_double_ _var_z_ = default;
				_var_z_ = *_var_y_;
				_type_int_ _var_number_ = default;
				_var_number_ = 1;
				_type_float_ _var_number2_ = default;
				_var_number2_ = 2.5f;
				_type_int3_at_2* _var_a_ = new _REF__type_int3_at_2(_type_int3_at_2_default).Pointer;
				_type_int3_at_2 _var_b_ = _type_int3_at_2_default;
				*_var_y_ = *_var_x_;
				_type_Ray_at_2 _var_ray_ = _type_Ray_at_2_default;
				_type__AnonymousStruct254__at_2 _type__AnonymousStruct254__at_2_default = new _type__AnonymousStruct254__at_2 { };
				_type__AnonymousStruct310__at_2 _type__AnonymousStruct310__at_2_default = new _type__AnonymousStruct310__at_2 { _var_y_ = 1, _var_z_ = 12, };
				_type__AnonymousStruct287__at_2 _type__AnonymousStruct287__at_2_default = new _type__AnonymousStruct287__at_2 { _var_x_ = 123, };
				_type__AnonymousStruct287__at_2 _var_test_ = _type__AnonymousStruct287__at_2_default;
			}
		}
	}
}