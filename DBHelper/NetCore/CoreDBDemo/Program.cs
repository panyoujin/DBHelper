using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Threading;

namespace CoreDBDemo
{
    class Program
    {
        static bool IsStart = true;
        static void Main(string[] args)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["TableName"] = "table1";
            dic["PrimaryKey"] = "primarykey1";
            dic["LogType"] = "1,2,3,4";
            var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
            
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
