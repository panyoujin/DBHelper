using DBHelper.SQLHelper;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DBDemo
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var list = SQLHelperFactory.Instance.QueryForList("GetData", null);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        Console.WriteLine(string.Format("{0}:{1}", item.ID, item.NAME));
                    }
                }
            }
            catch (Exception ex)
            {

            }
            Console.Read();
        }
    }
}
