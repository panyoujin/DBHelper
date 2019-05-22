using Dapper;
using DBHelper.Interface;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;

namespace DBHelper.SQLHelper
{
    /// <summary>
    /// 
    /// </summary>
    public class MySqlHelper : ISQLHelper
    {
        #region Fields

        private string _connectionString;
        public string ConnectionString { get => _connectionString; set => _connectionString = value; }

        //private static readonly BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;

        #endregion

        #region ExecuteNonQuery

        /// <summary>
        /// 执行sql命令，返回影响行数 (启用事务)
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="dictParams">sql命令的参数数组（可为空）</param>
        /// <returns></returns>
        public int ExecuteNonQuery(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.Execute(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 执行sql命令，返回影响行数 
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="dictParams">sql命令的参数数组（可为空）</param>
        /// <param name="isUseTrans">是否启用事务</param>
        /// <returns></returns>
        public int ExecuteNonQuery(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return ExecuteNonQuery(sqlText, cmdType, dictParams);
            }
            var result = 0;
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.Execute(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }

        #endregion

        #region ExecuteScalar

        /// <summary>
        /// 执行sql命令，返回第一行第一列（启用事务）
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="sqlParams">sql命令参数 （可为空）</param>
        /// <returns></returns>
        public object ExecuteScalar(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.ExecuteScalar(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        /// <summary>
        /// 执行sql命令，返回第一行第一列（启用事务）
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="sqlParams">sql命令参数 （可为空）</param>
        /// <returns></returns>
        public T ExecuteScalar<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.ExecuteScalar<T>(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        /// <summary>
        /// 执行sql命令，返回第一行第一列
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="sqlParams">sql命令参数 （可为空）</param>
        /// <param name="isUseTrans">是否使用事务</param>
        /// <returns></returns>
        public object ExecuteScalar(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return ExecuteScalar(sqlText, cmdType, dictParams);
            }
            object result = null;
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.ExecuteScalar(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }
        /// <summary>
        /// 执行sql命令，返回第一行第一列
        /// </summary>
        /// <param name="strSqlConn">数据库连接字符串</param>
        /// <param name="sqlText">数据库命令：存储过程名或sql语句</param>
        /// <param name="cmdType">命令类型</param>
        /// <param name="sqlParams">sql命令参数 （可为空）</param>
        /// <param name="isUseTrans">是否使用事务</param>
        /// <returns></returns>
        public T ExecuteScalar<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return ExecuteScalar<T>(sqlText, cmdType, dictParams);
            }
            T t = default(T);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    t = conn.ExecuteScalar<T>(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return t;
        }
        #endregion

        #region ExecuteReader


        public IDataReader ExecuteReader(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {

            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.ExecuteReader(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        #endregion ExecuteReader

        #region Query


        public IEnumerable<dynamic> QueryForList(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.Query(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        public IEnumerable<dynamic> QueryForList(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return QueryForList(sqlText, cmdType, dictParams);
            }
            IEnumerable<dynamic> result = default(IEnumerable<dynamic>);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.Query(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }

        public IEnumerable<T> QueryForList<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.Query<T>(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        public IEnumerable<T> QueryForList<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return QueryForList<T>(sqlText, cmdType, dictParams);
            }

            IEnumerable<T> result = default(IEnumerable<T>);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.Query<T>(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }

        public dynamic QueryForObject(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.QueryFirstOrDefault(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        public dynamic QueryForObject(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return QueryForObject(sqlText, cmdType, dictParams);
            }

            dynamic result = default(dynamic);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.QueryFirstOrDefault(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }

        public T QueryForObject<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    return conn.QueryFirstOrDefault<T>(sqlText, dictParams);
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        public T QueryForObject<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return QueryForObject<T>(sqlText, cmdType, dictParams);
            }

            T result = default(T);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    result = conn.QueryFirstOrDefault<T>(sqlText, dictParams, trans);
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return result;
        }




        public IEnumerable<T> QueryMultiple<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, out int total)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);

                    var list = result.Read<T>();
                    total = result.ReadFirst<int>();
                    return list;
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        public IEnumerable<T> QueryMultipleByPage<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, out int total, bool isUseTrans)
        {
            if (!isUseTrans)
            {
                return QueryMultiple<T>(sqlText, cmdType, dictParams, out total);
            }

            IEnumerable<T> list = default(IEnumerable<T>);
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                conn.Open();
                MySqlTransaction trans = conn.BeginTransaction();
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams, trans);
                    list = result.Read<T>();
                    total = result.ReadFirst<int>();
                    trans.Commit();
                }
                catch (Exception ex)
                {
                    trans.Rollback();
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
            return list;
        }


        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TReturn>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TReturn>> func, bool isUseTrans)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return func(result.Read<TFirst>(), result.Read<TSecond>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }

        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TReturn>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TReturn>> func, bool isUseTrans)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return func(result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TReturn>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TReturn>> func, bool isUseTrans)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return func(result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>(), result.Read<TFourth>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth, TReturn>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>, IEnumerable<TReturn>> func, bool isUseTrans)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return func(result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>(), result.Read<TFourth>(), result.Read<TFifth>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }


        #endregion Query



        #region Test
        /// <summary>
        /// Test Connection
        /// </summary>
        /// <returns></returns>
        public bool TestConnection()
        {
            try
            {
                using (MySqlConnection conn = new MySqlConnection(ConnectionString))
                {
                    conn.Open();
                    conn.Close();
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
            return true;
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sqlText"></param>
        /// <param name="cmdType"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public (IEnumerable<T>, int) QueryMultipleByPage<T>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return (result.Read<T>(), result.ReadFirst<int>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <param name="sqlText"></param>
        /// <param name="cmdType"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>) QueryMultiple<TFirst, TSecond>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return (result.Read<TFirst>(), result.Read<TSecond>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <param name="sqlText"></param>
        /// <param name="cmdType"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>) QueryMultiple<TFirst, TSecond, TThird>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return (result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <typeparam name="TFourth"></typeparam>
        /// <param name="sqlText"></param>
        /// <param name="cmdType"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>) QueryMultiple<TFirst, TSecond, TThird, TFourth>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return (result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>(), result.Read<TFourth>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="TFirst"></typeparam>
        /// <typeparam name="TSecond"></typeparam>
        /// <typeparam name="TThird"></typeparam>
        /// <typeparam name="TFourth"></typeparam>
        /// <typeparam name="TFifth"></typeparam>
        /// <param name="sqlText"></param>
        /// <param name="cmdType"></param>
        /// <param name="dictParams"></param>
        /// <returns></returns>
        public (IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TThird>, IEnumerable<TFourth>, IEnumerable<TFifth>) QueryMultiple<TFirst, TSecond, TThird, TFourth, TFifth>(string sqlText, CommandType cmdType, IDictionary<string, object> dictParams)
        {
            using (MySqlConnection conn = new MySqlConnection(ConnectionString))
            {
                try
                {
                    var result = conn.QueryMultiple(sqlText, dictParams);
                    return (result.Read<TFirst>(), result.Read<TSecond>(), result.Read<TThird>(), result.Read<TFourth>(), result.Read<TFifth>());
                }
                catch (Exception ex)
                {
                    ex.Source = ex.Source + sqlText;
                    throw ex;
                }
            }
        }



    }
}
