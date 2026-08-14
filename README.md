# 多人 To-do List API

本專案使用 C# 與 ASP.NET Core 實作一套多人待辦事項（To-do List）的 Web API，整合 Entity Framework Core、PostgreSQL，並完整支援 JWT 認證、資料隔離、自動化測試以及 Docker 容器化建置。

---

## 目錄

- [系統需求](#系統需求)
- [啟動與停止方式](#啟動與停止方式)
- [環境變數設定](#環境變數設定)
- [資料庫 Migration 策略與競態處理](#資料庫-migration-策略與競態處理)
- [執行自動化測試](#執行自動化測試)
- [重要設計決策](#重要設計決策)
- [API 端點說明](#api-端點說明)

---

## 系統需求

- 已安裝 **Docker** 與 **Docker Compose**
- （選用）本地執行與測試需要 **.NET SDK 8.0** 以上版本

---

## 啟動與停止方式

### 1. 啟動服務

於專案根目錄下執行以下指令，系統會自動編譯 API 專案並啟動 PostgreSQL 資料庫：

```bash
docker compose up --build
```

服務啟動後，將提供以下兩個容器：
1. **API 服務**：監聽於 `http://localhost:8080`
2. **PostgreSQL 服務**：健康狀態檢查通過後才允許 API 啟動

> **Swagger 說明文件**：請在啟動後瀏覽 `http://localhost:8080/swagger` 進行 API 互動測試。

### 2. 停止服務

停止並移除容器：

```bash
docker compose down
```

若欲連同資料庫 Volume 一併清除（重設資料庫）：

```bash
docker compose down -v
```

---

## 環境變數與安全設定

本專案採用環境變數注入敏感設定（例如資料庫密碼與 JWT 密鑰），程式碼與 `appsettings.json` 中均無硬編碼任何真實金鑰。

### 1. 本地開發與 Docker Compose (.env)
我們已於專案根目錄下建立了 `.env` 檔案與範本檔 [`.env.example`](/to-do-list-test/.env.example)。
Docker Compose (`compose.yaml`) 將自動加載 `.env` 內定義的變數並注入至容器中：
- `DB_PASSWORD`：PostgreSQL 資料庫密碼。
- `JWT_SECRET`：JWT 簽發金鑰（長度須大於 32 字元以確保安全強度）。
- `JWT_ISSUER` 與 `JWT_AUDIENCE`：JWT 簽發者與接收者識別標章。

> **安全性提示**：`.env` 檔案內含敏感憑證，已加入 [`.gitignore`](/to-do-list-test/.gitignore) 中，切勿將其提交至公開版本控制庫。

---

## 資料庫 Migration 策略與競態處理

### 本地與容器啟動策略
- 本專案採用 EF Core Code-First 方式管理資料庫 Schema，Migrations 檔案已放置於 `TodoList.Api/Migrations`。
- 在 `Program.cs` 中，系統啟動時會自動偵測是否有未套用的 Migration，並於 API 啟動前自動執行 `db.Database.MigrateAsync()`，實現資料庫 Schema 的自動初始化。

### 正式環境中的競態問題與解決方案
在多個 API 實例（Replicas）同時啟動的正式環境（如 Kubernetes、AWS ECS）中，多個節點同時執行 `MigrateAsync()` 會引發資料庫鎖競爭或重複建立 Schema 的競態問題。
**避免競態的正式環境策略**：
1. **CI/CD 階段執行**：於部署流程（Pipeline）中，透過專用任務（如 GitHub Action、GitLab CI）提取 EF Core 遷移 SQL，或獨立執行一次性的資料庫更新，更新成功後再滾動部署 API 服務。
2. **Init Containers (初始化容器)**：在 Kubernetes 中，可將 Migration 包裝成 Init Container 執行。Init Container 在 Pod 開啟時只會執行一次，執行成功後主 API Container 才會啟動。
3. **分布式鎖**：若必須在代碼中執行，可使用分布式鎖（如 Redis Lock 或 PostgreSQL Pg_advisory_xact_lock）確保在多節點環境中，同時只有一個 Instance 執行資料庫 Migration。

---

## 執行自動化測試

本專案包含一組整合測試（Integration Tests），完整驗證了 HTTP 管道、身分驗證、輸入欄位驗證、分頁與篩選、以及關鍵的**使用者資料隔離**。

本地測試不需要 PostgreSQL 容器，系統將在記憶體中自動初始化一個獨立的 SQLite Database 進行測試，保證測試快速且乾淨。

### 執行測試指令

#### 方法 A：若本機已安裝 .NET SDK 8.0
請於專案根目錄下執行：
```bash
dotnet test
```

#### 方法 B：使用 Docker 執行測試（免安裝 .NET SDK）

由於本專案採用較新的 `.slnx` 方案檔格式，Docker 的 `.NET 8.0 SDK` 容器無法直接識別它，因此執行測試時需要明確指定測試專案路徑 `TodoList.Tests/TodoList.Tests.csproj`：

- **Windows (PowerShell)**:
  ```powershell
  docker run --rm -v "$pwd:/source" -w /source mcr.microsoft.com/dotnet/sdk:8.0 dotnet test TodoList.Tests/TodoList.Tests.csproj
  ```
- **Windows (Command Prompt / CMD)**:
  ```cmd
  docker run --rm -v "%cd%:/source" -w /source mcr.microsoft.com/dotnet/sdk:8.0 dotnet test TodoList.Tests/TodoList.Tests.csproj
  ```
- **macOS / Linux / Git Bash**:
  ```bash
  MSYS_NO_PATHCONV=1 docker run --rm -v "$(pwd):/source" -w /source mcr.microsoft.com/dotnet/sdk:8.0 dotnet test TodoList.Tests/TodoList.Tests.csproj
  ```

---

## 重要設計決策

### 1. 使用者資料隔離（Data Isolation）
- 權限控管遵循「最小權限原則」。所有對單筆 Todo 進行 `GET`、`PATCH`、`DELETE` 的操作，後端皆使用目前登入者的 `UserId`（從 JWT Claim 提取）與 `Id` 進行聯合查詢。
- **防止資源探針（404 Not Found）**：如果查詢的 Todo 不存在或不屬於該登入使用者，系統一律回傳 `404 Not Found`。如此可防止惡意使用者透過遞增 ID 來探測或枚舉其他使用者的待辦事項是否存在。

### 2. 欄位存在感應與部分更新（PATCH 設計）
- API 規格書中的 `PATCH` 請求支援對指定欄位進行部分更新。為了區分「欄位未傳遞（不更新）」與「欄位傳遞為 null（清空資料，如 `DueDate`）」，本專案在 `UpdateTodoRequest` 的屬性 setter 中實作了欄位存在追蹤（Presence Tracking）。
- 此設計完美契合了 RESTful PATCH 語意，能有效區分 optional 欄位未傳入與傳入 `null` 的本質差別，且直接支援 ASP.NET Core 的 `IValidatableObject` 自動欄位校驗。

### 3. 大小寫無感之 Email 唯一性
- 專案在 `TodoDbContext` 中針對不同資料庫進行優化：
  - **PostgreSQL (正式環境)**：啟用 `citext` 延伸模組，將 `Email` 欄位類型設為 `citext`。這是 PostgreSQL 原生最推薦的無感大小寫唯一索引設定。
  - **SQLite (測試環境)**：設定欄位定序為 `NOCASE`。
- 此設定保證了註冊時 Email 的唯一性是由資料庫 constraint 100% 強制保證，完美排除併發註冊時的程式端繞過風險。

---

## API 端點說明

| 端點 | 方法 | 認證 | 說明 |
| :--- | :--- | :---: | :--- |
| `/api/auth/register` | `POST` | 免 | 註冊新帳號。密碼須大於 8 字元。 |
| `/api/auth/login` | `POST` | 免 | 登入並取得 JWT Bearer Token。 |
| `/api/todos` | `POST` | 需 | 建立一筆待辦事項。Status 預設為 `pending`。 |
| `/api/todos` | `GET` | 需 | 取得目前使用者的待辦事項列表。支援 status, priority 篩選與關鍵字搜尋、排序與分頁。 |
| `/api/todos/{id}` | `GET` | 需 | 取得單筆待辦事項詳細內容。 |
| `/api/todos/{id}` | `PATCH`| 需 | 部分更新待辦事項欄位。 |
| `/api/todos/{id}` | `DELETE`| 需 | 刪除單筆待辦事項。 |
| `/health` | `GET` | 免 | 健康檢查端點，回傳 API 與資料庫是否連線正常。 |
