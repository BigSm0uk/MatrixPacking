using ClosedXML.Extensions;
using Infrastructure;
using MatrixPacking.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MatrixPacking.Controllers;

[ApiController]
[Route("[controller]/[action]")]
public class MatrixPackingController(ILogger<MatrixPackingController> logger, MatrixPackingService matrixPackingService)
    : ControllerBase
{
    [HttpPost]
    public IActionResult Packing(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не предоставлен или пуст.");
        }

        file.CheckExcelExtension();
        try
        {
            using var stream = file.OpenReadStream();
            return File(matrixPackingService.ReadAndCalculate(stream),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "result.xlsx");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке файла.");
            return StatusCode(500, "Произошла ошибка при обработке файла.");
        }
    }
}