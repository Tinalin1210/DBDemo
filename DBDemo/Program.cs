using System; // 基本的 .NET 類庫
using System.Data.SQLite; // 用來操作 SQLite 數據庫
using System.IO; // 文件和資料夾操作
using Dapper; // 簡化 SQL 查詢
using System.Linq; // Linq 用於對集合進行查詢操作
using FastReport.Export.PdfSimple; // 用來將報表導出為 PDF 的 FastReport 功能
using FastReport; // FastReport 用來創建和管理報表的庫
using System.Text; // 用於字串操作和編碼
using System.Data;  // 用來處理數據庫和資料表的基本資料結構

namespace DBDemo
{
    public class Program
    {
        static void Main(string[] args)
        {
            // connectionString：SQLite 連線字串，表示將連接名為 database.db ，Version=3 是 SQLite 的版本。
            string connectionString = "Data Source=database.db;Version=3;";

            // 檢查資料庫檔案是否存在，若不存在則創建資料庫
            if (!File.Exists("./database.db"))
            {
                SQLiteConnection.CreateFile("database.db");  // 創建資料庫檔案
                Console.WriteLine("資料庫檔案不存在，已經創建資料庫");
            }
            else
            {
                Console.WriteLine("資料庫檔案已存在");
            }
            Console.WriteLine($"資料庫檔案位置: {Path.GetFullPath("database.db")}");

            // 開啟與資料庫的連線
            //使用 using 語法，確保使用完 SQLite 連線後會自動釋放資源。
            using (var connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open(); // 開啟資料庫連線

                    // 創建員工資料表格  @ 不需要 \n 來換行。
                    string createTableQuery = @"
                        CREATE TABLE IF NOT EXISTS Employees (
                            id INTEGER PRIMARY KEY AUTOINCREMENT,  -- id 欄位，自動遞增
                            name TEXT NOT NULL,                    -- name 欄位，不允許為 NULL
                            salary INTEGER NOT NULL,               -- salary 欄位，不允許為 NULL
                            managerId INTEGER                      -- managerId 欄位，允許為 NULL
                        );
                    ";

                    // 執行創建表格的 SQL 指令
                    connection.Execute(createTableQuery);
                    Console.WriteLine("表格創建成功。");

                    // 插入測試資料
                    string insertDataQuery = @"
                        INSERT OR REPLACE INTO Employees (id, name, salary, managerId) VALUES (1, 'Joe', 70000, 3);
                        INSERT OR REPLACE INTO Employees (id, name, salary, managerId) VALUES (2, 'Henry', 80000, 4);
                        INSERT OR REPLACE INTO Employees (id, name, salary, managerId) VALUES (3, 'Sam', 60000, NULL);
                        INSERT OR REPLACE INTO Employees (id, name, salary, managerId) VALUES (4, 'Max', 90000, NULL);
                    ";
                    connection.Execute(insertDataQuery);

                    Console.WriteLine("資料插入成功。");

                    // 查詢所有員工資料並顯示
                    var allEmployees = connection.Query("SELECT id, name, salary, managerId FROM Employees").ToList();  //Query：查詢資料，轉成 List。

                    // 顯示查詢結果，以表格樣式格式化
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    Console.WriteLine("| id  | name  | salary | managerId |");
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    foreach (var employee in allEmployees)  
                    {
                        //負數 ：左對齊  正數：右對齊
                        //employee.managerId 可能為 NULL，所以用 ?. 來確保不會發生 空指標異常
                        //managerId 不為 NULL，則 ToString() 轉成字串。managerId 為 NULL，則?.ToString() 會返回 NULL。
                        Console.WriteLine($"| {employee.id,-4} | {employee.name,-5} | {employee.salary,-6} | {employee.managerId?.ToString() ?? "NULL",-9} |");  
                    }
                    Console.WriteLine("+-----+-------+--------+-----------+");

                    // 查詢非管理職員工 (managerId 不等於 NULL)
                    var nonManagerEmployees = connection.Query(@"
                        SELECT id, name, salary, managerId 
                        FROM Employees 
                        WHERE managerId IS NOT NULL").ToList();

                    Console.WriteLine("\n非管理職員工：");
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    Console.WriteLine("| id  | name  | salary | managerId |");
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    foreach (var employee in nonManagerEmployees)
                    {
                        Console.WriteLine($"| {employee.id,-4} | {employee.name,-5} | {employee.salary,-6} | {employee.managerId,-9} |");
                    }
                    Console.WriteLine("+-----+-------+--------+-----------+");

                    // 查詢薪資高於其主管的非管理職員工
                    var employeesAboveManager = connection.Query(@"
                        SELECT e1.name AS Employee 
                        FROM Employees e1 
                        JOIN Employees e2 ON e1.managerId = e2.id
                        WHERE e1.salary > e2.salary").ToList();

                    Console.WriteLine("\n薪資高於主管的非管理職員工：");
                    Console.WriteLine("+----------+");
                    Console.WriteLine("| Employee |");
                    Console.WriteLine("+----------+");
                    foreach (var employee in employeesAboveManager)
                    {
                        Console.WriteLine($"| {employee.Employee,-8} |");
                    }
                    Console.WriteLine("+----------+");

                    // 動態設定報表檔案路徑
                    string reportFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Untitled.frx");//完整路徑

                    // 檢查報表模板檔案是否存在
                    if (!File.Exists(reportFilePath))
                    {
                        Console.WriteLine("報表模板檔案未找到！");
                        return;
                    }

                    try
                    {

                        Report report = new Report();  //創建一個 Report 物件，該物件來自 FastReport，負責處理報表。
                        report.Load(reportFilePath); // 載入報表檔案

                        // 取得有主管(managerId)的員工
                        DataTable employeesWithManagerTable = GetEmployeesWithManager(connection);
                        // 取得薪水高於主管的員工
                        DataTable employeesWithGoodSalaryTable = GetEmployeesWithGoodSalary(connection);

                        // 註冊資料源
                        report.RegisterData(employeesWithManagerTable, "employeesWithManager");
                        report.RegisterData(employeesWithGoodSalaryTable, "employeesWithGoodSalary");

                        // 啟用資料源
                        report.GetDataSource("employeesWithManager").Enabled = true;
                        report.GetDataSource("employeesWithGoodSalary").Enabled = true;

                        // 資料帶與資料源的綁定
                        ((DataBand)report.Report.FindObject("Data1")).DataSource = report.GetDataSource("employeesWithManager");
                        ((DataBand)report.Report.FindObject("Data2")).DataSource = report.GetDataSource("employeesWithGoodSalary");

                        // 準備報表，這會生成報表的內容，並且準備好進行匯出。
                        report.Prepare();

                        // Path.Combine(AppDomain.CurrentDomain.BaseDirectory)：生成輸出資料夾的完整路徑，並確保該資料夾存在。如果資料夾不存在，則創建它。
                        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
                        if (!Directory.Exists(outputDirectory)) //如果目錄不存在，則返回 false。 
                        {
                            Directory.CreateDirectory(outputDirectory);   //目錄不存在，就創建它
                        }
                        //設定 PDF 檔案名稱和儲存路徑
                        string pdfFilePath = Path.Combine(outputDirectory, "EmployeeReport.pdf");

                        // PDFSimpleExport物件是用來將報表轉換並匯出為 PDF 格式的工具。
                        PDFSimpleExport pdfExport = new PDFSimpleExport();

                        // 將報表匯出為 PDF
                        report.Export(pdfExport, pdfFilePath);
                        Console.WriteLine($"報表已成功生成並保存在: {pdfFilePath}");

                        report.Dispose();  // 釋放報表資源
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"報表處理錯誤: {ex}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"資料庫操作錯誤: {ex}");
                }
            }
        }

        // 取得有主管(managerId)的員工
        static DataTable GetEmployeesWithManager(SQLiteConnection connection)
        {
            string query = "SELECT id, name, salary, managerId FROM Employees WHERE managerId IS NOT NULL";
            return ExecuteQuery(connection, query);
        }

        // 取得薪水高於主管的員工
        static DataTable GetEmployeesWithGoodSalary(SQLiteConnection connection)
        {
            string query = @"
                SELECT e1.name AS Employee 
                FROM Employees e1 
                JOIN Employees e2 ON e1.managerId = e2.id
                WHERE e1.salary > e2.salary";

            //返回一個 DataTable，包含所有薪水高於其主管薪水的員工
            return ExecuteQuery(connection, query);
        }

        // 執行查詢並返回 DataTable
        static DataTable ExecuteQuery(SQLiteConnection connection, string query)
        {
            var dataTable = new DataTable();
            //SQLiteCommand：用來執行 SQL 查詢，並傳入資料庫連線和查詢語句。
            //SQLiteDataAdapter：用來 DataTable，這會將查詢結果載入到 DataTable 中。
            using (var command = new SQLiteCommand(query, connection))
            using (var dataAdapter = new SQLiteDataAdapter(command))
            {
                //dataAdapter.Fill(dataTable) 會執行 SQL 查詢，並將查詢的結果存入 dataTable 物件中。
                dataAdapter.Fill(dataTable);
            }
            return dataTable;
        }
    }
}
