using System.Diagnostics;
using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using DistributedSolver.Core.Utils;
using Xunit;
using Xunit.Abstractions;

namespace DistributedSolver.Tests.LoadTests;

public class PerformanceTests
{
    [Theory]
    [InlineData(100)]
    [InlineData(500)]
    [InlineData(1000)]
    public void GaussianElimination_ShouldSolveWithinReasonableTime(int size)
    {
        var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        var stopwatch = Stopwatch.StartNew();
        var (solution, elapsedMs) = GaussianElimination.Solve(system);
        stopwatch.Stop();

        solution.Should().NotBeNull();
        solution.Length.Should().Be(size);
        system.CalculateError(solution).Should().BeLessThan(1e-8);

        if (size >= 1000)
        {
            elapsedMs.Should().BeLessThan(60000);
        }
    }

    [Fact]
    public void GaussianElimination_ShouldHandleLargeButFeasibleSize()
    {
        int size = 2000;
        var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        var (solution, elapsedMs) = GaussianElimination.Solve(system);

        solution.Should().NotBeNull();
        solution.Length.Should().Be(size);
        system.CalculateError(solution).Should().BeLessThan(1e-6);
        output.WriteLine($"Solved {size}x{size} system in {elapsedMs} ms");
    }

    private readonly ITestOutputHelper output;

    public PerformanceTests(ITestOutputHelper outputHelper)
    {
        output = outputHelper;
    }
}


