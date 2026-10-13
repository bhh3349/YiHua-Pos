using Prism.Ioc;

namespace YiHua.Pos.App.Services;

/// <summary>Prism 容器适配 IServiceProvider，供业务编排层按需解析已注册服务。</summary>
internal sealed class PrismServiceProvider : IServiceProvider
{
    private readonly IContainerProvider _container;
    public PrismServiceProvider(IContainerProvider container) => _container = container;
    public object? GetService(Type serviceType)
    {
        try { return _container.Resolve(serviceType); }
        catch { return null; }
    }
}