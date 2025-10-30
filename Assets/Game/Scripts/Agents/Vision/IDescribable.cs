/// <summary>
/// Provides read-only access to a textual description for world objects.
/// </summary>
public interface IDescribable
{
    string Description { get; }
    string Name { get; }

}
