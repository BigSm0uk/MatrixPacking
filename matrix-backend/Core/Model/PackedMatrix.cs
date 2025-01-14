using Core.Abstractions;

namespace Core.Model;

public class PackedMatrix : BaseEntity
{
    public double[] Values { get; init; } = [];
    public int[] Pointers { get; init; } = [];
    public int PackedSize => Values.Length + Pointers.Length;
    public int TotalMatrixSize { get; init; }
    public int BandWidth { get; init; }
}