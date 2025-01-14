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

        var (nodes, bandWidth) = ParseNodesData(worksheet);
        var adjacencyMatrix = CreateAdjacencyMatrix(nodes);
        var (values, pointers) = PackMatrixScheme4(adjacencyMatrix, bandWidth);

        var id = Guid.NewGuid();

        var packedMatrixResult = new PackedMatrix
        {
            Id = id,
            Pointers = pointers,
            BandWidth = bandWidth,
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
            return Result<bool>.Failure("Индекс i или j вышел за границы матрицы");

        // Проверяем, находится ли элемент в пределах ширины ленты
        if (Math.Abs(row - col) >= matrix.BandWidth || row == col)
        {
            // Элемент за пределами ширины ленты, пересчитываем матрицу
            var adjacencyMatrix = UnPackMatrixScheme4(matrix.Values, matrix.Pointers, matrix.BandWidth);

            // Обновляем значение в распакованной матрице
            adjacencyMatrix[row, col] = newValue;
            adjacencyMatrix[col, row] = newValue; // Для симметричной матрицы

            var newBandWidth = CalculateBandwidth(adjacencyMatrix);

            // Перепаковываем матрицу с новой шириной ленты
            var newPackedMatrix = PackMatrixScheme4(adjacencyMatrix, newBandWidth);

            // Сохраняем новую матрицу в кэш
            var updatedMatrix = new PackedMatrix
            {
                Id = matrix.Id,
                Values = newPackedMatrix.Values,
                Pointers = newPackedMatrix.Pointers,
                TotalMatrixSize = matrix.TotalMatrixSize,
                BandWidth = newBandWidth
            };
            CreateOrUpdatePackedMatrixInCache(id, updatedMatrix);

            return Result<bool>.Success(true);
        }

        if (row > col)
        {
            var indexInValues = matrix.Pointers[row] - (row - col);
            matrix.Values[indexInValues] = newValue;
        }
        else
        {
            var indexInValues = matrix.Pointers[col] - (col - row);
            matrix.Values[indexInValues] = newValue;
        }

        // Обновляем кэш
        CreateOrUpdatePackedMatrixInCache(id, matrix);

        return Result<bool>.Success(true);
    }

    private static int CalculateBandwidth(double[,] adjacencyMatrix)
    {
        var n = adjacencyMatrix.GetLength(0); // Размер матрицы
        var maxBandwidth = 0;

        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (adjacencyMatrix[i, j] == 0) continue; // Ненулевой элемент
                var bandwidth = Math.Abs(i - j); // Смещение от диагонали
                if (bandwidth > maxBandwidth)
                {
                    maxBandwidth = bandwidth;
                }
            }
        }

        return maxBandwidth;
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
        var unPackedMatrix = UnPackMatrixScheme4(matrix!.Values, matrix.Pointers, matrix.BandWidth);

        AddPackingMatrixToExcel(workbook, matrix.Values, matrix.Pointers);
        AddMatrixToExcel(workbook, unPackedMatrix, matrix.BandWidth, "Распакованная матрица");

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


    private static void AddMatrixToExcel(XLWorkbook wb, double[,] matrix, int bandWidth,
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
                else if (Math.Abs(i - j) > bandWidth)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.DarkGreen;
                }

                // Установка стиля фона для всех ненулевых элементов
                if (matrix[i, j] != 0)
                {
                    cell.Style.Fill.BackgroundColor = XLColor.Red;
                }
            }
        }
    }


    private static void AddGraphToExcel(XLWorkbook workbook, IDictionary<string, List<string>> graph)
    {
        // Создаем новый лист в Excel
        var worksheet = workbook.AddWorksheet("Исходные данные");

        // Заполняем заголовки
        worksheet.Cell(1, 1).Value = "Узел";
        worksheet.Cell(1, 2).Value = "Связи"; // Мы будем объединять все связи в одну колонку

        // Находим максимальное количество соседей
        var maxNeighbors = graph.Values.Max(neighbors => neighbors.Count) + 1;
        worksheet.Range(1, 2, 1, maxNeighbors).Merge();
        worksheet.Range(1, 2, 1, maxNeighbors).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        worksheet.Range(1, 2, 1, maxNeighbors).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);
        worksheet.Cell(1, 1).Style.Border.SetOutsideBorder(XLBorderStyleValues.Thick);
        // Заполняем данные
        var row = 2; // Начинаем с 2-й строки
        foreach (var node in graph)
        {
            if (node.Value.Count == 0) continue;
            worksheet.Cell(row, 1).Value = node.Key; // Имя узла

            var col = 2;
            foreach (var value in node.Value)
            {
                worksheet.Cell(row, col++).Value = value;
            }

            row++;
        }
    }

    private static (OrderedDictionary<string, List<(string, double)>>, int) ParseNodesData(IXLWorksheet worksheet)
    {
        var result = new OrderedDictionary<string, List<(string, double)>>();
        var rows = worksheet.RowsUsed().Count();
        var maxBandWidth = 0;

        // Сопоставляем имена с индексами (ключи — строки/столбцы матрицы)
        for (var row = 1; row <= rows; row++)
        {
            var node = worksheet.Cell(row, 6).Value.ToString();
            if (string.IsNullOrWhiteSpace(node)) break;
            result[node] = [];
        }

        var keys = result.Keys
            .Select((node, index) => new { node, index })
            .ToDictionary(x => x.node, x => x.index);

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

            // Вычисляем ширину ленты (разница индексов)
            if (!keys.TryGetValue(from, out var fromIndex) || !keys.TryGetValue(to, out var toIndex)) continue;
            var bandWidth = Math.Abs(fromIndex - toIndex);
            maxBandWidth = Math.Max(maxBandWidth, bandWidth);
        }

        return (result, maxBandWidth);
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


    private static (double[] Values, int[] Pointers) PackMatrixScheme4(double[,] adjacencyMatrix, int bandWidth)
    {
        var size = adjacencyMatrix.GetLength(0); // Размер матрицы
        var values = new List<double>(); // Первый массив: элементы матрицы
        var pointers = new int[size]; // Второй массив: индексы диагональных элементов в Values

        for (var i = 0; i < size; i++)
        {
            // Ограничиваем диапазон столбцов шириной ленты
            var startColumn = Math.Max(0, i - bandWidth); // Начало - максимум между 0 и (i - ширина ленты)

            for (var j = startColumn; j <= i; j++)
            {
                values.Add(adjacencyMatrix[i, j]); // Добавляем элемент в Values
            }

            // Индекс диагонального элемента
            pointers[i] = values.Count - 1;
        }

        return (values.ToArray(), pointers);
    }

    private static double[,] UnPackMatrixScheme4(double[] values, int[] pointers, int bandWidth)
    {
        var size = pointers.Length; // Размер матрицы (по числу диагональных элементов)
        var adjacencyMatrix = new double[size, size]; // Инициализация пустой матрицы

        var valueIndex = 0; // Текущий индекс в массиве values

        for (var i = 0; i < size; i++)
        {
            // Определяем диапазон столбцов, которые входят в ширину ленты
            var startColumn = Math.Max(0, i - bandWidth); // Левый край ленты

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