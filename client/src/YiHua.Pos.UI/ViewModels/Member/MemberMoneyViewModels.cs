using YiHua.Pos.UI.Services.Member;

namespace YiHua.Pos.UI.ViewModels.Member;

/// <summary>
/// 余额调整 ViewModel（高频功能）。
/// </summary>
public class BalanceAdjustViewModel
{
    private readonly IMemberRepository _repo;

    public BalanceAdjustViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public MemberDto? Member { get; set; }
    public decimal Delta { get; set; }
    public BalanceChangeReason? Reason { get; set; }
    public string OperatorName { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }
    public bool IsSaving { get; private set; }

    /// <summary>原因选项（选择列表）。</summary>
    public static List<BalanceChangeReason> ReasonOptions { get; } = new()
    {
        BalanceChangeReason.Gift,
        BalanceChangeReason.Compensation,
        BalanceChangeReason.Correction,
        BalanceChangeReason.Other,
    };

    public async Task<bool> SubmitAsync(CancellationToken ct = default)
    {
        if (Member == null)
        {
            ErrorMessage = "请选择会员";
            return false;
        }

        var result = BalanceAdjuster.Adjust(Member, Delta, Reason, OperatorName);
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return false;
        }

        IsSaving = true;
        try
        {
            Member.Balance = result.NewBalance;
            await _repo.UpdateAsync(Member, ct);
            await _repo.AddBalanceRecordAsync(result.Record!, ct);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"保存失败：{ex.Message}";
            return false;
        }
        finally { IsSaving = false; }
    }
}

/// <summary>
/// 充值 ViewModel（含合规提示）。
/// </summary>
public class RechargeViewModel
{
    private readonly IMemberRepository _repo;

    public RechargeViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public MemberDto? Member { get; set; }
    public decimal Amount { get; set; }
    public decimal UpperLimit { get; set; } = RechargeValidator.DefaultUpperLimit;
    public string OperatorName { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }

    /// <summary>合规提示。</summary>
    public string ComplianceTip =>
        $"单张记名卡余额上限 ¥{RechargeValidator.DefaultUpperLimit:F0}，" +
        "依据《单用途商业预付卡管理办法》第十八条";

    public async Task<bool> SubmitAsync(CancellationToken ct = default)
    {
        if (Member == null)
        {
            ErrorMessage = "请选择会员";
            return false;
        }
        if (!RechargeValidator.Validate(Amount, UpperLimit, Member.Balance, out var error))
        {
            ErrorMessage = error;
            return false;
        }

        try
        {
            Member.Balance = Math.Round(Member.Balance + Amount, 2, MidpointRounding.AwayFromZero);
            await _repo.UpdateAsync(Member, ct);
            await _repo.AddBalanceRecordAsync(new BalanceRecordDto
            {
                MemberId = Member.Id,
                Amount = Amount,
                BalanceAfter = Member.Balance,
                Reason = BalanceChangeReason.Recharge,
                ReasonText = "充值",
                OperatorName = OperatorName,
            }, ct);
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"充值失败：{ex.Message}";
            return false;
        }
    }
}

/// <summary>
/// 退卡 ViewModel。
/// </summary>
public class MemberRefundViewModel
{
    private readonly IMemberRepository _repo;

    public MemberRefundViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public MemberDto? Member { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IdCardNo { get; set; } = string.Empty;
    public string RefundMethod { get; set; } = "现金";
    public string OperatorName { get; set; } = string.Empty;

    public string? ErrorMessage { get; private set; }
    public bool NeedConfirm { get; private set; }

    /// <summary>第一步：校验并请求确认。</summary>
    public bool RequestConfirm()
    {
        if (Member == null)
        {
            ErrorMessage = "请选择会员";
            return false;
        }
        var result = MemberRefundCalculator.Refund(Member, BuildRequest());
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return false;
        }
        NeedConfirm = true;
        ErrorMessage = null;
        return true;
    }

    /// <summary>第二步：确认后执行。</summary>
    public async Task<bool> ConfirmAsync(CancellationToken ct = default)
    {
        if (Member == null || !NeedConfirm) return false;
        var result = MemberRefundCalculator.Refund(Member, BuildRequest());
        if (!result.Success)
        {
            ErrorMessage = result.Error;
            return false;
        }
        try
        {
            Member.Balance = 0; // 清零
            await _repo.UpdateAsync(Member, ct);
            await _repo.AddBalanceRecordAsync(result.Record!, ct);
            NeedConfirm = false;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"退卡失败：{ex.Message}";
            return false;
        }
    }

    private MemberRefundRequest BuildRequest() => new()
    {
        Name = Name.Trim(),
        IdCardNo = IdCardNo.Trim(),
        RefundMethod = RefundMethod,
        OperatorName = OperatorName,
    };
}

/// <summary>
/// 券管理 ViewModel。
/// </summary>
public class CouponManageViewModel
{
    private readonly ICouponRepository _repo;

    public CouponManageViewModel(ICouponRepository repo)
    {
        _repo = repo;
    }

    public List<CouponDto> Coupons { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public async Task LoadByMemberAsync(long memberId, CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            Coupons = await _repo.GetByMemberAsync(memberId, ct);
        }
        finally { IsLoading = false; }
    }
}
