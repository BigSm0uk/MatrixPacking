using System.Data;
using System.Net.NetworkInformation;
using ClosedXML.Excel;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class MatrixPackingService(ILogger<MatrixPackingService> logger)
{
    public byte[] ReadAndCalculate(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.First(); // Берём первый лист
        if (worksheet is null)
        {
            logger.LogError("Не удалось получить первый лист");
            throw new DataException("Не удалось получить первый лист");
        }

        var nodes = ParseNodesData(worksheet);
        var adjacencyMatrix = CreateAdjacencyMatrix(nodes);
        var (values, pointers) = PackMatrixSchemeCustom(adjacencyMatrix);

        var wb = new XLWorkbook();

        AddGraphToExcel(wb, nodes);
        AddMatrixToExcel(wb, adjacencyMatrix);
        AddPackingMatrixToExcel(wb, values, pointers);
        using var wbStream = new MemoryStream();
        wb.SaveAs(wbStream);

        return wbStream.ToArray();
    }

    private static void AddPackingMatrixToExcel(XLWorkbook wb, int[] values, int[] pointers)
    {
        var ws = wb.AddWorksheet("Упакованная матрица");

        // Добавление заголовков
        ws.Cell(1, 1).Value = "Значения";
        ws.Cell(1, 2).Value = "Индексы диагональных элементов";

        // Заполнение массива Values (по столбцу 1)
        for (var i = 0; i < values.Length; i++)
        {
            ws.Cell(i + 2, 1).Value = values[i];
        }

        // Заполнение массива Pointers (по столбцу 2)
        for (var i = 0; i < pointers.Length; i++)
        {
            ws.Cell(i + 2, 2).Value = pointers[i];
        }
    }


    private static void AddMatrixToExcel(XLWorkbook wb, int[,] matrix)
    {
        var ws = wb.AddWorksheet("Матрица смежности");

        var rows = matrix.GetLength(0); // Количество строк в матрице
        var cols = matrix.GetLength(1); // Количество столбцов в матрице

        // Заполнение Excel с данными из матрицы
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                // Заполняем ячейку 
                ws.Cell(i + 1, j + 1).SetValue(matrix[i, j]);
            }
        }
    }

    private static void AddGraphToExcel(XLWorkbook workbook, Dictionary<string, List<string>> graph)
    {
        // Создаем новый лист в Excel
        var worksheet = workbook.AddWorksheet("Исходные данные");

        // Заполняем заголовки
        worksheet.Cell(1, 1).Value = "Узел";
        worksheet.Cell(1, 2).Value = "Связи"; // Мы будем объединять все связи в одну колонку

        // Находим максимальное количество соседей
        var maxNeighbors = graph.Values.Max(neighbors => neighbors.Count);
        worksheet.Range(1, 2, 1, maxNeighbors).Merge();
        worksheet.Range(1, 2, 1, maxNeighbors).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 2, 1, maxNeighbors).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);
        worksheet.Cell(1, 1).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);
        // Заполняем данные
        var row = 2; // Начинаем с 2-й строки
        foreach (var node in graph)
        {
            worksheet.Cell(row, 1).Value = node.Key; // Имя узла

            var col = 2;
            foreach (var value in node.Value)
            {
                worksheet.Cell(row, col++).Value = value;
            }

            row++;
        }
    }


    private static Dictionary<string, List<string>> ParseNodesData(IXLWorksheet worksheet)
    {
        Dictionary<string, List<string>> result = [];
        var rows = worksheet.RowsUsed().Count();
        for (var row = 1; row <= rows; row++)
        {
            var from = worksheet.Cell(row, 1).Value.ToString();
            var to = worksheet.Cell(row, 2).Value.ToString();
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) break;
            if (result.TryGetValue(from, out var valueFrom)) valueFrom.Add(to);
            // if (result.TryGetValue(to, out var valueTo)) valueTo.Add(from);
            if (!result.ContainsKey(from)) result[from] = [to];
        }

        return result;
    }

    private static int[,] CreateAdjacencyMatrix(Dictionary<string, List<string>> graph)
    {
        // 1. Собираем список всех узлов
        var nodes = graph.Keys.Union(graph.Values.SelectMany(v => v)).Distinct().ToList();

        // Создаем словарь для быстрого доступа к индексам узлов
        var nodeIndex = nodes.Select((node, index) => new { node, index })
            .ToDictionary(x => x.node, x => x.index);

        // 2. Инициализируем матрицу
        var adjacencyMatrix = new int[nodes.Count, nodes.Count];

        // 3. Заполняем матрицу
        foreach (var (fromNode, neighbors) in graph)
        {
            if (!nodeIndex.TryGetValue(fromNode, out var fromIndex)) continue;

            foreach (var toNode in neighbors)
            {
                if (!nodeIndex.TryGetValue(toNode, out var toIndex)) continue;
                // Устанавливаем связь в обе стороны для неориентированного графа
                adjacencyMatrix[fromIndex, toIndex] = 1;
                adjacencyMatrix[toIndex, fromIndex] = 1;
            }
        }

        return adjacencyMatrix;
    }

    private static (int[] Values, int[] Pointers) PackMatrixSchemeCustom(int[,] adjacencyMatrix)
    {
        var size = adjacencyMatrix.GetLength(0); // Размер матрицы
        var values = new List<int>(); // Первый массив: элементы матрицы
        var pointers = new int[size]; // Второй массив: индексы диагональных элементов в Values

        for (var i = 0; i < size; i++)
        {
            // Сохраняем строку полностью от первого ненулевого элемента до диагонального
            var startColumn = 0; // Начинаем с первого элемента строки
            var endColumn = i; // Диагональный элемент находится в столбце i

            for (var j = startColumn; j <= endColumn; j++)
            {
                values.Add(adjacencyMatrix[i, j]); // Добавляем элемент в Values
            }

            // Индекс диагонального элемента
            pointers[i] = values.Count - 1;
        }

        return (values.ToArray(), pointers);
    }
}