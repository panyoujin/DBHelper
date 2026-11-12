using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace DBHelper.SQLHelper
{
    /// <summary>
    /// SQLHelperFactory 的全局配置项。
    /// 通过 <see cref="SQLHelperFactory.Configure(Action{SqlHelperOptions})"/> 进行初始化。
    /// </summary>
    public class SqlHelperOptions
    {
        /// <summary>
        /// 默认连接字符串，当 XML 未指定或无法解析时使用。
        /// </summary>
        public string DefaultConnectionString { get; set; }

        /// <summary>
        /// 最大重试次数，小于等于 0 时表示使用内部默认值。
        /// </summary>
        public int MaxRetry { get; set; } = 3;

        /// <summary>
        /// 自定义重试判定逻辑，返回 true 表示可以重试。
        /// </summary>
        public Func<Exception, bool> TransientErrorDetector { get; set; }

        /// <summary>
        /// 自定义连接字符串解析委托。
        /// 参数为 (连接串名称)，返回真正的连接字符串。
        /// </summary>
        public Func<string, string> ConnectionStringResolver { get; set; }

        /// <summary>
        /// SQL 日志委托：sqlKey、SQL 文本、参数。
        /// 若设置，则会覆盖 <see cref="SQLHelperFactory.SqlLogWriter"/>。
        /// 可在业务层内包装 ILogger。
        /// </summary>
        public Action<string, string, IDictionary<string, object>> SqlLogWriter { get; set; }

        /// <summary>
        /// SQL 执行异常回调：异常、sqlKey、参数。
        /// </summary>
        public Action<Exception, string, IDictionary<string, object>> OnError { get; set; }
    }
}


