using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DBHelper.SQLAnalytical
{
    /// <summary>
    /// 全局 SQL 安全策略，可根据业务自行替换默认实现。
    /// </summary>
    public static class SqlSecurityOptions
    {
        private static readonly Regex DefaultRawSqlRegex = new Regex(@"^[A-Za-z0-9_,\s\.]+$", RegexOptions.Compiled);
        private static readonly string[] DangerousTokens = { "--", ";--", "/*", "*/", "xp_", "exec(", " execute ", " drop ", " alter ", " truncate " };

        /// <summary>
        /// 参数清洗委托，默认实现针对字符串做简单注入检测。
        /// </summary>
        public static Func<string, object, object> ParameterSanitizer { get; set; } = DefaultParameterSanitizer;

        /// <summary>
        /// 原始 SQL 片段解析委托，默认仅允许数字、字母、下划线、逗号、空格和点，或显式的 SqlLiteral。
        /// </summary>
        public static Func<string, object, string> RawSqlResolver { get; set; } = DefaultRawSqlResolver;

        internal static object SanitizeParameter(string key, object value)
        {
            return ParameterSanitizer?.Invoke(key, value);
        }

        internal static string ResolveRawValue(string key, object value)
        {
            if (RawSqlResolver == null)
            {
                throw new InvalidOperationException("RawSqlResolver 未配置。");
            }
            return RawSqlResolver(key, value);
        }

        private static object DefaultParameterSanitizer(string key, object value)
        {
            if (value is string strValue)
            {
                foreach (var token in DangerousTokens)
                {
                    if (strValue.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throw new InvalidOperationException($"参数 {key} 包含潜在危险片段: {token}");
                    }
                }
                return strValue;
            }
            if (value is IEnumerable<object> enumerable)
            {
                var list = new List<object>();
                foreach (var item in enumerable)
                {
                    list.Add(DefaultParameterSanitizer(key, item));
                }
                return list;
            }
            return value;
        }

        private static string DefaultRawSqlResolver(string key, object value)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"Raw SQL 参数 {key} 不能为空。");
            }
            if (value is SqlLiteral literal)
            {
                return literal.Value;
            }
            var raw = value.ToString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException($"Raw SQL 参数 {key} 不能为空。");
            }
            if (!DefaultRawSqlRegex.IsMatch(raw))
            {
                throw new InvalidOperationException($"Raw SQL 参数 {key} 含有非法字符，请改用 SqlLiteral 包装或调整 RawSqlResolver。");
            }
            return raw;
        }
    }
}

