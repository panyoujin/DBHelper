using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DBHelper.SQLAnalytical
{
    /// <summary>
    /// SQLCommand 中需要解析的项
    /// </summary>
    public class ParseItem
    {
        #region property define
        /// <summary>
        /// 需要解析的文本，一搬是 
        /// @@@@    仅使用该符号时，传递的字典必须包含该key
        /// <%= @@@@%> 如果为null则直接删除
        /// <R%= @@@@%R> 直接替换@@@@里的内容
        /// </summary>
        public string ItemContent
        {
            get;
            set;
        }

        /// <summary>
        /// 需要被解析出来被替换的关键字
        /// </summary>
        private List<KeywordVariable> Keywords = new List<KeywordVariable>();

        /// <summary>
        /// 关键字和值的集合 解析SQL使用,传递进来的参数
        /// </summary>
        private Dictionary<string, object> KeyValue;
        #endregion

        #region ctor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="fullContent"></param>
        /// <param name="keyValue"></param>
        public ParseItem(string fullContent, Dictionary<string, object> keyValue)
        {
            this.KeyValue = keyValue;
            this.ItemContent = fullContent;
            ResolveKeywords(fullContent);
        }
        #endregion

        #region method
        /// <summary>
        /// 将文本中的关键字全部解析出来
        /// </summary>
        /// <param name="fullContent"></param>
        private void ResolveKeywords(string fullContent)
        {
            Regex rgKeyword = new Regex("@@.*?@@");
            MatchCollection mc = rgKeyword.Matches(fullContent);
            foreach (Match m in mc)
            {
                var keyword = new KeywordVariable(m.Value);
                keyword.IsNotKeyReturnEmpty = fullContent.Length != m.Value.Length;
                if (KeyValue != null && KeyValue.ContainsKey(keyword.KeyName))
                {
                    keyword.Value = KeyValue[keyword.KeyName];
                }
                Keywords.Add(keyword);
            }
        }
        /// <summary>
        /// 返回解析后的结果
        /// </summary>
        /// <param name="type"></param>
        /// <param name="isParam"></param>
        /// <returns></returns>
        public virtual string GetResult(string type, bool isParam = true)
        {
            //至一个全部参数为空的标记，如果全部参数为空，则整个
            //解析串返回空
            bool allEmpty = false;
            //如果根本没有参数，直接返回串内容
            if (this.Keywords.Count == 0) return this.ItemContent;
            string returnValue = ItemContent;
            foreach (KeywordVariable keyItem in this.Keywords)
            {
                string result = keyItem.Value + "";
                //当使用<%= %> 或者<R%= %R> 同时不传递参数时
                if (!KeyValue.ContainsKey(keyItem.KeyName) && keyItem.IsNotKeyReturnEmpty)
                {
                    return string.Empty;
                }
                //当使用@@@@ 同时不传递参数时
                if (!KeyValue.ContainsKey(keyItem.KeyName) && !keyItem.IsNotKeyReturnEmpty)
                {
                    return "NULL";
                }
                if (isParam)
                {
                    switch (type.ToLower())
                    {
                        case "sqlserver":
                            returnValue = returnValue.Replace(keyItem.Keyword, string.Format("@{0}", keyItem.KeyName));
                            break;
                        case "mysql":
                            returnValue = returnValue.Replace(keyItem.Keyword, string.Format("?{0}", keyItem.KeyName));
                            break;
                        default:
                            returnValue = returnValue.Replace(keyItem.Keyword, string.Format("@{0}", keyItem.KeyName));
                            break;
                    }
                }
                else
                {
                    returnValue = returnValue.Replace(keyItem.Keyword, string.Format("{0}", keyItem.Value));
                }
            }

            return returnValue;
        }
        #endregion
    }
}
