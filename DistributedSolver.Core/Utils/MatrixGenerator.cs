using System;
using DistributedSolver.Core.Models;

namespace DistributedSolver.Core.Utils;

/// <summary>
/// Утилита для генерации тестовых матриц
/// </summary>
public static class MatrixGenerator
{
    private static readonly Random _random = new Random();

    /// <summary>
    /// Генерирует хорошо обусловленную матрицу (диагонально доминирующая)
    /// </summary>
    public static Matrix GenerateWellConditionedMatrix(int size)
    {
        var matrix = new Matrix(size, size);

        for (int i = 0; i < size; i++)
        {
            double rowSum = 0;
            for (int j = 0; j < size; j++)
            {
                if (i != j)
                {
                    double value = _random.NextDouble() * 2 - 1; // [-1, 1]
                    matrix[i, j] = value;
                    rowSum += Math.Abs(value);
                }
            }
            // Делаем диагональный элемент доминирующим
            matrix[i, i] = rowSum + 1 + _random.NextDouble();
        }

        return matrix;
    }

    /// <summary>
    /// Генерирует случайную матрицу
    /// </summary>
    public static Matrix GenerateRandomMatrix(int size, double minValue = -10, double maxValue = 10)
    {
        var matrix = new Matrix(size, size);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                matrix[i, j] = _random.NextDouble() * (maxValue - minValue) + minValue;
            }
        }

        return matrix;
    }

    /// <summary>
    /// Генерирует случайный вектор свободных членов
    /// </summary>
    public static double[] GenerateRandomVector(int size, double minValue = -10, double maxValue = 10)
    {
        var vector = new double[size];
        for (int i = 0; i < size; i++)
        {
            vector[i] = _random.NextDouble() * (maxValue - minValue) + minValue;
        }
        return vector;
    }

    /// <summary>
    /// Генерирует систему с известным решением
    /// </summary>
    public static (Matrix matrix, double[] freeTerms, double[] solution) GenerateSystemWithSolution(int size)
    {
        var solution = GenerateRandomVector(size);
        var matrix = GenerateWellConditionedMatrix(size);
        var freeTerms = new double[size];

        for (int i = 0; i < size; i++)
        {
            double sum = 0;
            for (int j = 0; j < size; j++)
            {
                sum += matrix[i, j] * solution[j];
            }
            freeTerms[i] = sum;
        }

        return (matrix, freeTerms, solution);
    }
}

