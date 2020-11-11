using DBHelper.Helper;
using DBHelper.Utilities;
using System.Text.RegularExpressions;
using System.Xml;

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
        /// 增加父key
        /// </summary>
        public string ParentKey { get; set; }
        /// <summary>
        /// 父key
        /// </summary>
        public SqlAnalyModel ParentAnaly { get; set; }
        /// <summary>
        /// ModelClassName
        /// </summary>
        public string ModelClassName { get; set; }
        /// <summary>
        /// 可空
        /// </summary>
        public MatchCollection CanEmptyMC { get; set; }
        /// <summary>
        /// 直接替换字符串
        /// </summary>
        public MatchCollection ReplaceMC { get; set; }
        /// <summary>
        /// 直接使用参数
        /// </summary>
        public MatchCollection ParamMC { get; set; }
        /// <summary>
        /// 超时设置
        /// </summary>
        public int Timeout { get; set; } = -1;


        public static SqlAnalyModel XmlToSqlAnalyModel(XmlNode node)
        {
            SqlAnalyModel model = new SqlAnalyModel();
            model.SqlText = XmlUtility.getNodeStringValue(node["SqlCommand"]);

            model.DBType = XmlUtility.getNodeAttributeStringValue(node, "type");
            model.SqlConnStringName = XmlUtility.getNodeAttributeStringValue(node, "ConnStringName", ConfigHelper.GetConfigValue("ConnStringName", "DbContext"));
            model.Assembly = XmlUtility.getNodeAttributeStringValue(node, "Assembly");
            model.ModelClassName = XmlUtility.getNodeAttributeStringValue(node, "ModelClassName");
            model.ParentKey = XmlUtility.getNodeAttributeStringValue(node, "ParentKey");
            if (!string.IsNullOrWhiteSpace(model.ParentKey))
            {
                model.ParentKey = model.ParentKey.ToLower();
            }
            if (!string.IsNullOrWhiteSpace(XmlUtility.getNodeAttributeStringValue(node, "Timeout")))
            {
                model.Timeout = XmlUtility.getNodeAttributeIntValue(node, "Timeout");
            }
            var sqltext = XmlUtility.getNodeStringValue(node["SqlCommand"]);
            Regex regKeyword = new Regex("<%=.*?%>");
            model.CanEmptyMC = regKeyword.Matches(sqltext);
            foreach (Match c in model.CanEmptyMC)
            {
                string matchingSql = c.ToString();
                sqltext = sqltext.Replace(matchingSql, "");
            }
            regKeyword = new Regex("<R%=.*?%R>");
            model.ReplaceMC = regKeyword.Matches(sqltext);
            foreach (Match c in model.ReplaceMC)
            {
                string matchingSql = c.ToString();
                sqltext = sqltext.Replace(matchingSql, "");
            }

            regKeyword = new Regex("@@.*?@@");
            model.ParamMC = regKeyword.Matches(sqltext);
            foreach (Match c in model.ParamMC)
            {
                string matchingSql = c.ToString();
                sqltext = sqltext.Replace(matchingSql, "");
            }
            return model;
        }
    }
}
