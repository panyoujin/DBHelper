using DBHelper.SQLHelper;
using System;

namespace CoreDBDemo
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
                Console.WriteLine(ex.Message);
            }
            Console.Read();
        }
    }
}
