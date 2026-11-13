using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Data.Common;

namespace DBDemo
{
    class Program
    {
        static bool IsStart = true;
        static void Main(string[] args)
        {
            try
            {
                while (true)
                {
                    try
                    {
                        Console.Write("请输入需要添加的值，多个值直接用英文,分割,输入Q/q退出:");
                        var s = Console.ReadLine();
                        if (string.IsNullOrWhiteSpace(s))
                        {
                            continue;
                        }
                        if ((s == "q" || s == "Q"))
                        {
                            break;
                        }
                        var ids = s.Split(',');
                        List<Dictionary<string, object>> dics = new List<Dictionary<string, object>>();
                        foreach (var id in ids)
                        {
                            dics.Add(new Dictionary<string, object>
                            {
                                ["cid"] = id,
                                ["name"] = "a" + id,
                                ["sort"] = 0
                            });
                        }
                        var connStr = SQLHelperFactory.Instance.ConnectionString("Insert2", null);
                        SQLHelperFactory.Instance.ExecuteInTransaction(connStr, "mariadb", (conn, trans) =>
                        {
                            var i = SQLHelperFactory.Instance.ExecuteNonQuery(conn, trans, "Insert2", dics);
                        });
                        var menuList = SQLHelperFactory.Instance.QueryForList("GetMenu", null);
                        Console.WriteLine($"当前数量：{menuList.Count}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
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

        public class area
        {
            public string AreaId { get; set; }
            public string AreaName { get; set; }
        }
        public class user
        {
            public string UserId { get; set; }
            public string UserAccount { get; set; }
            public List<area> AreaList { get; set; }
        }
    }
}

