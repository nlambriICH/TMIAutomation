using System;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;

namespace TMIJunction.Async
{
    /// <summary>
    /// Reperesents an object that runs operations asynchronously on the ESAPI thread.
    /// </summary>
    public class EsapiWorker
    {
        private readonly ScriptContext scriptContext;
        private readonly CurrentThreadWorker currentThreadWorker;

        /// <summary>
        /// Initializes an instance of this class with the given ESAPI ScriptContext.
        /// </summary>
        /// <param name="scriptContext"></param>
        public EsapiWorker(ScriptContext scriptContext)
        {
            this.scriptContext = scriptContext;
            this.currentThreadWorker = new CurrentThreadWorker();
        }

        /// <summary>
        /// Runs the given action asynchronously on the ESAPI thread.
        /// </summary>
        /// <param name="a">The action to run asynchronously.</param>
        /// <returns>The started task associated with the given action.</returns>
        public Task RunAsync(Action<ScriptContext> a)
        {
            return this.currentThreadWorker.RunAsync(() => a(scriptContext));
        }

        /// <summary>
        /// Runs the given function asynchronously on the ESAPI thread.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="f">The function to run asynchronously.</param>
        /// <returns>The started task associated with the given function.</returns>
        public Task<T> RunAsync<T>(Func<ScriptContext, T> f)
        {
            return this.currentThreadWorker.RunAsync(() => f(scriptContext));
        }

        public T Run<T>(Func<ScriptContext, T> f)
        {
            return this.currentThreadWorker.RunAsync(() => f(scriptContext)).Result;
        }
}
}
