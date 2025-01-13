namespace Core.Abstractions;

public abstract class BaseEntity
{
    public required Guid Id { get; init; }
}