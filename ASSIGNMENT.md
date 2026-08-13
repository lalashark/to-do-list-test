# 多人 To-do List API

## 作業目標

請使用 C# 與 ASP.NET Core Web API 實作一套多人使用的 To-do List 後端服務。系統須提供使用者註冊、登入、JWT 身分驗證，以及個人待辦事項的建立、查詢、更新與刪除功能。

本作業主要評估 API 設計、資料建模、身分驗證、權限控管、錯誤處理、Docker 環境建置及自動化測試能力。

## 指定技術

- C# 與 ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- JWT Bearer Authentication
- Docker 與 Docker Compose
- OpenAPI／Swagger

不得以記憶體集合或檔案取代 PostgreSQL。

## 啟動與容器要求

專案必須提供 `Dockerfile` 與 `compose.yaml`。只需安裝 Docker，並可使用以下指令啟動完整系統：

```bash
docker compose up --build
```

系統穩定運行時，`docker compose ps` 應顯示兩個長時間運行的服務：

1. ASP.NET Core Web API
2. PostgreSQL

並須符合以下要求：

- API 透過 Docker Compose network 連接 PostgreSQL。
- API 對外服務位址為 `http://localhost:8080`。
- PostgreSQL 必須設定 health check。
- API 應等待 PostgreSQL 可用後再啟動。
- 必須提供 EF Core Migration，且容器啟動後資料庫 schema 可自動完成初始化。
- PostgreSQL 資料必須儲存在 Docker named volume；一般停止及重啟容器後資料不得消失。
- Connection string、JWT Secret 與資料庫密碼須透過環境變數或等效的安全設定方式注入，不得寫死於程式碼。
- 不得提交真實環境的密碼、Token 或 Secret。
- 不得要求評審在本機額外安裝 .NET SDK 或 PostgreSQL。

## 資料模型

### User

至少包含：

- `Id`
- `Email`
- `PasswordHash`
- `DisplayName`
- `CreatedAt`

要求：

- Email 不分大小寫且不可重複。
- Email 的唯一性須由資料庫 constraint 或 unique index 保證，不能只靠應用程式檢查。
- 密碼不得以明文或可逆加密形式保存。
- API response 不得包含密碼或密碼雜湊。

### Todo

至少包含：

- `Id`
- `OwnerId`
- `Title`
- `Description`
- `Status`
- `Priority`
- `DueDate`
- `CreatedAt`
- `UpdatedAt`

欄位規則：

- `Title`：必填，去除前後空白後長度為 1～100 字元。
- `Description`：選填，最多 500 字元。
- `Status`：`pending`、`inProgress` 或 `completed`。
- `Priority`：`low`、`medium` 或 `high`。
- `DueDate`：選填，API 使用 ISO 8601 UTC 時間。
- 新增 Todo 時，Status 預設為 `pending`。
- `OwnerId` 必須是指向 User 的 foreign key。

## 必要 API

詳細 request、response 與 status code 定義請以 [`openapi.yaml`](./openapi.yaml) 為準。

### 身分驗證

- `POST /api/auth/register`：註冊使用者。
- `POST /api/auth/login`：登入並取得 JWT access token。

註冊密碼至少 8 個字元。帳號不存在或密碼錯誤時，登入 API 應回傳相同且不洩漏細節的錯誤訊息。

除註冊、登入與健康檢查外，其餘 API 均須提供有效的 JWT。

### Todo

- `POST /api/todos`：建立 Todo。
- `GET /api/todos`：取得目前使用者的 Todo 列表。
- `GET /api/todos/{id}`：取得單一 Todo。
- `PATCH /api/todos/{id}`：部分更新 Todo。
- `DELETE /api/todos/{id}`：刪除 Todo。

`GET /api/todos` 必須支援：

- 依 `status` 篩選。
- 依 `priority` 篩選。
- 以 `keyword` 搜尋標題。
- `page` 與 `pageSize` 分頁，`pageSize` 不得超過 100。
- 依 `createdAt`、`updatedAt`、`dueDate`、`title` 或 `priority` 排序。
- `asc` 或 `desc` 排序方向。

### 健康檢查

- `GET /health`：回報 API 與資料庫是否可用。

## 權限與資料隔離

使用者只能列出、讀取、修改及刪除自己的 Todo。所有 Todo 查詢都必須在後端依目前登入者限制資料範圍，不能依賴前端傳入的 OwnerId。

若資源不存在或不屬於目前使用者，單筆查詢、更新及刪除 API 統一回傳 `404 Not Found`，避免洩漏其他使用者的資源是否存在。

建立或更新 Todo 時，API 不得接受客戶端指定 `OwnerId`、`CreatedAt` 或 `UpdatedAt`。

## 錯誤處理

- 使用一致的 `application/problem+json` 錯誤格式。
- 驗證失敗回傳 `400 Bad Request`。
- JWT 缺少、過期或無效時回傳 `401 Unauthorized`。
- 資源不存在或使用者無權存取時回傳 `404 Not Found`。
- Email 已被使用時回傳 `409 Conflict`。
- 未預期錯誤回傳 `500 Internal Server Error`，且不得暴露 stack trace、connection string 或其他敏感資訊。

## Migration 與初始化

- Repository 必須包含可重建 schema 的 EF Core Migration。
- 第一次執行 `docker compose up --build` 時應可建立並初始化資料庫。
- 請在 README 說明 Migration 執行策略，以及正式環境中如何避免多個 API instance 同時執行 Migration 的競態問題。
- 不要求建立預設帳號或測試資料；若有建立，須在 README 清楚說明。

## 自動化測試

至少應涵蓋以下情境：

- 註冊成功。
- 重複 Email 無法註冊。
- 正確帳號密碼可以登入。
- 錯誤密碼無法登入，且錯誤訊息不洩漏細節。
- 未登入無法存取 Todo API。
- 使用者只能取得自己的 Todo。
- 使用者不能讀取、修改或刪除其他使用者的 Todo。
- Todo 欄位驗證。
- Todo 列表的分頁及至少一種篩選條件。

可使用單元測試與整合測試，但必須包含能實際驗證 HTTP pipeline、身分驗證及資料存取的整合測試。

## 繳交內容

Repository 至少應包含：

- 可編譯的 ASP.NET Core 原始碼。
- 自動化測試。
- `Dockerfile`。
- `compose.yaml`。
- EF Core Migration。
- Swagger/OpenAPI 支援。
- `README.md`，說明：
  - 啟動及停止方式。
  - 環境變數與設定方式。
  - Migration 策略。
  - 執行測試的方式。
  - 重要設計決策與已知限制。

不得提交 `bin`、`obj`、IDE 快取、資料庫資料檔或真實 Secret。

## 評分標準

| 項目 | 比重 |
| --- | ---: |
| 登入與 JWT 驗證 | 20% |
| Todo 功能正確性 | 20% |
| 權限控管與使用者資料隔離 | 15% |
| PostgreSQL 資料設計與 EF Core | 15% |
| API 設計、驗證與錯誤處理 | 10% |
| 程式結構與可維護性 | 10% |
| 自動化測試 | 7% |
| Docker、README 與啟動便利性 | 3% |

以下情況屬重大缺失：

- 明文儲存密碼。
- 可以存取或修改其他使用者的 Todo。
- 未實際使用 PostgreSQL。
- 專案無法依文件啟動。
- 未提供 Migration。
- 將 Secret 寫死於程式碼或提交真實憑證。



