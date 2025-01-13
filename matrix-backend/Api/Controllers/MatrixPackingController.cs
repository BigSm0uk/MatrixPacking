using Infrastructure;
using MatrixPacking.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace MatrixPacking.Controllers;

[ApiController]
[Route("matrix-api/[controller]/[action]")]
public class MatrixPackingController(ILogger<MatrixPackingController> logger, MatrixPackingService matrixPackingService)
    : ControllerBase
{
    [HttpPost]
    public IActionResult CreatePackingSession(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Файл не предоставлен или пуст.");
        }

        file.CheckExcelExtension();
        try
        {
            using var stream = file.OpenReadStream();
            return Ok(new { sessionId = matrixPackingService.ReadAndCalculate(stream).Value });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при обработке файла.");
            return StatusCode(500, "Произошла ошибка при обработке файла.");
        }
    }

    [HttpGet]
    public IActionResult GetResultMatrix(Guid id)
    {
        var result = matrixPackingService.GetPackedMatrixFromCache(id);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error); 
    }
    [HttpGet]
    public IActionResult GetResultMatrixFile(Guid id)
    {
        var result = matrixPackingService.GetResultMatrixFile(id);
        if (result.IsSuccess)
            return File(result.Value!,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Результаты.xlsx");
        return StatusCode(500, result.Error);
    }
}