using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using DistributedSolver.Client.ViewModels;

namespace DistributedSolver.Client
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации XAML: {ex.Message}\n\n{ex}", 
                    "Ошибка XAML", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
            
            try
            {
                _viewModel = new MainViewModel();
                DataContext = _viewModel;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка инициализации ViewModel: {ex.Message}\n\n{ex}", 
                    "Ошибка ViewModel", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }

        private void LoadMatrix_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.LoadMatrix(dialog.FileName);
                    MessageBox.Show("Матрица успешно загружена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки матрицы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadFreeTerms_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.LoadFreeTerms(dialog.FileName);
                    MessageBox.Show("Свободные члены успешно загружены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки свободных членов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadNodes_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.LoadNodes(dialog.FileName);
                    MessageBox.Show("Узлы успешно загружены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка загрузки узлов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void SolveDistributed_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SolveDistributedAsync();
        }

        private async void SolveLinear_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SolveLinearAsync();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.Cancel();
        }

        private void GenerateRandom_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.GenerateRandomSystem();
        }

        private void GenerateMatrix_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.GenerateMatrixWithDialog();
        }

        private async void LoadWorkers_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadWorkersFromCoordinatorAsync();
        }

        private async void CompareMethods_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.CompareMethodsAsync();
        }

        private async void SolveMultiple_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.SolveMultipleWorkersAsync();
        }

        private void SaveMatrix_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"matrix_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.SaveMatrix(dialog.FileName);
                    MessageBox.Show("Матрица успешно сохранена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения матрицы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveSolution_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"solution_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.SaveSolution(dialog.FileName);
                    MessageBox.Show("Решение успешно сохранено", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения решения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SaveComparison_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                FileName = $"comparison_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _viewModel.SaveComparison(dialog.FileName);
                    MessageBox.Show("Результаты сравнения успешно сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения сравнения: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}

