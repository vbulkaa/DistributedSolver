using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DistributedSolver.Core.Models;

namespace DistributedSolver.Core.Services;

/// <summary>
/// Класс для чтения файла с перечнем счётных узлов
/// </summary>
public class NodeFileReader
{
    /// <summary>
    /// Читает файл с узлами. Формат: каждая строка содержит адрес:порт
    /// </summary>
    public static List<NodeInfo> ReadNodesFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл не найден: {filePath}");

        var nodes = new List<NodeInfo>();
        var lines = File.ReadAllLines(filePath);

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            var parts = trimmedLine.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                throw new FormatException($"Некорректный формат строки: {trimmedLine}. Ожидается формат: адрес:порт");

            if (!int.TryParse(parts[1], out int port))
                throw new FormatException($"Некорректный порт в строке: {trimmedLine}");

            nodes.Add(new NodeInfo(parts[0].Trim(), port));
        }

        if (nodes.Count == 0)
            throw new InvalidOperationException("Файл не содержит валидных узлов");

        return nodes;
    }
}

