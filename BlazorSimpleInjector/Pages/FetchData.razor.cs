using Microsoft.AspNetCore.Components;
using System.Threading.Tasks;

namespace BlazorSimpleInjector.Pages
{
    public class BaseComponent : ComponentBase, IHandleEvent
    {
        public BaseComponent(SimpleInjectorEventHandlerScopeProvider scopeProvider)
        {
            _scopeProvider = scopeProvider;
        }

        private readonly SimpleInjectorEventHandlerScopeProvider _scopeProvider;

        Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object arg)
        {
            _scopeProvider.ApplyScope(); //<-- here

            var task = callback.InvokeAsync(arg);
            var shouldAwaitTask = task.Status != TaskStatus.RanToCompletion &&
                task.Status != TaskStatus.Canceled;

            // After each event, we synchronously re-render (unless !ShouldRender())
            // This just saves the developer the trouble of putting "StateHasChanged();"
            // at the end of every event callback.
            StateHasChanged();

            return shouldAwaitTask ?
                CallStateHasChangedOnAsyncCompletion(task) :
                Task.CompletedTask;
        }

        private async Task CallStateHasChangedOnAsyncCompletion(Task task)
        {
            try
            {
                await task;
            }
            catch // avoiding exception filters for AOT runtime support
            {
                // Ignore exceptions from task cancellations, but don't bother issuing a state change.
                if (task.IsCanceled)
                {
                    return;
                }

                throw;
            }

            StateHasChanged();
        }
    }

    public partial class FetchData : BaseComponent
    {
        public FetchData(IRequestProcessor requestHandler,
            SimpleInjectorEventHandlerScopeProvider scopeProvider) : base(scopeProvider)
        {
            _requestHandler = requestHandler;
        }

        private readonly IRequestProcessor _requestHandler;

        async Task Navigate()
        {
            await _requestHandler.Handle(new Request<Foo, Result<Foo>>
            {
                Model = new Foo()
                {
                    SomeProperty = "test"
                }
            });
        }
    }
}
