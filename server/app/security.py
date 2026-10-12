"""密码与 JWT。"""
import time
from typing import Optional

import bcrypt
import jwt

from .config import JWT_ALG, JWT_SECRET, REFRESH_TTL_MIN, TOKEN_TTL_MIN


def hash_password(raw: str) -> str:
    return bcrypt.hashpw(raw.encode("utf-8"), bcrypt.gensalt(rounds=10)).decode()


def verify_password(raw: str, hashed: str) -> bool:
    try:
        return bcrypt.checkpw(raw.encode("utf-8"), hashed.encode("utf-8"))
    except Exception:
        return False


def _encode(payload: dict, ttl_min: int) -> str:
    now = int(time.time())
    body = dict(payload)
    body.update({"iat": now, "exp": now + ttl_min * 60})
    return jwt.encode(body, JWT_SECRET, algorithm=JWT_ALG)


def create_token(user_id: int, tenant_id: int, store_id: int, terminal_no: str) -> str:
    return _encode({"sub": str(user_id), "tid": tenant_id, "sid": store_id, "term": terminal_no,
                    "typ": "access"}, TOKEN_TTL_MIN)


def create_refresh_token(user_id: int, tenant_id: int) -> str:
    return _encode({"sub": str(user_id), "tid": tenant_id, "typ": "refresh"}, REFRESH_TTL_MIN)


def decode_token(token: str) -> Optional[dict]:
    try:
        return jwt.decode(token, JWT_SECRET, algorithms=[JWT_ALG])
    except Exception:
        return None
