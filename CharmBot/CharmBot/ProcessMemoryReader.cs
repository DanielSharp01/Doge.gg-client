using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CharmBot
{

    public class ProcessMemoryReader
    {
        public Process Process { get; set; }
        private IntPtr handle;
        public int ModuleBaseAddr { get; private set; }

        public void Open()
        {
            ProcessMemoryReaderApi.ProcessAccessType access = ProcessMemoryReaderApi.ProcessAccessType.PROCESS_QUERY_INFORMATION | ProcessMemoryReaderApi.ProcessAccessType.PROCESS_VM_READ |ProcessMemoryReaderApi.ProcessAccessType.PROCESS_VM_OPERATION;
            handle = ProcessMemoryReaderApi.OpenProcess((uint)access, 0, (uint)Process.Id);
            ModuleBaseAddr = Process.Modules[0].BaseAddress.ToInt32();
        }

        public byte[] ReadBytes(int address, uint bytesToRead, out int bytesRead)
        {
            byte[] buffer = new byte[bytesToRead];
            IntPtr pBytesRead;
            ProcessMemoryReaderApi.ReadProcessMemory(handle, new IntPtr(address), buffer, bytesToRead, out pBytesRead);
            try
            {
                bytesRead = pBytesRead.ToInt32();
            }
            catch (Exception) { bytesRead = (int)bytesToRead; }
            return buffer;
        }

        public string ReadCString(int address, uint maxBytes)
        {
            List<byte> bytes = ReadBytes(address, maxBytes, out _).ToList();
            int i = bytes.FindIndex(b => b == 0);
            return Encoding.ASCII.GetString(bytes.ToArray(), 0, i < 0 ? (int)maxBytes : i);
        }

        public float ReadFloat(int address)
        {
            return BitConverter.ToSingle(ReadBytes(address, 4, out _));
        }

        public long ReadLong(int address)
        {
            return BitConverter.ToInt64(ReadBytes(address, 8, out _));
        }

        public ulong ReadUlong(int address)
        {
            return BitConverter.ToUInt64(ReadBytes(address, 8, out _));
        }

        public int ReadInt(int address)
        {
            return BitConverter.ToInt32(ReadBytes(address, 4, out _));
        }

        public uint ReadUint(int address)
        {
            return BitConverter.ToUInt32(ReadBytes(address, 4, out _));
        }

        public short ReadShort(int address)
        {
            return BitConverter.ToInt16(ReadBytes(address, 2, out _));
        }

        public byte ReadByte(int address)
        {
            return ReadBytes(address, 2, out _)[0];
        }

        public sbyte ReadSByte(int address)
        {
            return (sbyte)ReadByte(address);
        }

        public ushort ReadUshort(int address)
        {
            return BitConverter.ToUInt16(ReadBytes(address, 2, out _));
        }

        public bool Close()
        {
            return ProcessMemoryReaderApi.CloseHandle(handle) == 0;
        }
    }
}
