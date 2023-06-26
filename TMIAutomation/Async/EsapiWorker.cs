using System;
using System.Threading;
using System.Threading.Tasks;

namespace TMIAutomation.Async
{
    /// <summary>
    /// Reperesents an object that runs operations asynchronously on the ESAPI thread.
    /// </summary>
    public class EsapiWorker
    {
        private readonly PluginScriptContext scriptContext;
        private readonly CurrentThreadWorker currentThreadWorker;

        /// <summary>
        /// Initializes an instance of this class with the given PluginScriptContext.
        /// </summary>
        /// <param name="scriptContext"></param>
        public EsapiWorker(PluginScriptContext scriptContext)
        {
            this.scriptContext = scriptContext;
            this.currentThreadWorker = new CurrentThreadWorker();
        }

        /// <summary>
        /// Runs the given action asynchronously on the ESAPI thread.
        /// </summary>
        /// <param name="a">The action to run asynchronously.</param>
        /// <param name="isWriteable">True if the action modifies the data model</param>
        /// <returns>The started task associated with the given action.</returns>
        /// <remarks>
        /// The External Beam Application crashes with UnhandledException when tasks with write permission are executed
        /// in sequence with the EsapiRunner. A possible explanation is that once a task has completed, the Eclipse's thread
        /// "commits" the changes after some delay. Thus, if a new EsapiRunner starts, the application might crash.
        /// As a workaround, when isWriteable is true this method waits 500ms to give the Eclipse's thread the time
        /// to "commit" the changes to the data model
        /// </remarks>
        public Task RunAsync(Action<PluginScriptContext> a, bool isWriteable = true)
        {
            return this.currentThreadWorker.RunAsync(() => a(scriptContext))
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        throw task.Exception;
                    }
                    if (isWriteable)
                    {
                        Thread.Sleep(1000);
                    }
                });
        }

        /// <summary>
        /// Runs the given function asynchronously on the ESAPI thread.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="f">The function to run asynchronously.</param>
        /// <param name="isWriteable">True if the function modifies the data model</param>
        /// <returns>The started task associated with the given function.</returns>
        /// <remarks>
        /// The External Beam Application crashes with UnhandledException when tasks with write permission are executed
        /// in sequence with the EsapiRunner. A possible explanation is that once a task has completed, the Eclipse's thread
        /// "commits" the changes after some delay. Thus, if a new EsapiRunner starts, the application might crash.
        /// As a workaround, when isWriteable is true this method waits 500ms to give the Eclipse's thread the time
        /// to "commit" the changes to the data model
        /// </remarks>
        public Task<T> RunAsync<T>(Func<PluginScriptContext, T> f, bool isWriteable = true)
        {
            return this.currentThreadWorker.RunAsync(() => f(scriptContext))
                .ContinueWith(task =>
                {
                    if (task.Exception != null)
                    {
                        throw task.Exception;
                    }
                    if (isWriteable)
                    {
                        Thread.Sleep(500);
                    }
                    return task.Result;
                });
        }
    }
}