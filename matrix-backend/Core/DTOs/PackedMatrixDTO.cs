namespace Core.DTOs;

public record PackedMatrixDTO
{
    public required int[] Values { get; init; } = [];
    public required int[] Pointers { get; init; } = [];
    public required int BandWidth { get; init; }
}