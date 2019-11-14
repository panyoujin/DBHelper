using System;

namespace DBHelper.SQLAnalytical
{
    /// <summary>
    /// 变量解析的来源
    /// </summary>
    public enum VariableSource
    {
        /// <summary>
        /// 从QueryString中解析
        /// </summary>
        QueryString,
        /// <summary>
        /// 从Request.Form中解析
        /// </summary>
        Request,
        /// <summary>
        /// 从Session中解析
        /// </summary>
        Session,
        /// <summary>
        /// 从Cookie中解析
        /// </summary>
        Cookie,
        /// <summary>
        /// 从Cache中解析
        /// </summary>
        Cache,
        /// <summary>
        /// 框架中预制的
        /// </summary>
        Framework,
        /// <summary>
        /// 不知道
        /// </summary>
        UnKnow
    }
    public class KeywordVariable
    {
        private readonly string[] sourceArray = { "querystring", "request", "session", "cookie", "cache", "framework" };

        #region property define
        /// <summary>
        /// 
        /// </summary>
        public string Keyword
        {
            get;
            set;
        }
        private readonly VariableSource _source;
        /// <summary>
        /// 
        /// </summary>
        public VariableSource Source
        {
            get { return _source; }
        }
        private readonly string _keyname;
        /// <summary>
        /// 
        /// </summary>
        public string KeyName
        {
            get { return _keyname; }
        }
        /// <summary>
        /// 
        /// </summary>
        public object Value
        {
            get;
            set;
        }
        /// <summary>
        /// 参数中找不到Key时是否返回空字符串
        /// </summary>
        public bool IsNotKeyReturnEmpty
        {
            get;
            set;
        }
        #endregion

        #region ctor
        /// <summary>
        /// 
        /// </summary>
        /// <param name="keyword"></param>
        public KeywordVariable(string keyword)
        {
            this.Keyword = keyword;
            string key = keyword.Replace("@", "").Trim();
            int pointLocation = key.IndexOf('.');
            if (pointLocation == -1)
            {
                _source = VariableSource.UnKnow;
                _keyname = key;
            }
            else
            {
                string sourceKey = key.Substring(0, pointLocation).ToLower();
                if (Array.IndexOf(sourceArray, sourceKey) >= 0)
                {
                    _source = (VariableSource)Enum.Parse(typeof(VariableSource), sourceKey, true);
                    _keyname = key.Substring(pointLocation + 1);
                }
                else
                {

                    _source = VariableSource.UnKnow;
                    _keyname = key;
                }
            }
        }
        #endregion


    }
}
