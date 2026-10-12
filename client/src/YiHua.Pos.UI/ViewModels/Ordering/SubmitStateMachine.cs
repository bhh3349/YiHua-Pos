namespace YiHua.Pos.UI.ViewModels.Ordering;

/// <summary>提交状态</summary>
public enum SubmitState
{
    /// <summary>空闲</summary>
    Idle,
    /// <summary>提交中（按钮显示「处理中」，禁止重复点击）</summary>
    Submitting,
    /// <summary>提交成功</summary>
    Succeeded,
    /// <summary>提交失败（可重试，重试不产生新单）</summary>
    Failed,
    /// <summary>提交冲突（如订单已被他人结账，需人工处理）</summary>
    Conflict,
}

/// <summary>提交结果</summary>
public sealed class SubmitResult
{
    public bool IsSuccess { get; private set; }
    public bool IsDuplicateIgnored { get; private set; }
    public bool IsConflict { get; private set; }
    public long? OrderId { get; private set; }
    public string? ClientOrderNo { get; private set; }
    public string? Error { get; private set; }

    public static SubmitResult Success(long orderId, string clientOrderNo)
        => new() { IsSuccess = true, OrderId = orderId, ClientOrderNo = clientOrderNo };

    public static SubmitResult DuplicateIgnored()
        => new() { IsDuplicateIgnored = true };

    public static SubmitResult Failed(string? error)
        => new() { Error = error };

    public static SubmitResult Conflict(string? error)
        => new() { IsConflict = true, Error = error };
}

/// <summary>
/// 提交状态机：防重复提交 / 失败重试 / 冲突处理。
///
/// 幂等设计：
/// - 一个逻辑订单只生成一个 clientOrderNo（GUID），提交周期内复用；
/// - 并发提交（连点）被信号量挡掉，后到的直接返回 DuplicateIgnored；
/// - 失败重试复用同一个 clientOrderNo，数据层按幂等键去重，不产生新单；
/// - 成功后重置，允许下一单生成新的 clientOrderNo。
/// </summary>
public sealed class SubmitStateMachine : ViewModelBase
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SubmitState _state = SubmitState.Idle;
    private string? _errorMessage;
    private long? _orderId;

    /// <summary>当前逻辑订单的幂等键（提交周期内稳定）</summary>
    public string? ClientOrderNo { get; private set; }

    public SubmitState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public long? OrderId
    {
        get => _orderId;
        private set => SetProperty(ref _orderId, value);
    }

    public bool IsSubmitting => State == SubmitState.Submitting;
    public bool IsSucceeded => State == SubmitState.Succeeded;
    public bool IsFailed => State == SubmitState.Failed;
    public bool IsConflict => State == SubmitState.Conflict;

    public event EventHandler<SubmitState>? StateChanged;

    /// <summary>
    /// 提交。submitFn 接收 clientOrderNo，返回 (是否成功, 订单ID, 是否冲突, 错误信息)。
    /// </summary>
    public async Task<SubmitResult> SubmitAsync(
        Func<string, CancellationToken, Task<(bool ok, long orderId, bool conflict, string? error)>> submitFn,
        CancellationToken ct = default)
    {
        // 防重：拿不到信号量说明正在提交中，直接忽略
        if (!await _gate.WaitAsync(0, ct).ConfigureAwait(false))
            return SubmitResult.DuplicateIgnored();

        try
        {
            if (State == SubmitState.Submitting)
                return SubmitResult.DuplicateIgnored();

            // 幂等键：一次提交周期只生成一次（失败重试复用）
            ClientOrderNo ??= Guid.NewGuid().ToString("N");
            SetState(SubmitState.Submitting);
            ErrorMessage = null;

            var (ok, orderId, conflict, error) = await submitFn(ClientOrderNo, ct).ConfigureAwait(false);

            if (ok)
            {
                OrderId = orderId;
                string usedNo = ClientOrderNo;
                ClientOrderNo = null; // 成功后重置，下一单用新键
                SetState(SubmitState.Succeeded);
                return SubmitResult.Success(orderId, usedNo);
            }

            if (conflict)
            {
                ErrorMessage = error;
                SetState(SubmitState.Conflict);
                return SubmitResult.Conflict(error);
            }

            ErrorMessage = error;
            SetState(SubmitState.Failed);
            // 失败保留 ClientOrderNo，重试不产生新单
            return SubmitResult.Failed(error);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>重置为初始状态（开始新的一单）</summary>
    public void Reset()
    {
        ClientOrderNo = null;
        OrderId = null;
        ErrorMessage = null;
        SetState(SubmitState.Idle);
    }

    private void SetState(SubmitState state)
    {
        State = state;
        RaisePropertyChanged(nameof(IsSubmitting));
        RaisePropertyChanged(nameof(IsSucceeded));
        RaisePropertyChanged(nameof(IsFailed));
        RaisePropertyChanged(nameof(IsConflict));
        StateChanged?.Invoke(this, state);
    }
}
