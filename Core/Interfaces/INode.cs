namespace Core.Interfaces;

public interface INode
{
    public string Name { get; init; }
    int Row { get; init; }      // Строка, к которой относится элемент
    int Column { get; init; }   // Столбец, к которому относится элемент
}