using DBHelper.Interface;
using DBHelper.Models;
using DBHelper.SQLAnalytical;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;

namespace DBHelper.SQLHelper
{
    public class SQLHelperFactory
    {
        #region Instance

        private static readonly Lazy<SQLHelperFactory> _instance = new Lazy<SQLHelperFactory>(() => new SQLHelperFactory());
        private SQLHelperFactory()
        {

        }

        public static SQLHelperFactory Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        #endregion
        /// <summary>
        /// 重试的错误信息
        /// </summary>
        private List<string> RetryMessage = new List<string>() { "Unable to connect to any of the specified MySQL hosts.", "Too many connections", "error connecting: Timeout expired.  The timeout period elapsed prior to obtaining a connection from the pool.  This may have occurred because all pooled connections were in use and max pool size was reached." };
        /// <summary>
        /// 最大重试次数
        /// </summary>
        private const int MaxRetry = 3;
        /// <summary>
        /// 连接字符串缓存 使用非app.config配置时需要在程序入口处进行初始化
        /// </summary>
        public ConcurrentDictionary<string, string> ConnectionStringsDic = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// 连接字符串缓存 使用非app.config配置时需要在程序入口处进行初始化
        /// </summary>
        public string DefaultConnectionStrings;

        /// <summary>
        /// 获取指定 SQL KEY 所配置的数据库连接字符串（用于外部创建连接、开启事务）
        /// </summary>
        /// <param name="sqlKey">SQL 配置的 KEY</param>
        /// <param name="dictParam">参数字典（含 ConnectionPrefix 分库前缀等特殊 key 时需传入）</param>
        public string ConnectionString(string sqlKey, Dictionary<string, object> dictParam = null)
        {
            var model = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, dictParam ?? new Dictionary<string, object>());
            var connectionStr = string.Empty;
            if (!string.IsNullOrWhiteSpace(model.SqlConnStringName))
            {
                if (!ConnectionStringsDic.ContainsKey(model.SqlConnStringName))
                {
                    var config = System.Configuration.ConfigurationManager.ConnectionStrings[model.SqlConnStringName];
                    if (config == null || string.IsNullOrWhiteSpace(config.ConnectionString))
                    {
                        throw new InvalidOperationException(string.Format("未找到名称为 {0} 的连接字符串配置。", model.SqlConnStringName));
                    }
                    ConnectionStringsDic[model.SqlConnStringName] = config.ConnectionString;
                }
                connectionStr = ConnectionStringsDic[model.SqlConnStringName];
            }
            if (string.IsNullOrWhiteSpace(connectionStr))
            {
                if (string.IsNullOrWhiteSpace(DefaultConnectionStrings))
                {
                    throw new InvalidOperationException("数据库连接配置不正确，既未找到命名连接串，也未配置默认连接串。");
                }
                connectionStr = DefaultConnectionStrings;
            }
            return connectionStr;
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
            switch (model.DBType.ToLower())
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
                    sqlHelper = new NpgsqlHelper();
                    break;
                default:
                    sqlHelper = new MySqlHelper();
                    break;
            }
            if (!string.IsNullOrWhiteSpace(model.SqlConnStringName))
            {
                if (!ConnectionStringsDic.ContainsKey(model.SqlConnStringName))
                {
                    ConnectionStringsDic[model.SqlConnStringName] = System.Configuration.ConfigurationManager.ConnectionStrings[model.SqlConnStringName].ConnectionString;
                }

                sqlHelper.ConnectionString = ConnectionStringsDic[model.SqlConnStringName];
            }
            if (string.IsNullOrWhiteSpace(sqlHelper.ConnectionString))
            {
                if (string.IsNullOrWhiteSpace(DefaultConnectionStrings))
                {
                    throw new Exception(string.Format("数据库连接配置不正确"));
                }
                sqlHelper.ConnectionString = DefaultConnectionStrings;
            }

            if (model.Timeout >= 0)
            {
                sqlHelper.Timeout = model.Timeout;
            }
            return sqlHelper;
        }


        #region 外部控制链接的开启和事务的提交
        /// <summary>
        /// 执行单条语句：外部控制链接的开启和事务的提交
        /// </summary>
        /// <param name="conn">数据库连接：必须在外部进行开启</param>
        /// <param name="trans">外部事务</param>
        /// <param name="sqlKey">SQL配置的KEY</param>
        /// <param name="dictParam">参数（可为空）</param>
        /// <returns></returns>
        public int ExecuteNonQuery(DbConnection conn, DbTransaction trans, string sqlKey, Dictionary<string, object> dictParam = null)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, dictParam);
            return GetSQLHelper(sqlAnaly).ExecuteNonQuery(conn, trans, sqlAnaly.SqlText, CommandType.Text, dictParam);
        }
        /// <summary>
        /// 批量语句：外部控制链接的开启和事务的提交
        /// </summary>
        /// <param name="conn">数据库连接：必须在外部进行开启</param>
        /// <param name="trans">外部事务</param>
        /// <param name="sqlKey">SQL配置的KEY</param>
        /// <param name="dictParams">参数列表，每组参数执行一次</param>
        /// <returns></returns>
        public int ExecuteNonQuery(DbConnection conn, DbTransaction trans, string sqlKey, List<Dictionary<string, object>> dictParams)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, dictParams);
            return GetSQLHelper(sqlAnaly).ExecuteNonQuery(conn, trans, sqlAnaly.SqlText, CommandType.Text, dictParams);
        }
        #endregion

        #region 事务封装：内部管理连接的开启与事务的提交/回滚
        /// <summary>
        /// 按数据库类型创建连接
        /// </summary>
        /// <param name="connectionString">连接字符串</param>
        /// <param name="dbType">数据库类型：mysql（默认）/sqlserver/postgresql</param>
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
            ExecuteInTransaction<object>(connectionString, dbType, delegate(DbConnection conn, DbTransaction trans)
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
                throw new ArgumentException("连接字符串不能为空", "connectionString");
            }
            if (func == null)
            {
                throw new ArgumentNullException("func");
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
        public int ExecuteNonQuery(string sqlKey, Dictionary<string, object> paramDic = null, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteNonQuery(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public int ExecuteNonQuery(string sqlKey, List<Dictionary<string, object>> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteNonQuery(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IDataReader ExecuteReader(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteReader(sqlAnaly.SqlText, CommandType.Text, paramDic);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteReader(sqlAnaly.SqlText, CommandType.Text, paramDic);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteReader(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public object ExecuteScalar(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteScalar(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteScalar(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteScalar(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public T ExecuteScalarByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteScalar<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteScalar<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteScalarByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public List<dynamic> QueryForList(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
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
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForList(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public List<T> QueryForListByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
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
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForListByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public List<object> QueryForLisByAssembly(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            if (!string.IsNullOrWhiteSpace(sqlAnaly.ModelClassName))
            {
                Type t = null;
                if (!string.IsNullOrWhiteSpace(sqlAnaly.Assembly))
                {
                    try
                    {
                        var serviceDll = Assembly.LoadFrom(AppDomain.CurrentDomain.BaseDirectory + "/" + sqlAnaly.Assembly);
                        t = serviceDll.GetType(sqlAnaly.ModelClassName);//获得类型
                    }
                    catch
                    {

                    }
                }
                if (t == null)
                {
                    t = Assembly.GetExecutingAssembly().GetType(sqlAnaly.ModelClassName);
                }
                if (t != null)
                {
                    MethodInfo MethodToList = this.GetType().GetMethod("QueryForListByT");
                    if (MethodToList != null)
                    {
                        var to_list = MethodToList.MakeGenericMethod(new Type[] { t }).Invoke(this, new object[] { sqlKey, paramDic, isUseTrans });
                        if (to_list != null)
                        {
                            var list = to_list as IEnumerable<object>;
                            return list.ToList();
                        }
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
        public dynamic QueryForObject(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryForObject(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForObject(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public T QueryForObjectByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryForObject<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForObjectByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IEnumerable<T> QueryMultipleByPage<T>(string sqlKey, Dictionary<string, object> paramDic, out int total, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultipleByPage<T>(sqlAnaly.SqlText, CommandType.Text, paramDic, out total, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultipleByPage<T>(sqlKey, paramDic, out total, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple(sqlAnaly.SqlText, CommandType.Text, paramDic, func, isUseTrans);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
                default:
                    sqlHelper = new MySqlHelper();
                    break;
            }
            if (!string.IsNullOrWhiteSpace(connStringName) && !ConnectionStringsDic.ContainsKey(connStringName))
            {
                ConnectionStringsDic[connStringName] = System.Configuration.ConfigurationManager.ConnectionStrings[connStringName].ConnectionString;
            }
            if (string.IsNullOrWhiteSpace(connStringName))
            {
                sqlHelper.ConnectionString = DefaultConnectionStrings;
            }
            else
            {
                sqlHelper.ConnectionString = ConnectionStringsDic[connStringName];
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
        public (IEnumerable<T>, int) QueryMultipleByPage<T>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultipleByPage<T>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultipleByPage<T>(sqlKey, paramDic, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public (IEnumerable<TFirst>, IEnumerable<TSecond>) QueryMultiple<TFirst, TSecond>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple<TFirst, TSecond>(sqlKey, paramDic, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>) QueryMultiple<TFirst, TSecond, TThird>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple<TFirst, TSecond, TThird>(sqlKey, paramDic, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>) QueryMultiple<TFirst, TSecond, TThird, TFourth>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird, TFourth>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple<TFirst, TSecond, TThird, TFourth>(sqlKey, paramDic, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
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
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>) QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(string sqlKey, Dictionary<string, object> paramDic, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            try
            {
                var t = GetSQLHelper(sqlAnaly).QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(sqlAnaly.SqlText, CommandType.Text, paramDic);
                return t;
            }
            catch (MySqlConnector.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(sqlKey, paramDic, --maxretry);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
