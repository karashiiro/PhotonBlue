using PhotonBlue.Data;
using PhotonBlue.Data.Files;
using Xunit;

namespace PhotonBlue.Tests;

public class FileHandleTests
{
    [Fact]
    public void Should_Load()
    {
        var handle = new FileHandle<IceV4File>(@"..\..\..\..\..\testdata\0006b03a4c2763ffcd7d4547f71600dd");
        handle.Load();
        
        if (handle.LoadException != null)
        {
            throw handle.LoadException;
        }
        
        Assert.Equal(BaseFileHandle.FileState.Loaded, handle.State);
        Assert.NotNull(handle.Value);
    }
    
    [Fact]
    public void Should_Load_HeadersOnly()
    {
        var handle = new FileHandle<IceV4File>(@"..\..\..\..\..\testdata\0006b03a4c2763ffcd7d4547f71600dd");
        handle.LoadHeadersOnly();
        
        if (handle.LoadException != null)
        {
            throw handle.LoadException;
        }
        
        Assert.Equal(BaseFileHandle.FileState.Loaded, handle.State);
        Assert.Null(handle.LoadException);
        Assert.NotNull(handle.Value);
    }
}