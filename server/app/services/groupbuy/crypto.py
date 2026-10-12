"""密钥加密与脱敏。

任务卡 4.1 / 4.7 要求：平台 ``sign_key`` **加密存储**、**不落日志**。

实现取舍：用标准库的 ``hashlib`` 做流式密钥派生，不引入额外依赖
（``server/requirements.txt`` 里没有 cryptography）。派生过程是
``sha256(master || salt)`` 迭代 ``_KDF_ROUNDS`` 次 —— 对「防止数据库
被拖走后密钥直接可读」这个目标是够的，且不依赖任何第三方包。

对外只暴露三件事：
  * :func:`encrypt_secret` / :func:`decrypt_secret`  —— 配置的存 / 取
  * :func:`mask_secret`                            —— 任何要写日志的地方都用它
"""
import base64
import hashlib
import hmac
import os

from .errors import GroupbuyError, PlatformErrorCode

_KDF_ROUNDS = 20000
_SALT_BYTES = 16
_MASK_KEEP = 2          # 脱敏后保留的首尾字符数


def _master_key() -> bytes:
    """主密钥：优先环境变量，其次复用服务端已落盘的 dev 密钥。

    复用 ``data/dev_jwt_secret.key`` 是为了避免再多一个「必须配置」的项 ——
    服务端已经有这套机制（见 app/config.py），团购密钥跟着走同一条路。
    """
    env = (os.getenv("DSH_POS_GROUPBUY_SECRET_KEY") or "").strip()
    if env:
        return env.encode("utf-8")
    from ...config import JWT_SECRET
    return ("groupbuy::" + JWT_SECRET).encode("utf-8")


def _derive_key(salt: bytes) -> bytes:
    """把主密钥 + salt 派生成 32 字节的流密钥。"""
    block = _master_key() + salt
    for _ in range(_KDF_ROUNDS):
        block = hashlib.sha256(block).digest()
    return block


def _keystream(key: bytes, length: int) -> bytes:
    """用派生密钥生成伪随机字节流（CTR 模式）。"""
    out = bytearray()
    counter = 0
    while len(out) < length:
        out += hashlib.sha256(key + counter.to_bytes(4, "big")).digest()
        counter += 1
    return bytes(out[:length])


def encrypt_secret(plain: str) -> str:
    """把明文密钥加密成可落库的字符串。

    格式 ``gbs1.<salt_b64>.<cipher_b64>.<mac_b64>``，自带版本号便于以后换算法。
    """
    if plain is None:
        plain = ""
    raw = plain.encode("utf-8")
    salt = os.urandom(_SALT_BYTES)
    key = _derive_key(salt)
    cipher = bytes(a ^ b for a, b in zip(raw, _keystream(key, len(raw))))
    mac = hmac.new(key, salt + cipher, hashlib.sha256).digest()
    b64 = lambda bs: base64.urlsafe_b64encode(bs).decode("ascii")   # noqa: E731
    return "gbs1.%s.%s.%s" % (b64(salt), b64(cipher), b64(mac))


def decrypt_secret(stored: str) -> str:
    """解密。密文被篡改（MAC 对不上）时直接抛错，不返回可疑数据。"""
    if not stored:
        return ""
    try:
        ver, salt_b64, cipher_b64, mac_b64 = stored.split(".")
        if ver != "gbs1":
            raise ValueError("未知密文版本 %s" % ver)
        salt = base64.urlsafe_b64decode(salt_b64)
        cipher = base64.urlsafe_b64decode(cipher_b64)
        mac = base64.urlsafe_b64decode(mac_b64)
    except Exception:
        raise GroupbuyError(PlatformErrorCode.ADAPTER_ERROR, "平台密钥密文格式非法")

    key = _derive_key(salt)
    expect = hmac.new(key, salt + cipher, hashlib.sha256).digest()
    if not hmac.compare_digest(expect, mac):
        raise GroupbuyError(PlatformErrorCode.ADAPTER_ERROR, "平台密钥校验失败（密文被篡改）")
    raw = bytes(a ^ b for a, b in zip(cipher, _keystream(key, len(cipher))))
    return raw.decode("utf-8")


def mask_secret(value: str, keep: int = _MASK_KEEP) -> str:
    """脱敏：``abcdefgh`` -> ``ab****gh``。

    所有会写进日志/异常/响应体的密钥都必须先过这一层。
    """
    v = value or ""
    if not v:
        return ""
    if len(v) <= keep * 2:
        return "*" * len(v)
    return "%s%s%s" % (v[:keep], "*" * (len(v) - keep * 2), v[-keep:])
