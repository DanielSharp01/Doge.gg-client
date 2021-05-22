namespace Doge.gg_client
{
    public class Buff
    {
        public float StartTime { get; private set; }
        public float EndTime { get; private set; }
        public string Name { get; private set; }

        public Buff(ProcessMemoryReader reader, int pointer)
        {
            StartTime = reader.ReadFloat(pointer + Offsets.BuffStartTime);
            EndTime = reader.ReadFloat(pointer + Offsets.BuffEndTime);
            int buffInfo = reader.ReadInt(pointer + Offsets.BuffEntryBuff);
            if (buffInfo < 10) return;
            Name = reader.ReadCString(buffInfo + Offsets.BuffName, 240);
        }

        public override string ToString()
        {
            return $"{Name} ({StartTime} - {EndTime})";
        }
    }
}