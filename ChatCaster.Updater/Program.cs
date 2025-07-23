using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ChatCaster.Updater;

class Program
{
    private static string _logPath = "";
    
    static int Main(string[] args)
    {
        try
        {
            // Настраиваем логирование
            _logPath = Path.Combine(Path.GetTempPath(), "ChatCaster-updater.log");
            
            Log("=== ChatCaster Updater запущен ===");
            Log($"Аргументы: {string.Join(" ", args)}");
            
            // Проверяем аргументы
            if (args.Length < 3)
            {
                Log("ОШИБКА: Недостаточно аргументов");
                Log("Использование: ChatCaster.Updater.exe <путь_к_текущему_exe> <путь_к_ZIP_архиву> <перезапустить_true/false>");
                return 1;
            }
            
            string currentExePath = args[0];
            string zipFilePath = args[1];
            bool restartApp = bool.Parse(args[2]);
            
            Log($"Текущий exe: {currentExePath}");
            Log($"ZIP архив: {zipFilePath}");
            Log($"Перезапуск: {restartApp}");
            
            // Ждем закрытия основного приложения
            Log("Ожидание закрытия основного приложения...");
            Thread.Sleep(3000);
            
            // Принудительно завершаем процессы ChatCaster
            Log("Завершение процессов ChatCaster...");
            TerminateChatCasterProcesses();
            Thread.Sleep(2000);
            
            // Проверяем существование файлов
            if (!File.Exists(zipFilePath))
            {
                Log($"ОШИБКА: ZIP файл не найден: {zipFilePath}");
                return 1;
            }
            
            if (!File.Exists(currentExePath))
            {
                Log($"ОШИБКА: Текущий exe файл не найден: {currentExePath}");
                return 1;
            }
            
            // Получаем директорию приложения
            string appDirectory = Path.GetDirectoryName(currentExePath);
            if (string.IsNullOrEmpty(appDirectory))
            {
                Log("ОШИБКА: Не удалось определить директорию приложения");
                return 1;
            }
            
            Log($"Директория приложения: {appDirectory}");
            
            // Создаем временную папку для распаковки
            string tempExtractPath = Path.Combine(Path.GetTempPath(), "ChatCaster-Update-Extract");
            Log($"Временная папка: {tempExtractPath}");
            
            // Удаляем старую временную папку если есть
            if (Directory.Exists(tempExtractPath))
            {
                Log("Удаление старой временной папки...");
                Directory.Delete(tempExtractPath, true);
            }
            
            // Создаем временную папку
            Directory.CreateDirectory(tempExtractPath);
            Log("Временная папка создана");
            
            // Распаковываем ZIP архив
            Log("Распаковка ZIP архива...");
            ZipFile.ExtractToDirectory(zipFilePath, tempExtractPath);
            Log("ZIP архив распакован успешно");
            
            // Проверяем что распаковка прошла успешно
            var extractedFiles = Directory.GetFiles(tempExtractPath, "*", SearchOption.AllDirectories);
            Log($"Распаковано файлов: {extractedFiles.Length}");
            
            if (extractedFiles.Length == 0)
            {
                Log("ОШИБКА: ZIP архив пустой или не распаковался");
                return 1;
            }
            
            // Создаем резервную копию основного exe файла
            string backupPath = currentExePath + ".backup";
            Log("Создание резервной копии...");
            File.Copy(currentExePath, backupPath, true);
            Log($"Резервная копия создана: {backupPath}");
            
            // Копируем новые файлы
            Log("Копирование новых файлов...");
            CopyDirectory(tempExtractPath, appDirectory);
            Log("Файлы скопированы успешно");
            
            // Проверяем что основной файл обновился
            if (File.Exists(currentExePath))
            {
                var newFileInfo = new FileInfo(currentExePath);
                Log($"Основной файл обновлен успешно. Размер: {newFileInfo.Length} байт, Дата: {newFileInfo.LastWriteTime}");
                
                // Удаляем резервную копию
                File.Delete(backupPath);
                Log("Резервная копия удалена");
            }
            else
            {
                Log("КРИТИЧЕСКАЯ ОШИБКА: Основной файл не найден после копирования!");
                
                // Восстанавливаем из резерва
                if (File.Exists(backupPath))
                {
                    File.Copy(backupPath, currentExePath, true);
                    File.Delete(backupPath);
                    Log("Основной файл восстановлен из резервной копии");
                }
                return 1;
            }
            
            // Запускаем приложение если нужно
            if (restartApp)
            {
                Log("Запуск обновленного приложения...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = currentExePath,
                    UseShellExecute = true,
                    WorkingDirectory = appDirectory
                };
                
                Process.Start(startInfo);
                Log("Приложение запущено успешно");
            }
            
            // Очищаем временные файлы
            Log("Очистка временных файлов...");
            try
            {
                if (Directory.Exists(tempExtractPath))
                {
                    Directory.Delete(tempExtractPath, true);
                }
                
                if (File.Exists(zipFilePath))
                {
                    File.Delete(zipFilePath);
                }
                
                Log("Временные файлы удалены");
            }
            catch (Exception ex)
            {
                Log($"Предупреждение: Не удалось удалить временные файлы: {ex.Message}");
            }
            
            Log("=== Обновление завершено успешно ===");
            return 0;
        }
        catch (Exception ex)
        {
            Log($"КРИТИЧЕСКАЯ ОШИБКА: {ex.Message}");
            Log($"Stack trace: {ex.StackTrace}");
            return 1;
        }
    }
    
    private static void TerminateChatCasterProcesses()
    {
        try
        {
            var processes = Process.GetProcessesByName("ChatCaster.Windows");
            foreach (var process in processes)
            {
                try
                {
                    Log($"Завершение процесса: PID {process.Id}");
                    process.Kill();
                    process.WaitForExit(5000);
                }
                catch (Exception ex)
                {
                    Log($"Не удалось завершить процесс {process.Id}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"Ошибка при завершении процессов: {ex.Message}");
        }
    }
    
    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Исходная директория не найдена: {sourceDir}");
        
        DirectoryInfo[] dirs = dir.GetDirectories();
        
        // Создаем целевую директорию если не существует
        Directory.CreateDirectory(destinationDir);
        
        // Копируем файлы в текущей директории
        foreach (FileInfo file in dir.GetFiles())
        {
            string targetFilePath = Path.Combine(destinationDir, file.Name);
            Log($"Копирование файла: {file.Name}");
            file.CopyTo(targetFilePath, true);
        }
        
        // Рекурсивно копируем поддиректории
        foreach (DirectoryInfo subDir in dirs)
        {
            string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            Log($"Копирование директории: {subDir.Name}");
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
    
    private static void Log(string message)
    {
        string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Console.WriteLine(logMessage);
        
        try
        {
            File.AppendAllText(_logPath, logMessage + Environment.NewLine);
        }
        catch
        {
            // Игнорируем ошибки записи в лог
        }
    }
}