using DBHelper.Extend;
using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;

namespace CoreDBDemo
{
    class Program
    {
        static bool IsStart = true;
        static void Main(string[] args)
        {
            var ids = "18,19".Split(',');
            List<Dictionary<string, object>> dics = new List<Dictionary<string, object>>();
            foreach (var id in ids)
            {
                dics.Add(new Dictionary<string, object>
                {
                    ["cid"] = id,
                    ["name"] = "a" + id,
                    ["sort"] = id
                });
            }
            using (var conn = new MySql.Data.MySqlClient.MySqlConnection(SQLHelperFactory.Instance.ConnectionString("Insert2", null)))
            {
                conn.Open();
                DbTransaction trans = conn.BeginTransaction();
                var i = SQLHelperFactory.Instance.ExecuteNonQuery(conn, null, "Insert2", dics);
                trans.Commit();
            }
            var list = SQLHelperFactory.Instance.QueryForList("GetMenu", null);
            //var i = SQLHelperFactory.Instance.ExecuteNonQuery("Insert2", dics);
            Console.Read();
        }
        static void Query(object index)
        {
            while (IsStart)
            {
                try
                {
                    Dictionary<string, object> dic = new Dictionary<string, object>();
                    dic["ID"] = index;
                    dic["Start"] = 0;
                    dic["End"] = 1;
                    var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            Console.WriteLine(string.Format("{0} - {1}", index, item.ID));
                        }

                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(string.Format("{0} - {1}", index, ex.Message));
                }
                Thread.Sleep(10);
            }
        }
    }
}
