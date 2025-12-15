using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Utils;
using Xunit;

namespace DistributedSolver.Tests.UnitTests;

public class MatrixTests
{
    [Fact]
    public void Matrix_Constructor_ShouldCreateMatrixWithCorrectSize()
    {
        var matrix = new Matrix(5, 10);
        matrix.Rows.Should().Be(5);
        matrix.Columns.Should().Be(10);
    }

    [Fact]
    public void Matrix_Indexer_ShouldGetAndSetValues()
    {
        var matrix = new Matrix(3, 3);
        matrix[1, 2] = 42.5;

        matrix[1, 2].Should().Be(42.5);
    }

    [Fact]
    public void Matrix_GetRow_ShouldReturnCorrectRow()
    {
        var matrix = new Matrix(3, 3);
        matrix[1, 0] = 1;
        matrix[1, 1] = 2;
        matrix[1, 2] = 3;

        matrix.GetRow(1).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Matrix_SetRow_ShouldSetCorrectRow()
    {
        var matrix = new Matrix(3, 3);
        var newRow = new double[] { 10, 20, 30 };

        matrix.SetRow(1, newRow);

        matrix[1, 0].Should().Be(10);
        matrix[1, 1].Should().Be(20);
        matrix[1, 2].Should().Be(30);
    }

    [Fact]
    public void Matrix_Clone_ShouldCreateIndependentCopy()
    {
        var original = new Matrix(3, 3);
        original[0, 0] = 100;

        var clone = original.Clone();
        clone[0, 0] = 200;

        original[0, 0].Should().Be(100);
        clone[0, 0].Should().Be(200);
    }

    [Fact]
    public void Matrix_GetSubMatrix_ShouldReturnCorrectSubMatrix()
    {
        var matrix = new Matrix(5, 3);
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                matrix[i, j] = i * 10 + j;
            }
        }

        var subMatrix = matrix.GetSubMatrix(1, 3);

        subMatrix.Rows.Should().Be(3);
        subMatrix.Columns.Should().Be(3);
        subMatrix[0, 0].Should().Be(10);
        subMatrix[2, 2].Should().Be(32);
    }
}


