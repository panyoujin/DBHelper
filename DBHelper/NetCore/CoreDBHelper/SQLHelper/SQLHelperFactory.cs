using DBHelper.Interface;
using DBHelper.Models;
using DBHelper.SQLAnalytical;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace DBHelper.SQLHelper
{
    public class SQLHelperFactory
    {
        #region Instance

        private static readonly Lazy<SQLHelperFactory> _instance = new Lazy<SQLHelperFactory>(() => new SQLHelperFactory());

        private static readonly SqlHelperOptions _options = new SqlHelperOptions();

        /// <summary>
        /// 供宿主应用初始化 SQLHelper 的入口。
        /// 例如在 ASP.NET Core Startup 中调用：
        /// SQLHelperFactory.Configure(opt =&gt; { opt.DefaultConnectionString = "..."; });
        /// </summary>
        public static void Configure(Action<SqlHelperOptions> configure)
        {
            if (configure == null) return;
            configure(_options);

            // 同步部分配置到工厂实例（如已有实例已创建）。
            if (_instance.IsValueCreated)
            {
                _instance.Value.DefaultConnectionStrings = _options.DefaultConnectionString;
                if (_options.SqlLogWriter != null)
                {
                    _instance.Value.SqlLogWriter = _options.SqlLogWriter;
                    _instance.Value.EnableSqlLog = true;
                }
            }
        }

        private SQLHelperFactory()
        {
            DefaultConnectionStrings = _options.DefaultConnectionString;
            if (_options.SqlLogWriter != null)
            {
                SqlLogWriter = _options.SqlLogWriter;
                EnableSqlLog = true;
            }
        }

        public static SQLHelperFactory Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        #endregion

        private const int DefaultMaxRetry = 3;

        private static int EffectiveMaxRetry
        {
            get
            {
                return _options.MaxRetry <= 0 ? DefaultMaxRetry : _options.MaxRetry;
            }
        }

        /// <summary>
        /// 连接字符串缓存 使用非app.config配置时需要在程序入口处进行初始化
        /// </summary>
        public ConcurrentDictionary<string, string> ConnectionStringsDic = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// 默认连接字符串，当 XML 未指定连接串名称或找不到配置时使用。
        /// </summary>
        public string DefaultConnectionStrings;

        /// <summary>
        /// 是否启用 SQL 日志输出。
        /// </summary>
        public bool EnableSqlLog { get; set; }

        /// <summary>
        /// 自定义 SQL 日志委托。
        /// </summary>
        public Action<string, string, IDictionary<string, object>> SqlLogWriter { get; set; }

        /// <summary>
        /// 预览解析后的 SQL 及元数据。
        /// </summary>
        public SqlAnalyModel GetSqlAnalyModel(string sqlKey, Dictionary<string, object> paramDic = null)
        {
            return CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
        }

        /// <summary>
        /// 快速获取 SQL 文本。
        /// </summary>
        public string PreviewSql(string sqlKey, Dictionary<string, object> paramDic = null)
        {
            return GetSqlAnalyModel(sqlKey, paramDic).SqlText;
        }

        private SqlAnalyModel AnalyzeSql(string sqlKey, Dictionary<string, object> paramDic)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            LogSqlIfNeeded(sqlKey, sqlAnaly, paramDic);
            return sqlAnaly;
        }

        private SqlAnalyModel AnalyzeSql(string sqlKey, List<Dictionary<string, object>> paramDic)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            LogSqlIfNeeded(sqlKey, sqlAnaly, paramDic?.FirstOrDefault());
            return sqlAnaly;
        }

        private void LogSqlIfNeeded(string sqlKey, SqlAnalyModel analy, IDictionary<string, object> paramDic)
        {
            if ((!EnableSqlLog && SqlLogWriter == null) || analy == null)
            {
                return;
            }
            var snapshot = paramDic != null ? paramDic.ToDictionary(k => k.Key, v => v.Value) : new Dictionary<string, object>();
            var writer = SqlLogWriter ?? DefaultSqlLogWriter;
            writer(sqlKey, analy.SqlText, snapshot);
        }

        private void DefaultSqlLogWriter(string sqlKey, string sql, IDictionary<string, object> parameters)
        {
            Trace.WriteLine($"[DBHelper] Key:{sqlKey} Sql:{sql}");
        }

        private ISQLHelper GetSQLHelper(SqlAnalyModel model)
        {
            if (model == null)
            {
                throw new Exception(string.Format("数据库连接配置不正确"));
            }

            ISQLHelper sqlHelper = null;
            if (string.IsNullOrWhiteSpace(model.DBType))
            {
                model.DBType = "mysql";
            }
            sqlHelper = new AllHelper();
            //switch (model.DBType.ToLower())
            //{
            //    case "mysql":
            //        sqlHelper = new MySqlHelper();
            //        break;
            //    case "sqlserver":
            //        sqlHelper = new SqlServerHelper();
            //        break;
            //    default:
            //        sqlHelper = new AllHelper();
            //        break;
            //}
            sqlHelper.DBType = model.DBType;
            if (!string.IsNullOrWhiteSpace(model.SqlConnStringName))
            {
                // 优先使用外部注入的解析器，其次使用内部缓存与配置文件。
                if (_options.ConnectionStringResolver != null)
                {
                    sqlHelper.ConnectionString = _options.ConnectionStringResolver(model.SqlConnStringName);
                }
                else
                {
                    if (!ConnectionStringsDic.ContainsKey(model.SqlConnStringName))
                    {
                        var config = System.Configuration.ConfigurationManager.ConnectionStrings[model.SqlConnStringName];
                        if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
                        {
                            throw new InvalidOperationException($"未找到名称为 {model.SqlConnStringName} 的连接字符串配置。");
                        }
                        ConnectionStringsDic[model.SqlConnStringName] = config.ConnectionString;
                    }

                    sqlHelper.ConnectionString = ConnectionStringsDic[model.SqlConnStringName];
                }
            }
            if (string.IsNullOrWhiteSpace(sqlHelper.ConnectionString))
            {
                var fallback = DefaultConnectionStrings ?? _options.DefaultConnectionString;
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    throw new InvalidOperationException("数据库连接配置不正确，既未找到命名连接串，也未配置默认连接串。");
                }
                sqlHelper.ConnectionString = fallback;
            }

            if (model.Timeout >= 0)
            {
                sqlHelper.Timeout = model.Timeout;
            }
            return sqlHelper;
        }

        public string ConnectionString(string sqlKey, Dictionary<string, object> dictParam)
        {
            var connectionStr = string.Empty;
            var model = AnalyzeSql(sqlKey, dictParam);
            if (!string.IsNullOrWhiteSpace(model.SqlConnStringName))
            {
                if (_options.ConnectionStringResolver != null)
                {
                    connectionStr = _options.ConnectionStringResolver(model.SqlConnStringName);
                }
                else
                {
                    if (!ConnectionStringsDic.ContainsKey(model.SqlConnStringName))
                    {
                        var config = System.Configuration.ConfigurationManager.ConnectionStrings[model.SqlConnStringName];
                        if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
                        {
                            throw new InvalidOperationException($"未找到名称为 {model.SqlConnStringName} 的连接字符串配置。");
                        }
                        ConnectionStringsDic[model.SqlConnStringName] = config.ConnectionString;
                    }

                    connectionStr = ConnectionStringsDic[model.SqlConnStringName];
                }
            }
            if (string.IsNullOrWhiteSpace(connectionStr))
            {
                var fallback = DefaultConnectionStrings ?? _options.DefaultConnectionString;
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    throw new InvalidOperationException("数据库连接配置不正确，既未找到命名连接串，也未配置默认连接串。");
                }
                connectionStr = fallback;
            }
            return connectionStr;
        }

        #region 外部控制链接的开启和事务的提交
        /// <summary>
        /// 执行单条语句：外部控制链接的开启和事务的提交
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="trans"></param>
        /// <param name="sqlKey"></param>
        /// <param name="dictParam"></param>
        /// <returns></returns>
        public int ExecuteNonQuery(DbConnection conn, DbTransaction trans, string sqlKey, Dictionary<string, object> dictParam = null)
        {
            try
            {
                var sqlAnaly = AnalyzeSql(sqlKey, dictParam);
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(conn, trans, sqlAnaly.SqlText, CommandType.Text, dictParam);
            }
            catch (Exception)
            {
                throw;
            }
        }
        /// <summary>
        /// 批量语句：外部控制链接的开启和事务的提交
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="trans"></param>
        /// <param name="sqlKey"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public int ExecuteNonQuery(DbConnection conn, DbTransaction trans, string sqlKey, List<Dictionary<string, object>> dictParams)
        {
            try
            {
                var sqlAnaly = AnalyzeSql(sqlKey, dictParams);
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(conn, trans, sqlAnaly.SqlText, CommandType.Text, dictParams);
            }
            catch (Exception)
            {
                throw;
            }
        }
        #endregion

        #region 事务封装：内部管理连接的开启与事务的提交/回滚
        /// <summary>
        /// 按数据库类型创建连接
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="dbType">数据库类型：mysql（默认）/sqlserver</param>
        /// <returns></returns>
        private DbConnection CreateTransactionConnection(string connectionString, string dbType)
        {
            switch ((dbType ?? "mysql").ToLower())
            {
                case "sqlserver":
                    return new System.Data.SqlClient.SqlConnection(connectionString);
                case "postgresql":
                case "postgres":
                case "pgsql":
                    return new Npgsql.NpgsqlConnection(connectionString);
                default:
                    return new MySqlConnector.MySqlConnection(connectionString);
            }
        }
        /// <summary>
        /// 事务执行：内部创建连接并管理提交/回滚，业务代码在 action 中定义。
        /// action 正常返回则提交；抛出异常则回滚并原样抛出。
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="dbType">数据库类型：mysql（默认）/sqlserver</param>
        /// <param name="action">业务代码：通过 ExecuteNonQuery(conn, trans, sqlKey, ...) 在同一事务中执行多个 SQL key</param>
        public void ExecuteInTransaction(string connectionString, string dbType, Action<DbConnection, DbTransaction> action)
        {
            ExecuteInTransaction<object>(connectionString, dbType, (conn, trans) =>
            {
                action(conn, trans);
                return null;
            });
        }
        /// <summary>
        /// 事务执行并返回结果：内部创建连接并管理提交/回滚，业务代码在 func 中定义。
        /// func 正常返回则提交并返回结果；抛出异常则回滚并原样抛出。
        /// </summary>
        /// <typeparam name="T">返回值类型</typeparam>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="dbType">数据库类型：mysql（默认）/sqlserver</param>
        /// <param name="func">业务代码：通过 ExecuteNonQuery(conn, trans, sqlKey, ...) 在同一事务中执行多个 SQL key</param>
        /// <returns>func 的返回值</returns>
        public T ExecuteInTransaction<T>(string connectionString, string dbType, Func<DbConnection, DbTransaction, T> func)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException("连接字符串不能为空", nameof(connectionString));
            }
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func));
            }
            using (DbConnection conn = CreateTransactionConnection(connectionString, dbType))
            {
                conn.Open();
                using (DbTransaction trans = conn.BeginTransaction())
                {
                    try
                    {
                        T result = func(conn, trans);
                        trans.Commit();
                        return result;
                    }
                    catch (Exception)
                    {
                        //回滚失败（如连接已断开）时吞掉回滚自身的异常，保留原始业务异常
                        try { trans.Rollback(); } catch (Exception) { }
                        throw;
                    }
                }
            }
        }
        #endregion

        /// <summary>
        /// 返回影响行数
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public int ExecuteNonQuery(string sqlKey, Dictionary<string, object> paramDic = null, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return ExecuteNonQuery(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 执行批量语句，当SQL里出现直接替换语句时不适用 
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns>返回影响行数</returns>
        public int ExecuteNonQuery(string sqlKey, List<Dictionary<string, object>> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic?.FirstOrDefault(), ref maxretry))
                {
                    return ExecuteNonQuery(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic?.FirstOrDefault());
                throw;
            }
        }
        /// <summary>
        /// 返回一条数据
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IDataReader ExecuteReader(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteReader(sqlAnaly.SqlText, CommandType.Text, paramDic);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteReader(sqlAnaly.SqlText, CommandType.Text, paramDic);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return ExecuteReader(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 返回第一行第一列
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public object ExecuteScalar(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteScalar(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteScalar(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return ExecuteScalar(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 返回第一行第一列
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public T ExecuteScalarByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteScalar<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteScalar<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return ExecuteScalarByT<T>(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }

        /// <summary>
        /// 返回IEnumerable
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public List<dynamic> QueryForList(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            try
            {
                var list = GetSQLHelper(sqlAnaly).QueryForList(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);

                if (list == null)
                {
                    return null;
                }
                return list.ToList();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryForList(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 返回IEnumerable<T>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public List<T> QueryForListByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            try
            {
                var list = GetSQLHelper(sqlAnaly).QueryForList<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
                if (list == null)
                {
                    return null;
                }
                return list.ToList();
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryForListByT<T>(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 通过sql配置控制返回的类型，调用 QueryForListByT
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        private static readonly ConcurrentDictionary<string, Type> _typeCache = new ConcurrentDictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<Type, MethodInfo> _queryForListByTCache = new ConcurrentDictionary<Type, MethodInfo>();

        public List<object> QueryForLisByAssembly(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = AnalyzeSql(sqlKey, paramDic);
            if (!string.IsNullOrWhiteSpace(sqlAnaly.ModelClassName))
            {
                Type t = null;
                if (!string.IsNullOrWhiteSpace(sqlAnaly.Assembly))
                {
                    try
                    {
                        var key = sqlAnaly.Assembly + "|" + sqlAnaly.ModelClassName;
                        t = _typeCache.GetOrAdd(key, _ =>
                        {
                            var assemblyPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, sqlAnaly.Assembly);
                            var serviceDll = Assembly.LoadFrom(assemblyPath);
                            return serviceDll.GetType(sqlAnaly.ModelClassName);
                        });
                    }
                    catch
                    {

                    }
                }
                if (t == null)
                {
                    t = _typeCache.GetOrAdd(sqlAnaly.ModelClassName, name => Assembly.GetExecutingAssembly().GetType(name));
                }
                if (t != null)
                {
                    var genericMethod = _queryForListByTCache.GetOrAdd(t, tp =>
                    {
                        var method = GetType().GetMethod("QueryForListByT");
                        return method.MakeGenericMethod(tp);
                    });

                    var to_list = genericMethod.Invoke(this, new object[] { sqlKey, paramDic, isUseTrans });
                    if (to_list != null)
                    {
                        var list = to_list as IEnumerable<object>;
                        return list.ToList();
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 返回dynamic
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public dynamic QueryForObject(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            if (paramDic == null)
            {
                paramDic = new Dictionary<string, object>();
            }
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryForObject(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryForObject(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 返回T
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public T QueryForObjectByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryForObject<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryForObjectByT<T>(sqlKey, paramDic, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 返回结果集和数量 专为分页功能而准备  数据集的sql在前面，返回数量的在后面
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="total"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IEnumerable<T> QueryMultipleByPage<T>(string sqlKey, Dictionary<string, object> paramDic, out int total, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultipleByPage<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, out total, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultipleByPage<T>(sqlKey, paramDic, out total, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }

        /// <summary>
        /// 返回2个结果集
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="total"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }


        /// <summary>
        /// 返回3个结果集
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="total"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }


        /// <summary>
        /// 返回4个结果集
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="total"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }

        /// <summary>
        /// 返回5个结果集
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="total"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        #region Test
        /// <summary>
        /// Test Connection
        /// </summary>
        /// <returns></returns>
        public bool TestConnection(string sqlKey, Dictionary<string, object> paramDic)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            return GetSQLHelper(sqlAnaly).TestConnection();
        }

        /// <summary>
        /// Test Connection
        /// </summary>
        /// <returns></returns>
        public bool TestConnection(string connStringName = "", string dbType = "mysql")
        {
            ISQLHelper sqlHelper = null;
            switch (dbType.ToLower())
            {
                case "mysql":
                    sqlHelper = new MySqlHelper();
                    break;
                case "sqlserver":
                    sqlHelper = new SqlServerHelper();
                    break;
                case "postgresql":
                case "postgres":
                case "pgsql":
                    //主分发路径 GetSQLHelper 统一走 AllHelper，按 DBType 创建对应连接
                    sqlHelper = new AllHelper();
                    sqlHelper.DBType = "postgresql";
                    break;
                default:
                    sqlHelper = new MySqlHelper();
                    break;
            }
            if (!string.IsNullOrWhiteSpace(connStringName))
            {
                if (_options.ConnectionStringResolver != null)
                {
                    sqlHelper.ConnectionString = _options.ConnectionStringResolver(connStringName);
                }
                else
                {
                    if (!ConnectionStringsDic.ContainsKey(connStringName))
                    {
                        var config = System.Configuration.ConfigurationManager.ConnectionStrings[connStringName];
                        if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
                        {
                            throw new InvalidOperationException($"未找到名称为 {connStringName} 的连接字符串配置。");
                        }
                        ConnectionStringsDic[connStringName] = config.ConnectionString;
                    }
                    sqlHelper.ConnectionString = ConnectionStringsDic[connStringName];
                }
            }
            else
            {
                var fallback = DefaultConnectionStrings ?? _options.DefaultConnectionString;
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    throw new InvalidOperationException("数据库连接配置不正确，既未找到命名连接串，也未配置默认连接串。");
                }
                sqlHelper.ConnectionString = fallback;
            }
            return sqlHelper.TestConnection();
        }
        #endregion



        /// <summary>
        /// 基于C# 8.0 语法 返回结果集和数量 专用于分页
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="maxretry"></param>
        /// <returns></returns>
        public (IEnumerable<T>, int) QueryMultipleByPage<T>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultipleByPage<T>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultipleByPage<T>(sqlKey, paramDic, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 基于C# 8.0 语法 返回2个结果集 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="maxretry"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>) QueryMultiple<TFirst, TSecond>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple<TFirst, TSecond>(sqlKey, paramDic, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 基于C# 8.0 语法 返回3个结果集 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="maxretry"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>) QueryMultiple<TFirst, TSecond, TThird>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple<TFirst, TSecond, TThird>(sqlKey, paramDic, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 基于C# 8.0 语法 返回4个结果集 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <typeparam name="TFourth"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="maxretry"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>) QueryMultiple<TFirst, TSecond, TThird, TFourth>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird, TFourth>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple<TFirst, TSecond, TThird, TFourth>(sqlKey, paramDic, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }
        /// <summary>
        /// 基于C# 8.0 语法 返回5个结果集 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <typeparam name="TFourth"></typeparam>
        /// <typeparam name="TFifth"></typeparam>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="maxretry"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>) QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = DefaultMaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (HandleTransientException(ex, sqlKey, paramDic, ref maxretry))
                {
                    return QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(sqlKey, paramDic, maxretry);
                }
                throw;
            }
            catch (Exception ex)
            {
                OnError(ex, sqlKey, paramDic);
                throw;
            }
        }

        /// <summary>
        /// 内部处理可重试异常的统一入口。
        /// </summary>
        private static bool HandleTransientException(Exception ex, string sqlKey, IDictionary<string, object> param, ref int maxretry)
        {
            OnError(ex, sqlKey, param);

            var max = EffectiveMaxRetry;
            if (maxretry <= 0)
            {
                maxretry = max;
            }

            bool isTransient = false;
            if (_options.TransientErrorDetector != null)
            {
                isTransient = _options.TransientErrorDetector(ex);
            }

            if (!isTransient)
            {
                return false;
            }

            if (maxretry <= 0)
            {
                return false;
            }

            maxretry--;
            return maxretry >= 0;
        }

        private static void OnError(Exception ex, string sqlKey, IDictionary<string, object> param)
        {
            try
            {
                _options.OnError?.Invoke(ex, sqlKey, param ?? new Dictionary<string, object>());
            }
            catch
            {
                // 日志回调不影响主流程
            }
        }
    }
}
