using System;
using System.Text.RegularExpressions;

namespace DBHelper.SQLAnalytical
{
    /// <summary>
    /// 显式声明为可信的 SQL 片段，用于绕过默认的原始替换白名单校验。
    /// 仅限用于表名、字段名等完全由代码控制的标识符。
    /// </summary>
    public class SqlLiteral
    {
        public string Value { get; private set; }
        private SqlLiteral() { }
        public static SqlLiteral From(string trustedSql)
        {
            if (string.IsNullOrWhiteSpace(trustedSql))
            {
                throw new ArgumentException("SqlLiteral 不能为空。");
            }
            return new SqlLiteral { Value = trustedSql };
        }
    }

    /// <summary>
    /// 全局 SQL 安全策略，可根据业务自行替换默认实现（与 NetCore 版行为保持一致）。
    /// </summary>
    public static class SqlSecurityOptions
    {
        private static readonly Regex DefaultRawSqlRegex = new Regex("^[A-Za-z0-9_,\\.]+$", RegexOptions.Compiled);

        /// <summary>
        /// 原始 SQL 片段解析委托，默认仅允许数字、字母、下划线、逗号、空格和点，或显式的 SqlLiteral。
        /// 设为自定义委托即可替换默认白名单策略。
        /// </summary>
        public static Func<string, object, string> RawSqlResolver { get; set; }

        internal static string ResolveRawValue(string key, object value)
        {
            if (RawSqlResolver != null)
            {
                return RawSqlResolver(key, value);
            }
            return DefaultRawSqlResolver(key, value);
        }

        private static string DefaultRawSqlResolver(string key, object value)
        {
            //值缺失时保持旧行为：返回空串，由 ParseItem 的 allEmpty 逻辑删除整个块
            if (value == null)
            {
                return string.Empty;
            }
            SqlLiteral literal = value as SqlLiteral;
            if (literal != null)
            {
                return literal.Value;
            }
            string raw = value.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }
            if (!DefaultRawSqlRegex.IsMatch(raw))
            {
                throw new InvalidOperationException(string.Format(
                    "Raw SQL 参数 {0} 含有非法字符，请改用 SqlLiteral 包装或调整 RawSqlResolver。", key));
            }
            return raw;
        }
    }
}
