"""auth —— 认证域。"""
from datetime import datetime, timedelta

from fastapi import APIRouter, Depends, Header
from pydantic import BaseModel
from sqlalchemy.orm import Session

from ..db import get_db
from ..models import Store, SysRole, SysUser, Tenant, Terminal
from ..security import (create_refresh_token, create_token, decode_token,
                        verify_password)
from ..util import BizError, Ctx, get_ctx, ok

router = APIRouter(prefix="/api/v1/auth", tags=["auth"])


class LoginReq(BaseModel):
    account: str
    password: str = ""
    loginType: str = "1"
    deviceId: str = ""
    deviceName: str = ""
    terminalNo: str = ""
    timestamp: int = 0
    sign: str = ""


def _profile(db: Session, user: SysUser):
    role = db.get(SysRole, user.role_id) if user.role_id else None
    return {
        "userId": user.id,
        "account": user.account,
        "name": user.name or user.account,
        "roleId": user.role_id or 0,
        "roleName": role.name if role else "",
        "permissions": (role.permissions_json if role and role.permissions_json else []),
    }


@router.post("/login")
def login(req: LoginReq, db: Session = Depends(get_db)):
    user = (db.query(SysUser)
              .filter(SysUser.account == req.account, SysUser.status == 1)
              .first())
    if not user or not verify_password(req.password, user.pwd_hash):
        raise BizError("401", "账号或密码错误")

    tenant = db.get(Tenant, user.tenant_id)
    store = db.get(Store, user.store_id)
    if not tenant or tenant.status != 1:
        raise BizError("403", "租户不可用")
    if not store or store.status != 1:
        raise BizError("403", "门店不可用")

    terminal_no = req.terminalNo or "T0001"
    term = (db.query(Terminal)
              .filter(Terminal.store_id == store.id, Terminal.no == terminal_no).first())
    if not term:
        term = Terminal(store_id=store.id, no=terminal_no, name="POS-" + terminal_no)
        db.add(term)
    term.device_id = req.deviceId or term.device_id
    term.last_online_at = datetime.now()
    db.commit()

    token = create_token(user.id, tenant.id, store.id, terminal_no)
    return ok({
        "token": token,
        "refreshToken": create_refresh_token(user.id, tenant.id),
        "expireAt": (datetime.now() + timedelta(hours=12)).strftime("%Y-%m-%d %H:%M:%S"),
        "userInfo": _profile(db, user),
        "tenantInfo": {"tenantId": tenant.id, "code": tenant.code, "name": tenant.name,
                       "edition": tenant.edition},
        "storeInfo": {"storeId": store.id, "code": store.code, "name": store.name,
                      "storeMode": store.store_mode},
        "terminals": [{"no": t.no, "name": t.name} for t in
                      db.query(Terminal).filter(Terminal.store_id == store.id).all()],
        # systemData 留给客户端决定是否覆盖本地配置；这里回 null 表示「用我自己的 appsettings」
        "systemData": None,
    })


class ConfirmReq(BaseModel):
    loginTicket: str = ""
    userId: int = 0


@router.post("/loginConfirm")
def login_confirm(req: ConfirmReq, db: Session = Depends(get_db)):
    user = db.get(SysUser, req.userId)
    if not user:
        raise BizError("401", "用户不存在")
    store = db.get(Store, user.store_id)
    tenant = db.get(Tenant, user.tenant_id)
    terminal_no = "T0001"
    token = create_token(user.id, tenant.id, store.id, terminal_no)
    return ok({
        "token": token,
        "refreshToken": create_refresh_token(user.id, tenant.id),
        "userInfo": _profile(db, user),
        "tenantInfo": {"tenantId": tenant.id, "code": tenant.code, "name": tenant.name},
        "storeInfo": {"storeId": store.id, "code": store.code, "name": store.name},
        "systemData": None,
    })


class RefreshReq(BaseModel):
    refreshToken: str


@router.post("/refresh")
def refresh(req: RefreshReq):
    payload = decode_token(req.refreshToken)
    if not payload or payload.get("typ") != "refresh":
        raise BizError("401", "刷新令牌无效")
    token = create_token(int(payload["sub"]), int(payload.get("tid") or 0), 0, "")
    return ok({"token": token,
               "expireAt": (datetime.now() + timedelta(hours=12)).strftime("%Y-%m-%d %H:%M:%S")})


@router.post("/logout")
def logout(ctx: Ctx = Depends(get_ctx)):
    return ok(None)


@router.post("/profile")
def profile(ctx: Ctx = Depends(get_ctx), db: Session = Depends(get_db)):
    user = db.get(SysUser, ctx.user_id)
    if not user:
        raise BizError("401", "用户不存在")
    return ok(_profile(db, user))
