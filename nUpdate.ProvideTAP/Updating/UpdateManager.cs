// UpdateManager.cs, 10.06.2019
// Copyright (C) Dominic Beger 17.06.2019

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using nUpdate.Exceptions;
using nUpdate.Internal.Core;
using nUpdate.Internal.Core.Operations;
using nUpdate.UpdateEventArgs;

namespace nUpdate.Updating
{
    // PROVIDE TAP
    /// <summary>
    ///     Provides functionality to update .NET-applications.
    /// </summary>
    public partial class UpdateManager
    {
        /// <summary>
        ///     Downloads the available update packages from the server.
        /// </summary>
        /// <seealso cref="DownloadPackagesAsync" />
        public void DownloadPackages()
        {
            if (!Directory.Exists(_applicationUpdateDirectory))
                Directory.CreateDirectory(_applicationUpdateDirectory);

            foreach (var updateConfiguration in PackageConfigurations)
            {
                WebResponse webResponse = null;
                try
                {
                    var webRequest = WebRequestWrapper.Create(updateConfiguration.UpdatePackageUri);
                    if (HttpAuthenticationCredentials != null)
                        webRequest.Credentials = HttpAuthenticationCredentials;
                    using (webResponse = webRequest.GetResponse())
                    {
                        var buffer = new byte[1024];
                        _packageFilePaths.Add(new UpdateVersion(updateConfiguration.LiteralVersion),
                            Path.Combine(_applicationUpdateDirectory,
                                $"{updateConfiguration.LiteralVersion}.zip"));
                        using (var fileStream = File.Create(Path.Combine(_applicationUpdateDirectory,
                            $"{updateConfiguration.LiteralVersion}.zip")))
                        {
                            using (var input = webResponse.GetResponseStream())
                            {
                                if (input == null)
                                    throw new Exception("The response stream couldn't be read.");

                                var size = input.Read(buffer, 0, buffer.Length);
                                while (size > 0)
                                {
                                    fileStream.Write(buffer, 0, size);
                                    size = input.Read(buffer, 0, buffer.Length);
                                }

                                if (!updateConfiguration.UseStatistics || !IncludeCurrentPcIntoStatistics)
                                    continue;

                                var response =
                                    new WebClient {Credentials = HttpAuthenticationCredentials}.DownloadString(
                                        $"{updateConfiguration.UpdatePhpFileUri}?versionid={updateConfiguration.VersionId}&os={SystemInformation.OperatingSystemName}"); // Only for calling it

                                if (string.IsNullOrEmpty(response))
                                    return;
                            }
                        }
                    }
                }
                finally
                {
                    webResponse?.Close();
                }
            }
        }

        /// <summary>
        ///     Downloads the available update packages from the server, asynchronously.
        /// </summary>
        /// <exception cref="OperationCanceledException" />
        /// <exception cref="StatisticsException" />
        /// <seealso cref="DownloadPackages" />
        public Task DownloadPackagesAsync(IProgress<UpdateDownloadProgressChangedEventArgs> progress = null)
        {
            return Task.Run(async () =>
            {
                _downloadCancellationTokenSource?.Dispose();
                _downloadCancellationTokenSource = new CancellationTokenSource();

                long received = 0;
                var total = PackageConfigurations.Select(config => GetUpdatePackageSize(config.UpdatePackageUri))
                    .Where(updatePackageSize => updatePackageSize != null)
                    .Sum(updatePackageSize => updatePackageSize.Value);

                if (!Directory.Exists(_applicationUpdateDirectory))
                    Directory.CreateDirectory(_applicationUpdateDirectory);

                foreach (var updateConfiguration in PackageConfigurations)
                {
                    WebResponse webResponse = null;
                    try
                    {
                        if (_downloadCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            DeletePackages();
                            Cleanup();
                            throw new OperationCanceledException();
                        }

                        var webRequest = WebRequestWrapper.Create(updateConfiguration.UpdatePackageUri);
                        if (HttpAuthenticationCredentials != null)
                            webRequest.Credentials = HttpAuthenticationCredentials;
                        webResponse = await webRequest.GetResponseAsync();

                        var buffer = new byte[1024];
                        _packageFilePaths.Add(new UpdateVersion(updateConfiguration.LiteralVersion),
                            Path.Combine(_applicationUpdateDirectory,
                                $"{updateConfiguration.LiteralVersion}.zip"));
                        using (var fileStream = File.Create(Path.Combine(_applicationUpdateDirectory,
                            $"{updateConfiguration.LiteralVersion}.zip")))
                        {
                            using (var input = webResponse.GetResponseStream())
                            {
                                if (input == null)
                                    throw new Exception("The response stream couldn't be read.");

                                if (_downloadCancellationTokenSource.Token.IsCancellationRequested)
                                {
                                    DeletePackages();
                                    Cleanup();
                                    throw new OperationCanceledException();
                                }

                                var size = await input.ReadAsync(buffer, 0, buffer.Length);
                                while (size > 0)
                                {
                                    if (_downloadCancellationTokenSource.Token.IsCancellationRequested)
                                    {
                                        fileStream.Flush();
                                        fileStream.Close();
                                        DeletePackages();
                                        Cleanup();
                                        throw new OperationCanceledException();
                                    }

                                    await fileStream.WriteAsync(buffer, 0, size);
                                    received += size;
                                    progress?.Report(new UpdateDownloadProgressChangedEventArgs(received,
                                        (long) total, (float) (received / total) * 100));
                                    size = await input.ReadAsync(buffer, 0, buffer.Length);
                                }

                                if (!updateConfiguration.UseStatistics || !IncludeCurrentPcIntoStatistics)
                                    continue;

                                var response =
                                    new WebClient
                                    {
                                        Credentials =
                                            HttpAuthenticationCredentials
                                    }.DownloadString(
                                        $"{updateConfiguration.UpdatePhpFileUri}?versionid={updateConfiguration.VersionId}&os={SystemInformation.OperatingSystemName}"); // Only for calling it
                                if (!string.IsNullOrEmpty(response))
                                    throw new StatisticsException(string.Format(
                                        _lp.StatisticsScriptExceptionText, response));
                            }
                        }
                    }
                    finally
                    {
                        webResponse?.Close();
                    }
                }
            });
        }

        /// <summary>
        ///     Searches for updates.
        /// </summary>
        /// <returns>Returns <c>true</c> if updates were found; otherwise, <c>false</c>.</returns>
        /// <exception cref="SizeCalculationException">The calculation of the size of the available updates has failed.</exception>
        public bool SearchForUpdates()
        {
            // It may be that this is not the first search call and previously saved data needs to be disposed.
            Cleanup();

            if (!ConnectionManager.IsConnectionAvailable())
                return false;
            
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            var configuration =
                UpdateConfiguration.Download(UpdateConfigurationFileUri, HttpAuthenticationCredentials, Proxy,
                    SearchTimeout);

            var result = new UpdateResult(configuration, CurrentVersion,
                IncludeAlpha, IncludeBeta, Conditions);
            if (!result.UpdatesFound)
                return false;

            PackageConfigurations = result.NewestConfigurations;
            double updatePackageSize = 0;
            foreach (var updateConfiguration in PackageConfigurations)
            {
                updateConfiguration.UpdatePackageUri = ConvertPackageUri(updateConfiguration.UpdatePackageUri);
                updateConfiguration.UpdatePhpFileUri = ConvertStatisticsUri(updateConfiguration.UpdatePhpFileUri);

                var newPackageSize = GetUpdatePackageSize(updateConfiguration.UpdatePackageUri);
                if (newPackageSize == null)
                    throw new SizeCalculationException(_lp.PackageSizeCalculationExceptionText);

                updatePackageSize += newPackageSize.Value;
                if (updateConfiguration.Operations == null) continue;
                if (_packageOperations == null)
                    _packageOperations = new Dictionary<UpdateVersion, IEnumerable<Operation>>();
                _packageOperations.Add(new UpdateVersion(updateConfiguration.LiteralVersion),
                    updateConfiguration.Operations);
            }

            TotalSize = updatePackageSize;
            return true;
        }

        /// <summary>
        ///     Searches for updates, asynchronously.
        /// </summary>
        /// <seealso cref="SearchForUpdates" />
        /// <exception cref="SizeCalculationException" />
        /// <exception cref="OperationCanceledException" />
        public Task<bool> SearchForUpdatesAsync()
        {
            return Task.Run(async () =>
            {
                // It may be that this is not the first search call and previously saved data needs to be disposed.
                Cleanup();
                _searchCancellationTokenSource?.Dispose();
                _searchCancellationTokenSource = new CancellationTokenSource();

                if (!ConnectionManager.IsConnectionAvailable())
                    return false;

                _searchCancellationTokenSource.Token.ThrowIfCancellationRequested();
                ServicePointManager.Expect100Continue = true;
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                var configuration =
                    await UpdateConfiguration.DownloadAsync(UpdateConfigurationFileUri, HttpAuthenticationCredentials,
                        Proxy, _searchCancellationTokenSource, SearchTimeout);

                _searchCancellationTokenSource.Token.ThrowIfCancellationRequested();
                var result = new UpdateResult(configuration, CurrentVersion,
                    IncludeAlpha, IncludeBeta, Conditions);
                if (!result.UpdatesFound)
                    return false;

                PackageConfigurations = result.NewestConfigurations;
                double updatePackageSize = 0;
                foreach (var updateConfiguration in PackageConfigurations)
                {
                    updateConfiguration.UpdatePackageUri = ConvertPackageUri(updateConfiguration.UpdatePackageUri);
                    updateConfiguration.UpdatePhpFileUri = ConvertStatisticsUri(updateConfiguration.UpdatePhpFileUri);

                    _searchCancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var newPackageSize = GetUpdatePackageSize(updateConfiguration.UpdatePackageUri);
                    if (newPackageSize == null)
                        throw new SizeCalculationException(_lp.PackageSizeCalculationExceptionText);

                    updatePackageSize += newPackageSize.Value;
                    if (updateConfiguration.Operations == null) continue;
                    if (_packageOperations == null)
                        _packageOperations = new Dictionary<UpdateVersion, IEnumerable<Operation>>();
                    _packageOperations.Add(new UpdateVersion(updateConfiguration.LiteralVersion),
                        updateConfiguration.Operations);
                }

                TotalSize = updatePackageSize;
                if (!_searchCancellationTokenSource.Token.IsCancellationRequested)
                    return true;
                throw new OperationCanceledException();
            });
        }
    }
}