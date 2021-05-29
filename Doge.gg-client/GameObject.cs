using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Doge.gg_client
{
    public struct Vector3
    {
        public float X { get; private set; }
        public float Y { get; private set; }
        public float Z { get; private set; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X}, {Y}, {Z})";
        }

        public float DistanceBetween2D(Vector3 other)
        {
            return (float)Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y));
        }

        public float DistanceBetween(Vector3 other)
        {
            return (float)Math.Sqrt((X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y) + (Z - other.Z) * (Z - other.Z));
        }
    }
    public class GameObject
    {
        public int MemoryAddress { get; private set; }
        public float Health { get; private set; }
        public string Name { get; private set; }
        public string SpellName { get; private set; }
        public int Team { get; private set; }
        public Vector3 Position { get; private set; }
        private ProcessMemoryReader reader;
        public List<Buff> ActiveBuffs { get; private set; } = new List<Buff>();

        public GameObject(ProcessMemoryReader reader, int memoryAddress)
        {
            this.reader = reader;
            MemoryAddress = memoryAddress;
            float x = reader.ReadFloat(memoryAddress + Offsets.ObjPos);
            float y = reader.ReadFloat(memoryAddress + Offsets.ObjPos + 4);
            float z = reader.ReadFloat(memoryAddress + Offsets.ObjPos + 8);
            Position = new Vector3(x, y, z);
            Health = reader.ReadFloat(memoryAddress + Offsets.ObjHealth);
            Team = reader.ReadInt(memoryAddress + Offsets.ObjTeam);
            int name = reader.ReadInt(memoryAddress + Offsets.ObjName);
            if (name != 0)
            {
                Name = reader.ReadCString(name, 256);
            }

            int missilePointer = reader.ReadInt(MemoryAddress + Offsets.MissileSpellInfo);
            if (missilePointer == 0)
            {
                SpellName = null;
                return;
            }
            int spellPointer = reader.ReadInt(missilePointer + Offsets.SpellInfoSpellData);
            if (spellPointer == 0)
            {
                SpellName = null;
                return;
            }
            int spellName = reader.ReadInt(spellPointer + Offsets.SpellDataMissileName);
            if (spellName == 0)
            {
                SpellName = null;
                return;
            }

            SpellName = reader.ReadCString(spellName, 256);
        }

        public void ReadBuffs()
        {
            ActiveBuffs.Clear();
            int beginPtr = reader.ReadInt(MemoryAddress + Offsets.ObjBuffManager + Offsets.BuffManagerEntriesArray);
            int endPtr = reader.ReadInt(MemoryAddress + Offsets.ObjBuffManager + Offsets.BuffManagerEntriesArray + 0x4);
            if (beginPtr > endPtr || beginPtr <= 0 || endPtr <= 0 || endPtr - beginPtr > 1000) return;
            for (int i = beginPtr; i < endPtr; i += 0x4)
            {
                int buffPtr = reader.ReadInt(i);
                if (buffPtr == 0) continue;
                ActiveBuffs.Add(new Buff(reader, buffPtr));
            }
        }

        public static List<GameObject> GetGameObjects(ProcessMemoryReader reader, int objectManagerPointer)
        {
            int rootNode = reader.ReadInt(objectManagerPointer + Offsets.ObjectMapRoot);
            Queue<int> nodesToVisit = new Queue<int>();
            HashSet<int> visitedNodes = new HashSet<int>();
            List<int> pointers = new List<int>();

            nodesToVisit.Enqueue(rootNode);
            while (nodesToVisit.Count > 0)
            {
                var node = nodesToVisit.Dequeue();
                if (visitedNodes.Contains(node)) continue;
                visitedNodes.Add(node);
                nodesToVisit.Enqueue(reader.ReadInt(node));
                nodesToVisit.Enqueue(reader.ReadInt(node + 4));
                nodesToVisit.Enqueue(reader.ReadInt(node + 8));

                uint netId = reader.ReadUint(node + Offsets.ObjectMapNodeId);
                if (netId - 0x40000000 > 0x100000) continue;

                var pointer = reader.ReadInt(node + Offsets.ObjectMapNodeObject);
                if (pointer != 0)
                {
                    pointers.Add(pointer);
                }
            }

            return pointers.Select(p => new GameObject(reader, p)).ToList();
        }
    }
}