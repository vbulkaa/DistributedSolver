using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Utils;

namespace DistributedSolver.Client.Windows
{
    public class MatrixTypeInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public MatrixGenerationType Type { get; set; }
    }

    public enum MatrixGenerationType
    {
        WellConditioned,      // Хорошо обусловленная (диагонально доминирующая)
        Random,               // Случайная
        WithKnownSolution    // С известным решением
    }

    public partial class MatrixGeneratorDialog : Window, INotifyPropertyChanged
    {
        private int _matrixSize = 100;
        private MatrixTypeInfo? _selectedMatrixType;
        private double _minValue = -10;
        private double _maxValue = 10;
        private bool _canGenerate = true;

        public MatrixGeneratorDialog()
        {
            InitializeComponent();
            DataContext = this;

            MatrixTypes = new List<MatrixTypeInfo>
            {
                new MatrixTypeInfo
                {
                    Name = "Хорошо обусловленная (рекомендуется)",
                    Description = "Диагонально доминирующая матрица. Гарантирует стабильность решения.",
                    Type = MatrixGenerationType.WellConditioned
                },
                new MatrixTypeInfo
                {
                    Name = "Случайная матрица",
                    Description = "Полностью случайная матрица с элементами в заданном диапазоне.",
                    Type = MatrixGenerationType.Random
                },
                new MatrixTypeInfo
                {
                    Name = "С известным решением",
                    Description = "Генерирует систему с заранее известным решением для проверки точности.",
                    Type = MatrixGenerationType.WithKnownSolution
                }
            };

            MatrixType = MatrixTypes[0];
        }

        public List<MatrixTypeInfo> MatrixTypes { get; }

        public MatrixTypeInfo? MatrixType
        {
            get => _selectedMatrixType;
            set
            {
                _selectedMatrixType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowRangeControls));
                OnPropertyChanged(nameof(MatrixTypeDescription));
            }
        }

        public int MatrixSize
        {
            get => _matrixSize;
            set
            {
                _matrixSize = value;
                OnPropertyChanged();
                ValidateInput();
            }
        }

        public double MinValue
        {
            get => _minValue;
            set
            {
                _minValue = value;
                OnPropertyChanged();
                ValidateInput();
            }
        }

        public double MaxValue
        {
            get => _maxValue;
            set
            {
                _maxValue = value;
                OnPropertyChanged();
                ValidateInput();
            }
        }

        public bool CanGenerate
        {
            get => _canGenerate;
            set
            {
                _canGenerate = value;
                OnPropertyChanged();
            }
        }

        public Visibility ShowRangeControls => 
            MatrixType?.Type == MatrixGenerationType.Random 
                ? Visibility.Visible 
                : Visibility.Collapsed;

        public string MatrixTypeDescription => MatrixType?.Description ?? "";

        public Matrix? GeneratedMatrix { get; private set; }
        public double[]? GeneratedFreeTerms { get; private set; }
        public double[]? KnownSolution { get; private set; }

        private void ValidateInput()
        {
            bool isValid = MatrixSize >= 1 && MatrixSize <= 50000;
            
            if (MatrixType?.Type == MatrixGenerationType.Random)
            {
                isValid = isValid && MinValue < MaxValue;
            }

            CanGenerate = isValid;
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MatrixType == null)
                {
                    MessageBox.Show("Выберите тип матрицы", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MatrixSize < 1 || MatrixSize > 50000)
                {
                    MessageBox.Show("Размер матрицы должен быть от 1 до 50000", "Ошибка", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                switch (MatrixType.Type)
                {
                    case MatrixGenerationType.WellConditioned:
                        {
                            var matrix = MatrixGenerator.GenerateWellConditionedMatrix(MatrixSize);
                            var solution = MatrixGenerator.GenerateRandomVector(MatrixSize);
                            var freeTerms = new double[MatrixSize];
                            
                            for (int i = 0; i < MatrixSize; i++)
                            {
                                double sum = 0;
                                for (int j = 0; j < MatrixSize; j++)
                                {
                                    sum += matrix[i, j] * solution[j];
                                }
                                freeTerms[i] = sum;
                            }
                            
                            GeneratedMatrix = matrix;
                            GeneratedFreeTerms = freeTerms;
                            KnownSolution = solution;
                            break;
                        }
                    
                    case MatrixGenerationType.Random:
                        {
                            if (MinValue >= MaxValue)
                            {
                                MessageBox.Show("Минимальное значение должно быть меньше максимального", 
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            
                            var matrix = MatrixGenerator.GenerateRandomMatrix(MatrixSize, MinValue, MaxValue);
                            var solution = MatrixGenerator.GenerateRandomVector(MatrixSize, MinValue, MaxValue);
                            var freeTerms = new double[MatrixSize];
                            
                            for (int i = 0; i < MatrixSize; i++)
                            {
                                double sum = 0;
                                for (int j = 0; j < MatrixSize; j++)
                                {
                                    sum += matrix[i, j] * solution[j];
                                }
                                freeTerms[i] = sum;
                            }
                            
                            GeneratedMatrix = matrix;
                            GeneratedFreeTerms = freeTerms;
                            KnownSolution = solution;
                            break;
                        }
                    
                    case MatrixGenerationType.WithKnownSolution:
                        {
                            var (matrix, freeTerms, solution) = 
                                MatrixGenerator.GenerateSystemWithSolution(MatrixSize);
                            
                            GeneratedMatrix = matrix;
                            GeneratedFreeTerms = freeTerms;
                            KnownSolution = solution;
                            break;
                        }
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации матрицы: {ex.Message}", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

