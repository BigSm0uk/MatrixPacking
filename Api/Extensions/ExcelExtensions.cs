namespace MatrixPacking.Extensions;

public static class ExcelExtensions
{
    public static void CheckExcelExtension(this IFormFile file)
    {
        var fileExtension = Path.GetExtension(file.FileName);
        if (fileExtension != ".xls" && fileExtension != ".xlsx")
        {
            throw new ArgumentException(
                "Неверный формат файла. Пожалуйста, загрузите файл с расширением .xls или .xlsx.");
        }
    }
}