using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Collections.Concurrent;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace AppCache.DataCache
{
    
    public class Cache
    {
        internal static ConcurrentDictionary<string, DataCache> DataCacheDictionary = new ConcurrentDictionary<string, DataCache>();

        public static DataCache Instance([CallerMemberName]string domain = null)
        {
            if (string.IsNullOrEmpty(domain))
            {
                domain = "DefaultDomain";
            }
            DataCache dataCache = null;
            while (!DataCacheDictionary.ContainsKey(domain) || !DataCacheDictionary.TryGetValue(domain, out dataCache))
            {
                DataCacheDictionary.TryAdd(domain, new DataCache() { DefaultDomain = domain });
            }
            return dataCache;
        }
        public static string GetCacheKey(List<object> currParameters, [CallerMemberName]string Caller = null)
        {
            StringBuilder CacheKey = new StringBuilder();
            CacheKey.Append(Caller);
            CacheKey.Append(',');
            foreach (var item in currParameters)
            {
                if (item.GetType() == typeof(int) || item.GetType() == typeof(decimal) || item.GetType() == typeof(double)
                    || item.GetType() == typeof(float) )
                {
                    CacheKey.Append(item.ToString());
                }
                else if(item.GetType() == typeof(string))
                {
                    CacheKey.Append(item);
                }
                CacheKey.Append(',');
            }
            CacheKey.Length--;
            return CacheKey.ToString();
        }

        protected static void DeleteCache([CallerMemberName]string domain = null)
        {
            if (string.IsNullOrEmpty(domain))
            {
                domain = "DefaultDomain";
            }
            DataCache deletedCache = null;
            while (DataCacheDictionary.ContainsKey(domain))
            {
                DataCacheDictionary.TryRemove(domain, out deletedCache);
            }
        }
    }

    public class DataCache : Cache
    {
        private const string KeySeparator = "_";
        public string DefaultDomain = "DefaultDomain";
        private ObjectCache _dataCache = MemoryCache.Default;
        private Dictionary<string, DateTime> _cacheItemsCreatedDateTimes = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> _cacheItemsModifiedDateTimes = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> _cacheItemsLastAccessDateTimes = new Dictionary<string, DateTime>();
        private CacheEntryRemovedCallback callback = null;
        public List<string> Keys
        {
            get
            {
                return _cacheItemsCreatedDateTimes.Keys.ToList<string>();
            }
        }

        public object this[string key]
        {
            get
            {
                DateTime currentDT = DateTime.Now;
                object returnValue = _dataCache[CombinedKey(key, DefaultDomain)];

                if (returnValue != null)
                {
                    if (_cacheItemsLastAccessDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
                        _cacheItemsLastAccessDateTimes[CombinedKey(key, DefaultDomain)] = currentDT;
                    else
                    {
                        _cacheItemsLastAccessDateTimes.Add(CombinedKey(key, DefaultDomain), currentDT);
                    }
                    return returnValue;
                }
                else
                {
                    return null;
                }
            }
        }

        public void Set(string key, object value, CacheItemPolicy policy)
        {
            callback = new CacheEntryRemovedCallback(this.MyCachedItemRemovedCallback);
            policy.RemovedCallback = callback;

            DateTime currentDT = DateTime.Now;
            if (!_cacheItemsCreatedDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
                _cacheItemsCreatedDateTimes.Add(CombinedKey(key, DefaultDomain), currentDT);
            if (_cacheItemsModifiedDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
                _cacheItemsModifiedDateTimes[CombinedKey(key, DefaultDomain)] = currentDT;
            else
                _cacheItemsModifiedDateTimes.Add(CombinedKey(key, DefaultDomain), currentDT);
            _dataCache.Set(CombinedKey(key, DefaultDomain), value, policy);
        }

        public object Remove(string key)
        {
            _cacheItemsCreatedDateTimes.Remove(key);
            _cacheItemsModifiedDateTimes.Remove(key);
            _cacheItemsLastAccessDateTimes.Remove(key);
            return _dataCache.Remove(key);
        }

        public DateTime Created(string key)
        {
            if (_cacheItemsCreatedDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
            {
                return _cacheItemsCreatedDateTimes[CombinedKey(key, DefaultDomain)];
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        public DateTime LastModified(string key)
        {
            if (_cacheItemsModifiedDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
            {
                return _cacheItemsModifiedDateTimes[CombinedKey(key, DefaultDomain)];
            }
            else
            {
                return DateTime.MinValue;
            }
        }

        public DateTime LastAccessed(string key)
        {
            if (_cacheItemsLastAccessDateTimes.ContainsKey(CombinedKey(key, DefaultDomain)))
            {
                return _cacheItemsLastAccessDateTimes[CombinedKey(key, DefaultDomain)];
            }
            else
            {
                return DateTime.MinValue;
            }
        }
        #region Private methods
        private static string CombinedKey(object key, string domain)
        {
            return string.Format("{0}{1}{2}", string.IsNullOrEmpty(domain) ? "DefaultDomain" : domain, KeySeparator, key);
        }

        private void MyCachedItemRemovedCallback(CacheEntryRemovedArguments arguments)
        {
            this.Remove(arguments.CacheItem.Key);
            if (Keys.Count == 0)
            {
                //delete ConcurrentDictionary of Cache
                Cache.DeleteCache(this.DefaultDomain);
            }
        }
        #endregion
    }
}
