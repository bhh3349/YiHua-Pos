"""全局配置。生产切 MySQL 只需改 DSH_POS_DB。"""
import os
import secrets
import sys
from typing import NamedTuple, Optional

# 冻结（PyInstaller）运行时，数据目录放在 exe 同级；开发时放在 server/ 下
if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

DATA_DIR = os.getenv("DSH_POS_DATA", os.path.join(BASE_DIR, "data"))
os.makedirs(DATA_DIR, exist_ok=True)

DB_FILE = os.path.join(DATA_DIR, "pos.db")
DB_URL = os.getenv("DSH_POS_DB", "sqlite:///" + DB_FILE.replace("\\", "/"))

# ---------------- 运行环境 ----------------
# dev（默认）/ prod。生产环境对「密钥不合格」是拒绝启动，而不是打条警告了事。
ENV = (os.getenv("DSH_POS_ENV") or "dev").strip().lower()
IS_PROD = ENV in ("prod", "production", "生产")

# ---------------- JWT 密钥强度 ----------------
# HS256 的密钥短于 32 字节时，PyJWT 会抛 InsecureKeyLengthWarning，
# 且短密钥存在被离线爆破的风险，所以这里在启动阶段就卡死。
JWT_SECRET_ENV = "DSH_POS_JWT_SECRET"
JWT_SECRET_MIN_BYTES = 32


class JwtSecret(NamedTuple):
    secret: str
    source: str            # env | dev-generated | env-weak
    warning: Optional[str]  # 需要醒目提示时非空


def _dev_secret_file() -> str:
    return os.path.join(DATA_DIR, "dev_jwt_secret.key")


def _gen_dev_secret() -> str:
    """开发环境自动生成 32 字节随机密钥，并落盘。

    落盘是为了服务端重启后已发的 token 不失效 —— 每次启动换密钥会让
    开发者反复重新登录。
    """
    path = _dev_secret_file()
    try:
        if os.path.exists(path):
            with open(path, "r", encoding="utf-8") as f:
                saved = f.read().strip()
            if len(saved.encode("utf-8")) >= JWT_SECRET_MIN_BYTES:
                return saved
        secret = secrets.token_urlsafe(32)
        with open(path, "w", encoding="utf-8") as f:
            f.write(secret)
        return secret
    except OSError:                                   # pragma: no cover - 只读目录等极端情况
        return secrets.token_urlsafe(32)


def resolve_jwt_secret(raw: Optional[str] = None, is_prod: bool = IS_PROD) -> JwtSecret:
    """校验 JWT 密钥，返回 (密钥, 来源, 警告)。

    规则：
      * 配了 ``DSH_POS_JWT_SECRET`` 且 >= 32 字节  → 直接用；
      * 配了但过短 → 生产环境抛 RuntimeError（拒绝启动），开发环境打醒目警告；
      * 没配 → 生产环境抛 RuntimeError（拒绝启动），开发环境自动生成并落盘。
    """
    raw = (raw or "").strip()
    if raw:
        if len(raw.encode("utf-8")) >= JWT_SECRET_MIN_BYTES:
            return JwtSecret(raw, "env", None)
        if is_prod:
            raise RuntimeError(
                "生产环境 JWT 密钥不合格：%s 只有 %d 字节，HS256 要求至少 %d 字节。"
                "请换一个足够长的随机密钥再启动。"
                % (JWT_SECRET_ENV, len(raw.encode("utf-8")), JWT_SECRET_MIN_BYTES))
        return JwtSecret(raw, "env-weak", (
            "%s 只有 %d 字节，低于 HS256 建议的 %d 字节，仅可用于开发调试；"
            "生产环境必须替换成足够长的随机密钥（否则服务端会拒绝启动）。"
            % (JWT_SECRET_ENV, len(raw.encode("utf-8")), JWT_SECRET_MIN_BYTES)))

    if is_prod:
        raise RuntimeError(
            "生产环境必须显式配置 %s（>= %d 字节的随机密钥），不允许自动生成。"
            % (JWT_SECRET_ENV, JWT_SECRET_MIN_BYTES))
    return JwtSecret(_gen_dev_secret(), "dev-generated",
                     "未配置 %s，已自动生成开发密钥（%s）。"
                     "这是开发密钥，生产必须替换：请设置 %s 为 >= %d 字节的随机密钥。"
                     % (JWT_SECRET_ENV, _dev_secret_file(), JWT_SECRET_ENV,
                        JWT_SECRET_MIN_BYTES))


_JWT = resolve_jwt_secret(os.getenv(JWT_SECRET_ENV))
JWT_SECRET = _JWT.secret
JWT_SECRET_SOURCE = _JWT.source
JWT_SECRET_WARNING = _JWT.warning

JWT_ALG = "HS256"
TOKEN_TTL_MIN = int(os.getenv("DSH_POS_TOKEN_TTL_MIN", "720"))       # 12h
REFRESH_TTL_MIN = int(os.getenv("DSH_POS_REFRESH_TTL_MIN", "10080"))  # 7d

# 商品 CSV 投放目录（相当于对象存储的根）
OSS_DIR = os.getenv("DSH_POS_OSS_DIR", os.path.join(DATA_DIR, "oss"))
OSS_BASE_URL = os.getenv("DSH_POS_OSS_BASE", "http://127.0.0.1:8000/oss/")

APP_NAME = "自有餐饮 POS 服务端"
API_VER = "100"
