﻿//------------------------------------------------------------------------------
//	<auto-generated>
//		This code was generated from a template.
//		Manual changes will be overwritten if the code is regenerated.
//	</auto-generated>
//------------------------------------------------------------------------------

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

#if HWINTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace Blake2Fast.Implementation
{
	/// <summary>Defines the state associated with an incremental BLAKE2b hashing operation.</summary>
	/// <remarks>Instances of this struct must be created by <see cref="Blake2b.CreateIncrementalHasher()" />.  An instance created directly will be unusable.</remarks>
#if BLAKE2_PUBLIC
	public
#else
	internal
#endif
	unsafe partial struct Blake2bHashState : IBlake2Incremental
	{
		internal const int WordSize = sizeof(ulong);
		internal const int BlockWords = 16;
		internal const int BlockBytes = BlockWords * WordSize;
		internal const int HashWords = 8;
		internal const int HashBytes = HashWords * WordSize;
		internal const int MaxKeyBytes = HashBytes;

		private fixed byte b[BlockBytes];
		private fixed ulong h[HashWords];
		private fixed ulong t[2];
		private fixed ulong f[2];
		private uint c;
		private uint outlen;

		private static ReadOnlySpan<byte> ivle => new byte[] {
			0x08, 0xC9, 0xBC, 0xF3, 0x67, 0xE6, 0x09, 0x6A,
			0x3B, 0xA7, 0xCA, 0x84, 0x85, 0xAE, 0x67, 0xBB,
			0x2B, 0xF8, 0x94, 0xFE, 0x72, 0xF3, 0x6E, 0x3C,
			0xF1, 0x36, 0x1D, 0x5F, 0x3A, 0xF5, 0x4F, 0xA5,
			0xD1, 0x82, 0xE6, 0xAD, 0x7F, 0x52, 0x0E, 0x51,
			0x1F, 0x6C, 0x3E, 0x2B, 0x8C, 0x68, 0x05, 0x9B,
			0x6B, 0xBD, 0x41, 0xFB, 0xAB, 0xD9, 0x83, 0x1F,
			0x79, 0x21, 0x7E, 0x13, 0x19, 0xCD, 0xE0, 0x5B
		};

#if HWINTRINSICS
		private static ReadOnlySpan<byte> rormask => new byte[] {
			3, 4, 5, 6, 7, 0, 1, 2, 11, 12, 13, 14, 15, 8, 9, 10, //r24
			2, 3, 4, 5, 6, 7, 0, 1, 10, 11, 12, 13, 14, 15, 8, 9  //r16
		};
#endif

		/// <inheritdoc />
		public int DigestLength => (int)outlen;

		private void compress(ref byte input, uint offs, uint cb)
		{
			uint inc = Math.Min(cb, BlockBytes);

			fixed (byte* pinput = &input)
			fixed (Blake2bHashState* s = &this)
			{
				ulong* sh = s->h;
				byte* pin = pinput + offs;
				byte* end = pin + cb;

				do
				{
					t[0] += inc;
					if (t[0] < inc)
						t[1]++;

					ulong* m = (ulong*)pin;
#if HWINTRINSICS
#if !HWINTRINSICS_EXP
					if (Avx2.IsSupported)
						mixAvx2(sh, m);
					else
#endif
					if (Sse41.IsSupported)
						mixSse41(sh, m);
					else
#endif
						mixScalar(sh, m);

					pin += inc;
				} while (pin < end);
			}
		}

		public void Init(int digestLength = HashBytes, ReadOnlySpan<byte> key = default)
		{
			uint keylen = (uint)key.Length;

			if (!BitConverter.IsLittleEndian) ThrowHelper.NoBigEndian();
			if (digestLength == 0 || (uint)digestLength > HashBytes) ThrowHelper.DigestInvalidLength(HashBytes);
			if (keylen > MaxKeyBytes) ThrowHelper.KeyTooLong(MaxKeyBytes);

			UnsafeInit(digestLength, key);
		}
		
		public void UnsafeInit(int digestLength = HashBytes, ReadOnlySpan<byte> key = default)
		{
			uint keylen = (uint)key.Length;

			outlen = (uint)digestLength;

			Unsafe.CopyBlock(ref Unsafe.As<ulong, byte>(ref h[0]), ref MemoryMarshal.GetReference(ivle), HashBytes);
			h[0] ^= 0x01010000u ^ (keylen << 8) ^ outlen;

			if (keylen != 0)
			{
				Unsafe.CopyBlockUnaligned(ref b[0], ref MemoryMarshal.GetReference(key), keylen);
				c = BlockBytes;
			}
		}

		private void update(ReadOnlySpan<byte> input)
		{
			if (outlen == 0) ThrowHelper.HashNotInitialized();
			if (f[0] != 0) ThrowHelper.HashFinalized();

			UnsafeUpdate(input);
		}
		
		public void UnsafeUpdate(ReadOnlySpan<byte> input)
		{
			uint consumed = 0;
			uint remaining = (uint)input.Length;
			ref byte rinput = ref MemoryMarshal.GetReference(input);

			uint blockrem = BlockBytes - c;
			if ((c != 0) && (remaining > blockrem))
			{
				if (blockrem != 0)
					Unsafe.CopyBlockUnaligned(ref b[c], ref rinput, blockrem);

				c = 0;
				compress(ref b[0], 0, BlockBytes);
				consumed += blockrem;
				remaining -= blockrem;
			}

			if (remaining > BlockBytes)
			{
				uint cb = (remaining - 1) & ~((uint)BlockBytes - 1);
				compress(ref rinput, consumed, cb);
				consumed += cb;
				remaining -= cb;
			}

			if (remaining != 0)
			{
				Unsafe.CopyBlockUnaligned(ref b[c], ref Unsafe.Add(ref rinput, (int)consumed), remaining);
				c += remaining;
			}
		}

		/// <inheritdoc />
		public void Update<T>(ReadOnlySpan<T> input) where T : struct
		{
			ThrowHelper.ThrowIfIsRefOrContainsRefs<T>();

			update(MemoryMarshal.AsBytes(input));
		}

		/// <inheritdoc />
		public void Update<T>(Span<T> input) where T : struct => Update((ReadOnlySpan<T>)input);

		/// <inheritdoc />
		public void Update<T>(ArraySegment<T> input) where T : struct => Update((ReadOnlySpan<T>)input);

		/// <inheritdoc />
		public void Update<T>(T[] input) where T : struct => Update((ReadOnlySpan<T>)input);

		/// <inheritdoc />
		public void Update<T>(T input) where T : struct
		{
			ThrowHelper.ThrowIfIsRefOrContainsRefs<T>();

			if (Unsafe.SizeOf<T>() > BlockBytes - c)
			{
#if BUILTIN_SPAN
				Update(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<T, byte>(ref input), Unsafe.SizeOf<T>()));
#else
				var buff = (Span<byte>)stackalloc byte[Unsafe.SizeOf<T>()];
				Unsafe.WriteUnaligned(ref buff[0], input);
				update(buff);
#endif
				return;
			}

			if (f[0] != 0) ThrowHelper.HashFinalized();

			Unsafe.WriteUnaligned(ref b[c], input);
			c += (uint)Unsafe.SizeOf<T>();
		}

		private void finish(Span<byte> hash)
		{
			if (outlen == 0) ThrowHelper.HashNotInitialized();
			if (f[0] != 0) ThrowHelper.HashFinalized();

			UnsafeFinish(hash);
		}
		
		public void UnsafeFinish(Span<byte> hash)
		{
			if (c < BlockBytes)
				Unsafe.InitBlockUnaligned(ref b[c], 0, BlockBytes - c);

			f[0] = ~0ul;
			compress(ref b[0], 0, c);

			Unsafe.CopyBlockUnaligned(ref hash[0], ref Unsafe.As<ulong, byte>(ref h[0]), outlen);
		}

		/// <inheritdoc />
		public byte[] Finish()
		{
			byte[] hash = new byte[outlen];
			finish(hash);

			return hash;
		}

		/// <inheritdoc />
		public void Finish(Span<byte> output)
		{
			if ((uint)output.Length < outlen) ThrowHelper.OutputTooSmall(DigestLength);

			finish(output);
		}

		/// <inheritdoc />
		public bool TryFinish(Span<byte> output, out int bytesWritten)
		{
			if ((uint)output.Length < outlen)
			{
				bytesWritten = 0;
				return false;
			}

			finish(output);
			bytesWritten = (int)outlen;
			return true;
		}
	}
}
