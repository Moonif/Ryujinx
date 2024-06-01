using Ryujinx.Common.Logging;
using Ryujinx.Graphics.GAL;
using SharpMetal.Metal;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Ryujinx.Graphics.Metal
{
    [SupportedOSPlatform("macos")]
    public class BufferManager : IDisposable
    {
        private readonly IdList<BufferHolder> _buffers;

        private readonly MTLDevice _device;

        public int BufferCount { get; private set; }

        public BufferManager(MTLDevice device)
        {
            _device = device;
            _buffers = new IdList<BufferHolder>();
        }

        public BufferHandle Create(nint pointer, int size)
        {
            var buffer = _device.NewBuffer(pointer, (ulong)size, MTLResourceOptions.ResourceStorageModeShared);

            if (buffer == IntPtr.Zero)
            {
                Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create buffer with size 0x{size:X}, and pointer 0x{pointer:X}.");

                return BufferHandle.Null;
            }

            var holder = new BufferHolder(buffer, size);

            BufferCount++;

            ulong handle64 = (uint)_buffers.Add(holder);

            return Unsafe.As<ulong, BufferHandle>(ref handle64);
        }

        public BufferHandle CreateWithHandle(int size)
        {
            return CreateWithHandle(size, out _);
        }

        public BufferHandle CreateWithHandle(int size, out BufferHolder holder)
        {
            holder = Create(size);

            if (holder == null)
            {
                return BufferHandle.Null;
            }

            BufferCount++;

            ulong handle64 = (uint)_buffers.Add(holder);

            return Unsafe.As<ulong, BufferHandle>(ref handle64);
        }

        public BufferHolder Create(int size)
        {
            var buffer = _device.NewBuffer((ulong)size, MTLResourceOptions.ResourceStorageModeShared);

            if (buffer != IntPtr.Zero)
            {
                return new BufferHolder(buffer, size);
            }

            Logger.Error?.PrintMsg(LogClass.Gpu, $"Failed to create buffer with size 0x{size:X}.");

            return null;
        }

        public MTLBuffer? GetBuffer(BufferHandle handle, int offset, int size, bool isWrite)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetBuffer(offset, size, isWrite);
            }

            return null;
        }

        public MTLBuffer? GetBuffer(BufferHandle handle, bool isWrite, out int size)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                size = holder.Size;
                return holder.GetBuffer(isWrite);
            }

            size = 0;
            return null;
        }

        public PinnedSpan<byte> GetData(BufferHandle handle, int offset, int size)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                return holder.GetData(offset, size);
            }

            return new PinnedSpan<byte>();
        }

        public void SetData<T>(BufferHandle handle, int offset, ReadOnlySpan<T> data) where T : unmanaged
        {
            SetData(handle, offset, MemoryMarshal.Cast<T, byte>(data), null);
        }

        public void SetData(BufferHandle handle, int offset, ReadOnlySpan<byte> data, Action endRenderPass)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                holder.SetData(offset, data, endRenderPass);
            }
        }

        public void Delete(BufferHandle handle)
        {
            if (TryGetBuffer(handle, out var holder))
            {
                holder.Dispose();
                _buffers.Remove((int)Unsafe.As<BufferHandle, ulong>(ref handle));
            }
        }

        private bool TryGetBuffer(BufferHandle handle, out BufferHolder holder)
        {
            return _buffers.TryGetValue((int)Unsafe.As<BufferHandle, ulong>(ref handle), out holder);
        }

        public void Dispose()
        {
            foreach (var buffer in _buffers)
            {
                buffer.Dispose();
            }
        }
    }
}
