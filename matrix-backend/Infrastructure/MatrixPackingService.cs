using ClosedXML.Excel;
using Core.Abstractions;
using Core.Model;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public class MatrixPackingService(ILogger<MatrixPackingService> logger, IMemoryCache memoryCache)
{
    public Result<Guid> ReadAndCalculate(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault(); // Берём первый лист
        if (worksheet == null)
        {
            logger.LogError("Не удалось получить первый лист");
            return Result<Guid>.Failure("Не удалось получить первый лист");
        }

        var nodes = ParseNodesData(worksheet);
        var adjacencyMatrix = CreateAdjacencyMatrix(nodes);
        var bandWidths = CalculateBandWidths(adjacencyMatrix);
        var (values, pointers) = PackMatrixScheme4(adjacencyMatrix, bandWidths);

        var id = Guid.NewGuid();

        var packedMatrixResult = new PackedMatrix
        {
            Id = id,
            Pointers = pointers,
            MaxBandWidth = bandWidths.Max(),
            Values = values,
            TotalMatrixSize = adjacencyMatrix.Length
        };

        memoryCache.Set(id.ToString(), packedMatrixResult, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30) // Время жизни продлевается при каждом обращении
        }); // Сохраняем в локальный кэш

        return Result<Guid>.Success(id);
    }

    public Result<bool> ChangeMatrixElementInPackedForm(Guid id, int row, int col, int newValue)
    {
        // Получаем упакованную матрицу из кэша
        var matrixResult = GetPackedMatrixFromCache(id);
        if (!matrixResult.IsSuccess) return Result<bool>.Failure(matrixResult.Error!);
        var matrix = matrixResult.Value!;

        // Проверяем корректность входных данных
        if (row < 0 || row >= matrix.TotalMatrixSize || col < 0 || col >= matrix.TotalMatrixSize)
            return Result<bool>.Failure("Индекс строки или столбца вышел за границы матрицы.");

        // Для симметричной матрицы приводим индексы к стандартной форме (row >= col)
        if (row < col) (row, col) = (col, row);

        // Если работаем с первой строкой (диагональ)
        if (row == 0 && col == 0)
        {
            matrix.Values[0] = newValue; // Диагональ всегда находится в первом элементе Values
            CreateOrUpdatePackedMatrixInCache(id, matrix);
            return Result<bool>.Success(true);
        }

        // Определяем текущую локальную ширину ленты для строки
        var bandWidth = matrix.Pointers[row] - matrix.Pointers[row - 1] - 1;

        if (col >= row - bandWidth)
        {
            // Элемент находится в пределах ленты или на границе
            var pos = matrix.Pointers[row] - (row - col);

            if (col == row - bandWidth && newValue == 0)
            {
                // Если значение на границе ленты становится нулевым, пересчитываем локальную ширину
                var newBandWidth = RecalculateLocalBandWidth(matrix, row);
                var difference = newBandWidth - bandWidth;

                if (difference < 0) // Если локальная ширина уменьшилась
                {
                    // Удаляем лишние элементы из Values
                    var startIndex = matrix.Pointers[row - 1] + newBandWidth + 1;
                    var endIndex = matrix.Pointers[row];
                    var valuesList = matrix.Values.ToList();
                    valuesList.RemoveRange(startIndex, endIndex - startIndex);
                    matrix.Values = valuesList.ToArray();

                    // Корректируем указатели
                    for (var i = row; i < matrix.Pointers.Length; i++)
                    {
                        matrix.Pointers[i] += difference;
                    }
                }
            }
            else
            {
                // Обновляем значение
                matrix.Values[pos] = newValue;
            }
        }
        else if (newValue != 0)
        {
            // Если элемент за границей текущей ленты и новое значение не 0
            ExpandBandWidth(matrix, row, col, newValue);
        }

        // Сохраняем изменённую матрицу
        CreateOrUpdatePackedMatrixInCache(id, matrix);
        return Result<bool>.Success(true);
    }

    /// <summary>
    /// Пересчитывает локальную ширину ленты для строки после изменения значений.
    /// </summary>
    private static int RecalculateLocalBandWidth(PackedMatrix matrix, int row)
    {
        var startIndex = matrix.Pointers[row - 1] + 2; // Начало ленты
        var endIndex = matrix.Pointers[row]; // Конец ленты
        for (var i = startIndex; i <= endIndex; i++)
        {
            if (matrix.Values[i] != 0)
                return endIndex - i;
        }
        return 0;
    }

    /// <summary>
    /// Расширяет ленту для строки, добавляя новый элемент за её границей, а также все промежуточные нулевые элементы.
    /// </summary>
    private static void ExpandBandWidth(PackedMatrix matrix, int row, int col, int newValue)
    {
        // Вычисляем новую ширину ленты
        var newBandWidth = row - col;

        // Текущая ширина ленты
        var currentBandWidth = matrix.Pointers[row] - matrix.Pointers[row - 1] - 1;

        // Разница между новой и текущей шириной
        var widthDifference = newBandWidth - currentBandWidth;

        // Позиция вставки нового элемента
        var insertPosition = matrix.Pointers[row - 1] + 1;

        // Создаём список значений и вставляем новый элемент
        var valuesList = matrix.Values.ToList();
        valuesList.Insert(insertPosition, newValue);

        // Вставляем промежуточные нули (widthDifference - 1, так как один элемент уже вставили)
        valuesList.InsertRange(insertPosition + 1, Enumerable.Repeat(0.0, widthDifference - 1));

        // Обновляем массив значений
        matrix.Values = valuesList.ToArray();

        // Корректируем указатели всех строк, начиная с текущей
        for (var i = row; i < matrix.Pointers.Length; i++)
        {
            matrix.Pointers[i] += widthDifference; // Увеличиваем указатель на разницу ширин
        }
    }

    private static int[] CalculateBandWidths(double[,] adjacencyMatrix)
    {
        var n = adjacencyMatrix.GetLength(0); // Размер матрицы
        var bandwidths = new int[adjacencyMatrix.GetLength(0)];

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j <= i; j++)
            {
                var bandwidth = Math.Abs(i - j); // Смещение от диагонали
                bandwidths[i] = bandwidth;
                if (adjacencyMatrix[i, j] == 0) continue;
                bandwidths[i] = bandwidth;
                break;
            }
        }

        return bandwidths;
    }

    private static int[] CalculateBandWidths(int[] pointers)
    {
        var n = pointers.Length; // Размер матрицы
        var bandwidths = new int[pointers.Length];
        bandwidths[0] = 0;
        for (var i = 1; i < n; i++)
        {
            bandwidths[i] = pointers[i] - pointers[i - 1] - 1;
        }

        return bandwidths;
    }

    private void CreateOrUpdatePackedMatrixInCache(Guid id, PackedMatrix updatedMatrix)
    {
        memoryCache.Set(id.ToString(), updatedMatrix, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(20) // Время жизни продлевается при каждом обращении
        }); // Сохраняем в локальный кэш
    }


    public Result<PackedMatrix> GetPackedMatrixFromCache(Guid id)
    {
        return !memoryCache.TryGetValue(id.ToString(), out PackedMatrix? matrix)
            ? Result<PackedMatrix>.Failure("Нет сессии с таким id")
            : Result<PackedMatrix>.Success(matrix!);
    }

    public Result<byte[]> GetResultMatrixFile(Guid id)
    {
        if (!memoryCache.TryGetValue(id.ToString(), out PackedMatrix? matrix))
            return Result<byte[]>.Failure("Нет сессии с таким id");

        using var workbook = new XLWorkbook();
        var bandWidths = CalculateBandWidths(matrix!.Pointers);
        var unPackedMatrix = UnPackMatrixScheme4(matrix.Values, matrix.Pointers, bandWidths);

        AddPackingMatrixToExcel(workbook, matrix.Values, matrix.Pointers);
        AddMatrixToExcel(workbook, unPackedMatrix, bandWidths, "Распакованная матрица");

        using var wbStream = new MemoryStream();

        // Убедитесь, что сохраняете в формат Excel
        workbook.SaveAs(wbStream);

        // Перемещаем позицию потока в начало перед тем, как читать его
        wbStream.Seek(0, SeekOrigin.Begin);

        // Проверка, не пуст ли поток (если не пуст, возвращаем файл)
        return wbStream.Length == 0
            ? Result<byte[]>.Failure("Не удалось создать файл Excel.")
            : Result<byte[]>.Success(wbStream.ToArray());
    }


    private static void AddPackingMatrixToExcel(XLWorkbook wb, double[] values, int[] pointers)
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


    private static void AddMatrixToExcel(XLWorkbook wb, double[,] matrix, int[] bandWidths,
        string sheetName = "Матрица смежности")
    {
        var ws = wb.AddWorksheet(sheetName);

        var rows = matrix.GetLength(0); // Количество строк в матрице
        var cols = matrix.GetLength(1); // Количество столбцов в матрице

        // Заполнение Excel с данными из матрицы
        for (var i = 0; i < rows; i++)
        {
            for (var j = 0; j < cols; j++)
            {
                // Заполняем ячейку значением из матрицы
                var cell = ws.Cell(i + 1, j + 1);
                cell.SetValue(matrix[i, j]);

                // Установка стиля фона для диагональных элементов
                if (i == j)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
                }
                // Установка стиля фона для элементов за пределами ширины ленты
                else if ((j > i && j - i > bandWidths[j]) || (i > j && i - j > bandWidths[i]))
                {
                    cell.Style.Fill.BackgroundColor = XLColor.LightGreen;
                }

                // Установка стиля фона для всех ненулевых элементов
                if (matrix[i, j] != 0)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.CoralRed;
                }
            }
        }
    }

    private static OrderedDictionary<string, List<(string, double)>> ParseNodesData(IXLWorksheet worksheet)
    {
        var result = new OrderedDictionary<string, List<(string, double)>>();
        var rows = worksheet.RowsUsed().Count();

        // Сопоставляем имена с индексами (ключи — строки/столбцы матрицы)
        for (var row = 1; row <= rows; row++)
        {
            var node = worksheet.Cell(row, 6).Value.ToString();
            if (string.IsNullOrWhiteSpace(node)) break;
            result[node] = [];
        }

        for (var row = 1; row <= rows; row++)
        {
            var from = worksheet.Cell(row, 1).Value.ToString();
            var to = worksheet.Cell(row, 2).Value.ToString();

            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to)) break;

            var nodeValue = double.Parse(worksheet.Cell(row, 3).Value.ToString());

            if (result.TryGetValue(from, out var valueFrom))
                valueFrom.Add((to, nodeValue));
            else
                result[from] = [(to, nodeValue)];
        }

        return result;
    }

    private static double[,] CreateAdjacencyMatrix(IDictionary<string, List<(string, double)>> graph)
    {
        // 1. Собираем список всех узлов
        var nodes = graph.Keys.Union(graph.Values.SelectMany(v => v.Select(edge => edge.Item1))).Distinct().ToList();

        // Создаем словарь для быстрого доступа к индексам узлов
        var nodeIndex = nodes.Select((node, index) => new { node, index })
            .ToDictionary(x => x.node, x => x.index);

        // 2. Инициализируем матрицу
        var adjacencyMatrix = new double[nodes.Count, nodes.Count];

        // 3. Заполняем матрицу
        foreach (var (fromNode, neighbors) in graph)
        {
            if (!nodeIndex.TryGetValue(fromNode, out var fromIndex)) continue;

            foreach (var (toNode, weight) in neighbors)
            {
                if (!nodeIndex.TryGetValue(toNode, out var toIndex)) continue;

                // Устанавливаем вес в обе стороны для графа
                adjacencyMatrix[fromIndex, toIndex] = weight;
                adjacencyMatrix[toIndex, fromIndex] = weight;
            }
        }

        return adjacencyMatrix;
    }


    private static (double[] Values, int[] Pointers) PackMatrixScheme4(double[,] adjacencyMatrix, int[] bandWidths)
    {
        var size = adjacencyMatrix.GetLength(0); // Размер матрицы
        var values = new List<double>(); // Первый массив: элементы матрицы
        var pointers = new int[size]; // Второй массив: индексы диагональных элементов в Values
        for (var i = 0; i < size; i++)
        {
            var startColumn = i - bandWidths[i];
            for (var j = startColumn; j <= i; j++)
            {
                values.Add(adjacencyMatrix[i, j]);
            }

            pointers[i] = values.Count - 1;
        }

        return (values.ToArray(), pointers);
    }

    private static double[,] UnPackMatrixScheme4(double[] values, int[] pointers, int[] bandWidths)
    {
        var size = pointers.Length; // Размер матрицы (по числу диагональных элементов)
        var adjacencyMatrix = new double[size, size]; // Инициализация пустой матрицы

        var valueIndex = 0; // Текущий индекс в массиве values

        for (var i = 0; i < size; i++)
        {
            // Определяем диапазон столбцов, которые входят в ширину ленты
            var startColumn = Math.Max(0, i - bandWidths[i]); // Левый край ленты
            for (var j = startColumn; j <= i; j++)
            {
                // Заполняем элемент из values
                var value = values[valueIndex++];
                adjacencyMatrix[i, j] = value;
                adjacencyMatrix[j, i] = value;
            }
        }

        return adjacencyMatrix;
    }
}