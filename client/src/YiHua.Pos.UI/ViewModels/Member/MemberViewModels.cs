using YiHua.Pos.Domain.Entities;
using YiHua.Pos.Domain.Interfaces;

namespace YiHua.Pos.UI.ViewModels.Member;

// Member 既是本命名空间名、又是实体名，直接用会 CS0118。
// 别名必须写在 namespace 内部才生效（文件层级的 using 会被更近的作用域盖掉）。
using MemberEntity = YiHua.Pos.Domain.Entities.Member;

/// <summary>
/// 会员操作的计算与校验（纯逻辑，可测试）。
///
/// 会员余额是客人的钱，所以：
///   · 每次变动都算清楚「变动后是多少」，交给仓储一次写库
///   · 手工调整必须填原因 —— 事后对账要能说清这 50 块为什么少了
///   · 余额不能变成负数
/// </summary>
public static class MemberCalculator
{
    /// <summary>
    /// 充值：余额增加。
    /// </summary>
    /// <returns>(变动后余额, 流水)</returns>
    public static (decimal BalanceAfter, MemberTransaction Tx) Recharge(
        MemberEntity member, decimal amount, string payTypeName, string operatorName, long operatorId)
    {
        if (amount <= 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), "充值金额必须大于 0");

        var after = Round(member.Balance + amount);
        return (after, new MemberTransaction
        {
            MemberId = member.Id,
            CardNo = member.CardNo,
            MemberName = member.Name,
            Type = MemberTransactionType.Recharge,
            Amount = Round(amount),
            PointsChange = 0,
            BalanceAfter = after,
            PointsAfter = member.Points,
            PayTypeName = payTypeName,
            OperatorName = operatorName,
            OperatorId = operatorId,
        });
    }

    /// <summary>
    /// 余额调整：商家手工把余额改成指定值。
    ///
    /// 为什么不是「加/减多少」而是「改成多少」：
    /// 商家说「他卡里应该是 80」，直接填 80 比让他自己算差额不容易错。
    /// 原因必填，否则事后查不出这笔调整是为什么。
    /// </summary>
    public static (bool Ok, string? Error, decimal BalanceAfter, MemberTransaction? Tx) AdjustBalance(
        MemberEntity member, decimal targetBalance, string reason, string operatorName, long operatorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "余额调整必须填写原因", member.Balance, null);

        if (targetBalance < 0m)
            return (false, "余额不能为负数", member.Balance, null);

        var after = Round(targetBalance);
        var delta = Round(after - member.Balance);

        if (delta == 0m)
            return (false, "调整后余额和当前一样，不用调整", member.Balance, null);

        return (true, null, after, new MemberTransaction
        {
            MemberId = member.Id,
            CardNo = member.CardNo,
            MemberName = member.Name,
            Type = MemberTransactionType.BalanceAdjust,
            Amount = delta,
            PointsChange = 0,
            BalanceAfter = after,
            PointsAfter = member.Points,
            Reason = reason.Trim(),
            OperatorName = operatorName,
            OperatorId = operatorId,
        });
    }

    /// <summary>
    /// 积分调整：同样是「改成多少」+ 原因必填。
    /// </summary>
    public static (bool Ok, string? Error, int PointsAfter, MemberTransaction? Tx) AdjustPoints(
        MemberEntity member, int targetPoints, string reason, string operatorName, long operatorId)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return (false, "积分调整必须填写原因", member.Points, null);

        if (targetPoints < 0)
            return (false, "积分不能为负数", member.Points, null);

        var delta = targetPoints - member.Points;
        if (delta == 0)
            return (false, "调整后积分和当前一样，不用调整", member.Points, null);

        return (true, null, targetPoints, new MemberTransaction
        {
            MemberId = member.Id,
            CardNo = member.CardNo,
            MemberName = member.Name,
            Type = MemberTransactionType.PointAdjust,
            Amount = 0m,
            PointsChange = delta,
            BalanceAfter = member.Balance,
            PointsAfter = targetPoints,
            Reason = reason.Trim(),
            OperatorName = operatorName,
            OperatorId = operatorId,
        });
    }

    /// <summary>
    /// 用余额消费。
    /// 余额不够就拒绝 —— 不允许赊账（赊账是「挂账」那条路，不是会员余额）。
    /// </summary>
    public static (bool Ok, string? Error, decimal BalanceAfter, MemberTransaction? Tx) Consume(
        MemberEntity member, decimal amount, string orderNo, string operatorName, long operatorId)
    {
        if (amount <= 0m)
            return (false, "消费金额必须大于 0", member.Balance, null);

        if (member.Balance < amount)
            return (false, $"余额不足：卡里 ¥{member.Balance:F2}，本单 ¥{amount:F2}", member.Balance, null);

        var after = Round(member.Balance - amount);
        return (true, null, after, new MemberTransaction
        {
            MemberId = member.Id,
            CardNo = member.CardNo,
            MemberName = member.Name,
            Type = MemberTransactionType.Consume,
            Amount = -Round(amount),
            PointsChange = 0,
            BalanceAfter = after,
            PointsAfter = member.Points,
            OrderNo = orderNo,
            OperatorName = operatorName,
            OperatorId = operatorId,
        });
    }

    /// <summary>
    /// 注销退款：把余额退给客人，然后注销。
    /// 有余额没退就注销，等于把客人的钱吞了。
    /// </summary>
    public static (bool Ok, string? Error, decimal BalanceAfter, MemberTransaction? Tx) CancelRefund(
        MemberEntity member, string payTypeName, string operatorName, long operatorId)
    {
        if (member.Balance <= 0m)
            return (true, null, 0m, null);   // 没余额，直接注销

        return (true, null, 0m, new MemberTransaction
        {
            MemberId = member.Id,
            CardNo = member.CardNo,
            MemberName = member.Name,
            Type = MemberTransactionType.CancelRefund,
            Amount = -Round(member.Balance),
            PointsChange = 0,
            BalanceAfter = 0m,
            PointsAfter = member.Points,
            PayTypeName = payTypeName,
            Reason = "注销会员退还余额",
            OperatorName = operatorName,
            OperatorId = operatorId,
        });
    }

    /// <summary>新增会员的资料校验。</summary>
    public static (bool Ok, string? Error) ValidateProfile(
        string name, string cardNo, string phone, bool cardNoTaken)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
            return (false, "卡号不能为空");

        if (cardNoTaken)
            return (false, $"卡号「{cardNo.Trim()}」已经被用了");

        if (string.IsNullOrWhiteSpace(name))
            return (false, "会员姓名不能为空");

        var trimmedPhone = (phone ?? string.Empty).Trim();
        if (trimmedPhone.Length > 0)
        {
            if (trimmedPhone.Length != 11 || !trimmedPhone.All(char.IsDigit))
                return (false, "手机号要填 11 位数字，不填也可以");
        }

        return (true, null);
    }

    /// <summary>金额按分取整。</summary>
    private static decimal Round(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>会员列表项（界面用）。</summary>
public class MemberRowDto
{
    public long Id { get; set; }
    public string CardNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MaskedPhone { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public int Points { get; set; }
    public bool IsActive { get; set; } = true;

    public string StatusText => IsActive ? "正常" : "已注销";
}

/// <summary>
/// 会员列表 ViewModel。
/// </summary>
public class MemberListViewModel
{
    private readonly IMemberRepository _repo;

    public MemberListViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public List<MemberRowDto> Members { get; private set; } = new();
    public string Keyword { get; set; } = string.Empty;
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    public int ActiveCount => Members.Count(m => m.IsActive);

    public decimal TotalBalance => Members.Where(m => m.IsActive).Sum(m => m.Balance);

    public bool IsEmpty => !IsLoading && ErrorMessage == null && Members.Count == 0;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var list = await _repo.SearchAsync(Keyword, 300, ct);
            Members = list.Select(ToRow).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载会员失败：{ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public static MemberRowDto ToRow(MemberEntity m) => new()
    {
        Id = m.Id,
        CardNo = m.CardNo,
        Name = m.Name,
        MaskedPhone = m.MaskedPhone,
        Level = m.Level,
        Balance = m.Balance,
        Points = m.Points,
        IsActive = m.IsActive,
    };
}