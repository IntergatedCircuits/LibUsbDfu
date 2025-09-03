namespace DeviceProgramming.Memory
{
    public class NamedLayout : Layout
    {
        public string Name { get; private set; }

        public NamedLayout(string name)
            : base()
        {
            Name = name;
        }
    }
}
