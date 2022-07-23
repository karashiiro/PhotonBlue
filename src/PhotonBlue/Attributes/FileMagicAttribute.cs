namespace PhotonBlue.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class FileMagicAttribute : Attribute
{
    public string Value { get; }

    public FileMagicAttribute(string magic)
    {
        Value = magic;
    }
}