using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.WindowsAzure.MobileServices.Caching
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization. This type is fully threadsafe.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
    public sealed class AsyncLazy<T>
    {
        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        private Lazy<Task<T>> _instance;
        private readonly Func<Task<T>> valueFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The delegate that is invoked on a background thread to produce the value when it is needed. May not be <c>null</c>.</param>
        public AsyncLazy(Func<T> factory)
        {
            valueFactory = () =>
            {
                return TaskEx.Run(factory);
            };
            _instance = new Lazy<Task<T>>(valueFactory);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AsyncLazy&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="factory">The asynchronous delegate that is invoked on a background thread to produce the value when it is needed. May not be <c>null</c>.</param>
        public AsyncLazy(Func<Task<T>> factory)
        {
            valueFactory = () =>
            {
                return factory();
            };
            _instance = new Lazy<Task<T>>(valueFactory);
        }

        /// <summary>
        /// Asynchronous infrastructure support. This method permits instances of <see cref="AsyncLazy&lt;T&gt;"/> to be await'ed.
        /// </summary>
        public TaskAwaiter<T> GetAwaiter()
        {
            return _instance.Value.GetAwaiter();
        }

        public Task<T> Value
        {
            get { return _instance.Value; }
        }

        public void Reset()
        {
            _instance = new Lazy<Task<T>>(valueFactory);
        }
    }
}
