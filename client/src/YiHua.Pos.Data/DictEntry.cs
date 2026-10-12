namespace YiHua.Pos.Data;

/// <summary>
/// 通用键值配置的内部存储实体（对应 <c>IDictionaryRepository</c>，T-003）。
///
/// Domain 层没有为键值配置定义实体（冻结），因此该表由 Data 层内部维护，
/// 不对外暴露。Key 建唯一索引。
/// </summary>
internal class DictEntry
{
    public long Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
