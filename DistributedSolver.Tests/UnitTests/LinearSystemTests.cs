using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Utils;
using Xunit;

namespace DistributedSolver.Tests.UnitTests;

public class LinearSystemTests
{
    [Fact]
    public void LinearSystem_Constructor_ShouldCreateValidSystem()
    {
        var matrix = MatrixGenerator.GenerateWellConditionedMatrix(5);
        var freeTerms = MatrixGenerator.GenerateRandomVector(5);

        var system = new LinearSystem(matrix, freeTerms);

        system.Size.Should().Be(5);
        system.CoefficientMatrix.Should().NotBeNull();
        system.FreeTerms.Should().NotBeNull();
    }

    [Fact]
    public void LinearSystem_Constructor_ShouldThrowOnMismatchedSizes()
    {
        var matrix = new Matrix(5, 5);
        var freeTerms = new double[3];

        Assert.Throws<ArgumentException>(() => new LinearSystem(matrix, freeTerms));
    }

    [Fact]
    public void LinearSystem_CalculateError_ShouldReturnZeroForExactSolution()
    {
        var (matrix, freeTerms, solution) = MatrixGenerator.GenerateSystemWithSolution(5);
        var system = new LinearSystem(matrix, freeTerms);

        var error = system.CalculateError(solution);

        error.Should().BeLessThan(1e-10);
    }

    [Fact]
    public void LinearSystem_CalculateError_ShouldReturnNonZeroForIncorrectSolution()
    {
        var matrix = MatrixGenerator.GenerateWellConditionedMatrix(5);
        var freeTerms = MatrixGenerator.GenerateRandomVector(5);
        var system = new LinearSystem(matrix, freeTerms);

        var incorrectSolution = new double[] { 1, 2, 3, 4, 5 };
        var error = system.CalculateError(incorrectSolution);

        error.Should().BeGreaterThan(0);
    }
}


