using Microsoft.AspNetCore.Mvc;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.DTOs;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;

namespace DistributedSolver.Coordinator.Controllers;

/// <summary>
/// Контроллер для координации распределённых вычислений
/// Сервер только координирует, вычисления выполняются на воркерах
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CoordinatorController : ControllerBase
{
    private readonly ILogger<CoordinatorController> _logger;
    private static readonly Dictionary<string, List<NodeInfo>> _workerRegistrations = new();
    private static readonly Dictionary<int, WorkerTaskStatus> _activeTasks = new();
    private static int _nextTaskId = 1;
    private static readonly object _lockObject = new();
    private const int MaxUnknowns = 50000;

    public CoordinatorController(ILogger<CoordinatorController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Регистрация воркера
    /// </summary>
    [HttpPost("register")]
    public IActionResult RegisterWorker([FromBody] NodeInfo workerInfo)
    {
        lock (_lockObject)
        {
            var key = "default";
            if (!_workerRegistrations.ContainsKey(key))
            {
                _workerRegistrations[key] = new List<NodeInfo>();
            }

            if (!_workerRegistrations[key].Any(w => w.Address == workerInfo.Address && w.Port == workerInfo.Port))
            {
                _workerRegistrations[key].Add(workerInfo);
                _logger.LogInformation($"Воркер зарегистрирован: {workerInfo}");
            }
        }

        return Ok(new { success = true, message = "Воркер зарегистрирован" });
    }

    /// <summary>
    /// Получение списка воркеров
    /// </summary>
    [HttpGet("workers")]
    public IActionResult GetWorkers()
    {
        lock (_lockObject)
        {
            var key = "default";
            var workers = _workerRegistrations.ContainsKey(key) 
                ? _workerRegistrations[key] 
                : new List<NodeInfo>();
            return Ok(workers);
        }
    }

    /// <summary>
    /// Решение СЛАУ на одном воркере (для обратной совместимости)
    /// </summary>
    [HttpPost("solve")]
    public IActionResult SolveSystem([FromBody] SolveRequest request, [FromQuery] string? method = "distributed")
    {
        try
        {
            ValidateRequest(request);

            NodeInfo? selectedWorker;
            string normalizedMethod;
            int taskId;

            lock (_lockObject)
            {
                var key = "default";
                if (!_workerRegistrations.TryGetValue(key, out var pool) || pool.Count == 0)
                {
                    return BadRequest(new { error = "Нет зарегистрированных воркеров" });
                }

                normalizedMethod = string.Equals(method, "linear", StringComparison.OrdinalIgnoreCase)
                    ? "linear"
                    : "distributed";

                var busyWorkers = _activeTasks.Values
                    .Where(t => t.Status is "Pending" or "Running")
                    .Select(t => t.WorkerUrl)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                selectedWorker = pool
                    .FirstOrDefault(w => !busyWorkers.Contains(w.FullUrl));

                if (selectedWorker == null)
                {
                    return Conflict(new { error = "Все воркеры заняты, попробуйте позже" });
                }

                taskId = _nextTaskId++;
                _activeTasks[taskId] = new WorkerTaskStatus
                {
                    TaskId = taskId,
                    WorkerUrl = selectedWorker!.FullUrl,
                    Method = normalizedMethod,
                    Status = "Pending",
                    Progress = 0,
                    StartTime = DateTime.UtcNow
                };
            }

            _logger.LogInformation($"Задача {taskId} отправлена на воркер {selectedWorker!.FullUrl} (метод: {normalizedMethod})");
            _ = SolveOnWorkerAsync(taskId, selectedWorker, normalizedMethod, request.Matrix, request.FreeTerms);

            return Ok(new
            {
                taskId,
                worker = selectedWorker!.FullUrl,
                message = "Задача отправлена на воркер"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при постановке задачи");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Отправка задач на несколько воркеров с разными методами
    /// </summary>
    [HttpPost("solve-multiple")]
    public async Task<IActionResult> SolveMultiple([FromBody] MultipleSolveRequest request)
    {
        try
        {
            List<NodeInfo> workers;
            lock (_lockObject)
            {
                var key = "default";
                workers = _workerRegistrations.ContainsKey(key) 
                    ? _workerRegistrations[key].ToList() 
                    : new List<NodeInfo>();
            }

            if (workers.Count == 0)
            {
                return BadRequest(new { error = "Нет зарегистрированных воркеров" });
            }

            // Валидация запроса
            if (request.Matrix == null || request.Matrix.Length == 0)
                return BadRequest(new { error = "Матрица не может быть пустой" });
            if (request.FreeTerms == null || request.FreeTerms.Length == 0)
                return BadRequest(new { error = "Столбец свободных членов не может быть пустым" });
            if (request.Matrix.Length != request.FreeTerms.Length)
                return BadRequest(new { error = "Число строк матрицы должно совпадать с числом свободных членов" });

            var firstRow = request.Matrix[0];
            if (firstRow == null || firstRow.Length == 0)
                return BadRequest(new { error = "Строки матрицы не могут быть пустыми" });

            if (request.Matrix.Length > MaxUnknowns || firstRow.Length > MaxUnknowns)
                return BadRequest(new { error = $"Размер системы превышает ограничение {MaxUnknowns} неизвестных" });

            if (request.Matrix.Any(row => row == null || row.Length != firstRow.Length))
                return BadRequest(new { error = "Все строки матрицы должны иметь одинаковую длину" });

            if (firstRow.Length != request.Matrix.Length)
                return BadRequest(new { error = "Матрица должна быть квадратной" });

            if (request.WorkerAssignments == null || request.WorkerAssignments.Count == 0)
                return BadRequest(new { error = "Не указаны назначения воркеров" });

            // Создаём задачи для каждого воркера
            var taskIds = new List<int>();
            var tasks = new List<Task>();

            lock (_lockObject)
            {
                foreach (var workerAssignment in request.WorkerAssignments)
                {
                    var worker = workers.FirstOrDefault(w => w.FullUrl == workerAssignment.WorkerUrl);
                    if (worker == null)
                    {
                        _logger.LogWarning($"Воркер {workerAssignment.WorkerUrl} не найден");
                        continue;
                    }

                    int taskId = _nextTaskId++;
                    taskIds.Add(taskId);

                    var taskStatus = new WorkerTaskStatus
                    {
                        TaskId = taskId,
                        WorkerUrl = worker.FullUrl,
                        Method = workerAssignment.Method,
                        Status = "Pending",
                        Progress = 0,
                        StartTime = DateTime.UtcNow
                    };

                    _activeTasks[taskId] = taskStatus;

                    // Запускаем задачу асинхронно
                    tasks.Add(SolveOnWorkerAsync(taskId, worker, workerAssignment.Method, request.Matrix, request.FreeTerms));
                }
            }

            // Ждём запуска всех задач
            await Task.WhenAll(tasks);

            return Ok(new { taskIds, message = $"Запущено {taskIds.Count} задач" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при запуске множественных задач");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Получение статуса задач
    /// </summary>
    [HttpGet("tasks/status")]
    public IActionResult GetTasksStatus([FromQuery] int[]? taskIds = null)
    {
        lock (_lockObject)
        {
            if (taskIds == null || taskIds.Length == 0)
            {
                return Ok(_activeTasks.Values.ToList());
            }

            var statuses = taskIds
                .Where(id => _activeTasks.ContainsKey(id))
                .Select(id => _activeTasks[id])
                .ToList();

            return Ok(statuses);
        }
    }

    /// <summary>
    /// Очистка завершённых задач
    /// </summary>
    [HttpPost("tasks/cleanup")]
    public IActionResult CleanupTasks([FromQuery] int? olderThanMinutes = 60)
    {
        lock (_lockObject)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-(olderThanMinutes ?? 60));
            var toRemove = _activeTasks
                .Where(kvp => kvp.Value.Status == "Completed" || kvp.Value.Status == "Failed")
                .Where(kvp => kvp.Value.EndTime.HasValue && kvp.Value.EndTime.Value < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in toRemove)
            {
                _activeTasks.Remove(id);
            }

            return Ok(new { removed = toRemove.Count });
        }
    }

    /// <summary>
    /// Получение полного результата по задаче (включая вектор решения)
    /// </summary>
    [HttpGet("tasks/{taskId}/result")]
    public IActionResult GetTaskResult(int taskId)
    {
        lock (_lockObject)
        {
            if (!_activeTasks.TryGetValue(taskId, out var task))
            {
                return NotFound(new { error = $"Задача {taskId} не найдена" });
            }

            if (task.Status != "Completed")
            {
                return BadRequest(new { error = $"Задача {taskId} ещё не завершена", status = task.Status });
            }

            return Ok(task);
        }
    }

    private async Task SolveOnWorkerAsync(int taskId, NodeInfo worker, string method, double[][] matrix, double[] freeTerms)
    {
        WorkerTaskStatus? taskStatus;
        lock (_lockObject)
        {
            if (!_activeTasks.TryGetValue(taskId, out taskStatus))
                return;
            taskStatus.Status = "Running";
            taskStatus.Progress = 10;
        }

        try
        {
            string endpoint = method == "linear" 
                ? "/api/Computation/solve-linear" 
                : "/api/Computation/solve-distributed";

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(30)
            };

            var workerRequest = new WorkerSolveRequest
            {
                Matrix = matrix,
                FreeTerms = freeTerms
            };

            lock (_lockObject)
            {
                if (taskStatus != null)
                    taskStatus.Progress = 30;
            }

            var response = await httpClient.PostAsJsonAsync(
                $"{worker.FullUrl}{endpoint}",
                workerRequest);

            lock (_lockObject)
            {
                if (taskStatus != null)
                    taskStatus.Progress = 70;
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                lock (_lockObject)
                {
                    if (taskStatus != null)
                    {
                        taskStatus.Status = "Failed";
                        taskStatus.ErrorMessage = $"HTTP {response.StatusCode}: {errorContent}";
                        taskStatus.EndTime = DateTime.UtcNow;
                        taskStatus.Progress = 100;
                    }
                }
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<SolveResponse>();

            lock (_lockObject)
            {
                if (taskStatus != null)
                {
                    if (result?.Success == true)
                    {
                        taskStatus.Status = "Completed";
                        taskStatus.Solution = result.Solution;
                        taskStatus.ElapsedMs = result.ElapsedMs;
                        taskStatus.Error = result.Error;
                        taskStatus.Progress = 100;
                    }
                    else
                    {
                        taskStatus.Status = "Failed";
                        taskStatus.ErrorMessage = "Не удалось получить решение";
                        taskStatus.Progress = 100;
                    }
                    taskStatus.EndTime = DateTime.UtcNow;
                }
            }
        }
        catch (Exception ex)
        {
            lock (_lockObject)
            {
                if (taskStatus != null)
                {
                    taskStatus.Status = "Failed";
                    taskStatus.ErrorMessage = ex.Message;
                    taskStatus.EndTime = DateTime.UtcNow;
                    taskStatus.Progress = 100;
                }
            }
            _logger.LogError(ex, $"Ошибка при выполнении задачи {taskId} на воркере {worker}");
        }
    }

    private void ValidateRequest(SolveRequest request)
    {
        if (request.Matrix == null || request.Matrix.Length == 0)
            throw new ArgumentException("Матрица не может быть пустой");
        if (request.FreeTerms == null || request.FreeTerms.Length == 0)
            throw new ArgumentException("Столбец свободных членов не может быть пустым");
        if (request.Matrix.Length != request.FreeTerms.Length)
            throw new ArgumentException("Число строк матрицы должно совпадать с числом свободных членов");

        var firstRow = request.Matrix[0];
        if (firstRow == null || firstRow.Length == 0)
            throw new ArgumentException("Строки матрицы не могут быть пустыми");

        if (request.Matrix.Length > MaxUnknowns || firstRow.Length > MaxUnknowns)
            throw new ArgumentException($"Размер системы превышает ограничение {MaxUnknowns} неизвестных");

        if (request.Matrix.Any(row => row == null || row.Length != firstRow.Length))
            throw new ArgumentException("Все строки матрицы должны иметь одинаковую длину");

        if (firstRow.Length != request.Matrix.Length)
            throw new ArgumentException("Матрица должна быть квадратной");
    }

    private class SolveResponse
    {
        public bool Success { get; set; }
        public double[]? Solution { get; set; }
        public long ElapsedMs { get; set; }
        public double Error { get; set; }
    }

    /// <summary>
    /// Проверка работоспособности координатора
    /// </summary>
    [HttpGet("health")]
    [HttpHead("health")]
    public IActionResult Health()
    {
        lock (_lockObject)
        {
            var key = "default";
            var workerCount = _workerRegistrations.ContainsKey(key) 
                ? _workerRegistrations[key].Count 
                : 0;
            return Ok(new 
            { 
                status = "healthy", 
                type = "coordinator",
                workerCount = workerCount,
                timestamp = DateTime.UtcNow 
            });
        }
    }

    public class SolveRequest
    {
        public double[][] Matrix { get; set; } = Array.Empty<double[]>();
        public double[] FreeTerms { get; set; } = Array.Empty<double>();
    }

    public class MultipleSolveRequest : SolveRequest
    {
        public List<WorkerAssignment> WorkerAssignments { get; set; } = new();
    }

    public class WorkerAssignment
    {
        public string WorkerUrl { get; set; } = string.Empty;
        public string Method { get; set; } = "linear"; // "linear" или "distributed"
    }

    public class WorkerSolveRequest
    {
        public double[][] Matrix { get; set; } = Array.Empty<double[]>();
        public double[] FreeTerms { get; set; } = Array.Empty<double>();
    }
}

