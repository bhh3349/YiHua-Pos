// 仓鼠2号 · T-105 电子秤协议
// 帧缓冲：累积串口字节流，处理粘包/半包。
// 容量上限为 8 帧（《协议规范》1.2/6.2 节），超出时丢弃最旧字节。

namespace YiHua.Pos.Hardware.Scales;

/// <summary>
/// 串口字节流累积缓冲。只负责存取字节，帧切分由各协议的
/// <see cref="Protocols.ScaleProtocolBase.FindFrameStart"/> 完成。
/// </summary>
public sealed class ScaleFrameBuffer
{
    private readonly byte[] _data;
    private readonly int _capacity;
    private int _length;

    /// <param name="frameLength">单帧字节数</param>
    /// <param name="maxFrames">缓冲上限帧数（默认 8）</param>
    public ScaleFrameBuffer(int frameLength, int maxFrames = 8)
    {
        if (frameLength <= 0) throw new ArgumentOutOfRangeException(nameof(frameLength));
        if (maxFrames <= 0) throw new ArgumentOutOfRangeException(nameof(maxFrames));
        _capacity = frameLength * maxFrames;
        _data = new byte[_capacity];
    }

    /// <summary>当前缓冲字节数</summary>
    public int Length => _length;

    /// <summary>已缓冲的数据</summary>
    public ReadOnlySpan<byte> Buffered => _data.AsSpan(0, _length);

    /// <summary>追加数据；放不下时先丢弃最旧的，保证不溢出</summary>
    public void Append(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length >= _capacity)
        {
            // 单次数据超过整个缓冲：只保留最后 _capacity 字节
            chunk.Slice(chunk.Length - _capacity).CopyTo(_data);
            _length = _capacity;
            return;
        }
        if (_length + chunk.Length > _capacity)
            Consume(_length + chunk.Length - _capacity);
        chunk.CopyTo(_data.AsSpan(_length));
        _length += chunk.Length;
    }

    /// <summary>从头部消费 count 字节（已解析的帧/噪声）</summary>
    public void Consume(int count)
    {
        if (count <= 0) return;
        if (count >= _length)
        {
            _length = 0;
            return;
        }
        _data.AsSpan(count, _length - count).CopyTo(_data);
        _length -= count;
    }

    /// <summary>清空缓冲</summary>
    public void Clear() => _length = 0;
}
