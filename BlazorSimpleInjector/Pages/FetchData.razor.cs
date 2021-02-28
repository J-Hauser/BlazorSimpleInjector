using BlazorSimpleInjector.Data;
using Microsoft.AspNetCore.Components;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSimpleInjector.Pages
{
    public partial class FetchData : IHandleEvent
    {
        public FetchData(IRequestProcessor requestHandler,
            TestRequestHandler testRequestHandler,
            TestRequestHandler2 testRequestHandler2
            , IRequestHandler<Request<Bar, Result<Bar>>, Result<Bar>, Bar> decorated,
            TestRequestHandlerComposite<Request<Foo, Result<Foo>>, Result<Foo>, Foo> composite,
            SimpleInjectorEventHandlerScopeProviderFactory handlerFactory,
            Container container)
        {
            _requestHandler = requestHandler;
            _testRequestHandler = testRequestHandler;
            _testRequestHandler2 = testRequestHandler2;
            _decorated = decorated;
            _composite = composite;
            _handlerFactory = handlerFactory;
            _container = container;
        }

        private readonly IRequestProcessor _requestHandler;
        private readonly TestRequestHandler _testRequestHandler;
        private readonly TestRequestHandler2 _testRequestHandler2;
        private readonly IRequestHandler<Request<Bar,Result<Bar>>,Result<Bar>,Bar> _decorated;
        private readonly TestRequestHandlerComposite<Request<Foo, Result<Foo>>, Result<Foo>, Foo> _composite;
        private readonly SimpleInjectorEventHandlerScopeProviderFactory _handlerFactory;
        private readonly Container _container;

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

        Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object arg)
        {
            _handlerFactory.ApplyScope();
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
}
