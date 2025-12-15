using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using DistributedSolver.Core.Models;

namespace DistributedSolver.Client.Services;

/// <summary>
/// Обеспечивает регистрацию и периодический опрос вычислительных узлов.
/// </summary>
public class NodeHandshakeService
{
    private readonly HttpClient _httpClient;
    private readonly string _coordinatorUrl;

    public NodeHandshakeService(string coordinatorUrl = "http://localhost:5000")
    {
        _coordinatorUrl = coordinatorUrl;
        _httpClient = new HttpClient();
    }

    /// <summary>
    /// Регистрирует воркер на координаторе
    /// </summary>
    public async Task<bool> RegisterWorkerAsync(NodeInfo worker)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_coordinatorUrl}/api/Coordinator/register", worker);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Получает список зарегистрированных воркеров
    /// </summary>
    public async Task<List<NodeInfo>> GetWorkersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_coordinatorUrl}/api/Coordinator/workers");
            if (response.IsSuccessStatusCode)
            {
                var workers = await response.Content.ReadFromJsonAsync<List<NodeInfo>>();
                return workers ?? new List<NodeInfo>();
            }
        }
        catch
        {
            // Игнорируем ошибки
        }
        return new List<NodeInfo>();
    }
}

