using System;
using System.Windows;
using System.Windows.Threading;

namespace DistributedSolver.Client
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Обработка необработанных исключений
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Произошла ошибка:\n{e.Exception.Message}\n\nПодробности:\n{e.Exception}",
                "Ошибка приложения",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(
                $"Критическая ошибка:\n{e.ExceptionObject}",
                "Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}

