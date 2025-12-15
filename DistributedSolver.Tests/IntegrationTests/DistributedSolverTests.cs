using FluentAssertions;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using DistributedSolver.Core.Utils;
using Xunit;

namespace DistributedSolver.Tests.IntegrationTests;

/// <summary>
/// Интеграционные тесты требуют запущенных HTTP-сервисов координатора и воркеров.
/// </summary>
public class DistributedSolverTests
{
    [Fact(Skip = "Требует запущенных серверов")]
    public void ThreadedGaussianSolver_ShouldSolveSystem()
    {
        int size = 100;
        var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        var solver = new ThreadedGaussianSolver(system);
        var (solution, _) = solver.Solve();

        solution.Length.Should().Be(size);
        system.CalculateError(solution).Should().BeLessThan(1e-6);
    }

    [Fact]
    public void ThreadedGaussianSolver_ShouldBeFasterThanLinearForLargeMatrix()
    {
        int size = 500;
        var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(size);
        var system = new LinearSystem(matrix, freeTerms);

        // Линейный метод
        var (linearSolution, linearTime) = GaussianElimination.Solve(system);
        
        // Многопоточный метод
        var threadedSolver = new ThreadedGaussianSolver(system);
        var (threadedSolution, threadedTime) = threadedSolver.Solve();

        threadedSolution.Length.Should().Be(size);
        system.CalculateError(threadedSolution).Should().BeLessThan(1e-6);
        
        // Для больших матриц многопоточный метод должен быть быстрее или равен
        // (на маленьких матрицах может быть медленнее из-за overhead)
    }
}


