namespace DBHelper.SQLAnalytical
{
    /// <summary>
    /// 显式声明为可信的原始 SQL 片段。
    /// </summary>
    public sealed class SqlLiteral
    {
        private SqlLiteral(string value)
        {
            Value = value ?? string.Empty;
        }

        public string Value { get; }

        public static SqlLiteral From(string value)
        {
            return new SqlLiteral(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}

