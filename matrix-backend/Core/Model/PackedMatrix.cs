using Core.Abstractions;

namespace Core.Model;

public class PackedMatrix : BaseEntity
{
    public double[] Values { get; set; } = [];
    public int[] Pointers { get; set; } = [];
    public int PackedSize => Values.Length + Pointers.Length;
    public int TotalMatrixSize { get; set; }
    public int MaxBandWidth { get; set; }
}