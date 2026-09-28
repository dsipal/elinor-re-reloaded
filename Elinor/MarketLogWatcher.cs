using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Elinor
{
    /// <summary>
    /// Watches the market log folder and parses new exports one at a time on a
    /// background task. Callbacks are posted to the SynchronizationContext that
    /// created the watcher (the UI thread), so handlers can touch controls directly.
    /// </summary>
    internal sealed class MarketLogWatcher : IDisposable
    {
        internal static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(5);

        private readonly FileSystemWatcher _watcher;
        private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SynchronizationContext _context;
        private readonly Action<string> _onStarted;
        private readonly Action<MarketSnapshot> _onParsed;
        private readonly Action<string, Exception?> _onFailed;

        internal MarketLogWatcher(string directory,
            Action<string> onStarted,
            Action<MarketSnapshot> onParsed,
            Action<string, Exception?> onFailed)
        {
            _context = SynchronizationContext.Current ?? new SynchronizationContext();
            _onStarted = onStarted;
            _onParsed = onParsed;
            _onFailed = onFailed;

            _watcher = new FileSystemWatcher(directory, "*.txt")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Created += (s, e) => Enqueue(e.FullPath);
            _watcher.Renamed += (s, e) => Enqueue(e.FullPath);
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;

            _ = Task.Run(ConsumeAsync);
        }

        private void Enqueue(string path)
        {
            if (path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                _queue.Writer.TryWrite(path);
        }

        private async Task ConsumeAsync()
        {
            try
            {
                while (await _queue.Reader.WaitToReadAsync(_cts.Token))
                {
                    if (!_queue.Reader.TryRead(out string? path)) continue;

                    // Several exports in a burst: only the newest one matters.
                    while (_queue.Reader.TryRead(out string? newer)) path = newer;

                    Post(() => _onStarted(path));

                    try
                    {
                        string? csv = await TryReadAllTextAsync(path, ReadTimeout, _cts.Token);
                        if (csv == null)
                        {
                            Post(() => _onFailed(path, null));
                            continue;
                        }

                        MarketSnapshot snapshot = MarketLogParser.Parse(path, csv);
                        Post(() => _onParsed(snapshot));
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("Could not process market log " + path, ex);
                        Post(() => _onFailed(path, ex));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed.
            }
            catch (Exception ex)
            {
                Log.Error("Market log watcher stopped", ex);
            }
        }

        /// <summary>
        /// Waits until the writer (EVE) has closed the file, then reads it.
        /// Returns null if the file disappeared; throws IOException after <paramref name="timeout"/>.
        /// </summary>
        internal static async Task<string?> TryReadAllTextAsync(string path, TimeSpan timeout, CancellationToken ct)
        {
            Stopwatch elapsed = Stopwatch.StartNew();

            while (true)
            {
                try
                {
                    // FileShare.Read denies other writers, so this only succeeds once EVE is done
                    // writing, but it still coexists with readers such as antivirus and OneDrive.
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    using var reader = new StreamReader(stream);
                    return await reader.ReadToEndAsync(ct);
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
                catch (DirectoryNotFoundException)
                {
                    return null;
                }
                catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) && elapsed.Elapsed < timeout)
                {
                    await Task.Delay(50, ct);
                }
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Log.Warn("File watcher error", e.GetException());

            try
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not restart file watcher", ex);
            }
        }

        private void Post(Action action)
        {
            _context.Post(_ =>
            {
                if (!_cts.IsCancellationRequested) action();
            }, null);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _queue.Writer.TryComplete();
            _watcher.Dispose();
            // _cts is not disposed: the consumer task may still be reading its token.
        }
    }
}
