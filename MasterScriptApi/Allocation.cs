using System.Runtime.InteropServices;

namespace MasterScriptApi;

public static unsafe class Allocation
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Head
	{
		public const int Size = sizeof(uint);
		public uint ReferenceCount;
	}
	
	public static void* Allocate(int size)
	{
		var ptr = (void*)Marshal.AllocHGlobal(size + Head.Size);
		return AddRef((byte*)ptr + Head.Size);
	}
	
	public static Head* GetHead(void* ptr)
	{
		return (Head*)((byte*)ptr - Head.Size);
	}
	
	public static void* AddRef(void* ptr)
	{
		Interlocked.Increment(ref GetHead(ptr)->ReferenceCount);
		return ptr;
	}
	
	public static void RemoveRef(void* ptr)
	{
		var head = GetHead(ptr);
		if (head->ReferenceCount == 0) return;
		// ReSharper disable once ConditionIsAlwaysTrueOrFalse
		if (head->ReferenceCount < 0) throw new Exception("Reference count is negative. This should never happen.");
		if (Interlocked.Decrement(ref head->ReferenceCount) == 0)
			Marshal.FreeHGlobal((IntPtr)head);
	}
} 