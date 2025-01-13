using Core.Abstractions;
using Core.Interfaces;

namespace Core.Model;

public class Node : BaseEntity, INode
{
    public required string Name { get; init; }
    public int Row { get; init; }
    public int Column { get; init; }
}