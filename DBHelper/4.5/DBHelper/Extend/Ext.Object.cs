using System;
using System.Collections.Generic;
using System.Reflection;

namespace DBHelper.Extend
{
    public static partial class Ext
    {

        private static readonly BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic;


        /// <summary>
        /// 把对象的属性转换为Dictionary<string, object>
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static Dictionary<string, object> ToDictionary(this object obj)
        {
            Dictionary<string, object> dic = new Dictionary<string, object>();
            var t = obj.GetType();
            foreach (var item in t.GetProperties())
            {
                dic.Add(item.Name, item.GetValue(obj, null));
            }
            return dic;
        }

        /// <summary>
        /// 获取指定属性的值
        /// </summary>
        /// <param name="data">数据</param>
        public static object GetByProperties(this object data, string name)
        {
            Type objType = data.GetType();
            PropertyInfo[] props = objType.GetProperties(bf);
            foreach (PropertyInfo item in props)
            {
                if (item.Name == name)
                {
                    return item.GetValue(data, null);
                }
            }
            return null;
        }

    }
}
