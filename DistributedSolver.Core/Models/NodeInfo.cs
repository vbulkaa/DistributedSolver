namespace DistributedSolver.Core.Models;

/// <summary>
/// Информация о счётном узле
/// </summary>
public class NodeInfo
{
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public string FullUrl => $"http://{Address}:{Port}";
    public bool IsActive { get; set; } = true;

    public NodeInfo()
    {
    }

    public NodeInfo(string address, int port)
    {
        Address = address;
        Port = port;
    }

    public override string ToString()
    {
        return $"{Address}:{Port}";
    }
}

