using System.Runtime.Caching;
using Resthopper.IO;

namespace GDH
{
	internal static class MemoryCache
	{
		private static System.Runtime.Caching.MemoryCache _memCache = new System.Runtime.Caching.MemoryCache("HopsCache");

		public static int EntryCount { get; set; } = 0;


		public static Schema Get(string key)
		{
			return _memCache.Get(key) as Schema;
		}

		public static void Set(string key, Schema schema)
		{
			EntryCount++;
			_memCache.Set(key, schema, new CacheItemPolicy());
		}

		public static void ClearCache()
		{
			_memCache.Dispose();
			_memCache = new System.Runtime.Caching.MemoryCache("HopsCache");
			EntryCount = 0;
		}
	}
}