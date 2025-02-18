namespace DBDemo
{
    /// <summary>
    /// 員工資料
    /// </summary>
    public class Employee
    {
        /// <summary>
        /// 員工編號
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 員工名字
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 員工薪水
        /// </summary>
        public int Salary { get; set; }
        /// <summary>
        /// 主管編號
        /// </summary>
        public int? ManagerId { get; set; }
    }
}