using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;

namespace FuGetGallery
{
    public abstract class DataCacheBase
    {
        protected readonly MemoryCache cache = new MemoryCache (new MemoryCacheOptions {            
        });
    }

    public abstract class DataCache<TResult> : DataCacheBase
    {
        public Task<TResult> GetAsync ()
        {
            var key = "";
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }
            return GetValueAsync ().ContinueWith (t => {
                var rt = t.Result;
                cache.Set (key, rt);
                return rt;
            });
        }
        protected abstract Task<TResult> GetValueAsync ();
    }

    public abstract class DataCache<TArg, TResult> : DataCacheBase
    {
        public Task<TResult> GetAsync (TArg arg)
        {
            var key = arg;
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }
            return GetValueAsync (arg).ContinueWith (t => {
                var rt = t.Result;
                cache.Set (key, rt);
                return rt;
            });
        }
        protected abstract Task<TResult> GetValueAsync (TArg arg);
    }

    public abstract class DataCache<TArg0, TArg1, TResult> : DataCacheBase
    {
        public Task<TResult> GetAsync (TArg0 arg0, TArg1 arg1)
        {
            var key = Tuple.Create (arg0, arg1);
            if (cache.TryGetValue (key, out var result) && result is TResult rm) {
                return Task.FromResult (rm);
            }
            return GetValueAsync (arg0, arg1).ContinueWith (t => {
                var rt = t.Result;
                cache.Set (key, rt);
                return rt;
            });
        }
        protected abstract Task<TResult> GetValueAsync (TArg0 arg0, TArg1 arg1);
    }
}
