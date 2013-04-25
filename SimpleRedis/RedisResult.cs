using System;
using System.Dynamic;
using System.Globalization;
using System.Text;

namespace SimpleRedis
{
    /// <summary>
    /// Like a RedisResult, but boolean depends on an "OK" message
    /// </summary>
    class RedisStatusResult: RedisResult
    {
        public RedisStatusResult(byte[] value) : base(value) { }
        protected override bool GetBoolean(out bool value)
        {
            string s;
            if (GetString(out s))
            {
                value = string.Equals(s, "OK", StringComparison.InvariantCultureIgnoreCase);
                return true;
            }
            value = false;
            return false;
        }
    }

    class RedisExceptionResult : RedisResult
    {
        public RedisExceptionResult(byte[] value) : base(value) { }
        internal Exception GetException()
        {
            string s;
            if(!GetString(out s)) s = "unknown error";
            return new RedisException(s);
        }
        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            var ex = GetException();
            if (binder.Type == typeof(Exception))
            {
                result = ex;
                return true;
            }
            // else, let 'em have it!
            throw ex;
        }
    }

    /// <summary>
    /// Represents a raw blob of redis data, parseable as various common types
    /// </summary>
    class RedisResult : DynamicObject
    {
        public override string ToString()
        {
            string s;
            if (GetString(out s)) return s;
            return base.ToString();
        }
        private readonly byte[] value;
        public RedisResult(byte[] value)
        {
            this.value = value;
        }
        protected virtual bool GetBytes(out byte[] value)
        {
            value = this.value;
            return true;
        }
        protected virtual bool GetString(out string value)
        {
            byte[] bytes;
            if (GetBytes(out bytes))
            {
                value = Encoding.UTF8.GetString(bytes);
                return true;
            }
            value = null;
            return false;
        }
        protected virtual bool GetBoolean(out bool value)
        {
            byte[] bytes;
            if (GetBytes(out bytes) && bytes.Length == 1)
            {
                switch (bytes[0])
                {
                    case (byte)'0': value = false; return true;
                    case (byte)'1': value = true; return true;
                }
            }
            value = false;
            return false;

        }
        protected virtual bool GetInt64(out long value)
        {
            string s;
            if (GetString(out s))
            {
                value = long.Parse(s, CultureInfo.InvariantCulture);
                return true;
            }
            value = 0;
            return false;
        }
        protected virtual bool GetInt32(out int value)
        {
            string s;
            if (GetString(out s))
            {
                value = int.Parse(s, CultureInfo.InvariantCulture);
                return true;
            }
            value = 0;
            return false;
        }

        public override bool TryConvert(ConvertBinder binder, out object result)
        {
            if (binder.Type == typeof(byte[]))
            {
                byte[] val;
                if (GetBytes(out val))
                {
                    result = val;
                    return true;
                }
            }
            if (binder.Type == typeof(string))
            {
                string val;
                if (GetString(out val))
                {
                    result = val;
                    return true;
                }
            }
            if (binder.Type == typeof(int))
            {
                int val;
                if (GetInt32(out val))
                {
                    result = val;
                    return true;
                }
            }
            if (binder.Type == typeof(long))
            {
                long val;
                if (GetInt64(out val))
                {
                    result = val;
                    return true;
                }
            }
            if (binder.Type == typeof(bool))
            {
                bool val;
                if (GetBoolean(out val))
                {
                    result = val;
                    return true;
                }
            }
            return base.TryConvert(binder, out result);
        }
    }
}
