using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace YiHua.Pos.UI.Views.Member;

/// <summary>
/// 会员资料弹窗（新增 / 编辑）。
///
/// 用命名控件手工读写而不是绑定：卡号要能「自动生成」，
/// 生日是可空的 CalendarDatePicker，绑到普通 POCO 上反而更绕。
/// </summary>
public partial class MemberEditDialog : Window
{
    public MemberEditDialog() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>确认保存。</summary>
    public event EventHandler<MemberProfileInput>? Confirmed;

    /// <summary>点「自动生成」时向 App 层要一个新卡号。</summary>
    public event EventHandler? GenerateCardNoRequested;

    /// <summary>可选等级。</summary>
    public IReadOnlyList<string> Levels { get; set; } = new[] { "普通", "银卡", "金卡", "钻石卡" };

    /// <summary>载入已有会员（新增时传 null）。</summary>
    public void LoadMember(string? cardNo, string name, string phone,
        string level, DateTime? birthday, string remark, bool isNew)
    {
        this.FindControl<TextBlock>("TitleText")!.Text = isNew ? "新增会员" : "编辑会员资料";
        this.FindControl<TextBox>("CardNoBox")!.Text = cardNo ?? string.Empty;
        this.FindControl<TextBox>("NameBox")!.Text = name;
        this.FindControl<TextBox>("PhoneBox")!.Text = phone;
        this.FindControl<TextBox>("RemarkBox")!.Text = remark;
        this.FindControl<CalendarDatePicker>("BirthdayPicker")!.SelectedDate = birthday;

        var combo = this.FindControl<ComboBox>("LevelBox")!;
        combo.ItemsSource = Levels;
        combo.Text = string.IsNullOrWhiteSpace(level) ? "普通" : level;
    }

    /// <summary>App 层生成卡号后回填。</summary>
    public void SetCardNo(string cardNo)
        => this.FindControl<TextBox>("CardNoBox")!.Text = cardNo;

    public void SetError(string? message)
        => this.FindControl<TextBlock>("ErrorText")!.Text = message ?? string.Empty;

    private void OnGenerateCardNo(object? sender, RoutedEventArgs e)
        => GenerateCardNoRequested?.Invoke(this, EventArgs.Empty);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnConfirm(object? sender, RoutedEventArgs e)
    {
        Confirmed?.Invoke(this, new MemberProfileInput
        {
            CardNo = this.FindControl<TextBox>("CardNoBox")!.Text?.Trim() ?? string.Empty,
            Name = this.FindControl<TextBox>("NameBox")!.Text?.Trim() ?? string.Empty,
            Phone = this.FindControl<TextBox>("PhoneBox")!.Text?.Trim() ?? string.Empty,
            Level = this.FindControl<ComboBox>("LevelBox")!.Text?.Trim() ?? "普通",
            Birthday = this.FindControl<CalendarDatePicker>("BirthdayPicker")!.SelectedDate,
            Remark = this.FindControl<TextBox>("RemarkBox")!.Text?.Trim() ?? string.Empty,
        });
    }
}

/// <summary>会员资料输入。</summary>
public class MemberProfileInput
{
    public string CardNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Level { get; set; } = "普通";
    public DateTime? Birthday { get; set; }
    public string Remark { get; set; } = string.Empty;
}