namespace XFrame.Persistence.EFCore.Attributes
{
    [AttributeUsage(
        AttributeTargets.Property,
        AllowMultiple = true
        )]
    public class MapTo : Attribute
    {
        public MapTo(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
