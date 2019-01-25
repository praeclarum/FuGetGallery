using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace FuGetGallery
{
    public abstract class DataCacheBase
    {
        protected readonly MemoryCache cache = new MemoryCache (new MemoryCacheOptions {
        });
    }

    public abstract class DataCache<TResult> : DataCacheBase
    {
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
                cache.Set (key, rt, expireAfter);
                return rt;
            });
        }
        protected abstract Task<TResult> GetValueAsync (CancellationToken token);
    }

    public abstract class DataCache<TArg, TResult> : DataCacheBase
    {
        readonly TimeSpan expireAfter;
        public DataCache (TimeSpan expireAfter)
        {
            this.expireAfter = expireAfter;
        }
        public Task<TResult> GetAsync (TArg arg, HttpClient httpClient) => GetAsync (arg, httpClient, CancellationToken.None);
        public Task<TResult> GetAsync (TArg arg, HttpClient httpClient, CancellationToken token)
        {
            var key = arg;
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }
            return GetValueAsync (arg, httpClient, token).ContinueWith (t => {
                var rt = t.Result;
                cache.Set (key, rt, expireAfter);
                return rt;
            });
        }
        public void Invalidate (TArg arg)
        {
            var key = arg;
            cache.Remove (key);
        }
        protected abstract Task<TResult> GetValueAsync (TArg arg, HttpClient httpClient, CancellationToken token);
    }

    public abstract class DataCache<TArg0, TArg1, TResult> : DataCacheBase
    {
        readonly TimeSpan expireAfter;

        public DataCache (TimeSpan expireAfter)
        {
            this.expireAfter = expireAfter;
        }

        public Task<TResult> GetAsync (TArg0 arg0, TArg1 arg1, HttpClient client) => GetAsync (arg0, arg1, client, CancellationToken.None);

        public Task<TResult> GetAsync (TArg0 arg0, TArg1 arg1, HttpClient client, CancellationToken token)
        {
            var key = Tuple.Create (arg0, arg1);
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }

            return GetValueAsync (arg0, arg1, client, token).ContinueWith (t => {
                var rt = t.Result;
                cache.Set (key, rt, expireAfter);
                return rt;
            });
        }

        protected abstract Task<TResult> GetValueAsync (TArg0 arg0, TArg1 arg1, HttpClient httpClient, CancellationToken token);
    }
}
