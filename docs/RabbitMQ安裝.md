# RabbitMQ 4.3.2 Windows 5 分鐘快速安裝指南

第一次安裝請依序完成：下載 → Erlang → RabbitMQ → Service → Management → mq_admin → 驗證。

---

## 1. 安裝前準備

| 項目 | 值 |
|---|---|
| Erlang/OTP | 27.3.4.15 |
| RabbitMQ | 4.3.2 |
| AMQP Port | 5672 |
| Management Port | 15672 |
| 管理帳號 | mq_admin / mq_admin |
| Virtual Host | / |

安裝順序：

```text
Erlang
  ↓
RabbitMQ
  ↓
Windows Service
  ↓
Management
  ↓
mq_admin
```

---

## 2. 下載安裝檔

主要來源（專案 Box）：

```text
FY26-P097_LOG-ILC系統開發專案
→ RabbitMQ安裝
```

URL：

```text
https://tel.ent.box.com/folder/407951254967
```

下載：

```text
otp_win64_27.3.4.15.exe
rabbitmq-server-4.3.2.exe
```

請使用專案指定版本，不要自行下載最新版取代。

官方來源（僅備用參考）：

```text
Erlang：
https://www.erlang.org/

RabbitMQ 4.3.2：
https://github.com/rabbitmq/rabbitmq-server/releases/tag/v4.3.2
```

若電腦已有 Erlang 29、28、26 或其他版本，請先解除安裝舊 Erlang。

原因：避免 ERLANG_HOME、PATH 或 RabbitMQ Windows Service 指向錯誤 Erlang。

---

## 3. 安裝 Erlang

執行：

```text
otp_win64_27.3.4.15.exe
```

安裝路徑：

```text
C:\Program Files\Erlang OTP
```

設定系統環境變數：

```text
ERLANG_HOME=C:\Program Files\Erlang OTP
```

並將下列路徑加入 Path：

```text
%ERLANG_HOME%\bin
```

重新開啟 CMD 後驗證：

```cmd
where erl
```

預期：

```text
C:\Program Files\Erlang OTP\bin\erl.exe
```

再執行：

```cmd
erl -noshell -eval "io:format(\"OTP=~s~n\", [erlang:system_info(otp_release)]), halt()."
```

預期：

```text
OTP=27
```

再確認：

```cmd
echo %ERLANG_HOME%
```

預期：

```text
C:\Program Files\Erlang OTP
```

---

## 4. 安裝 RabbitMQ

執行：

```text
rabbitmq-server-4.3.2.exe
```

預期安裝路徑：

```text
C:\Program Files\RabbitMQ Server\rabbitmq_server-4.3.2
```

接下來 Service / Plugin 操作，必須使用「Run as administrator」的 CMD。

---

## 5. 啟動 RabbitMQ Windows Service

以 Administrator 開啟 CMD，進入：

```cmd
cd "C:\Program Files\RabbitMQ Server\rabbitmq_server-4.3.2\sbin"
```

若之前安裝過 RabbitMQ / Erlang，先重建 Service：

```cmd
rabbitmq-service.bat stop
rabbitmq-service.bat remove
rabbitmq-service.bat install
rabbitmq-service.bat start
```

若是全新安裝，通常直接：

```cmd
rabbitmq-service.bat install
rabbitmq-service.bat start
```

設定自動啟動：

```cmd
sc config RabbitMQ start= auto
```

確認狀態：

```cmd
sc query RabbitMQ
```

預期：

```text
STATE              : 4  RUNNING
```

確認自動啟動：

```cmd
sc qc RabbitMQ
```

預期：

```text
START_TYPE         : 2   AUTO_START
```

正常完成後 RabbitMQ 是 Windows Service，不需要每次開 CMD 執行 `rabbitmq-server.bat`。

---

## 6. 啟用 Management

仍在 `sbin` 目錄（Administrator CMD）：

```cmd
rabbitmq-plugins.bat enable rabbitmq_management
```

若出現：

```text
Offline change; changes will take effect at broker restart.
```

執行：

```cmd
net stop RabbitMQ
net start RabbitMQ
```

瀏覽器開啟：

```text
http://localhost:15672
```

第一次登入：

```text
Username: guest
Password: guest
```

---

## 7. 建立 mq_admin

從 Web UI：

```text
Admin
→ Users
→ Add a user
```

設定：

```text
Username: mq_admin
Password: mq_admin
Confirm password: mq_admin
Tags: administrator
```

（Tags 可按快捷鈕 Admin，會寫入 `administrator`）

按 Add user。

注意：`administrator` Tag 不代表具有 Virtual Host 權限，還要設定 Permissions。

接著：

```text
Admin
→ Users
→ mq_admin
→ Permissions
```

設定：

```text
Virtual host: /
Configure regexp: .*
Write regexp: .*
Read regexp: .*
```

按 Set permission。

最後登出 guest，改用：

```text
Username: mq_admin
Password: mq_admin
```

重新登入驗證。

---

## 8. Application 連線設定

| 項目 | 值 |
|---|---|
| Host | localhost |
| Port | 5672 |
| Virtual Host | / |
| Username | mq_admin |
| Password | mq_admin |

Port 區分：

```text
5672  = Application / AMQP
15672 = Management Web UI
```

AMQP Connection String：

```text
amqp://mq_admin:mq_admin@localhost:5672/
```

### appsettings.json 範例

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Port": 5672,
    "VirtualHost": "/",
    "Username": "mq_admin",
    "Password": "mq_admin"
  }
}
```

正式環境請改由 Environment Variables / Secret Manager / Key Vault 提供密碼。

### MassTransit 範例

```csharp
services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h =>
        {
            h.Username("mq_admin");
            h.Password("mq_admin");
        });

        cfg.ConfigureEndpoints(context);
    });
});
```

---

## 9. 安裝完成驗證

Checklist：

```text
[ ] where erl 指到 C:\Program Files\Erlang OTP\bin\erl.exe
[ ] Erlang 顯示 OTP=27
[ ] ERLANG_HOME 正確
[ ] RabbitMQ Service = RUNNING
[ ] RabbitMQ = AUTO_START
[ ] http://localhost:15672 可以開啟
[ ] mq_admin 可以登入
[ ] mq_admin 可以存取 Virtual Host /
[ ] Application 使用 localhost:5672
```

完成後環境應為：

```text
Erlang       : 27.3.4.15
RabbitMQ     : 4.3.2
Service      : RUNNING
Startup      : AUTO_START
AMQP         : localhost:5672
Management   : http://localhost:15672
User         : mq_admin
Virtual Host : /
```

---

## 10. 常見問題

### System Error 5 / Access is denied

原因：CMD 沒有 Administrator 權限。

解決：以「Run as administrator」重新開啟 CMD，再進入 `sbin` 執行指令。

### Service 1067

依序檢查：

```cmd
where erl
echo %ERLANG_HOME%
```

若正確，在 `sbin` 手動啟動 Broker 觀察錯誤：

```cmd
cd "C:\Program Files\RabbitMQ Server\rabbitmq_server-4.3.2\sbin"
rabbitmq-server.bat
```

若手動 Broker 可正常啟動，再重建 Service：

```cmd
rabbitmq-service.bat remove
rabbitmq-service.bat install
rabbitmq-service.bat start
```

### Erlang Cookie Error

若執行：

```cmd
rabbitmqctl.bat status
```

出現：

```text
TCP connection succeeded but Erlang distribution failed
```

或：

```text
check if the Erlang cookie is identical
```

代表 RabbitMQ Windows Service 使用的 Erlang Cookie，與目前 Windows User 的 Erlang Cookie 不一致。

若同時滿足：

```text
Service = RUNNING
Management UI = OK
localhost:5672 = OK
```

則 RabbitMQ Broker 本身正常，不需要重新安裝。
