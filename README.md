# DBHelper
.NET 使用正则表达式，xml解析，封装 Dapper 让SQL从代码迁移到xml文件上，并实现简单的逻辑处理

使用方法

1.配置连接字符串

  .NET 4.5 引用 DBHelper.dll
  在项目的config文件中添加连接字符串
  <connectionStrings>
      <add name="test_mysql" connectionString="Server =127.0.0.1; Database =backstage; Uid =root; Pwd =123456;Pooling=true; Max Pool Size=100;Min Pool Size=10;Allow Batch=true; Allow User Variables=True;Charset=utf8;SslMode=none" providerName="MySql.Data.MySqlClient" />
  </connectionStrings>
  这里的name="test_mysql"在后面的xml中会使用，如果有多个数据库连接直接添加对应的连接字符串即可。

  代码中添加连接字符串
  SQLHelperFactory.Instance.ConnectionStringsDic["test_mysql"]="";
  SQLHelperFactory.Instance.ConnectionStringsDic["test_mysql2"]="";

  .NET CORE 引用 CoreDBHelper.dll
  在appsettings.json中增加 
  "ConnectionStrings": {
      "test_mysql": "server=127.0.0.1;userid=root;pwd=123456;port=3306;database=backstage;sslmode=none;"
  }
  然后在Startup.cs的ConfigureServices中通过代码附加连接字符串
  SQLHelperFactory.Instance.ConnectionStringsDic["test_mysql"] = Configuration.GetConnectionString("test_mysql");


2.配置SQL语句
  在项目上增加文件夹SqlConfig,增加DemoSql.xml文件

  xml内容如下：
  <?xml version="1.0" encoding="utf-8" ?>
  <HCIP.AjaxDataSetting>
    <Data name="GetData">
      <SqlDefinition type="MySql" ConnStringName="test_mysql">
        <SqlCommand>
          <![CDATA[
        SELECT * FROM `sys_log`WHERE Table_Name=@@TableName@@ <%= AND Primary_Key=@@PrimaryKey@@ %><R%= AND Log_Type IN (@@LogType@@)%R>;
        ]]>
        </SqlCommand>
      </SqlDefinition>
    </Data>
  </HCIP.AjaxDataSetting>

  其中 <HCIP.AjaxDataSetting> 可以是任何名称 代表根节点
  <Data name="GetData"> 这里的GetData是使用的时候的KEY
  type="MySql" 指使用的数据库是 mysql 可以通过这个配置不同的数据库服务器 暂时支持mysql和sqlserver
  ConnStringName="test_mysql" 指使用的连接字符串是 test_mysql 通过这个可以配置不同的数据源
  @@TableName@@ 表示 解析sql时会找Dictionary 里面key为TableName的值按照参数化的方式赋值到sql
  <%= AND Primary_Key=@@PrimaryKey@@ %> 表示 解析sql时会找Dictionary 里面key为PrimaryKey的值，如果不存在则整段sql会被清空，如果存在则按照参数化的方式赋值到sql
  <R%= Log_Type IN (@@LogType@@)%R> 表示 解析sql时会找Dictionary 里面key为LogType的值，如果不存在则整段sql会被清空，如果存在则将值直接替换对应@@LogType@@

代码使用
  static void Main(string[] args)
  {
      Dictionary<string, object> dic = new Dictionary<string, object>();
      dic["TableName"] = "table1";
      var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
      Console.Read();
  }
  生成的sql：SELECT * FROM `sys_log`WHERE Table_Name=?TableName;

  static void Main(string[] args)
  {
      Dictionary<string, object> dic = new Dictionary<string, object>();
      dic["TableName"] = "table1";
      dic["PrimaryKey"] = "primarykey1";
      var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
      Console.Read();
  }
  生成的sql：SELECT * FROM `sys_log`WHERE Table_Name=?TableName AND Primary_Key=?PrimaryKey;

  static void Main(string[] args)
  {
      Dictionary<string, object> dic = new Dictionary<string, object>();
      dic["TableName"] = "table1";
      dic["PrimaryKey"] = "primarykey1";
      dic["LogType"] = "1,2,3,4";
      var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
      Console.Read();
  }
  生成的sql：SELECT * FROM `sys_log`WHERE Table_Name=?TableName  AND Primary_Key=?PrimaryKey  AND Log_Type IN (1,2,3,4);


接口说明
  /// <summary>
  /// 返回影响行数
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public int ExecuteNonQuery(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)；

  /// <summary>
  /// 返回一条数据
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public IDataReader ExecuteReader(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)


  /// <summary>
  /// 返回第一行第一列
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public object ExecuteScalar(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)

  /// <summary>
  /// 返回第一行第一列
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public T ExecuteScalarByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)

  /// <summary>
  /// 返回IEnumerable
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public List<dynamic> QueryForList(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)


  /// <summary>
  /// 返回IEnumerable<T>
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public List<T> QueryForListByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)


  /// <summary>
  /// 通过sql配置控制返回的类型，调用 QueryForListByT
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public List<object> QueryForLisByAssembly(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)


  /// <summary>
  /// 返回dynamic
  /// </summary>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public dynamic QueryForObject(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)

  /// <summary>
  /// 返回T
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public T QueryForObjectByT<T>(string sqlKey, Dictionary<string, object> paramDic, bool isUseTrans = false, int maxretry = MaxRetry)


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


  /// <summary>
  /// 返回多个结果集
  /// </summary>
  /// <typeparam name="T"></typeparam>
  /// <param name="sqlKey"></param>
  /// <param name="paramDic"></param>
  /// <param name="total"></param>
  /// <param name="isUseTrans"></param>
  /// <returns></returns>
  public IEnumerable<TReturn> QueryMultiple<TFirst, TSecond, TReturn>(string sqlKey, Dictionary<string, object> paramDic, Func<IEnumerable<TFirst>, IEnumerable<TSecond>, IEnumerable<TReturn>> func, bool isUseTrans = false, int maxretry = MaxRetry)        
        
        
        
具体参考 CoreDBDemo
        
        
        
        
        
        
        
