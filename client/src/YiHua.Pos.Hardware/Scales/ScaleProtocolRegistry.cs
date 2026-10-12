// 仓鼠2号 · T-105 电子秤协议
// 协议注册表：按名创建协议实例，并定义自动探测顺序（按市场占有率）。

using YiHua.Pos.Hardware.Abstractions;
using YiHua.Pos.Hardware.Scales.Protocols;

namespace YiHua.Pos.Hardware.Scales;

/// <summary>电子秤协议注册表</summary>
public sealed class ScaleProtocolRegistry
{
    private readonly Dictionary<string, Func<ScaleProtocolBase>> _factories
        = new(StringComparer.OrdinalIgnoreCase);

    public ScaleProtocolRegistry()
    {
        Register("DaHua.OS2X", () => new DaHuaOs2xProtocol());
        Register("DaHua.PS1X", () => new DaHuaPs1xProtocol());
        Register("Toledo.ArivaS", () => new ToledoArivaProtocol());
        Register("CAS.CAS216", () => new KssCas216Protocol());
    }

    /// <summary>注册自定义协议（名区分大小写不敏感）</summary>
    public void Register(string protocolName, Func<ScaleProtocolBase> factory)
    {
        if (string.IsNullOrWhiteSpace(protocolName))
            throw new ArgumentException("协议名不能为空", nameof(protocolName));
        _factories[protocolName] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>按协议名创建实例</summary>
    public IScaleProtocol Create(string protocolName)
        => _factories.TryGetValue(protocolName, out var factory)
            ? factory()
            : throw new ArgumentException($"未知协议: '{protocolName}'", nameof(protocolName));

    /// <summary>已注册的协议名</summary>
    public IReadOnlyList<string> ProtocolNames => new List<string>(_factories.Keys);

    /// <summary>
    /// 自动探测顺序的协议实例（按市场占有率：大华 → 托利多 → 凯士 → 自定义）。
    /// 每次调用返回新实例，探测互不干扰。
    /// </summary>
    public IEnumerable<ScaleProtocolBase> CreateInDetectOrder()
    {
        var builtIn = new[] { "DaHua.OS2X", "DaHua.PS1X", "Toledo.ArivaS", "CAS.CAS216" };
        foreach (var name in builtIn)
            yield return _factories[name]();
        foreach (var kv in _factories)
        {
            if (Array.IndexOf(builtIn, kv.Key) < 0)
                yield return kv.Value();
        }
    }
}
