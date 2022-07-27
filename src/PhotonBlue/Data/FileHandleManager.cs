﻿namespace PhotonBlue.Data;

public class FileHandleManager
{
    /// <summary>
    /// Creates a new handle to a game file but does not load it. You will need to call <see cref="ProcessQueue"/>
    /// yourself for these handles to be loaded on a different thread.
    /// </summary>
    /// <param name="path">The path to the file to load.</param>
    /// <param name="loadComplete">Whether or not to load the complete file when processing the queue.</param>
    /// <typeparam name="T">The type of <see cref="FileResource"/> to load.</typeparam>
    /// <returns>A handle to the file to be loaded.</returns>
    public static FileHandle<T> CreateHandle<T>(string path, bool loadComplete = true) where T : FileResource, new()
    {
        var handle = new FileHandle<T>(path);
        var weakRef = new WeakReference<BaseFileHandle>(handle);
        if (loadComplete)
        {
            ThreadPool.UnsafeQueueUserWorkItem(LoadFileHandle, weakRef);
        }
        else
        {
            ThreadPool.UnsafeQueueUserWorkItem(LoadFileHandleHeadersOnly, weakRef);
        }

        return handle;
    }

    private static void LoadFileHandle(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandle(weakRef);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    private static void LoadFileHandle(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.Load();
        }
    }

    private static void LoadFileHandleHeadersOnly(object? o)
    {
        if (o is WeakReference<BaseFileHandle> weakRef)
        {
            LoadFileHandleHeadersOnly(weakRef);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }

    private static void LoadFileHandleHeadersOnly(WeakReference<BaseFileHandle> weakRef)
    {
        if (weakRef.TryGetTarget(out var handle))
        {
            handle.LoadHeadersOnly();
        }
    }
}