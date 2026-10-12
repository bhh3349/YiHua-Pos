#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
多 AI 协作 —— 分支集成验证工具

用法：
    python integrate.py                    # 列出所有待验证分支
    python integrate.py task/T-102-escpos  # 验证指定分支
    python integrate.py --merge task/T-102-escpos   # 验证通过后合并到 main

验证内容：
    1. 越界检查 —— 有没有改别人的目录 / 冻结文件
    2. 全量编译 —— dotnet build（net8.0 + net6.0）
    3. 全量测试 —— dotnet test
    4. 代码规范 —— 硬编码路径 / 明文密钥 / TODO
    5. 依赖检查 —— 有没有引入未经批准的包
"""
import os
import re
import subprocess
import sys

REPO = r"C:\WorkSpace\purge\yihua-pos"
DOTNET = r"C:\WorkSpace\purge\work\dotnet\dotnet.exe"

# 任务 → 允许修改的路径前缀
SCOPE = {
    "T-102": ["client/src/YiHua.Pos.Hardware/Printing/EscPos/",
              "client/tests/YiHua.Pos.Tests/Hardware/EscPos/"],
    "T-103": ["client/src/YiHua.Pos.Hardware/Platforms/Linux/",
              "client/tests/YiHua.Pos.Tests/Hardware/Linux/"],
    "T-104": ["client/src/YiHua.Pos.Hardware/Platforms/Windows/",
              "client/tests/YiHua.Pos.Tests/Hardware/Windows/"],
    "T-105": ["client/src/YiHua.Pos.Hardware/Scales/",
              "client/tests/YiHua.Pos.Tests/Hardware/Scales/"],
    "T-106": ["client/src/YiHua.Pos.Hardware/Devices/",
              "client/tests/YiHua.Pos.Tests/Hardware/Devices/"],
    "T-107": ["client/src/YiHua.Pos.Hardware/Printing/Templates/",
              "client/tests/YiHua.Pos.Tests/Hardware/Templates/"],
    "T-003": ["client/src/YiHua.Pos.Data/",
              "client/tests/YiHua.Pos.Tests/Data/"],
    "T-005": ["client/src/YiHua.Pos.UI/Controls/",
              "client/tests/YiHua.Pos.Tests/UI/"],
    "T-201": ["server/"],
    "T-202": ["server/"],
    "T-203": ["server/"],
    "T-301": ["client/src/YiHua.Pos.Modules.Login/"],
    "T-302": ["client/src/YiHua.Pos.Modules.Product/"],
    "T-303": ["client/src/YiHua.Pos.Modules.OrderMeal/"],
    "T-304": ["client/src/YiHua.Pos.Modules.OrderMeal.Fast/"],
    "T-306": ["client/src/YiHua.Pos.Modules.Desk/"],
    "T-307": ["client/src/YiHua.Pos.Modules.Pay/"],
    "T-308": ["client/src/YiHua.Pos.Modules.Order/"],
    "T-309": ["client/src/YiHua.Pos.Modules.Shift/"],
    "T-310": ["client/src/YiHua.Pos.Modules.Report/"],
    "T-311": ["client/src/YiHua.Pos.Modules.Settings/"],
    "T-401": ["client/src/YiHua.Pos.Services/Sync/"],
}

# 冻结文件（任何任务都不许改）
FROZEN = [
    "client/YiHua.Pos.sln",
    "client/Directory.Build.props",
    "client/src/YiHua.Pos.Domain/",
    "client/src/YiHua.Pos.Hardware/Abstractions/",
    "client/src/YiHua.Pos.UI/Themes/Tokens.axaml",
    "client/src/YiHua.Pos.App/App.axaml.cs",
    "deploy/",
    "docs/",
    "README.md",
]

# 允许的第三方依赖白名单
ALLOWED_PKGS = {
    "Avalonia", "Avalonia.Desktop", "Avalonia.Themes.Fluent", "Avalonia.Fonts.Inter",
    "Avalonia.Diagnostics", "Prism.Avalonia", "Prism.DryIoc.Avalonia",
    "System.IO.Ports", "SqlSugarCore", "Newtonsoft.Json",
    "xunit", "xunit.runner.visualstudio", "Microsoft.NET.Test.Sdk",
    "coverlet.collector", "FluentAssertions",
    "fastapi", "uvicorn", "sqlalchemy", "pydantic", "python-jose", "passlib",
    "bcrypt", "python-multipart", "pymysql", "cryptography", "httpx", "pytest",
}


def run(cmd, cwd=REPO, timeout=1800):
    r = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True,
                       encoding="utf-8", errors="replace", timeout=timeout, shell=isinstance(cmd, str))
    return r.returncode, (r.stdout or "") + (r.stderr or "")


def task_of(branch):
    m = re.search(r"T-(\d+)", branch)
    return ("T-" + m.group(1)) if m else None


def check_scope(branch):
    """越界检查"""
    code, out = run(["git", "diff", "--name-only", "origin/main...%s" % branch])
    if code != 0:
        return None, "无法获取 diff: %s" % out[:200]
    files = [f.strip() for f in out.splitlines() if f.strip()]

    t = task_of(branch)
    allowed = SCOPE.get(t)
    problems = []

    for f in files:
        # 冻结文件
        if any(f.startswith(x) for x in FROZEN):
            problems.append(("❌ 冻结文件", f))
            continue
        # 越界
        if allowed and not any(f.startswith(x) for x in allowed):
            problems.append(("⚠️ 越界", f))
        elif not allowed:
            problems.append(("? 未知任务", f))

    return files, problems


def check_code(files):
    """代码规范检查"""
    issues = []
    RED = {
        "硬编码绝对路径": r"[A-Z]:\\\\WorkSpace|C:/WorkSpace",
        "明文密钥": r"github_pat_|ghp_|sk-[A-Za-z0-9]{20,}|password\s*=\s*[\"'][^\"']{6,}",
        "TODO/FIXME": r"\bTODO\b|\bFIXME\b|\bHACK\b",
        "第三方代码痕迹": r"扫呗|saobei|Saobei|反编译|ILSpy|dnlib",
    }
    for f in files:
        p = os.path.join(REPO, f)
        if not os.path.exists(p) or not f.endswith((".cs", ".py", ".axaml", ".json")):
            continue
        try:
            t = open(p, encoding="utf-8", errors="ignore").read()
        except Exception:
            continue
        for cat, pat in RED.items():
            for m in re.finditer(pat, t):
                line = t[:m.start()].count("\n") + 1
                issues.append((cat, "%s:%d" % (f, line)))
    return issues


def check_deps(files):
    """依赖白名单检查"""
    issues = []
    for f in files:
        if not f.endswith(".csproj"):
            continue
        p = os.path.join(REPO, f)
        if not os.path.exists(p):
            continue
        t = open(p, encoding="utf-8").read()
        for m in re.finditer(r'PackageReference\s+Include="([^"]+)"', t):
            pkg = m.group(1)
            base = re.sub(r"\.\d.*$", "", pkg)
            if pkg not in ALLOWED_PKGS and base not in ALLOWED_PKGS:
                issues.append(("未批准依赖", "%s → %s" % (f, pkg)))
    return issues


def verify(branch):
    print("=" * 92)
    print("验证分支：%s" % branch)
    print("=" * 92)

    # 1. 越界
    files, problems = check_scope(branch)
    if files is None:
        print("❌ %s" % problems)
        return False
    print("\n① 越界检查（改动 %d 个文件）" % len(files))
    if problems:
        for cat, f in problems[:20]:
            print("   %s %s" % (cat, f))
    else:
        print("   ✅ 全部在允许范围内")

    # 2. 代码规范
    issues = check_code(files)
    print("\n② 代码规范")
    if issues:
        for cat, loc in issues[:15]:
            print("   ⚠️ %-14s %s" % (cat, loc))
    else:
        print("   ✅ 无问题")

    # 3. 依赖
    deps = check_deps(files)
    print("\n③ 依赖检查")
    if deps:
        for cat, loc in deps[:10]:
            print("   ⚠️ %-14s %s" % (cat, loc))
    else:
        print("   ✅ 无新增未批准依赖")

    # 4. 编译 + 测试
    print("\n④ 全量编译")
    code, out = run([DOTNET, "build", "client/YiHua.Pos.sln", "-v", "q", "--nologo",
                     "-clp:NoSummary"])
    errs = [l for l in out.splitlines() if ": error " in l]
    if code == 0:
        print("   ✅ 编译通过")
    else:
        print("   ❌ 编译失败（%d 个错误）" % len(errs))
        for l in errs[:15]:
            print("      " + l.strip()[:170])

    # 5. 测试
    print("\n⑤ 全量测试")
    code2, out2 = run([DOTNET, "test", "client/YiHua.Pos.sln", "-v", "q", "--nologo",
                       "--no-build", "-clp:NoSummary"])
    passed = len(re.findall(r"已通过|Passed!", out2))
    failed = [l for l in out2.splitlines() if "失败" in l or "Failed" in l]
    if code2 == 0:
        print("   ✅ 测试通过")
    else:
        print("   ❌ 测试失败")
        for l in failed[:10]:
            print("      " + l.strip()[:170])

    ok = (code == 0) and (code2 == 0) and not any(c.startswith("❌") for c, _ in problems)
    print("\n" + "=" * 92)
    print("结论：%s" % ("✅ 通过，可以合并" if ok else "❌ 不通过，需打回"))
    print("=" * 92)
    return ok


def main():
    args = sys.argv[1:]
    do_merge = "--merge" in args
    args = [a for a in args if not a.startswith("--")]

    code, out = run(["git", "fetch", "--all", "--prune"])
    print("已同步远程分支\n")

    if not args:
        code, out = run(["git", "branch", "-r"])
        branches = [b.strip() for b in out.splitlines()
                    if b.strip() and "origin/main" not in b and "HEAD" not in b]
        if not branches:
            print("没有待验证的分支。")
            print("\n（worker 推完分支后会出现在这里）")
            return 0
        print("待验证分支：")
        for b in branches:
            print("  %s" % b)
        print("\n用法: python integrate.py <分支名>")
        return 0

    branch = args[0]
    if branch.startswith("origin/"):
        branch = branch[len("origin/"):]

    code, out = run(["git", "checkout", branch])
    if code != 0:
        code, out = run(["git", "checkout", "-b", branch, "origin/" + branch])
    if code != 0:
        print("❌ 无法切换分支: %s" % out[:200])
        return 1

    ok = verify(branch)

    if ok and do_merge:
        print("\n合并到 main ...")
        run(["git", "checkout", "main"])
        code, out = run(["git", "merge", "--no-ff", branch, "-m",
                         "合并 %s" % branch])
        if code == 0:
            run(["git", "push", "origin", "main"])
            print("✅ 已合并并推送")
        else:
            print("❌ 合并失败: %s" % out[:300])
            return 1
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
