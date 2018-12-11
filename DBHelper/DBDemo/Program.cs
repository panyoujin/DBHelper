using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace DBDemo
{
    class Program
    {
        static bool IsStart = true;
        static void Main(string[] args)
        {
            try
            {
                Dictionary<string, object> dic = new Dictionary<string, object>();
                var ulist = SQLHelperFactory.Instance.QueryMultiple<area, user, user>("GetData", dic, (a, u) =>
                  {
                      foreach (user item in u)
                      {
                          item.AreaList = a.ToList();
                      }
                      return u;
                  });
                var t = 1;
            }
            catch (Exception ex)
            {

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

