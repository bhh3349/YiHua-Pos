using YiHua.Pos.UI.Services.Member;

namespace YiHua.Pos.UI.ViewModels.Member;

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

    public List<MemberDto> Members { get; private set; } = new();
    public string SearchKeyword { get; set; } = string.Empty;
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }

    /// <summary>脱敏手机号（右对齐显示）。</summary>
    public static string MaskPhone(string phone) => MemberPhoneMasker.Mask(phone);

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Members = string.IsNullOrWhiteSpace(SearchKeyword)
                ? await _repo.SearchAsync("", ct)
                : await _repo.SearchAsync(SearchKeyword, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"加载失败：{ex.Message}";
        }
        finally { IsLoading = false; }
    }
}

/// <summary>
/// 会员详情 ViewModel。
/// </summary>
public class MemberDetailViewModel
{
    private readonly IMemberRepository _repo;

    public MemberDetailViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public MemberDto? Member { get; private set; }
    public List<BalanceRecordDto> Records { get; private set; } = new();
    public bool IsLoading { get; private set; }

    public string MaskedPhone => Member != null ? MemberPhoneMasker.Mask(Member.Phone) : "";

    public async Task LoadAsync(long memberId, CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            Member = await _repo.GetByIdAsync(memberId, ct);
            if (Member != null)
                Records = await _repo.GetBalanceRecordsAsync(memberId, ct);
        }
        finally { IsLoading = false; }
    }

    public async Task SetLostAsync(bool lost, CancellationToken ct = default)
    {
        if (Member == null) return;
        Member.IsLost = lost;
        await _repo.UpdateAsync(Member, ct);
    }

    public async Task SetDisabledAsync(bool disabled, CancellationToken ct = default)
    {
        if (Member == null) return;
        // 停用 ≠ 删除：只打标记，历史记录保留
        Member.IsDisabled = disabled;
        await _repo.UpdateAsync(Member, ct);
    }
}

/// <summary>
/// 开卡 ViewModel。
/// </summary>
public class MemberCreateViewModel
{
    private readonly IMemberRepository _repo;

    public MemberCreateViewModel(IMemberRepository repo)
    {
        _repo = repo;
    }

    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? ErrorMessage { get; private set; }
    public bool IsSaving { get; private set; }

    public async Task<MemberDto?> CreateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = "姓名不能为空";
            return null;
        }
        if (!MemberPhoneMasker.IsValid(Phone, out var phoneError))
        {
            ErrorMessage = phoneError;
            return null;
        }

        // 手机号唯一
        var existing = await _repo.GetByPhoneAsync(Phone.Trim(), ct);
        if (existing != null)
        {
            ErrorMessage = $"手机号 {MemberPhoneMasker.Mask(Phone)} 已开过卡，不能重复开卡";
            return null;
        }

        IsSaving = true;
        try
        {
            var member = new MemberDto
            {
                Name = Name.Trim(),
                Phone = Phone.Trim(),
            };
            return await _repo.CreateAsync(member, ct);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"开卡失败：{ex.Message}";
            return null;
        }
        finally { IsSaving = false; }
    }
}
