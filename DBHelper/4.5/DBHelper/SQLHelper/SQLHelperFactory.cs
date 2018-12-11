using DBHelper.Interface;
using DBHelper.Models;
using DBHelper.SQLAnalytical;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
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
        private List<string> RetryMessage = new List<string>() { "Unable to connect to any of the specified MySQL hosts.", "Too many connections" };
        /// <summary>
        /// 最大重试次数
        /// </summary>
        private const int MaxRetry = 3;
        /// <summary>
        /// 连接字符串缓存 使用非app.config配置时需要在程序入口处进行初始化
        /// </summary>
        public ConcurrentDictionary<string, string> ConnectionStringsDic = new ConcurrentDictionary<string, string>();

        private ISQLHelper GetSQLHelper(SqlAnalyModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SqlConnStringName))
            {
                throw new Exception(string.Format("数据库连接配置不正确"));
            }
            ISQLHelper sqlHelper = null;
            switch (model.DBType.ToLower())
            {
                case "mysql":
                    sqlHelper = new MySqlHelper();
                    break;
                case "sqlserver":
                    sqlHelper = new SqlServerHelper();
                    break;
                default:
                    throw new Exception("暂不支持" + model.DBType + "数据库");
            }
            if (!ConnectionStringsDic.ContainsKey(model.SqlConnStringName))
            {
                ConnectionStringsDic[model.SqlConnStringName] = System.Configuration.ConfigurationManager.ConnectionStrings[model.SqlConnStringName].ConnectionString;
            }

            sqlHelper.ConnectionString = ConnectionStringsDic[model.SqlConnStringName];
            if (string.IsNullOrWhiteSpace(sqlHelper.ConnectionString))
            {
                throw new Exception(string.Format("数据库连接地址【{0}】不正确", model.SqlConnStringName));
            }

            return sqlHelper;
        }
        /// <summary>
        /// 返回影响行数
        /// </summary>
        /// <param name="sqlKey"></param>
        /// <param name="paramDic"></param>
        /// <param name="isUseTrans"></param>
        /// <returns></returns>
        public int ExecuteNonQuery(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)
        {
            var sqlAnaly = CacheSqlConfig.Instance.GetSqlAnalyByKey(sqlKey, paramDic);
            //return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            try
            {
                return GetSQLHelper(sqlAnaly).ExecuteNonQuery(sqlAnaly.SqlText, CommandType.Text, paramDic, isUseTrans);
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteNonQuery(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteReader(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteScalar(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return ExecuteScalarByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForList(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForListByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForObject(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryForObjectByT<T>(sqlKey, paramDic, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultipleByPage<T>(sqlKey, paramDic, out total, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                if (maxretry > 0 && RetryMessage.Contains(ex.Message))
                {
                    return QueryMultiple(sqlKey, paramDic, func, isUseTrans, --maxretry);
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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

        #endregion
    }
}
