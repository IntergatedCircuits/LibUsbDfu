namespace DeviceProgramming.Memory
{
    public class NamedMemory : RawMemory
    {
        public string Name { get; private set; }

        public NamedMemory(string name)
            : base()
        {
            Name = name;
        }
    }
}
