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

            string connectionString = "Data Source=database.db;Version=3;"; // 資料庫連線字串

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
            using (var connection = new SQLiteConnection(connectionString))
            {
                try
                {
                    connection.Open(); // 開啟資料庫連線

                    // 創建員工資料表格
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
                    var allEmployees = connection.Query("SELECT id, name, salary, managerId FROM Employees").ToList();

                    // 顯示查詢結果，以表格樣式格式化
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    Console.WriteLine("| id  | name  | salary | managerId |");
                    Console.WriteLine("+-----+-------+--------+-----------+");
                    foreach (var employee in allEmployees)
                    {
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
                    string reportFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Untitled.frx");

                    // 檢查報表模板檔案是否存在
                    if (!File.Exists(reportFilePath))
                    {
                        Console.WriteLine("報表模板檔案未找到！");
                        return;
                    }

                    try
                    {


                        Report report = new Report();
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

                        // 準備報表
                        report.Prepare();

                        // 設定 PDF 輸出路徑
                        string outputDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
                        if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);

                        // 設定 PDF 輸出檔案名稱
                        string pdfFilePath = Path.Combine(outputDirectory, "EmployeeReport.pdf");

                        // 創建 PDF 匯出器
                        PDFSimpleExport pdfExport = new PDFSimpleExport();

                        // 將報表匯出為 PDF
                        report.Export(pdfExport, pdfFilePath);
                        Console.WriteLine($"報表已成功生成並保存在: {pdfFilePath}");

                        report.Dispose();  // 釋放報表資源
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"報表處理錯誤: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"資料庫操作錯誤: {ex.Message}");
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
            return ExecuteQuery(connection, query);
        }

        // 執行查詢並返回 DataTable
        static DataTable ExecuteQuery(SQLiteConnection connection, string query)
        {
            var dataTable = new DataTable();
            using (var command = new SQLiteCommand(query, connection))
            using (var dataAdapter = new SQLiteDataAdapter(command))
            {
                dataAdapter.Fill(dataTable);
            }
            return dataTable;
        }
    }
}
