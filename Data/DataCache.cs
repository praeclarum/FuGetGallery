using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Collections.Concurrent;

namespace FuGetGallery
{
    public abstract class DataCacheBase
    {
    }

    public abstract class DataCache<TResult> : DataCacheBase
    {
        readonly ConcurrentDictionary<string, object> cache =
            new ConcurrentDictionary<string, object> ();

        readonly TimeSpan expireAfter;
        public DataCache (TimeSpan expireAfter)
        {
            this.expireAfter = expireAfter;
        }
        public Task<TResult> GetAsync () => GetAsync (CancellationToken.None);
        public Task<TResult> GetAsync (CancellationToken token)
        {
            var key = "";
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }
            return GetValueAsync (token).ContinueWith (t => {
                var rt = t.Result;
                cache[key] = rt;
                return rt;
            });
        }
        protected abstract Task<TResult> GetValueAsync (CancellationToken token);
    }

    public abstract class DataCache<TArg, TResult> : DataCacheBase
    {
        readonly ConcurrentDictionary<TArg, TResult> cache =
            new ConcurrentDictionary<TArg, TResult> ();

        readonly TimeSpan expireAfter;
        public DataCache (TimeSpan expireAfter)
        {
            this.expireAfter = expireAfter;
        }
        public Task<TResult> GetAsync (TArg arg, HttpClient httpClient) => GetAsync (arg, httpClient, CancellationToken.None);
        public Task<TResult> GetAsync (TArg arg, HttpClient httpClient, CancellationToken token)
        {
            var key = arg;
            Console.WriteLine ("GET FROM CACHE " + key);
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                Console.WriteLine ("CACHE HIT " + key);
                return Task.FromResult (rm);
            }
            Console.WriteLine ("CACHE MISS " + key);
            return GetValueAsync (arg, httpClient, token).ContinueWith (t => {
                var rt = t.Result;
                cache[key] = rt;
                return rt;
            });
        }
        public void Invalidate (TArg arg)
        {
            var key = arg;
            cache.TryRemove (key, out var _);
        }
        protected abstract Task<TResult> GetValueAsync (TArg arg, HttpClient httpClient, CancellationToken token);
    }

    public abstract class DataCache<TArg0, TArg1, TResult> : DataCacheBase
    {
        readonly ConcurrentDictionary<(TArg0, TArg1), TResult> cache =
            new ConcurrentDictionary<(TArg0, TArg1), TResult> ();

        readonly TimeSpan expireAfter;

        public DataCache (TimeSpan expireAfter)
        {
            this.expireAfter = expireAfter;
        }

        public Task<TResult> GetAsync (TArg0 arg0, TArg1 arg1, HttpClient client) => GetAsync (arg0, arg1, client, CancellationToken.None);

        public Task<TResult> GetAsync (TArg0 arg0, TArg1 arg1, HttpClient client, CancellationToken token)
        {
            var key = (arg0, arg1);
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }

            return GetValueAsync (arg0, arg1, client, token).ContinueWith (t => {
                var rt = t.Result;
                cache[key] = rt;
                return rt;
            });
        }

        protected abstract Task<TResult> GetValueAsync (TArg0 arg0, TArg1 arg1, HttpClient httpClient, CancellationToken token);
    }
}
