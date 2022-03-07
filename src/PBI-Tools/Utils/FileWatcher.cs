// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Serilog;

namespace PbiTools.Utils
{

    public class FileWatcher : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<FileWatcher>();

        private readonly SemaphoreSlim _semaphore;
        private readonly FileSystemWatcher _watcher;

        private System.Threading.Timer _timer;

        private readonly Action _onChanged;
        private DateTime _lastExecution = DateTime.MinValue;
        
        /// <summary>
        /// Used as a synchronization token for thread-safe access to the <see cref="CancellationTokenSource"/>
        /// which schedules the OnChanged handler.
        /// </summary>
        private readonly object _lock = new object();
        private readonly string _path;

        /// <summary>
        /// Gets or sets the time span within which subsequent file change events are ignored. Default is 1000ms.
        /// </summary>
        public TimeSpan ThrottleTimeSpan { get; set; } = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// Gets or sets the time span used to wait for further change notifications before triggering the OnChanged handler. Default is 1000ms.
        /// </summary>
        public TimeSpan OnChangeHandlerDelay { get; set; } = TimeSpan.FromMilliseconds(1000);

        public event FileSystemEventHandler FileDeleted;
        public event RenamedEventHandler FileRenamed;

        public FileWatcher(string path, Action onChanged, CancellationToken cancellation)
        {
            var file = new FileInfo(path);
            if (!file.Exists) throw new FileNotFoundException("The file does not exist.", path);
            _path = file.FullName;

            _onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            _semaphore = new SemaphoreSlim(1); // Ensures only a single OnChanged handler can be running at any time
            _watcher = new FileSystemWatcher(file.DirectoryName) { Filter = file.Name };

            _timer = new Timer(RunChangeHandler);

            _watcher.NotifyFilter = NotifyFilters.LastWrite 
                                 //| NotifyFilters.Attributes
                                 //| NotifyFilters.CreationTime
                                 //| NotifyFilters.DirectoryName
                                 //| NotifyFilters.FileName
                                 //| NotifyFilters.LastAccess
                                 //| NotifyFilters.Security
                                 //| NotifyFilters.Size
                                 ;

            _watcher.Changed += OnChanged;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
            _watcher.Error += OnError;

            _watcher.EnableRaisingEvents = true;

            cancellation.Register(OnCancelled);
        }


        private void RunChangeHandler(object _)
        {
            if (!_watcher.EnableRaisingEvents) return;

            if (_semaphore.Wait(0)) // Prevent firing while callback is running
            {
                var handlerRun = false;
                try
                {
                    if ((DateTime.Now - _lastExecution) > ThrottleTimeSpan) // Prevent firing within xxx ms from last invocation
                    {
                        Log.Debug("Invoking OnChanged handler for {Path}", _path);
                        handlerRun = true;
                        _onChanged();
                    }
                    else
                    {
                        Log.Verbose("Skipping OnChanged handler for {Path} due to ThrottleTimeSpan.", _path);
                    }
                }
                finally
                {
                    Log.Verbose("OnChanged handler for {Path} completed.", _path);
                    if (handlerRun) _lastExecution = DateTime.Now; // Thread synchronization is ensured via the Semaphore
                    _semaphore.Release();
                }
            }
            else if (Log.IsEnabled(Serilog.Events.LogEventLevel.Verbose))
            {
                Log.Verbose("Skipping OnChanged handler for {Path} because another handler is still running.", _path);
            }
        }


        private void OnCancelled()
        {
            // Disable watcher
            _watcher.EnableRaisingEvents = false;
        }


        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lock)
            {
                if (e.ChangeType != WatcherChangeTypes.Changed)
                    return;

                _timer.Change(OnChangeHandlerDelay, Timeout.InfiniteTimeSpan);
                Log.Debug("Scheduled OnChange handler for {Path} at {AbsoluteExpiration}", e.FullPath, DateTime.Now.Add(OnChangeHandlerDelay));
            }
        }

        private void OnDeleted(object sender, FileSystemEventArgs e)
        {
            Log.Warning("The file being watched was deleted: {Path}", e.FullPath);

            _watcher.EnableRaisingEvents = false;

            this.FileDeleted?.Invoke(this, e);
        }

        private void OnRenamed(object sender, RenamedEventArgs e)
        {
            Log.Debug("File renamed from {OldPath} to {NewPath}", e.OldFullPath, e.FullPath);

            _watcher.EnableRaisingEvents = false;

            this.FileRenamed?.Invoke(this, e);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Log.Warning(e.GetException(), "A FileSystemWatcher error occurred monitoring {Path}", Path.Combine(_watcher.Path, _watcher.Filter));
        }

        private bool disposed;

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;

                _semaphore.Wait(TimeSpan.FromSeconds(10)); // Do not dispose while OnChanged handler is running...

                _watcher.Dispose();
                _semaphore.Dispose();
                _timer.Dispose();
            }
        }
    }

}