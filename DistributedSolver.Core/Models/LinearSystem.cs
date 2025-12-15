using System;
using System.Linq;

namespace DistributedSolver.Core.Models;

/// <summary>
/// Класс для представления системы линейных алгебраических уравнений
/// </summary>
public class LinearSystem
{
    public Matrix CoefficientMatrix { get; }
    public double[] FreeTerms { get; }

    public int Size => CoefficientMatrix.Rows;

    public LinearSystem(Matrix coefficientMatrix, double[] freeTerms)
    {
        if (coefficientMatrix == null)
            throw new ArgumentNullException(nameof(coefficientMatrix));
        if (freeTerms == null)
            throw new ArgumentNullException(nameof(freeTerms));
        if (coefficientMatrix.Rows != freeTerms.Length)
            throw new ArgumentException("Количество строк матрицы должно совпадать с длиной вектора свободных членов");
        if (coefficientMatrix.Rows != coefficientMatrix.Columns)
            throw new ArgumentException("Матрица коэффициентов должна быть квадратной");

        CoefficientMatrix = coefficientMatrix;
        FreeTerms = (double[])freeTerms.Clone();
    }

    /// <summary>
    /// Проверка корректности решения
    /// </summary>
    public double CalculateError(double[] solution)
    {
        if (solution == null || solution.Length != Size)
            throw new ArgumentException("Размер решения должен совпадать с размером системы");

        double maxError = 0;
        for (int i = 0; i < Size; i++)
        {
            double sum = 0;
            for (int j = 0; j < Size; j++)
            {
                sum += CoefficientMatrix[i, j] * solution[j];
            }
            double error = Math.Abs(sum - FreeTerms[i]);
            if (error > maxError)
                maxError = error;
        }
        return maxError;
    }
}

