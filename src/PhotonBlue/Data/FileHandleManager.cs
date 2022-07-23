using System.Collections.Concurrent;

namespace PhotonBlue.Data;

public class FileHandleManager : IDisposable
{
    private readonly ConcurrentQueue<(bool, WeakReference<BaseFileHandle>)> _fileQueue;

    private readonly CancellationTokenSource? _tokenSource;
    private readonly Thread? _loadThread1;
    private readonly Thread? _loadThread2;

    public FileHandleManager(bool processQueueInternally)
    {
        _fileQueue = new ConcurrentQueue<(bool, WeakReference<BaseFileHandle>)>();

        if (processQueueInternally)
        {
            _tokenSource = new CancellationTokenSource();
            
            _loadThread1 = new Thread(() =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    if (HasPendingFileLoads)
                    {
                        ProcessQueue(_tokenSource.Token);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
            _loadThread1.Start();
            
            _loadThread2 = new Thread(() =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    if (HasPendingFileLoads)
                    {
                        ProcessQueue(_tokenSource.Token);
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
            });
            _loadThread2.Start();
        }
    }

    /// <summary>
    /// Creates a new handle to a game file but does not load it. You will need to call <see cref="ProcessQueue"/>
    /// yourself for these handles to be loaded on a different thread.
    /// </summary>
    /// <param name="path">The path to the file to load.</param>
    /// <param name="loadComplete">Whether or not to load the complete file when processing the queue.</param>
    /// <typeparam name="T">The type of <see cref="FileResource"/> to load.</typeparam>
    /// <returns>A handle to the file to be loaded.</returns>
    public FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        var handle = new FileHandle<T>(path);
        var weakRef = new WeakReference<BaseFileHandle>(handle);
        _fileQueue.Enqueue((loadComplete, weakRef));

        return handle;
    }

    /// <summary>
    /// Processes enqueued file handles that haven't been loaded yet. You should call this on a different thread to process handles.
    /// </summary>
    public void ProcessQueue(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested && HasPendingFileLoads)
        {
            var res = _fileQueue.TryDequeue(out var entry);
            var (loadComplete, weakRef) = entry;
            if (res && weakRef!.TryGetTarget(out var handle))
            {
                if (loadComplete)
                {
                    handle.Load();
                }
                else
                {
                    handle.LoadHeadersOnly();
                }
            }
        }
    }

    /// <summary>
    /// Whether the file queue contains any files that are yet to be loaded.
    /// </summary>
    public bool HasPendingFileLoads => !_fileQueue.IsEmpty;

    public void Dispose()
    {
        _tokenSource?.Cancel();
        _loadThread1?.Join();
        _loadThread2?.Join();
        GC.SuppressFinalize(this);
    }
}