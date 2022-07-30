using Ninject.Modules;
using PhotonBlue.Cryptography;
using PhotonBlue.Data;
using PhotonBlue.Persistence;

namespace PhotonBlue;

internal class ServiceModule : NinjectModule
{
    private readonly Func<IGameFileIndex>? _indexProvider;

    public ServiceModule(Func<IGameFileIndex>? indexProvider)
    {
        _indexProvider = indexProvider;
    }

    public override void Load()
    {
        Bind<IObjectPool<BlowfishGpuHandle, Blowfish>>().To<BlowfishGpuBufferPool>().InSingletonScope();
        Bind<IFileHandleProvider>().To<FileHandleManager>().InSingletonScope();
        Bind<IGameFileIndexer>().To<GameFileIndexer>().InSingletonScope();

        if (_indexProvider == null)
        {
            Bind<IGameFileIndex>().To<MemoryIndex>().InSingletonScope();
        }
        else
        {
            Bind<IGameFileIndex>().ToMethod(_ => _indexProvider());
        }
    }
}