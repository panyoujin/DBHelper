using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DBDemo
{
    class Program
    {
        static bool IsStart = true;
        static void Main(string[] args)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            dic["ID"] = 1;
            dic["Start"] = 0;
            dic["End"] = 1;
            var list = SQLHelperFactory.Instance.QueryForList("GetData", dic);
            Thread t4 = new Thread(() =>
            {
                while (IsStart)
                {
                    Thread.Sleep(1000 * 5);
                    IsStart = false;
                }
                Console.WriteLine("完成");
            });
            t4.Start();
            for (var i = 0; i < 999; i++)
            {
                Thread t = new Thread((s) =>
                {
                    Query(s);
                });
                t.Start(i);
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
    }
}
