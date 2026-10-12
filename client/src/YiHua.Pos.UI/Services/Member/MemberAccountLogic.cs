namespace YiHua.Pos.UI.Services.Member;

/// <summary>
/// 手机号脱敏（纯逻辑）。
/// </summary>
public static class MemberPhoneMasker
{
    /// <summary>
    /// 13812345678 → 138****5678。
    /// </summary>
    public static string Mask(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return string.Empty;
        var p = phone.Trim();
        if (p.Length < 7)
            return p; // 太短不脱敏，直接返回
        // 保留前3 + 后4
        return p.Substring(0, 3) + "****" + p.Substring(p.Length - 4);
    }

    /// <summary>
    /// 校验手机号格式（简单版：11位数字，1开头）。
    /// </summary>
    public static bool IsValid(string? phone, out string? error)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            error = "手机号不能为空，开卡需要实名";
            return false;
        }
        var p = phone.Trim();
        if (p.Length != 11 || !p.StartsWith("1") || !p.All(char.IsDigit))
        {
            error = "手机号格式不对，应为 11 位数字（如 13812345678）";
            return false;
        }
        error = null;
        return true;
    }
}

/// <summary>
/// 充值校验（纯逻辑，合规重点）。
/// 法规第十八条：单张记名卡限额 ≤ 5000 元。
/// </summary>
public static class RechargeValidator
{
    /// <summary>
    /// 法规默认上限：5000 元。
    /// </summary>
    public const decimal DefaultUpperLimit = 5000m;

    /// <summary>
    /// 校验充值金额。
    /// </summary>
    /// <param name="amount">充值金额</param>
    /// <param name="upperLimit">上限（可配置，但不能高于 5000）</param>
    /// <param name="currentBalance">当前余额</param>
    public static bool Validate(decimal amount, decimal upperLimit, decimal currentBalance, out string? error)
    {
        if (amount <= 0)
        {
            error = "充值金额必须大于 0";
            return false;
        }
        // 上限不能高于法规的 5000
        var effectiveLimit = Math.Min(upperLimit, DefaultUpperLimit);
        if (amount > effectiveLimit)
        {
            error = $"单次充值不能超过 ¥{effectiveLimit:F0}（依据《单用途商业预付卡管理办法》第十八条，单张记名卡限额 5000 元）";
            return false;
        }
        if (currentBalance + amount > DefaultUpperLimit)
        {
            error = $"充值后余额 ¥{currentBalance + amount:F2} 将超过单卡上限 ¥{DefaultUpperLimit:F0}，请减少充值金额";
            return false;
        }
        error = null;
        return true;
    }

    /// <summary>
    /// 校验上限配置：不能高于 5000（地方可能更严，可以调低）。
    /// </summary>
    public static bool ValidateUpperLimit(decimal limit, out string? error)
    {
        if (limit > DefaultUpperLimit)
        {
            error = $"上限不能高于 ¥{DefaultUpperLimit:F0}（法规要求）";
            return false;
        }
        if (limit <= 0)
        {
            error = "上限必须大于 0";
            return false;
        }
        error = null;
        return true;
    }
}

/// <summary>
/// 余额调整结果。
/// </summary>
public class BalanceAdjustResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public decimal NewBalance { get; set; }
    public BalanceRecordDto? Record { get; set; }
}

/// <summary>
/// 余额调整（纯逻辑）。
/// 可加可减，必须填原因（选择列表），调整后不能为负，每次记流水。
/// </summary>
public static class BalanceAdjuster
{
    /// <summary>
    /// 调整余额。
    /// </summary>
    /// <param name="member">会员</param>
    /// <param name="delta">变动金额（正=加，负=减）</param>
    /// <param name="reason">原因（必填，选择列表）</param>
    /// <param name="operatorName">操作人</param>
    public static BalanceAdjustResult Adjust(
        MemberDto member,
        decimal delta,
        BalanceChangeReason? reason,
        string operatorName)
    {
        if (delta == 0)
            return Fail("调整金额不能为 0");
        if (reason == null)
            return Fail("必须选择调整原因（赠送/补偿/纠错/其他）");
        if (string.IsNullOrWhiteSpace(operatorName))
            return Fail("操作人不能为空");

        var newBalance = Round(member.Balance + delta);
        if (newBalance < 0)
            return Fail($"调整后余额 ¥{newBalance:F2} 不能为负（当前 ¥{member.Balance:F2}）");

        return new BalanceAdjustResult
        {
            Success = true,
            NewBalance = newBalance,
            Record = new BalanceRecordDto
            {
                MemberId = member.Id,
                Amount = delta,
                BalanceAfter = newBalance,
                Reason = reason.Value,
                ReasonText = GetReasonText(reason.Value),
                OperatorName = operatorName,
            },
        };
    }

    private static BalanceAdjustResult Fail(string error)
        => new() { Success = false, Error = error };

    public static string GetReasonText(BalanceChangeReason reason) => reason switch
    {
        BalanceChangeReason.Recharge => "充值",
        BalanceChangeReason.Consume => "消费",
        BalanceChangeReason.Gift => "赠送",
        BalanceChangeReason.Compensation => "补偿",
        BalanceChangeReason.Correction => "纠错",
        BalanceChangeReason.Refund => "退卡",
        BalanceChangeReason.Other => "其他",
        _ => "未知",
    };

    private static decimal Round(decimal value)
        => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
