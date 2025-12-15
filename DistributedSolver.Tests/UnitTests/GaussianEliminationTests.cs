using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using DistributedSolver.Core.Utils;
using Xunit;

namespace DistributedSolver.Tests.UnitTests;

public class GaussianEliminationTests
{
    [Fact]
    public void GaussianElimination_Solve_ShouldSolveSmallSystem()
    {
        var matrix = new Matrix(3, 3);
        matrix.SetRow(0, new double[] { 2, 1, -1 });
        matrix.SetRow(1, new double[] { -3, -1, 2 });
        matrix.SetRow(2, new double[] { -2, 1, 2 });

        var freeTerms = new double[] { 8, -11, -3 };
        var system = new LinearSystem(matrix, freeTerms);

        var (solution, _) = GaussianElimination.Solve(system);

        solution.Should().NotBeNull();
        solution.Length.Should().Be(3);
        system.CalculateError(solution).Should().BeLessThan(1e-10);
    }

    [Fact]
    public void GaussianElimination_Solve_ShouldSolveSystemWithKnownSolution()
    {
        int size = 10;
        var (matrix, freeTerms, expectedSolution) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        var (solution, _) = GaussianElimination.Solve(system);

        for (int i = 0; i < size; i++)
        {
            solution[i].Should().BeApproximately(expectedSolution[i], 1e-6);
        }
        system.CalculateError(solution).Should().BeLessThan(1e-10);
    }

    [Fact]
    public void GaussianElimination_Solve_ShouldHandleLargeSystem()
    {
        int size = 100;
        var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        var (solution, elapsedMs) = GaussianElimination.Solve(system);

        solution.Length.Should().Be(size);
        elapsedMs.Should().BeGreaterThan(0);
        system.CalculateError(solution).Should().BeLessThan(1e-8);
    }

    [Fact]
    public void GaussianElimination_Solve_ShouldThrowOnSingularMatrix()
    {
        var matrix = new Matrix(3, 3);
        matrix.SetRow(0, new double[] { 1, 2, 3 });
        matrix.SetRow(1, new double[] { 1, 2, 3 });
        matrix.SetRow(2, new double[] { 4, 5, 6 });

        var freeTerms = new double[] { 1, 2, 3 };
        var system = new LinearSystem(matrix, freeTerms);

        Assert.Throws<InvalidOperationException>(() => GaussianElimination.Solve(system));
    }
}


