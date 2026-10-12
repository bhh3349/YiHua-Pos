-- ============================================================
-- 逸华餐饮收银 · 本地 SQLite 初始化脚本 V001（T-003）
--
-- 约定（与 SqlSugar 默认映射保持一致，实体无需特性标记）：
--   * 表名 / 列名与 Domain 实体属性名一一对应
--   * 金额 decimal 列使用 TEXT 存储，避免浮点精度丢失
--   * 时间统一 TEXT('yyyy-MM-dd HH:mm:ss')，DateTime? 允许 NULL
--   * 枚举按 INTEGER 存储，bool 按 INTEGER(0/1) 存储
--   * "Order" 为 SQL 关键字，表名加双引号
--   * 全部使用 IF NOT EXISTS，脚本幂等，可重复执行
-- ============================================================

-- ---------------- 订单主表 ----------------
CREATE TABLE IF NOT EXISTS "Order" (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderNo       TEXT NOT NULL DEFAULT '',
    ClientOrderNo TEXT NOT NULL DEFAULT '',
    DeskId        INTEGER NOT NULL DEFAULT 0,
    OrderType     INTEGER NOT NULL DEFAULT 1,
    Status        INTEGER NOT NULL DEFAULT 0,
    GuestCount    INTEGER NOT NULL DEFAULT 0,
    AmountTotal   TEXT NOT NULL DEFAULT '0',
    AmountDiscount TEXT NOT NULL DEFAULT '0',
    AmountPay     TEXT NOT NULL DEFAULT '0',
    AmountRefund  TEXT NOT NULL DEFAULT '0',
    AmountReceived TEXT NOT NULL DEFAULT '0',
    AmountChange  TEXT NOT NULL DEFAULT '0',
    TakeNo        TEXT NOT NULL DEFAULT '',
    ShiftId       INTEGER NOT NULL DEFAULT 0,
    OperatorId    INTEGER NOT NULL DEFAULT 0,
    Remark        TEXT NOT NULL DEFAULT '',
    CreatedAt     TEXT NOT NULL DEFAULT '',
    SettleTime    TEXT NULL,
    SyncStatus    INTEGER NOT NULL DEFAULT 0
);
-- 幂等键：断网补传去重的基础，必须唯一
CREATE UNIQUE INDEX IF NOT EXISTS UX_Order_ClientOrderNo ON "Order"(ClientOrderNo);
CREATE INDEX IF NOT EXISTS IX_Order_CreatedAt  ON "Order"(CreatedAt);
CREATE INDEX IF NOT EXISTS IX_Order_DeskId     ON "Order"(DeskId);
CREATE INDEX IF NOT EXISTS IX_Order_ShiftId    ON "Order"(ShiftId);
CREATE INDEX IF NOT EXISTS IX_Order_SyncStatus ON "Order"(SyncStatus);
CREATE INDEX IF NOT EXISTS IX_Order_Status     ON "Order"(Status);

-- ---------------- 订单明细 ----------------
CREATE TABLE IF NOT EXISTS OrderItem (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId        INTEGER NOT NULL DEFAULT 0,
    ProductId      INTEGER NOT NULL DEFAULT 0,
    ProductName    TEXT NOT NULL DEFAULT '',
    SpecText       TEXT NOT NULL DEFAULT '',
    ExtraText      TEXT NOT NULL DEFAULT '',
    Price          TEXT NOT NULL DEFAULT '0',
    Qty            TEXT NOT NULL DEFAULT '1',
    Amount         TEXT NOT NULL DEFAULT '0',
    DiscountAmount TEXT NOT NULL DEFAULT '0',
    IsGift         INTEGER NOT NULL DEFAULT 0,
    IsWeighted     INTEGER NOT NULL DEFAULT 0,
    Remark         TEXT NOT NULL DEFAULT '',
    IsRefunded     INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_OrderItem_OrderId ON OrderItem(OrderId);

-- ---------------- 支付记录 ----------------
CREATE TABLE IF NOT EXISTS Payment (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    OrderId     INTEGER NOT NULL DEFAULT 0,
    PayType     INTEGER NOT NULL DEFAULT 1,
    PayTypeName TEXT NOT NULL DEFAULT '',
    Amount      TEXT NOT NULL DEFAULT '0',
    PayTime     TEXT NOT NULL DEFAULT '',
    OperatorId  INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_Payment_OrderId ON Payment(OrderId);

-- ---------------- 商品 ----------------
CREATE TABLE IF NOT EXISTS Product (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT NOT NULL DEFAULT '',
    Code         TEXT NOT NULL DEFAULT '',
    Barcode      TEXT NOT NULL DEFAULT '',
    CategoryId   INTEGER NOT NULL DEFAULT 0,
    CategoryName TEXT NOT NULL DEFAULT '',
    UnitId       INTEGER NOT NULL DEFAULT 0,
    UnitName     TEXT NOT NULL DEFAULT '',
    Price        TEXT NOT NULL DEFAULT '0',
    MemberPrice  TEXT NOT NULL DEFAULT '0',
    ProductType  INTEGER NOT NULL DEFAULT 1,
    WeightWay    INTEGER NOT NULL DEFAULT 0,
    SpecGroupName TEXT NOT NULL DEFAULT '',
    PrePinYin    TEXT NOT NULL DEFAULT '',
    ImageUrl     TEXT NOT NULL DEFAULT '',
    DataStatus   INTEGER NOT NULL DEFAULT 1,
    Sort         INTEGER NOT NULL DEFAULT 0,
    Version      INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS IX_Product_CategoryId ON Product(CategoryId);
CREATE INDEX IF NOT EXISTS IX_Product_Barcode    ON Product(Barcode);
CREATE INDEX IF NOT EXISTS IX_Product_DataStatus ON Product(DataStatus);

-- ---------------- 商品分类 ----------------
CREATE TABLE IF NOT EXISTS ProductCategory (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    Name         TEXT NOT NULL DEFAULT '',
    CategoryType INTEGER NOT NULL DEFAULT 1,
    Sort         INTEGER NOT NULL DEFAULT 0,
    DataStatus   INTEGER NOT NULL DEFAULT 1,
    Version      INTEGER NOT NULL DEFAULT 1
);

-- ---------------- 计量单位 ----------------
CREATE TABLE IF NOT EXISTS ProductUnit (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT NOT NULL DEFAULT '',
    Sort    INTEGER NOT NULL DEFAULT 0,
    Version INTEGER NOT NULL DEFAULT 1
);

-- ---------------- 桌台区域 ----------------
CREATE TABLE IF NOT EXISTS DeskArea (
    Id      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name    TEXT NOT NULL DEFAULT '',
    Sort    INTEGER NOT NULL DEFAULT 0,
    Version INTEGER NOT NULL DEFAULT 1
);

-- ---------------- 桌台 ----------------
CREATE TABLE IF NOT EXISTS Desk (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    AreaId        INTEGER NOT NULL DEFAULT 0,
    AreaName      TEXT NOT NULL DEFAULT '',
    Name          TEXT NOT NULL DEFAULT '',
    Capacity      INTEGER NOT NULL DEFAULT 4,
    Status        INTEGER NOT NULL DEFAULT 0,
    CurrentOrderId INTEGER NOT NULL DEFAULT 0,
    GuestCount    INTEGER NOT NULL DEFAULT 0,
    OpenTime      TEXT NULL,
    CurrentAmount TEXT NOT NULL DEFAULT '0',
    Sort          INTEGER NOT NULL DEFAULT 0,
    Version       INTEGER NOT NULL DEFAULT 1
);
CREATE INDEX IF NOT EXISTS IX_Desk_AreaId ON Desk(AreaId);

-- ---------------- 交班记录 ----------------
CREATE TABLE IF NOT EXISTS Shift (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    TerminalNo   TEXT NOT NULL DEFAULT '',
    OperatorId   INTEGER NOT NULL DEFAULT 0,
    OperatorName TEXT NOT NULL DEFAULT '',
    StartAt      TEXT NOT NULL DEFAULT '',
    EndAt        TEXT NULL,
    Status       INTEGER NOT NULL DEFAULT 0,
    CashOpen     TEXT NOT NULL DEFAULT '0',
    CashShould   TEXT NOT NULL DEFAULT '0',
    CashActual   TEXT NOT NULL DEFAULT '0',
    CashDiff     TEXT NOT NULL DEFAULT '0',
    OrderCount   INTEGER NOT NULL DEFAULT 0,
    AmountTotal  TEXT NOT NULL DEFAULT '0',
    CashAmount   TEXT NOT NULL DEFAULT '0',
    CustomAmount TEXT NOT NULL DEFAULT '0',
    Remark       TEXT NOT NULL DEFAULT '',
    SyncStatus   INTEGER NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IX_Shift_Status  ON Shift(Status);
CREATE INDEX IF NOT EXISTS IX_Shift_StartAt ON Shift(StartAt);

-- ---------------- 离线补传队列 ----------------
CREATE TABLE IF NOT EXISTS OutboxItem (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ClientOrderNo TEXT NOT NULL DEFAULT '',
    ApiPath       TEXT NOT NULL DEFAULT '',
    PayloadJson   TEXT NOT NULL DEFAULT '',
    Status        INTEGER NOT NULL DEFAULT 0,
    RetryCount    INTEGER NOT NULL DEFAULT 0,
    LastError     TEXT NOT NULL DEFAULT '',
    CreatedAt     TEXT NOT NULL DEFAULT '',
    SentAt        TEXT NULL
);
-- 补传按时间顺序取出：(Status, CreatedAt) 复合索引
CREATE INDEX IF NOT EXISTS IX_Outbox_StatusCreated ON OutboxItem(Status, CreatedAt);
CREATE INDEX IF NOT EXISTS IX_Outbox_ClientOrderNo ON OutboxItem(ClientOrderNo);

-- ---------------- 通用键值配置（Data 层内部表，对应 IDictionaryRepository） ----------------
CREATE TABLE IF NOT EXISTS DictEntry (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Key       TEXT NOT NULL DEFAULT '',
    Value     TEXT NOT NULL DEFAULT '',
    UpdatedAt TEXT NOT NULL DEFAULT ''
);
CREATE UNIQUE INDEX IF NOT EXISTS UX_DictEntry_Key ON DictEntry(Key);
