using System;
using System.Runtime.InteropServices;
using MasterScriptApi;

namespace MasterScript
{
	public static unsafe class Program
	{
		[StructLayout(LayoutKind.Sequential)]
		public struct int3_at_efe5e58f9264
		{
			public int x;
			public int y;
			public int z;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct Ray_at_efe5e58f9264
		{
			public int3_at_efe5e58f9264 origin;
			public int3_at_efe5e58f9264 direction;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct _REF_double
		{
			public static readonly int Size = Marshal.SizeOf<double>();
			public readonly double* Pointer;

			public _REF_double(double initialValue)
			{
				Pointer = (double*)MasterScriptApi.Allocation.Alloc(Size);
				*Pointer = initialValue;
			}
		}

		public static void Main()
		{
			{
				// Block: efe5e58f9264
// Struct: int3_at_efe5e58f9264
// ;
// Struct: Ray_at_efe5e58f9264
// ;
				double* _x_;
				_x_ = (double*)MasterScriptApi.Allocation.AddRef(new _REF_double(1).Pointer);
				double* _y_;
				_y_ = (double*)MasterScriptApi.Allocation.AddRef(new _REF_double(2).Pointer);
				int _number_;
				_number_ = 1;
				float _number2_;
				_number2_ = 2.5f;
				MasterScriptApi.Allocation.RemoveRef(_y_);
				_y_ = (double*)MasterScriptApi.Allocation.AddRef(_x_);
				Ray_at_efe5e58f9264 _ray_;
				MasterScriptApi.Allocation.RemoveRef(_x_);
				MasterScriptApi.Allocation.RemoveRef(_y_);
			}
		}
	}
}