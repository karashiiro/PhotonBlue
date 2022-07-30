namespace PhotonBlue;

public interface IObjectPool<T, in TData>
{
    T Acquire(TData data);

    void Release(T instance);
}