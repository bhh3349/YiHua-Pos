#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
逸华餐饮收银 —— 回归护栏检查

把验收中真实发现过的 bug 变成自动检查，避免同类问题再次发生。
CI 会自动跑这个脚本。

退出码 0 = 通过，1 = 有问题
"""
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

ISSUES = []
PASSES = []


def check(name, ok, detail=""):
    if ok:
        PASSES.append(name)
        print("  [通过] %s" % name)
    else:
        ISSUES.append((name, detail))
        print("  [失败] %s" % name)
        if detail:
            for line in detail.splitlines():
                print("         %s" % line)


def read(path):
    p = os.path.join(ROOT, path)
    if not os.path.exists(p):
        return None
    with open(p, encoding="utf-8", errors="replace") as f:
        return f.read()


def walk(exts, subdir=None):
    """遍历文件，返回 [(相对路径, 内容)]"""
    base = os.path.join(ROOT, subdir) if subdir else ROOT
    out = []
    for dp, dns, fn in os.walk(base):
        dns[:] = [d for d in dns if d not in (".git", "obj", "bin", "__pycache__", "node_modules")]
        for f in fn:
            if f.endswith(exts):
                p = os.path.join(dp, f)
                rel = os.path.relpath(p, ROOT).replace("\\", "/")
                out.append((rel, read(rel) or ""))
    return out


# ============================================================================
#  护栏 1：预览文本的行尾 CR
#  来源：T-107 验收 —— AppendLine 在 Windows 写 CRLF，
#        测试用 Split('\n') 切分后 CR 留在行尾，被 DisplayWidth 多算一列
# ============================================================================
print("\n=== 护栏 1：预览文本切分方式 ===")
ESCAPED_CRLF = "\\" + "r" + "\\" + "n"   # 源码里写的是 \r\n（四个字符）
cs_files = walk((".cs",), "client")
bad_split = []
for rel, content in cs_files:
    if "RenderPreview" not in content:
        continue
    # 找出 RenderPreview 调用附近的 Split
    for m in re.finditer(r"RenderPreview[\s\S]{0,200}?\.Split\(", content):
        snippet = content[m.start():m.start() + 260]
        seg = snippet.split(".Split(")[1].split(")")[0]
        # 只切 \n 而没切 \r  ->  行尾 CR 会被算成一列
        has_lf = "\\n" in seg
        has_crlf = ESCAPED_CRLF in seg
        if has_lf and not has_crlf:
            line_no = content[:m.start()].count("\n") + 1
            bad_split.append("%s:%d  Split(%s)" % (rel, line_no, seg[:40]))

check(
    "RenderPreview 结果必须按 CRLF 切分",
    not bad_split,
    "\n".join(bad_split) if bad_split else "",
)

# 契约文档：必须说明行分隔符（CRLF 的字面量或转义形式 \r\n）
renderer = read("client/src/YiHua.Pos.Hardware/Printing/Templates/TemplateRenderer.cs")
ESCAPED_CRLF = "\\" + "r" + "\\" + "n"   # 源码里的 \r\n 是反斜杠+r+反斜杠+n 四个字符
has_contract = bool(renderer) and ("CRLF" in renderer or ESCAPED_CRLF in renderer)
check(
    "RenderPreview 有行分隔符契约说明",
    has_contract,
    "TemplateRenderer.RenderPreview 缺少契约注释",
)


# ============================================================================
#  护栏 2：取整规则必须显式
#  来源：T-303/T-307 要求 —— 收银不能用银行家舍入，
#        但 MidpointRounding 默认是 ToEven（银行家舍入）
# ============================================================================
print("\n=== 护栏 2：金额取整规则 ===")

# 只检查「金额相关」的文件 —— 图像缩放、重量换算不涉及金额舍入
MONEY_HINTS = ("Amount", "Price", "Money", "Total", "Payable", "Discount",
               "Change", "Received", "Paid", "Cash", "QTY", "Qty")

money_targets = [
    (rel, c) for rel, c in cs_files
    if any(h in os.path.basename(rel) or h in rel for h in MONEY_HINTS)
]
ROUND_FUNCS = ("decimal.Round", "Math.Round")

missing_mode = []
for rel, content in money_targets:
    for i, line in enumerate(content.splitlines(), 1):
        s = line.strip()
        if s.startswith("//") or s.startswith("*"):
            continue
        for fn in ROUND_FUNCS:
            if fn not in s or "MidpointRounding" in s:
                continue
            # 排除：方法定义、比较、纯转换中间变量
            if re.search(r"(public|private|protected|internal|static).*\b(Round)\s*\(", s):
                continue
            if "=>" in s and "Round" in s and "decimal.Round" not in s:
                # 表达式体里的重量换算等，跳过
                if any(k in rel for k in ("Scale", "Image", "Protocol")):
                    continue
            missing_mode.append("%s:%d  %s" % (rel, i, s[:70]))

check(
    "金额计算的 Round 必须显式传 MidpointRounding",
    not missing_mode,
    "\n".join(missing_mode[:10]) if missing_mode else "",
)

# 至少要有一处显式用 AwayFromZero（收银惯例）
has_away = any("MidpointRounding.AwayFromZero" in c for _, c in cs_files)
check(
    "至少一处显式使用 MidpointRounding.AwayFromZero（收银惯例）",
    has_away,
    "未找到 AwayFromZero —— .NET 默认是 ToEven（银行家舍入），收银不能用",
)


# ============================================================================
#  护栏 3：提交操作必须防重复
#  来源：订单写入走 clientOrderNo 幂等键（架构约束）
# ============================================================================
print("\n=== 护栏 3：幂等键 ===")
server_py = walk((".py",), "server")
server_text = "\n".join(c for _, c in server_py)

check(
    "服务端有 clientOrderNo 幂等键",
    "client_order_no" in server_text or "clientOrderNo" in server_text,
)

# 复合唯一约束（跨门店可各建一单）
models = read("server/app/models.py") or ""
check(
    "幂等键按门店隔离（store_id + client_order_no）",
    "store_id" in models and "client_order_no" in models,
    "models.py 缺少 (store_id, client_order_no) 复合唯一约束",
)

outbox = [c for rel, c in cs_files if "Outbox" in rel]
check(
    "客户端有 outbox 队列（离线写入）",
    bool(outbox),
    "找不到 Outbox 相关实现",
)

# 冒烟测试必须可重复运行：写了持久化数据就要还原
# 来源：验收实测 —— smoke_test.py 用 strategy/save 改策略名但没还原，
#        第 1 次跑 157 全过，第 2 次必失败（测试污染自己）
smoke = read("server/smoke_test.py")
check(
    "冒烟测试写持久化数据后有还原",
    bool(smoke) and ("还原" in smoke or "恢复默认" in smoke),
    "smoke_test.py 有写库操作但没还原 —— 第二次运行必然失败",
)


# ============================================================================
#  护栏 4：测试路径约定
#  来源：Wave 1 出过 MUSE-1 和 MUSE-3 用根 tests/，MUSE-2 用 client/tests/
# ============================================================================
print("\n=== 护栏 4：测试路径约定 ===")
wrong = []
for dp, dns, fn in os.walk(ROOT):
    dns[:] = [d for d in dns if d not in (".git", "obj", "bin", "__pycache__", "node_modules")]
    for f in fn:
        if not f.endswith((".cs", ".csproj")):
            continue
        p = os.path.join(dp, f)
        rel = os.path.relpath(p, ROOT).replace("\\", "/")
        # 根级 tests/ 目录（client/tests/ 是对的）
        if re.match(r"^tests/", rel):
            wrong.append(rel)
        if rel.startswith("tests/") or (rel.startswith("client/tests/") and "YiHua.Pos.Tests" not in rel and "/" not in rel[len("client/tests/"):]):
            pass

check(
    "C# 测试必须在 client/tests/YiHua.Pos.Tests/ 下",
    not wrong,
    "\n".join(wrong[:10]) if wrong else "",
)

# 不应同时存在两个测试项目
proj_dir = "client/tests/YiHua.Pos.Tests"
check("存在 %s" % proj_dir, os.path.isdir(os.path.join(ROOT, proj_dir)))


# ============================================================================
#  护栏 5：gitignore 不能误伤源文件
#  来源：T-501 验收 —— [Bb]in/ 把 overlay/usr/local/bin/ 也排除了
# ============================================================================
print("\n=== 护栏 5：gitignore 精确性 ===")
gi = read(".gitignore") or ""
check(
    "构建产物规则已限定到 client/ 下（不能是裸 [Bb]in/）",
    not re.search(r"^\[Bb\]in/", gi, re.M),
    ".gitignore 里仍有裸的 [Bb]in/ 规则，会误伤 overlay/usr/local/bin/",
)
check(
    "有 shell 脚本 LF 换行规则（.gitattributes）",
    "*.sh text eol=lf" in (read(".gitattributes") or ""),
)


# ============================================================================
#  护栏 6：UI 反 slop（关键 3 项）
#  来源：ui-craft-pro 工艺标准
# ============================================================================
print("\n=== 护栏 6：UI 反 slop ===")
# Themes/Tokens.axaml 是 token 定义文件本身，颜色必须写在那里，豁免
axaml = [(rel, c) for rel, c in walk((".axaml",), "client/src/YiHua.Pos.UI")
         if "/Themes/" not in rel]

emoji_ranges = [(0x1F300, 0x1FAFF), (0x2600, 0x27BF), (0x2B00, 0x2BFF),
                (0x1F000, 0x1F0FF), (0x1F900, 0x1F9FF), (0x2705, 0x2705),
                (0x274C, 0x274C), (0x2B50, 0x2B50)]


def has_emoji(s):
    for ch in s:
        cp = ord(ch)
        for lo, hi in emoji_ranges:
            if lo <= cp <= hi:
                return True
    return False


emo = []
for rel, content in axaml:
    for i, line in enumerate(content.splitlines(), 1):
        if has_emoji(line):
            emo.append("%s:%d" % (rel, i))
check("UI 无 emoji 图标（Themes 除外）", not emo, "\n".join(emo[:5]))

hardcoded = []
for rel, content in axaml:
    for i, line in enumerate(content.splitlines(), 1):
        for m in re.finditer(r'(?:Background|Foreground|BorderBrush)="#([0-9A-Fa-f]{6})"', line):
            hardcoded.append("%s:%d  #%s" % (rel, i, m.group(1)))
check(
    "UI 控件无硬编码颜色（Themes/ 是 token 定义，豁免）",
    not hardcoded,
    "\n".join(hardcoded[:5]),
)

animated = []
for rel, content in axaml:
    for i, line in enumerate(content.splitlines(), 1):
        if re.search(r'Transitions=|Transition ', line) and "0s" not in line:
            animated.append("%s:%d  %s" % (rel, i, line.strip()[:50]))
check("高频控件无过渡动画", not animated, "\n".join(animated[:5]))


# ============================================================================
#  护栏 7：第三方依赖白名单
# ============================================================================
print("\n=== 护栏 7：依赖白名单 ===")
ALLOWED_PKGS = {
    "avalonia", "avalonia.controls.itemsrepeater", "avalonia.diagnostics",
    "avalonia.themes.fluent", "prism.avalonia", "prism.dryioc.avalonia",
    "prism.core", "microsoft.extensions", "system.io.ports",
    "sqlsugarcore", "microsoft.data.sqlite", "sqlitepclraw",
    "newtonsoft.json", "system.text.json",
    "xunit", "xunit.runner.visualstudio", "fluentassertions",
    "microsoft.net.test.sdk", "coverlet.collector",
}
new_pkg = []
for rel, content in walk((".csproj",), "client"):
    for m in re.finditer(r'PackageReference\s+Include="([^"]+)"', content):
        name = m.group(1).lower()
        if not any(name.startswith(a) for a in ALLOWED_PKGS):
            new_pkg.append("%s  %s" % (rel, m.group(1)))
check("客户端无未授权第三方包", not new_pkg, "\n".join(new_pkg))


# ============================================================================
#  汇总
# ============================================================================
print("\n" + "=" * 60)
print("通过 %d 项，失败 %d 项" % (len(PASSES), len(ISSUES)))
if ISSUES:
    print("\n失败项：")
    for name, _ in ISSUES:
        print("  - %s" % name)
    sys.exit(1)
print("全部通过")
sys.exit(0)