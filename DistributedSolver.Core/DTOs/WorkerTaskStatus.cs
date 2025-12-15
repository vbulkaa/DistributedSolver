namespace DistributedSolver.Core.DTOs;

/// <summary>
/// Статус задачи на воркере
/// </summary>
public class WorkerTaskStatus
{
    public int TaskId { get; set; }
    public string WorkerUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty; // "linear" или "distributed"
    public string Status { get; set; } = "Pending"; // Pending, Running, Completed, Failed
    public double Progress { get; set; } = 0; // 0-100
    public string? ErrorMessage { get; set; }
    public long? ElapsedMs { get; set; }
    public double[]? Solution { get; set; }
    public double? Error { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

