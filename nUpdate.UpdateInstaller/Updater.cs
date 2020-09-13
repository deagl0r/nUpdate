// Updater.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Ionic.Zip;
using Newtonsoft.Json.Linq;
using nUpdate.Actions;
using nUpdate.UpdateInstaller.Client.GuiInterface;
using nUpdate.UpdateInstaller.UI.Popups;

namespace nUpdate.UpdateInstaller
{
    public class Updater
    {
        private int _doneTaskAmount;
        private IProgressReporter _progressReporter;
        private int _totalTaskCount;

        /// <summary>
        ///     Runs the updating process.
        /// </summary>
        public void RunUpdate()
        {
            try
            {
                _progressReporter = GetProgressReporter();
            }
            catch (Exception ex)
            {
                Popup.ShowPopup(SystemIcons.Error, "Error while initializing the graphic user interface.", ex,
                    PopupButtons.Ok);
                return;
            }

            ThreadPool.QueueUserWorkItem(arg => RunUpdateAsync());
            try
            {
                _progressReporter.Initialize();
            }
            catch (Exception ex)
            {
                _progressReporter.InitializingFail(ex);
                _progressReporter.Terminate();
            }
        }

        /// <summary>
        ///     Loads the GUI either from a given external assembly, if one is set, or otherwise, from the integrated GUI.
        /// </summary>
        /// <returns>
        ///     Returns a new instance of the given object that implements the
        ///     <see cref="IProgressReporter" />-interface.
        /// </returns>
        private IProgressReporter GetProgressReporter()
        {
            var assembly = string.IsNullOrEmpty(Program.ExternalGuiAssemblyPath) ||
                           !File.Exists(Program.ExternalGuiAssemblyPath)
                ? Assembly.GetExecutingAssembly()
                : Assembly.LoadFrom(Program.ExternalGuiAssemblyPath);
            var provider = ServiceProviderHelper.CreateServiceProvider(assembly);
            return (IProgressReporter) provider.GetService(typeof(IProgressReporter));
        }

        /// <summary>
        ///     Runs the updating process. This method does not block the calling thread.
        /// </summary>
        private void RunUpdateAsync()
        {
            Thread.Sleep(500);
            Process hostProcess = null;
            try
            { 
                hostProcess = Process.GetProcessById(Program.HostProcessId);
                hostProcess.WaitForExit();
            }
            catch (Win32Exception)
            {
                try
                {
                    if (hostProcess != null)
                    {
                        while (!hostProcess.HasExited)
                        {
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (SystemException)
                { }
            }
            catch (SystemException) // Process already killed
            { }

            var parentPath = Directory.GetParent(Program.PackageFilePaths.First()).FullName;
            /* Extract and count for the progress */
            foreach (var packageFilePath in Program.PackageFilePaths)
            {
                var versionString = Path.GetFileNameWithoutExtension(packageFilePath);
                var extractedDirectoryPath =
                    Path.Combine(parentPath, versionString ?? throw new InvalidOperationException());
                Directory.CreateDirectory(extractedDirectoryPath);
                using (var zf = ZipFile.Read(packageFilePath))
                {
                    zf.ParallelDeflateThreshold = -1;
                    try
                    {
                        foreach (var entry in zf) entry.Extract(extractedDirectoryPath);
                    }
                    catch (Exception ex)
                    {
                        _progressReporter.Fail(ex);
                        CleanUp();
                        _progressReporter.Terminate();
                        if (Program.HostApplicationOptions != HostApplicationOptions.CloseAndRestart)
                            return;

                        var process = new Process
                        {
                            StartInfo =
                            {
                                UseShellExecute = true,
                                FileName = Program.ApplicationExecutablePath,
                                Arguments =
                                    string.Join("|",
                                        Program.Arguments.Where(
                                                item =>
                                                    item.ExecutionOptions ==
                                                    UpdateArgumentExecutionOptions.OnlyOnFaulted)
                                            .Select(item => item.Argument))
                            }
                        };
                        process.Start();
                        return;
                    }

                    _totalTaskCount += new DirectoryInfo(extractedDirectoryPath).GetDirectories().Sum(
                        directory => Directory.GetFiles(directory.FullName, "*.*", SearchOption.AllDirectories).Length);

                    string operationsFile = Path.Combine(extractedDirectoryPath, "operations.json");
                    var currentVersionOperations = !File.Exists(operationsFile)
                        ? Program.Operations[versionString].ToList()
                        : Serializer.Deserialize<IEnumerable<IUpdateAction>>(
                            File.ReadAllText(operationsFile)).ToList();

                    // Multiple entries for:
                    /* 
                     * Files: Delete operation
                     * Registry: All operations
                     */

                    _totalTaskCount +=
                        currentVersionOperations.Count(
                            item => item.Name == "DeleteFile" || item.Name.Contains("Registry"));

                    /*foreach (var op in currentVersionOperations.Where(item => item.Name == "DeleteFile" || item.Name.Contains("Registry")))
                        _totalTaskCount += ((JArray) op.Value2).Count;*/
                }
            }

            foreach (
                var packageFilePath in
                Program.PackageFilePaths.OrderBy(item => new UpdateVersion(Path.GetFileNameWithoutExtension(item))))
            {
                var versionString = Path.GetFileNameWithoutExtension(packageFilePath);
                var extractedDirectoryPath =
                    Path.Combine(parentPath, versionString);

                List<IUpdateAction> currentVersionOperations;
                try
                {
                    string operationsFile = Path.Combine(extractedDirectoryPath, "operations.json");
                    currentVersionOperations = !File.Exists(operationsFile)
                        ? Program.Operations[versionString].ToList()
                        : Serializer.Deserialize<IEnumerable<IUpdateAction>>(
                            File.ReadAllText(operationsFile)).ToList();
                    ExecuteOperations(currentVersionOperations.Where(o => o.ExecuteBeforeReplacingFiles));
                }
                catch (Exception ex)
                {
                    _progressReporter.Fail(ex);
                    CleanUp();
                    _progressReporter.Terminate();
                    if (Program.HostApplicationOptions != HostApplicationOptions.CloseAndRestart)
                        return;

                    var process = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = true,
                            FileName = Program.ApplicationExecutablePath,
                            Arguments =
                                string.Join("|",
                                    Program.Arguments.Where(
                                        item =>
                                            item.ExecutionOptions ==
                                            UpdateArgumentExecutionOptions.OnlyOnFaulted).Select(item => item.Argument))
                        }
                    };
                    process.Start();
                    return;
                }

                foreach (var directory in new DirectoryInfo(extractedDirectoryPath).GetDirectories())
                    switch (directory.Name)
                    {
                        case "Program":
                            CopyDirectoryRecursively(directory.FullName, Program.AimFolder);
                            break;
                        case "AppData":
                            CopyDirectoryRecursively(directory.FullName,
                                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
                            break;
                        case "Temp":
                            CopyDirectoryRecursively(directory.FullName, Path.GetTempPath());
                            break;
                        case "Desktop":
                            if (WindowsServiceHelper.IsRunningInServiceContext) continue;
                            CopyDirectoryRecursively(directory.FullName,
                                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
                            break;
                    }

                try
                {
                    ExecuteOperations(currentVersionOperations.Where(o => !o.ExecuteBeforeReplacingFiles));
                }
                catch (Exception ex)
                {
                    _progressReporter.Fail(ex);
                    CleanUp();
                    _progressReporter.Terminate();
                    if (Program.HostApplicationOptions != HostApplicationOptions.CloseAndRestart)
                        return;

                    var process = new Process
                    {
                        StartInfo =
                        {
                            UseShellExecute = true,
                            FileName = Program.ApplicationExecutablePath,
                            Arguments =
                                string.Join("|",
                                    Program.Arguments.Where(
                                        item =>
                                            item.ExecutionOptions ==
                                            UpdateArgumentExecutionOptions.OnlyOnFaulted).Select(item => item.Argument))
                        }
                    };
                    process.Start();
                    return;
                }
            }

            CleanUp();
            if (Program.HostApplicationOptions == HostApplicationOptions.CloseAndRestart)
            {
                var p = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = Program.ApplicationExecutablePath,
                        Arguments =
                            string.Join("|",
                                Program.Arguments.Where(
                                        item =>
                                            item.ExecutionOptions == UpdateArgumentExecutionOptions.OnlyOnSucceeded)
                                    .Select(item => item.Argument))
                    }
                };
                p.Start();
            }

            _progressReporter.Terminate();
        }

        private async void ExecuteOperations(IEnumerable<IUpdateAction> actions)
        {
            foreach (var action in actions)
            {
                await action.Execute();
            }
        }

        /// <summary>
        ///     Cleans up all resources.
        /// </summary>
        private void CleanUp()
        {
            try
            {
                Directory.Delete(Directory.GetParent(Program.PackageFilePaths.First()).FullName, true);
            }
            catch (Exception ex)
            {
                _progressReporter.Fail(ex);
            }
        }

        /// <summary>
        ///     Performs a recursive copy of a given directory.
        /// </summary>
        /// <param name="sourceDirName">The path of the source directory.</param>
        /// <param name="destDirName">The path of the destination directory.</param>
        private void CopyDirectoryRecursively(string sourceDirName, string destDirName)
        {
            try
            {
                var dir = new DirectoryInfo(sourceDirName);
                var sourceDirectories = dir.GetDirectories();

                if (!Directory.Exists(destDirName))
                    Directory.CreateDirectory(destDirName);

                var files = dir.GetFiles();
                foreach (var file in files)
                {
                    var continueCopyLoop = true;
                    var aimPath = Path.Combine(destDirName, file.Name);
                    while (continueCopyLoop)
                        try
                        {
                            file.CopyTo(aimPath, true);
                            continueCopyLoop = false;
                        }
                        catch (IOException ex)
                        {
                            if (FileHelper.IsFileLocked(ex))
                                _progressReporter.Fail(new Exception(string.Format(Program.FileInUseError, aimPath)));
                            else
                                throw;
                        }

                    _doneTaskAmount += 1;
                    var percentage = (float) _doneTaskAmount / _totalTaskCount * 100f;
                    _progressReporter.ReportUnpackingProgress(percentage, file.Name);
                }

                foreach (var subDirectories in sourceDirectories)
                {
                    var aimDirectoryPath = Path.Combine(destDirName, subDirectories.Name);
                    CopyDirectoryRecursively(subDirectories.FullName, aimDirectoryPath);
                }
            }
            catch (Exception ex)
            {
                _progressReporter.Fail(ex);
                CleanUp();
                _progressReporter.Terminate();
                if (Program.HostApplicationOptions != HostApplicationOptions.CloseAndRestart)
                    return;

                var process = new Process
                {
                    StartInfo =
                    {
                        UseShellExecute = true,
                        FileName = Program.ApplicationExecutablePath,
                        Arguments =
                            string.Join("|",
                                Program.Arguments.Where(
                                    item =>
                                        item.ExecutionOptions ==
                                        UpdateArgumentExecutionOptions.OnlyOnFaulted).Select(item => item.Argument))
                    }
                };
                process.Start();
            }
        }
    }
}