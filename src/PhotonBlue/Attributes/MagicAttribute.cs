namespace PhotonBlue.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class MagicAttribute : Attribute
{
    public string Magic { get; }

    public MagicAttribute(string magic)
    {
        Magic = magic;
    }
}