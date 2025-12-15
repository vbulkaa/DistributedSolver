using System;
using DistributedSolver.Core.Models;

namespace DistributedSolver.Core.Services;

/// <summary>
/// Класс для решения СЛАУ методом Гаусса (линейный вариант)
/// </summary>
public class GaussianElimination
{
    /// <summary>
    /// Решает СЛАУ методом Гаусса
    /// </summary>
    /// <returns>Вектор решения и время выполнения в миллисекундах</returns>
    public static (double[] solution, long elapsedMs) Solve(LinearSystem system)
    {
        var startTime = DateTime.UtcNow;

        var matrix = system.CoefficientMatrix.Clone();
        var freeTerms = (double[])system.FreeTerms.Clone();
        int n = system.Size;

        // Прямой ход метода Гаусса
        for (int k = 0; k < n - 1; k++)
        {
            // Поиск главного элемента
            int maxRow = k;
            double maxVal = Math.Abs(matrix[k, k]);
            for (int i = k + 1; i < n; i++)
            {
                if (Math.Abs(matrix[i, k]) > maxVal)
                {
                    maxVal = Math.Abs(matrix[i, k]);
                    maxRow = i;
                }
            }

            // Перестановка строк
            if (maxRow != k)
            {
                SwapRows(matrix, freeTerms, k, maxRow);
            }

            // Проверка на вырожденность
            if (Math.Abs(matrix[k, k]) < 1e-10)
                throw new InvalidOperationException("Матрица вырождена или близка к вырожденной");

            // Исключение
            for (int i = k + 1; i < n; i++)
            {
                double factor = matrix[i, k] / matrix[k, k];
                for (int j = k; j < n; j++)
                {
                    matrix[i, j] -= factor * matrix[k, j];
                }
                freeTerms[i] -= factor * freeTerms[k];
            }
        }

        // Обратный ход
        var solution = new double[n];
        for (int i = n - 1; i >= 0; i--)
        {
            double sum = freeTerms[i];
            for (int j = i + 1; j < n; j++)
            {
                sum -= matrix[i, j] * solution[j];
            }
            if (Math.Abs(matrix[i, i]) < 1e-10)
                throw new InvalidOperationException("Матрица вырождена или близка к вырожденной");

            solution[i] = sum / matrix[i, i];
        }

        var elapsedMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return (solution, elapsedMs);
    }

    private static void SwapRows(Matrix matrix, double[] freeTerms, int row1, int row2)
    {
        int n = matrix.Columns;
        for (int j = 0; j < n; j++)
        {
            (matrix[row1, j], matrix[row2, j]) = (matrix[row2, j], matrix[row1, j]);
        }
        (freeTerms[row1], freeTerms[row2]) = (freeTerms[row2], freeTerms[row1]);
    }
}

