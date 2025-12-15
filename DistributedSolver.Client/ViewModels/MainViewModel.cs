nText = "";
        private string _logText = "";
        private string _matrixPreview = "";
        private bool _isSolving;using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using DistributedSolver.Client.Services;
using DistributedSolver.Client.Windows;
using DistributedSolver.Core.Models;
using DistributedSolver.Core.Services;
using DistributedSolver.Core.Utils;
using Matrix = DistributedSolver.Core.Models.Matrix;

namespace DistributedSolver.Client.ViewModels
{
    // Модель для результата сравнения методов
    public class ComparisonResult : INotifyPropertyChanged
    {
        public string Method { get; set; } = string.Empty;
        public double ExecutionTimeMs { get; set; }
        public double ExecutionTimeSec => ExecutionTimeMs / 1000.0;
        public double Error { get; set; }
        public double Speedup { get; set; }
        public string Status { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;
        public virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Модель для статуса задачи на воркере
    public class WorkerTaskViewModel : INotifyPropertyChanged
    {
        private int _taskId;
        private string _workerUrl = string.Empty;
        private string _method = string.Empty;
        private string _status = "Pending";
        private double _progress = 0;
        private string? _errorMessage;
        private long? _elapsedMs;
        private double? _error;
        private DateTime _startTime;
        private DateTime? _endTime;
        private bool _resultRecorded;

        public int TaskId
        {
            get => _taskId;
            set { _taskId = value; OnPropertyChanged(); }
        }

        public string WorkerUrl
        {
            get => _workerUrl;
            set { _workerUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(WorkerName)); }
        }

        public string WorkerName => WorkerUrl.Replace("http://", "").Replace("https://", "");

        public string Method
        {
            get => _method;
            set { _method = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        public System.Windows.Media.Brush StatusColor
        {
            get
            {
                return Status switch
                {
                    "Completed" => System.Windows.Media.Brushes.Green,
                    "Failed" => System.Windows.Media.Brushes.Red,
                    "Running" => System.Windows.Media.Brushes.Blue,
                    _ => System.Windows.Media.Brushes.Gray
                };
            }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public string? ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public long? ElapsedMs
        {
            get => _elapsedMs;
            set { _elapsedMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(ElapsedTimeText)); }
        }

        public string ElapsedTimeText => ElapsedMs.HasValue 
            ? $"{ElapsedMs.Value:N2} мс ({ElapsedMs.Value / 1000.0:F3} сек)" 
            : "-";

        public double? Error
        {
            get => _error;
            set { _error = value; OnPropertyChanged(); OnPropertyChanged(nameof(ErrorText)); }
        }

        public string ErrorText => Error.HasValue ? $"{Error.Value:E6}" : "-";

        public DateTime StartTime
        {
            get => _startTime;
            set { _startTime = value; OnPropertyChanged(); }
        }

        public DateTime? EndTime
        {
            get => _endTime;
            set { _endTime = value; OnPropertyChanged(); }
        }

        public bool ResultRecorded
        {
            get => _resultRecorded;
            set { _resultRecorded = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // Модель для элемента решения
    public class SolutionItem : INotifyPropertyChanged
    {
        public int Index { get; set; }
        public string Variable => $"x[{Index}]";
        public double Value { get; set; }
        public string ValueNormal => Value.ToString("F6");

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            _registrationService = new NodeHandshakeService();
            ComparisonResults = new ObservableCollection<ComparisonResult>();
            SolutionItems = new ObservableCollection<SolutionItem>();
            WorkerTasks = new ObservableCollection<WorkerTaskViewModel>();
        }

        private const string CoordinatorBaseUrl = "http://localhost:5000";

        private Matrix? _matrix;
        private double[]? _freeTerms;
        private List<NodeInfo>? _nodes;
        private double[]? _solution;
        private readonly NodeHandshakeService _registrationService;
        private string _status = "Готов";
        private string _methodName = "-";
        private string _executionTime = "-";
        private string _error = "-";
        private string _errorText = "";
        private string _solutio
        private int _randomSize = 100;
        private CancellationTokenSource? _cancellationTokenSource;
        private double _progressValue;
        private string _progressText = "";
        private string _currentMethod = "";
        private string _statusBarText = "Готов к работе";
        private string _timestamp = "";
        private readonly HashSet<int> _trackedTaskIds = new();
        private Task? _taskMonitoringTask;
        private CancellationTokenSource? _taskMonitoringCts;
        private readonly object _taskTrackingLock = new();

        // Коллекции для отображения
        public ObservableCollection<ComparisonResult> ComparisonResults { get; }
        public ObservableCollection<SolutionItem> SolutionItems { get; }
        public ObservableCollection<WorkerTaskViewModel> WorkerTasks { get; }

        public string SystemSize => _matrix != null ? $"{_matrix.Rows}x{_matrix.Columns}" : "Не загружена";
        public int NodeCount => _nodes?.Count ?? 0;
        public bool CanSolve => _matrix != null && _freeTerms != null;
        
        public bool IsSolving
        {
            get => _isSolving;
            set
            {
                _isSolving = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                UpdateStatusBar();
            }
        }

        public Brush StatusColor => _status.Contains("Ошибка") ? Brushes.Red 
            : _status.Contains("Готов") ? Brushes.Green 
            : Brushes.Blue;

        public string MethodName
        {
            get => _methodName;
            set
            {
                _methodName = value;
                OnPropertyChanged();
            }
        }

        public string ExecutionTime
        {
            get => _executionTime;
            set
            {
                _executionTime = value;
                OnPropertyChanged();
            }
        }

        public string Error
        {
            get => _error;
            set
            {
                _error = value;
                OnPropertyChanged();
            }
        }

        public string ErrorText
        {
            get => _errorText;
            set
            {
                _errorText = value;
                OnPropertyChanged();
            }
        }

        public string SolutionText
        {
            get => _solutionText;
            set
            {
                _solutionText = value;
                OnPropertyChanged();
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged();
            }
        }

        public string MatrixPreview
        {
            get => _matrixPreview;
            set
            {
                _matrixPreview = value;
                OnPropertyChanged();
            }
        }

        public int RandomSize
        {
            get => _randomSize;
            set
            {
                _randomSize = value;
                OnPropertyChanged();
            }
        }

        public double ProgressValue
        {
            get => _progressValue;
            set
            {
                _progressValue = value;
                OnPropertyChanged();
            }
        }

        public string ProgressText
        {
            get => _progressText;
            set
            {
                _progressText = value;
                OnPropertyChanged();
            }
        }

        public string CurrentMethod
        {
            get => _currentMethod;
            set
            {
                _currentMethod = value;
                OnPropertyChanged();
            }
        }

        public System.Windows.Visibility ProgressVisibility => _isSolving 
            ? System.Windows.Visibility.Visible 
            : System.Windows.Visibility.Hidden;

        // Статистика сравнения
        public string BestMethod
        {
            get
            {
                if (ComparisonResults.Count == 0) return "-";
                var best = ComparisonResults.OrderBy(r => r.ExecutionTimeMs).FirstOrDefault();
                return best?.Method ?? "-";
            }
        }

        public double BestSpeedup
        {
            get
            {
                if (ComparisonResults.Count == 0) return 0;
                return ComparisonResults.Max(r => r.Speedup);
            }
        }

        public double AvgDistributedTime
        {
            get
            {
                var distributed = ComparisonResults.Where(r => r.Method.Contains("Распределённый"));
                return distributed.Any() ? distributed.Average(r => r.ExecutionTimeMs) : 0;
            }
        }

        public double AvgLinearTime
        {
            get
            {
                var linear = ComparisonResults.Where(r => r.Method.Contains("Линейный"));
                return linear.Any() ? linear.Average(r => r.ExecutionTimeMs) : 0;
            }
        }

        public int TotalComparisons => ComparisonResults.Count;

        public string StatusBarText
        {
            get => _statusBarText;
            set
            {
                _statusBarText = value;
                OnPropertyChanged();
            }
        }

        public string Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        public void LoadMatrix(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            int rows = lines.Length;
            if (rows == 0) throw new InvalidOperationException("Файл пуст");

            var firstLine = lines[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            int cols = firstLine.Length;

            _matrix = new Matrix(rows, cols);
            for (int i = 0; i < rows; i++)
            {
                var values = lines[i].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (values.Length != cols)
                    throw new InvalidOperationException($"Несоответствие количества элементов в строке {i + 1}");

                for (int j = 0; j < cols; j++)
                {
                    if (!double.TryParse(values[j], out double value))
                        throw new FormatException($"Некорректное значение в строке {i + 1}, столбце {j + 1}");
                    _matrix[i, j] = value;
                }
            }

            UpdateMatrixPreview();
            AddLog($"✓ Матрица загружена: {rows}x{cols}");
            OnPropertyChanged(nameof(SystemSize));
            OnPropertyChanged(nameof(CanSolve));
            ClearResults();
        }

        public void LoadFreeTerms(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            _freeTerms = lines.Select(line =>
            {
                if (!double.TryParse(line.Trim(), out double value))
                    throw new FormatException($"Некорректное значение: {line}");
                return value;
            }).ToArray();

            AddLog($"✓ Свободные члены загружены: {_freeTerms.Length} элементов");
            OnPropertyChanged(nameof(CanSolve));
        }

        public async void LoadNodes(string filePath)
        {
            _nodes = NodeFileReader.ReadNodesFromFile(filePath);
            AddLog($"✓ Узлы загружены: {_nodes.Count} узлов");
            
            int registered = 0;
            foreach (var node in _nodes)
            {
                if (await _registrationService.RegisterWorkerAsync(node))
                {
                    registered++;
                    AddLog($"  ✓ {node} зарегистрирован");
                }
                else
                {
                    AddLog($"  ✗ {node} не удалось зарегистрировать");
                }
            }
            
            AddLog($"Зарегистрировано воркеров: {registered}/{_nodes.Count}");
            OnPropertyChanged(nameof(NodeCount));
            OnPropertyChanged(nameof(CanSolve));
        }

        public async Task LoadWorkersFromCoordinatorAsync()
        {
            try
            {
                Status = "Получение списка воркеров...";
                ProgressText = "Подключение к координатору...";
                _nodes = await _registrationService.GetWorkersAsync();
                AddLog($"✓ Получено воркеров с координатора: {_nodes.Count}");
                foreach (var node in _nodes)
                {
                    AddLog($"  - {node}");
                }
                OnPropertyChanged(nameof(NodeCount));
                OnPropertyChanged(nameof(CanSolve));
                Status = "Готов";
                ProgressText = "";
            }
            catch (Exception ex)
            {
                Status = $"Ошибка получения воркеров: {ex.Message}";
                AddLog($"✗ Ошибка получения воркеров: {ex.Message}");
            }
        }

        public void GenerateRandomSystem()
        {
            try
            {
                if (_randomSize <= 0 || _randomSize > 50000)
                {
                    Status = "Ошибка: Размер должен быть от 1 до 50000";
                    return;
                }

                ProgressText = $"Генерация системы {_randomSize}x{_randomSize}...";
                var (matrix, freeTerms, _) = MatrixGenerator.GenerateSystemWithSolution(_randomSize);
                _matrix = matrix;
                _freeTerms = freeTerms;

                UpdateMatrixPreview();
                AddLog($"✓ Сгенерирована случайная система: {_randomSize}x{_randomSize}");
                OnPropertyChanged(nameof(SystemSize));
                OnPropertyChanged(nameof(CanSolve));
                Status = "Случайная система сгенерирована";
                ProgressText = "";
                ClearResults();
            }
            catch (Exception ex)
            {
                Status = $"Ошибка генерации: {ex.Message}";
                AddLog($"✗ Ошибка генерации: {ex.Message}");
            }
        }

        public void GenerateMatrixWithDialog()
        {
            try
            {
                var dialog = new MatrixGeneratorDialog
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true && dialog.GeneratedMatrix != null)
                {
                    ProgressText = $"Генерация матрицы {dialog.MatrixSize}x{dialog.MatrixSize}...";
                    
                    _matrix = dialog.GeneratedMatrix;
                    _freeTerms = dialog.GeneratedFreeTerms ?? Array.Empty<double>();

                    UpdateMatrixPreview();
                    
                    var matrixTypeName = dialog.MatrixType?.Name ?? "неизвестный тип";
                    AddLog($"✓ Сгенерирована матрица: {dialog.MatrixSize}x{dialog.MatrixSize} (тип: {matrixTypeName})");
                    
                    if (dialog.KnownSolution != null)
                    {
                        AddLog($"  Известное решение: x[0]={dialog.KnownSolution[0]:F6}, ..., x[{dialog.KnownSolution.Length-1}]={dialog.KnownSolution[dialog.KnownSolution.Length-1]:F6}");
                    }
                    
                    OnPropertyChanged(nameof(SystemSize));
                    OnPropertyChanged(nameof(CanSolve));
                    Status = "Матрица сгенерирована";
                    ProgressText = "";
                    ClearResults();
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка генерации: {ex.Message}";
                AddLog($"✗ Ошибка генерации: {ex.Message}");
            }
        }

        /// <summary>
        /// Отправка задачи на распределённый метод (не блокирует UI, можно отправлять несколько задач)
        /// </summary>
        public async Task SolveDistributedAsync()
        {
            await SubmitSolveTaskAsync("distributed", "Распределённый метод Гаусса");
        }

        /// <summary>
        /// Отправка задачи на линейный метод (не блокирует UI, можно отправлять несколько задач)
        /// </summary>
        public async Task SolveLinearAsync()
        {
            await SubmitSolveTaskAsync("linear", "Линейный метод Гаусса");
        }

        private async Task SubmitSolveTaskAsync(string method, string friendlyName)
        {
            if (_matrix == null || _freeTerms == null)
            {
                Status = "Ошибка: Не все данные загружены";
                return;
            }

            if (_nodes == null || _nodes.Count == 0)
            {
                await LoadWorkersFromCoordinatorAsync();
                if (_nodes == null || _nodes.Count == 0)
                {
                    Status = "Ошибка: Нет доступных воркеров";
                    return;
                }
            }

            try
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromMinutes(2);
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

                var request = new
                {
                    Matrix = _matrix.ToJaggedArray(),
                    FreeTerms = _freeTerms
                };

                var payload = HttpCompressionHelper.SerializeToCompressedJson(request);
                using var content = HttpCompressionHelper.CreateCompressedContent(payload);
                var response = await httpClient.PostAsync(
                    $"{CoordinatorBaseUrl}/api/Coordinator/solve?method={method}",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync();
                    if (response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        Status = "Нет свободных воркеров";
                        AddLog($"✗ Нет свободных воркеров для {friendlyName}: {error}");
                        return;
                    }

                    Status = $"Ошибка отправки: {response.StatusCode}";
                    AddLog($"✗ Ошибка отправки задачи ({friendlyName}): {error}");
                    return;
                }

                var result = await response.Content.ReadFromJsonAsync<StartSolveResponse>();
                if (result == null || result.TaskId <= 0)
                {
                    Status = "Ошибка: Не удалось получить идентификатор задачи";
                    AddLog($"✗ Сервер не вернул идентификатор для {friendlyName}");
                    return;
                }

                Status = $"Задача отправлена: {friendlyName}";
                var workerUrl = string.IsNullOrWhiteSpace(result.Worker) ? "-" : result.Worker;
                AddLog($"🚀 {friendlyName} отправлен на {workerUrl} (taskId={result.TaskId})");
                AddTrackedTask(result.TaskId, method, workerUrl);
            }
            catch (Exception ex)
            {
                Status = $"Ошибка отправки: {ex.Message}";
                AddLog($"✗ Ошибка при отправке задачи ({friendlyName}): {ex.Message}");
            }
        }

        /// <summary>
        /// Сравнение методов - отправляет обе задачи независимо
        /// </summary>
        public async Task CompareMethodsAsync()
        {
            if (_matrix == null || _freeTerms == null)
            {
                Status = "Ошибка: Не все данные загружены";
                return;
            }

            AddLog("═══════════════════════════════════════════════════════");
            AddLog("🔄 Начато сравнение методов");
            AddLog("═══════════════════════════════════════════════════════");

            // Отправляем оба метода одновременно
            AddLog("1️⃣ Отправка задачи (линейный метод)...");
            await SolveLinearAsync();
            
            AddLog("2️⃣ Отправка задачи (распределённый метод)...");
            await SolveDistributedAsync();

            AddLog("📊 Обе задачи отправлены. Результаты появятся по мере выполнения.");
            AddLog("═══════════════════════════════════════════════════════");
        }

        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
            _taskMonitoringCts?.Cancel();
            Status = "Отменено";
            IsSolving = false;
            ProgressText = "Отменено";
        }

        private void UpdateSolutionDisplay()
        {
            if (_solution == null) return;

            SolutionItems.Clear();
            for (int i = 0; i < _solution.Length; i++)
            {
                SolutionItems.Add(new SolutionItem
                {
                    Index = i,
                    Value = _solution[i]
                });
            }

            SolutionText = string.Join("\n", _solution.Select((x, i) => $"x[{i}] = {x:E10}"));
        }

        /// <summary>
        /// Отправка задач на несколько воркеров с разными методами
        /// </summary>
        public async Task SolveMultipleWorkersAsync()
        {
            if (_matrix == null || _freeTerms == null)
            {
                Status = "Ошибка: Не все данные загружены";
                return;
            }

            if (_nodes == null || _nodes.Count == 0)
            {
                await LoadWorkersFromCoordinatorAsync();
                if (_nodes == null || _nodes.Count == 0)
                {
                    Status = "Ошибка: Нет доступных воркеров";
                    return;
                }
            }

            IsSolving = true;
            Status = "Отправка задач на воркеры...";
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromMinutes(30);
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
                httpClient.DefaultRequestHeaders.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

                // Формируем задания для воркеров: чередуем linear и distributed
                var workerAssignmentsPayload = new List<object>();
                var workerAssignmentsMeta = new List<(string WorkerUrl, string Method)>();
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var method = (i % 2 == 0) ? "linear" : "distributed";
                    var workerUrl = _nodes[i].FullUrl;
                    workerAssignmentsMeta.Add((workerUrl, method));
                    workerAssignmentsPayload.Add(new
                    {
                        WorkerUrl = workerUrl,
                        Method = method
                    });
                }

                var request = new
                {
                    Matrix = _matrix.ToJaggedArray(),
                    FreeTerms = _freeTerms,
                    WorkerAssignments = workerAssignmentsPayload
                };

                var payload = HttpCompressionHelper.SerializeToCompressedJson(request);
                using var content = HttpCompressionHelper.CreateCompressedContent(payload);
                
                var response = await httpClient.PostAsync(
                    $"{CoordinatorBaseUrl}/api/Coordinator/solve-multiple", 
                    content, 
                    _cancellationTokenSource.Token);

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<MultipleSolveResponse>();

                if (result?.TaskIds != null && result.TaskIds.Count > 0)
                {
                    Status = $"Запущено {result.TaskIds.Count} задач на воркерах";
                    
                    for (int i = 0; i < result.TaskIds.Count; i++)
                    {
                        var meta = i < workerAssignmentsMeta.Count
                            ? workerAssignmentsMeta[i]
                            : (WorkerUrl: "-", Method: "linear");
                        AddTrackedTask(result.TaskIds[i], meta.Method, meta.WorkerUrl);
                        AddLog($"🚀 Задача {result.TaskIds[i]} ({meta.Method}) отправлена на {meta.WorkerUrl}");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Не удалось запустить задачи");
                }
            }
            catch (Exception ex)
            {
                Status = $"Ошибка: {ex.Message}";
                AddLog($"✗ Ошибка при отправке задач: {ex.Message}");
            }
        }

        private void AddTrackedTask(int taskId, string method, string? workerUrl)
        {
            if (taskId <= 0)
                return;

            var taskVm = WorkerTasks.FirstOrDefault(t => t.TaskId == taskId);
            if (taskVm == null)
            {
                taskVm = new WorkerTaskViewModel
                {
                    TaskId = taskId,
                    Method = method,
                    WorkerUrl = workerUrl ?? "-",
                    Status = "Pending",
                    StartTime = DateTime.UtcNow
                };
                WorkerTasks.Add(taskVm);
            }
            else
            {
                taskVm.Method = method;
                if (!string.IsNullOrWhiteSpace(workerUrl))
                {
                    taskVm.WorkerUrl = workerUrl;
                }
                taskVm.Status = "Pending";
                taskVm.ResultRecorded = false;
            }

            lock (_taskTrackingLock)
            {
                _trackedTaskIds.Add(taskId);
            }

            UpdateTaskProgressIndicator();

            IsSolving = true;
            EnsureTaskMonitoring();
        }

        private void EnsureTaskMonitoring()
        {
            if (_taskMonitoringTask != null && !_taskMonitoringTask.IsCompleted)
            {
                return;
            }

            _taskMonitoringCts?.Cancel();
            _taskMonitoringCts = new CancellationTokenSource();
            _taskMonitoringTask = Task.Run(() => MonitorTasksAsync(_taskMonitoringCts.Token));
        }

        private async Task MonitorTasksAsync(CancellationToken token)
        {
            try
            {
                var handler = new SocketsHttpHandler
                {
                    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                using var httpClient = new HttpClient(handler);
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                while (!token.IsCancellationRequested)
                {
                    List<int> taskIds;
                    lock (_taskTrackingLock)
                    {
                        taskIds = _trackedTaskIds.ToList();
                    }

                    if (taskIds.Count == 0)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            IsSolving = WorkerTasks.Any(t => t.Status == "Pending" || t.Status == "Running");
                        });

                        await Task.Delay(500, token);

                        lock (_taskTrackingLock)
                        {
                            if (_trackedTaskIds.Count == 0)
                            {
                                return;
                            }
                        }

                        continue;
                    }

                    try
                    {
                        var taskIdsParam = string.Join("&", taskIds.Select(id => $"taskIds={id}"));
                        var response = await httpClient.GetAsync(
                            $"{CoordinatorBaseUrl}/api/Coordinator/tasks/status?{taskIdsParam}",
                            token);

                        if (!response.IsSuccessStatusCode)
                        {
                            await Task.Delay(1000, token);
                            continue;
                        }

                        var statuses = await response.Content.ReadFromJsonAsync<List<WorkerTaskStatusDto>>(cancellationToken: token);
                        if (statuses == null)
                        {
                            await Task.Delay(500, token);
                            continue;
                        }

                        foreach (var status in statuses)
                        {
                            if (status.Status == "Completed" && (status.Solution == null || status.Solution.Length == 0))
                            {
                                var enriched = await FetchTaskResultAsync(httpClient, status.TaskId, token);
                                if (enriched != null)
                                {
                                    if (enriched.Solution != null && enriched.Solution.Length > 0)
                                    {
                                        status.Solution = enriched.Solution;
                                    }
                                    status.ElapsedMs ??= enriched.ElapsedMs;
                                    status.Error ??= enriched.Error;
                                }
                            }
                        }

                        var completedIds = new List<int>();

                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var status in statuses)
                            {
                                var taskVm = WorkerTasks.FirstOrDefault(t => t.TaskId == status.TaskId);
                                if (taskVm == null)
                                {
                                    taskVm = new WorkerTaskViewModel
                                    {
                                        TaskId = status.TaskId
                                    };
                                    WorkerTasks.Add(taskVm);
                                }

                                taskVm.WorkerUrl = status.WorkerUrl;
                                taskVm.Method = status.Method;
                                taskVm.Status = status.Status;
                                taskVm.Progress = status.Progress;
                                taskVm.ErrorMessage = status.ErrorMessage;
                                taskVm.ElapsedMs = status.ElapsedMs;
                                taskVm.Error = status.Error;
                                taskVm.StartTime = status.StartTime;
                                taskVm.EndTime = status.EndTime;

                                if ((status.Status == "Completed" || status.Status == "Failed") && !completedIds.Contains(status.TaskId))
                                {
                                    completedIds.Add(status.TaskId);
                                }

                                if (status.Status == "Completed" && !taskVm.ResultRecorded && status.ElapsedMs.HasValue)
                                {
                                    var methodName = status.Method == "linear"
                                        ? $"Линейный метод (воркер {taskVm.WorkerName})"
                                        : $"Распределённый метод (воркер {taskVm.WorkerName})";

                                    UpdateRuntimeStats(status, methodName);

                                    ComparisonResults.Add(new ComparisonResult
                                    {
                                        Method = methodName,
                                        ExecutionTimeMs = status.ElapsedMs.Value,
                                        Error = status.Error ?? 0,
                                        Status = "Успешно"
                                    });
                                    taskVm.ResultRecorded = true;
                                }
                                else if (status.Status == "Failed" && !taskVm.ResultRecorded)
                                {
                                    AddLog($"✗ Задача {status.TaskId} завершилась с ошибкой: {status.ErrorMessage}");
                                    taskVm.ResultRecorded = true;
                                }
                            }

                            UpdateComparisonStats();
                            UpdateAllSpeedups();
                            UpdateTaskProgressIndicator();

                            var anyActive = WorkerTasks.Any(t => t.Status == "Pending" || t.Status == "Running");
                            IsSolving = anyActive;
                            if (!anyActive)
                            {
                                Status = "Все задачи завершены";
                            }
                        });

                        if (completedIds.Count > 0)
                        {
                            lock (_taskTrackingLock)
                            {
                                foreach (var id in completedIds)
                                {
                                    _trackedTaskIds.Remove(id);
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            AddLog($"⚠ Ошибка при получении статуса задач: {ex.Message}");
                        });
                    }

                    await Task.Delay(500, token);
                }
            }
            finally
            {
                _taskMonitoringTask = null;
            }
        }

        /// <summary>
        /// Пересчет ускорений для всех записей относительно базового линейного метода
        /// </summary>
        private void UpdateAllSpeedups()
        {
            // Находим базовое время (линейный метод) - берем среднее
            var linearResults = ComparisonResults
                .Where(r => r.Method.Contains("Линейный"))
                .ToList();
            
            if (linearResults.Count > 0)
            {
                // Используем среднее время линейных методов как базовое
                var baseTime = linearResults.Average(r => r.ExecutionTimeMs);
                
                // Обновляем ускорения для всех записей
                foreach (var result in ComparisonResults)
                {
                    if (result.Method.Contains("Линейный"))
                    {
                        result.Speedup = 1.0; // Базовый метод
                    }
                    else
                    {
                        result.Speedup = baseTime > 0 ? baseTime / result.ExecutionTimeMs : 0;
                    }
                    // Уведомляем об изменении свойства Speedup
                    result.OnPropertyChanged(nameof(ComparisonResult.Speedup));
                }
            }
        }

        private void UpdateComparisonStats()
        {
            OnPropertyChanged(nameof(BestMethod));
            OnPropertyChanged(nameof(BestSpeedup));
            OnPropertyChanged(nameof(AvgDistributedTime));
            OnPropertyChanged(nameof(AvgLinearTime));
            OnPropertyChanged(nameof(TotalComparisons));
        }

        private void UpdateRuntimeStats(WorkerTaskStatusDto status, string methodName)
        {
            MethodName = methodName;
            CurrentMethod = methodName;

            if (status.ElapsedMs.HasValue)
            {
                var elapsed = status.ElapsedMs.Value;
                ExecutionTime = $"{elapsed:N2} мс ({elapsed / 1000.0:F3} сек)";
            }
            else
            {
                ExecutionTime = "-";
            }

            if (status.Error.HasValue)
            {
                Error = status.Error.Value.ToString("E6");
                ErrorText = $"Макс. погрешность: {Error}";
            }
            else
            {
                Error = "-";
                ErrorText = "Погрешность не вычислена";
            }

            if (status.Solution != null && status.Solution.Length > 0)
            {
                _solution = status.Solution;
                UpdateSolutionDisplay();
            }
        }

        private async Task<WorkerTaskStatusDto?> FetchTaskResultAsync(HttpClient httpClient, int taskId, CancellationToken token)
        {
            try
            {
                var response = await httpClient.GetAsync(
                    $"{CoordinatorBaseUrl}/api/Coordinator/tasks/{taskId}/result",
                    token);

                if (!response.IsSuccessStatusCode)
                    return null;

                return await response.Content.ReadFromJsonAsync<WorkerTaskStatusDto>(cancellationToken: token);
            }
            catch
            {
                return null;
            }
        }

        private void ClearResults()
        {
            ComparisonResults.Clear();
            SolutionItems.Clear();
            SolutionText = "";
            ExecutionTime = "-";
            Error = "-";
            ErrorText = "";
            MethodName = "-";
            UpdateComparisonStats();
        }

        private void UpdateMatrixPreview()
        {
            if (_matrix == null)
            {
                MatrixPreview = "";
                return;
            }

            int previewSize = Math.Min(10, _matrix.Rows);
            var lines = new List<string>();
            lines.Add($"Матрица {_matrix.Rows}x{_matrix.Columns} (показаны первые {previewSize} строк и столбцов):\n");
            
            for (int i = 0; i < previewSize; i++)
            {
                var row = _matrix.GetRow(i);
                var rowPreview = row.Take(Math.Min(10, row.Length))
                    .Select(x => $"{x,12:F4}")
                    .ToArray();
                lines.Add($"Строка {i,4}: {string.Join(" ", rowPreview)}");
            }
            
            if (_matrix.Rows > previewSize || _matrix.Columns > 10)
            {
                lines.Add($"\n... (показано {previewSize} из {_matrix.Rows} строк, 10 из {_matrix.Columns} столбцов)");
            }
            
            MatrixPreview = string.Join("\n", lines);
        }

        private void UpdateStatusBar()
        {
            StatusBarText = Status;
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
        }

        private void UpdateTaskProgressIndicator()
        {
            List<int> trackedIds;
            lock (_taskTrackingLock)
            {
                trackedIds = _trackedTaskIds.ToList();
            }

            if (trackedIds.Count == 0)
            {
                if (!IsSolving)
                {
                    ProgressValue = 0;
                    ProgressText = "";
                }
                return;
            }

            var trackedSet = new HashSet<int>(trackedIds);
            var relevantTasks = WorkerTasks
                .Where(t => trackedSet.Contains(t.TaskId))
                .ToList();

            if (relevantTasks.Count == 0)
            {
                if (!IsSolving)
                {
                    ProgressValue = 0;
                    ProgressText = "";
                }
                return;
            }

            double totalProgress = 0;
            foreach (var task in relevantTasks)
            {
                totalProgress += task.Status == "Completed" || task.Status == "Failed"
                    ? 100
                    : Math.Clamp(task.Progress, 0, 100);
            }

            var overall = totalProgress / relevantTasks.Count;
            ProgressValue = Math.Clamp(overall, 0, 100);

            var completed = relevantTasks.Count(t => t.Status == "Completed");
            var failed = relevantTasks.Count(t => t.Status == "Failed");
            var running = relevantTasks.Count(t => t.Status == "Pending" || t.Status == "Running");

            var parts = new List<string> { $"{ProgressValue:F0}%" };
            if (running > 0)
            {
                parts.Add($"Активно: {running}");
            }
            if (completed > 0)
            {
                parts.Add($"Готово: {completed}");
            }
            if (failed > 0)
            {
                parts.Add($"Ошибок: {failed}");
            }

            ProgressText = string.Join(" • ", parts);
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            LogText += $"[{timestamp}] {message}\n";
            OnPropertyChanged(nameof(LogText));
            UpdateStatusBar();
        }

        public void SaveMatrix(string filePath)
        {
            if (_matrix == null)
            {
                throw new InvalidOperationException("Матрица не загружена");
            }

            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"{_matrix.Rows} {_matrix.Columns}");
            
            for (int i = 0; i < _matrix.Rows; i++)
            {
                var row = _matrix.GetRow(i);
                writer.WriteLine(string.Join(" ", row.Select(x => x.ToString("F10"))));
            }
            
            AddLog($"Матрица сохранена в {filePath}");
        }

        public void SaveSolution(string filePath)
        {
            if (_solution == null || _solution.Length == 0)
            {
                throw new InvalidOperationException("Решение не найдено");
            }

            using var writer = new StreamWriter(filePath);
            writer.WriteLine($"Решение системы ({_solution.Length} переменных)");
            writer.WriteLine($"Метод: {MethodName}");
            writer.WriteLine($"Время выполнения: {ExecutionTime}");
            writer.WriteLine($"Погрешность: {Error}");
            writer.WriteLine();
            writer.WriteLine("Значения переменных:");
            
            for (int i = 0; i < _solution.Length; i++)
            {
                writer.WriteLine($"x[{i}] = {_solution[i]:E10} ({_solution[i]:F10})");
            }
            
            AddLog($"Решение сохранено в {filePath}");
        }

        public void SaveComparison(string filePath)
        {
            if (ComparisonResults.Count == 0)
            {
                throw new InvalidOperationException("Нет данных для сравнения");
            }

            using var writer = new StreamWriter(filePath);
            writer.WriteLine("Сравнение методов решения СЛАУ");
            writer.WriteLine($"Дата: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine($"Размер системы: {SystemSize}");
            writer.WriteLine();
            writer.WriteLine("Результаты:");
            writer.WriteLine();
            writer.WriteLine($"{"Метод",-30} {"Время (мс)",15} {"Время (сек)",15} {"Погрешность",20} {"Ускорение",15} {"Статус",-20}");
            writer.WriteLine(new string('-', 115));
            
            foreach (var result in ComparisonResults)
            {
                writer.WriteLine($"{result.Method,-30} {result.ExecutionTimeMs,15:F2} {result.ExecutionTimeSec,15:F6} {result.Error,20:E6} {result.Speedup,15:F2} {result.Status,-20}");
            }
            
            writer.WriteLine();
            writer.WriteLine("Статистика:");
            writer.WriteLine($"Лучший метод: {BestMethod}");
            writer.WriteLine($"Максимальное ускорение: {BestSpeedup:F2}x");
            writer.WriteLine($"Среднее время (распределённый): {AvgDistributedTime:F2} мс");
            writer.WriteLine($"Среднее время (линейный): {AvgLinearTime:F2} мс");
            
            AddLog($"Результаты сравнения сохранены в {filePath}");
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private class MultipleSolveResponse
        {
            public List<int> TaskIds { get; set; } = new();
            public string Message { get; set; } = string.Empty;
        }

        private class StartSolveResponse
        {
            public int TaskId { get; set; }
            public string Worker { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
        }

        private class WorkerTaskStatusDto
        {
            public int TaskId { get; set; }
            public string WorkerUrl { get; set; } = string.Empty;
            public string Method { get; set; } = string.Empty;
            public string Status { get; set; } = "Pending";
            public double Progress { get; set; } = 0;
            public string? ErrorMessage { get; set; }
            public long? ElapsedMs { get; set; }
            public double[]? Solution { get; set; }
            public double? Error { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime? EndTime { get; set; }
        }
    }
}

