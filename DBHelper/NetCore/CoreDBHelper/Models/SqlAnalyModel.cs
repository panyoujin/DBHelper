namespace DBHelper.Models
{
    public class SqlAnalyModel
    {
        /// <summary>
        /// 数据库类型
        /// </summary>
        public string DBType { get; set; }
        /// <summary>
        /// 连接字符串名称
        /// </summary>
        public string SqlConnStringName { get; set; }
        /// <summary>
        /// SQL
        /// </summary>
        public string SqlText { get; set; }
        /// <summary>
        /// Assembly
        /// </summary>
        public string Assembly { get; set; }
        /// <summary>
        /// ModelClassName
        /// </summary>
        public string ModelClassName { get; set; }
    }
}
