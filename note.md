在根目錄下，我們主要分成了兩個大資料夾：
  - TodoList.Api：真正的程式碼。
  - TodoList.Tests：測試程式碼。

  下面我們拆解 TodoList.Api 內部各個資料夾的作用：

    TodoList.Api/
    ├── Models/        
    ├── Data/          
    ├── Migrations/    
    ├── DTOs/          
    ├── Controllers/   
    ├── Services/      
    └── Middleware/    

  ### 1. Models 
  - User.cs 寫明了：一個會員必須有 ID、Email、密碼雜湊、顯示名稱。
  - Todo.cs 寫明了：一個待辦事項必須有標題、描述、狀態（pending 等）、擁有者 ID。

  ### 2. Data

  - TodoDbContext.cs 是程式與資料庫之間的橋樑。
  - 它是唯一有權限打開資料庫、把資料塞進去或拿出來的。

  ### 3. Migrations (

  - 當我們在 Models 修改了規格時，需要去資料庫改建表格。
  - 這裡面的檔案記錄了步驟（例如：第一步建 Users 表，第二步建 Todos 表）。當系統啟動時，會自動照著這些圖紙建好資料庫。

  ### 4. DTOs (申辦表單與收據範本)

  當客人來辦理業務時，他們填寫的表單格式，以及辦理完後經理給客人的收據格式。

  - 客人登入填的表單格式：Auth.cs。
  - 客人的待辦事項明細收據格式：Todo.cs。

  ### 5. Controllers 


  - AuthController.cs：負責幫客人辦理註冊、核對密碼登入。
  - TodoController.cs：負責處理待辦事項的增刪改查。

  ### 6. Services 

  - TokenService.cs 像是會館內印製「感應會員卡（JWT）」的機器。當會員中心確認身分後，會請警衛蓋章印製這張感應卡給客人。

  ### 7. Middleware 

  - ExceptionHandlingMiddleware.cs 
  - 所有客人的請求在進去櫃檯前，安檢門會先檢查有沒有帶卡；如果櫃檯裡面發生了意外（程式當機），應變小組會負責出面，給客人一個標準而禮貌的說明，而不是讓客人看到凌亂
  的系統崩潰畫面（不暴露 stack trace 敏感資訊）。
  ──────
  ## 系統運作串接流程範例

  當客人發出一個請求：「我想新增一筆待辦事項：『買牛奶』」，這條生產線是這樣串接運作的：

    graph TD
        Guest[1. 客人發送請求] -->|攜帶會員卡與買牛奶表單| Middleware[2. 大門安檢門 Middleware]
        Middleware -->|檢查會員卡有效| TodoController[3. 寄物櫃檯 TodoController]
        TodoController -->|檢查表單欄位是否正確| Validate[4. 表單驗證 DTO]
        Validate -->|欄位正確, 建立物品| TodoDbContext[5. 保險庫管家 TodoDbContext]
        TodoDbContext -->|寫入保險庫| DB[(6. PostgreSQL 資料庫)]
        TodoDbContext -->|回傳成功| TodoController
        TodoController -->|印製收據 TodoResponse| Guest

  1. 大門安檢門（Middleware） 攔截請求，檢查客人有沒有帶會員卡（JWT）。確認卡片合法且在效期內，並從卡片感應出客人的身分是「張三」，然後放行。
  2. 寄物櫃檯（TodoController） 接過客人的表單（DTO 中的 CreateTodoRequest）。櫃檯人員先用尺量一量，確認標題長度在 1~100 個字內，沒有寫錯。
  3. 櫃檯人員將資料轉換成保險庫懂的物品規格（Models 中的 Todo），並在大管家（TodoDbContext）的帳本上記下一筆「張三新增了買牛奶事項」。
  4. 大管家（TodoDbContext） 將這個帳目記錄寫進 PostgreSQL 資料庫（保險庫）中保存。
  5. 寫入成功後，櫃檯人員開出一張精美的收據（DTO 中的 TodoResponse）遞回給客人，大功告成！
  ──────
  ## 🧪 測試專案 (TodoList.Tests) 又是怎麼串接的？

  當我們執行 dotnet test 時：

  1. CustomWebApplicationFactory.cs**
  會在記憶體中偷偷蓋一個「微型的臨時會館」**，這個臨時會館大體上和主會館一模一樣，但是它的保險庫大管家改用一個超快速、用完就丟的臨時保險庫（SQLite
  記憶體資料庫）。
  2.
  **IntegrationTests.cs**（神秘客檢測隊）會直接對這個臨時會館發送各種註冊、登入、寄放、偽裝偷東西的請求，驗證大門安檢、櫃檯人員、大管家是不是都各司其職、毫無漏洞
  。
