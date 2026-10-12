namespace YiHua.Pos.UI.ViewModels.Payment;

/// <summary>
/// 结账提交状态机：防重复点击、冲突检测、失败重试。
/// 打印失败不回滚结账（结账成功后打印失败走重试流程）。
/// </summary>
public class PaymentSubmitStateMachine
{
    public PaymentSubmitState State { get; private set; } = PaymentSubmitState.Idle;

    /// <summary>冲突信息（本单已被其他设备结账）。</summary>
    public string? ConflictMessage { get; private set; }

    /// <summary>最后一次错误。</summary>
    public string? LastError { get; private set; }

    /// <summary>是否正在提交（用于防重）。</summary>
    public bool IsSubmitting => State == PaymentSubmitState.Submitting;

    /// <summary>是否可提交（空闲或失败重试）。</summary>
    public bool CanSubmit => State is PaymentSubmitState.Idle or PaymentSubmitState.Failed;

    public event EventHandler<PaymentSubmitState>? StateChanged;

    private void SetState(PaymentSubmitState state)
    {
        State = state;
        StateChanged?.Invoke(this, state);
    }

    /// <summary>
    /// 提交结账（可测试）。
    /// </summary>
    /// <param name="submitFunc">实际提交逻辑，返回 (成功, 是否冲突, 错误信息)</param>
    public async Task<bool> SubmitAsync(
        Func<CancellationToken, Task<(bool Success, bool IsConflict, string? Error)>> submitFunc,
        CancellationToken ct = default)
    {
        if (!CanSubmit)
            return false; // 防重：提交中或已成功时忽略

        SetState(PaymentSubmitState.Submitting);
        ConflictMessage = null;
        LastError = null;

        try
        {
            var (success, isConflict, error) = await submitFunc(ct);
            if (success)
            {
                SetState(PaymentSubmitState.Succeeded);
                return true;
            }
            if (isConflict)
            {
                ConflictMessage = error ?? "本单已在其他设备结账";
                SetState(PaymentSubmitState.Conflicted);
                return false;
            }
            LastError = error ?? "提交失败，请重试";
            SetState(PaymentSubmitState.Failed);
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"提交异常：{ex.Message}";
            SetState(PaymentSubmitState.Failed);
            return false;
        }
    }

    /// <summary>重置（用于新订单）。</summary>
    public void Reset()
    {
        ConflictMessage = null;
        LastError = null;
        SetState(PaymentSubmitState.Idle);
    }
}

public enum PaymentSubmitState
{
    Idle,
    Submitting,
    Succeeded,
    Failed,
    Conflicted,
}
