using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using DistributedSolver.Core.Models;

namespace DistributedSolver.Core.Services;

/// <summary>
/// Многопоточная реализация метода Гаусса для распределённых вычислений
/// Использует параллельную обработку строк для ускорения вычислений
/// </summary>
public class ThreadedGaussianSolver
{
    private readonly Matrix _matrix;
    private readonly double[] _freeTerms;
    private readonly int _threadCount;
    private readonly bool[] _processedRows;
    private readonly object _lockObject = new object();

    private record PivotInfo(int RowIndex, double Value);

    public ThreadedGaussianSolver(LinearSystem system, int threadCount = 0)
    {
        if (system == null)
            throw new ArgumentNullException(nameof(system));

        _matrix = system.CoefficientMatrix.Clone();
        _freeTerms = (double[])system.FreeTerms.Clone();
        _threadCount = threadCount > 0 ? threadCount : Environment.ProcessorCount;
        _processedRows = new bool[system.Size];
    }

    /// <summary>
    /// Решает СЛАУ многопоточным методом Гаусса
    /// </summary>
    /// <returns>Вектор решения и время выполнения в миллисекундах</returns>
    public (double[] solution, long elapsedMs) Solve()
    {
        var stopwatch = Stopwatch.StartNew();
        int size = _matrix.Rows;
        var pivotSequence = new List<(int column, int row, double value)>(size);

        // Прямой ход метода Гаусса с многопоточностью
        for (int k = 0; k < size - 1; k++)
        {
            // 1. Поиск главного элемента (параллельно)
            var pivot = FindPivotParallel(k);
            
            if (Math.Abs(pivot.Value) < 1e-10)
                throw new InvalidOperationException("Матрица вырождена или близка к вырожденной");

            // 2. Перестановка строк (если нужно)
            if (pivot.RowIndex != k)
            {
                SwapRows(k, pivot.RowIndex);
            }

            // 3. Применение итерации (параллельно)
            ApplyIterationParallel(k, pivot.Value);
            
            pivotSequence.Add((k, pivot.RowIndex, pivot.Value));
            _processedRows[k] = true;
        }

        // Обратный ход
        var solution = BackSubstitution(pivotSequence, size);

        stopwatch.Stop();
        return (solution, stopwatch.ElapsedMilliseconds);
    }

    /// <summary>
    /// Параллельный поиск главного элемента в столбце
    /// </summary>
    private PivotInfo FindPivotParallel(int column)
    {
        double maxAbs = 0;
        int maxRow = column;
        object lockObj = new object();

        // Параллельный поиск максимального элемента в столбце
        Parallel.For(column, _matrix.Rows, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, i =>
        {
            if (_processedRows[i])
                return;

            double value = _matrix[i, column];
            double absValue = Math.Abs(value);

            lock (lockObj)
            {
                if (absValue > maxAbs)
                {
                    maxAbs = absValue;
                    maxRow = i;
                }
            }
        });

        return new PivotInfo(maxRow, _matrix[maxRow, column]);
    }

    /// <summary>
    /// Параллельное применение итерации метода Гаусса
    /// </summary>
    private void ApplyIterationParallel(int pivotColumn, double pivotValue)
    {
        int size = _matrix.Rows;

        // Параллельная обработка строк ниже опорной
        Parallel.For(pivotColumn + 1, size, new ParallelOptions { MaxDegreeOfParallelism = _threadCount }, i =>
        {
            if (_processedRows[i])
                return;

            double factor = _matrix[i, pivotColumn] / pivotValue;
            
            // Обработка всей строки
            for (int j = pivotColumn; j < size; j++)
            {
                _matrix[i, j] -= factor * _matrix[pivotColumn, j];
            }
            _freeTerms[i] -= factor * _freeTerms[pivotColumn];
        });
    }

    /// <summary>
    /// Перестановка двух строк матрицы и свободных членов
    /// </summary>
    private void SwapRows(int row1, int row2)
    {
        int n = _matrix.Columns;
        
        // Перестановка элементов матрицы
        for (int j = 0; j < n; j++)
        {
            (_matrix[row1, j], _matrix[row2, j]) = (_matrix[row2, j], _matrix[row1, j]);
        }
        
        // Перестановка свободных членов
        (_freeTerms[row1], _freeTerms[row2]) = (_freeTerms[row2], _freeTerms[row1]);
    }

    /// <summary>
    /// Обратный ход метода Гаусса
    /// </summary>
    private double[] BackSubstitution(List<(int column, int row, double value)> pivotSequence, int totalSize)
    {
        var solution = new double[totalSize];

        // Обратный ход: решаем от последнего уравнения к первому
        for (int i = totalSize - 1; i >= 0; i--)
        {
            double sum = _freeTerms[i];
            for (int j = i + 1; j < totalSize; j++)
            {
                sum -= _matrix[i, j] * solution[j];
            }
            
            if (Math.Abs(_matrix[i, i]) < 1e-10)
                throw new InvalidOperationException("Матрица вырождена или близка к вырожденной");

            solution[i] = sum / _matrix[i, i];
        }

        return solution;
    }
}

