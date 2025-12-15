using Microsoft.AspNetCore.Mvc;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace DistributedSolver.Worker.Controllers;

/// <summary>
/// Контроллер для выполнения вычислений на воркере
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ComputationController : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Решение СЛАУ линейным (однопоточным) методом Гаусса
    /// </summary>
    [HttpPost("solve-linear")]
    public IActionResult SolveLinear([FromBody] SolveRequest request)
    {
        try
        {
            if (request.Matrix == null || request.Matrix.Length == 0)
                return BadRequest(new { error = "Матрица не может быть пустой" });
            if (request.FreeTerms == null || request.FreeTerms.Length == 0)
                return BadRequest(new { error = "Столбец свободных членов не может быть пустым" });
            if (request.Matrix.Length != request.FreeTerms.Length)
                return BadRequest(new { error = "Число строк матрицы должно совпадать с числом свободных членов" });

            var firstRow = request.Matrix[0];
            if (firstRow == null || firstRow.Length == 0)
                return BadRequest(new { error = "Строки матрицы не могут быть пустыми" });

            if (request.Matrix.Any(row => row == null || row.Length != firstRow.Length))
                return BadRequest(new { error = "Все строки матрицы должны иметь одинаковую длину" });

            if (firstRow.Length != request.Matrix.Length)
                return BadRequest(new { error = "Матрица должна быть квадратной" });

            var system = new LinearSystem(
                Matrix.FromJaggedArray(request.Matrix),
                request.FreeTerms
            );

            // Используем однопоточный метод Гаусса
            var (solution, elapsedMs) = GaussianElimination.Solve(system);
            var error = system.CalculateError(solution);

            return Ok(new
            {
                success = true,
                solution = solution,
                elapsedMs = elapsedMs,
                error = error
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Решение СЛАУ распределённым (многопоточным) методом Гаусса
    /// </summary>
    [HttpPost("solve-distributed")]
    public IActionResult SolveDistributed([FromBody] SolveRequest request)
    {
        try
        {
            if (request.Matrix == null || request.Matrix.Length == 0)
                return BadRequest(new { error = "Матрица не может быть пустой" });
            if (request.FreeTerms == null || request.FreeTerms.Length == 0)
                return BadRequest(new { error = "Столбец свободных членов не может быть пустым" });
            if (request.Matrix.Length != request.FreeTerms.Length)
                return BadRequest(new { error = "Число строк матрицы должно совпадать с числом свободных членов" });

            var firstRow = request.Matrix[0];
            if (firstRow == null || firstRow.Length == 0)
                return BadRequest(new { error = "Строки матрицы не могут быть пустыми" });

            if (request.Matrix.Any(row => row == null || row.Length != firstRow.Length))
                return BadRequest(new { error = "Все строки матрицы должны иметь одинаковую длину" });

            if (firstRow.Length != request.Matrix.Length)
                return BadRequest(new { error = "Матрица должна быть квадратной" });

            var system = new LinearSystem(
                Matrix.FromJaggedArray(request.Matrix),
                request.FreeTerms
            );

            // Используем многопоточный метод Гаусса
            var solver = new ThreadedGaussianSolver(system);
            var (solution, elapsedMs) = solver.Solve();
            var error = system.CalculateError(solution);

            return Ok(new
            {
                success = true,
                solution = solution,
                elapsedMs = elapsedMs,
                error = error
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Проверка работоспособности воркера
    /// </summary>
    [HttpGet("health")]
    [HttpHead("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", type = "worker", timestamp = DateTime.UtcNow });
    }


    public class SolveRequest
    {
        public double[][] Matrix { get; set; } = Array.Empty<double[]>();
        public double[] FreeTerms { get; set; } = Array.Empty<double>();
    }
}

