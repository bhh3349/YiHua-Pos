using Xunit;

namespace YiHua.Pos.Tests.UI.SoldOutTests;

/// <summary>
/// AST 级检查：业务代码不得出现平台硬编码分支。
/// 与 T-405 同一条铁律：if platform == "meituan" 之类的写法禁止出现。
/// 新增平台应该加 ISoldOutSync 实现类，而不是在业务代码里加分支。
/// </summary>
public class NoPlatformBranchTests
{
    private static string ServicesDir
    {
        get
        {
            // 测试 DLL 在 client/tests/YiHua.Pos.Tests/bin/Debug/<tf>/ 下，
            // 向上 5 层到 client/，再进 src/YiHua.Pos.UI/Services/SoldOut
            var asmDir = Path.GetDirectoryName(
                typeof(NoPlatformBranchTests).Assembly.Location) ?? AppContext.BaseDirectory;
            var clientDir = Path.GetFullPath(Path.Combine(asmDir,
                "..", "..", "..", "..", ".."));
            var dir = Path.Combine(clientDir, "src", "YiHua.Pos.UI", "Services", "SoldOut");
            return dir;
        }
    }

    [Fact]
    public void Business_Code_Has_No_Platform_Branch()
    {
        var dir = ServicesDir;
        Assert.True(Directory.Exists(dir), $"目录不存在：{dir}");

        var violations = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.cs"))
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            // 禁止的模式：平台字符串字面量比较
            string[] banned = { "\"meituan\"", "\"eleme\"", "\"douyin\"", "'meituan'" };
            foreach (var pattern in banned)
            {
                // SkeletonSoldOutSync 的构造函数参数是允许的（那是注册，不是分支）
                // 但业务逻辑文件（Service/Calculator/Scheduler）中不允许出现
                if (fileName is "SoldOutService.cs" or "SoldOutCalculator.cs" or "SoldOutScheduler.cs")
                {
                    if (content.Contains(pattern))
                        violations.Add($"{fileName} 包含平台字面量 {pattern}");
                }
            }

            // 禁止的模式：platform == "..." 比较
            if (fileName is "SoldOutService.cs" or "SoldOutCalculator.cs" or "SoldOutScheduler.cs")
            {
                foreach (var line in content.Split('\n'))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("//")) continue;
                    if (System.Text.RegularExpressions.Regex.IsMatch(
                        trimmed, @"platform\s*==|PlatformId\s*==\s*"""))
                    {
                        violations.Add($"{fileName}: {trimmed}");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void Sync_Interface_Exists_For_Extension()
    {
        // 架构保证：新增平台只需实现 ISoldOutSync，无需改业务代码
        var interfaceType = typeof(YiHua.Pos.UI.Services.SoldOut.ISoldOutSync);
        Assert.True(interfaceType.IsInterface);
        Assert.NotNull(interfaceType.GetMethod("PushSoldOutAsync"));
        Assert.NotNull(interfaceType.GetMethod("PushRestoreAsync"));
    }
}
