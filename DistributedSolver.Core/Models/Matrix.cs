using System;
using System.Collections.Generic;
using System.Linq;

namespace DistributedSolver.Core.Models;

/// <summary>
/// Класс для представления матрицы
/// </summary>
public class Matrix
{
    private readonly double[,] _data;
    public int Rows { get; }
    public int Columns { get; }

    public Matrix(int rows, int columns)
    {
        if (rows <= 0 || columns <= 0)
            throw new ArgumentException("Размеры матрицы должны быть положительными");

        Rows = rows;
        Columns = columns;
        _data = new double[rows, columns];
    }

    public Matrix(double[,] data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        Rows = data.GetLength(0);
        Columns = data.GetLength(1);
        _data = (double[,])data.Clone();
    }

    public double this[int row, int col]
    {
        get => _data[row, col];
        set => _data[row, col] = value;
    }

    /// <summary>
    /// Получить строку матрицы
    /// </summary>
    public double[] GetRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= Rows)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));

        var row = new double[Columns];
        for (int j = 0; j < Columns; j++)
        {
            row[j] = _data[rowIndex, j];
        }
        return row;
    }

    /// <summary>
    /// Установить строку матрицы
    /// </summary>
    public void SetRow(int rowIndex, double[] row)
    {
        if (rowIndex < 0 || rowIndex >= Rows)
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        if (row == null || row.Length != Columns)
            throw new ArgumentException("Длина строки должна совпадать с количеством столбцов");

        for (int j = 0; j < Columns; j++)
        {
            _data[rowIndex, j] = row[j];
        }
    }

    /// <summary>
    /// Получить подматрицу (для распределённых вычислений)
    /// </summary>
    public Matrix GetSubMatrix(int startRow, int endRow)
    {
        if (startRow < 0 || endRow >= Rows || startRow > endRow)
            throw new ArgumentException("Некорректные границы подматрицы");

        int subRows = endRow - startRow + 1;
        var subMatrix = new Matrix(subRows, Columns);

        for (int i = 0; i < subRows; i++)
        {
            for (int j = 0; j < Columns; j++)
            {
                subMatrix[i, j] = _data[startRow + i, j];
            }
        }

        return subMatrix;
    }

    /// <summary>
    /// Создать копию матрицы
    /// </summary>
    public Matrix Clone()
    {
        return new Matrix(_data);
    }

    /// <summary>
    /// Преобразовать в массив массивов
    /// </summary>
    public double[][] ToJaggedArray()
    {
        var result = new double[Rows][];
        for (int i = 0; i < Rows; i++)
        {
            result[i] = GetRow(i);
        }
        return result;
    }

    /// <summary>
    /// Создать из массива массивов
    /// </summary>
    public static Matrix FromJaggedArray(double[][] data)
    {
        if (data == null || data.Length == 0)
            throw new ArgumentException("Данные не могут быть пустыми");

        int rows = data.Length;
        int cols = data[0].Length;

        var matrix = new Matrix(rows, cols);
        for (int i = 0; i < rows; i++)
        {
            if (data[i].Length != cols)
                throw new ArgumentException("Все строки должны иметь одинаковую длину");
            matrix.SetRow(i, data[i]);
        }

        return matrix;
    }
}

