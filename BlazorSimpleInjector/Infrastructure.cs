using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SimpleInjector;
using SimpleInjector.Lifestyles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSimpleInjector
{
    public class TestRequestHandlerComposite<TRequest, TResult, TModel> : IRequestHandler<TRequest, TResult, TModel>
    where TResult : IResult<TModel>
where TRequest : IRequest<TModel, TResult>
    {
        private readonly IEnumerable<IRequestHandler<TRequest, TResult, TModel>> _requestHandlers;

        public TestRequestHandlerComposite(IEnumerable<IRequestHandler<TRequest, TResult, TModel>> requestHandlers)
        {
            Debugger.Break();
            // testhandler1 NavigationManager is not initialized and authState in second handler is null.
            _requestHandlers = requestHandlers;
        }

        public Task<TResult> Handle(TRequest request)
        {
            //filter for some runtimecondition. these requests are triggered when a field changes, so filter on fieldname for the same model.
            var handler = _requestHandlers.First();
            return handler.Handle(request);
        }
    }

    public class TestModel
    {
        public string SomeProperty { get; set; }
    }

    public interface IUserInfoService
    {
        Task<string> GetUserName();
    }

    public class UserInfoService : IUserInfoService
    {
        private readonly AuthenticationStateProvider _authenticationStateProvider;

        public UserInfoService(AuthenticationStateProvider authenticationStateProvider)
        {
            _authenticationStateProvider = authenticationStateProvider;
        }

        public Task<string> GetUserName()
        {
            return GetClaimValue("USERNAME");
        }

        private async Task<AuthenticationState> GetAuthenticationState()
        {
            return await _authenticationStateProvider.GetAuthenticationStateAsync();
        }

        private async Task<string> GetClaimValue(string claim)
        {
            return (await GetAuthenticationState()).User?.FindFirst(claim)?.Value ?? "anonymous";
        }
    }

    public interface INavigationManager
    {
        void NavigateTo(string destination, bool forceNavigation = false);
    }


    public class BlazorNavigationManager : INavigationManager
    {
        private readonly NavigationManager _navigationManager;

        public BlazorNavigationManager(NavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public void NavigateTo(string destination, bool forceNavigation = false)
        {
            Debug.Assert(_navigationManager.Uri != null);
            _navigationManager.NavigateTo(destination, forceNavigation);
        }
    }
    public interface IRequestProcessor
    {
        Task<TResult> Handle<TResult, TModel>(IRequest<TModel, TResult> request)
                where TResult : IResult<TModel>;
    }

    public class RequestProcessor : IRequestProcessor
    {
        private readonly Container _container;

        public RequestProcessor(Container container)
        {
            _container = container;
        }
        public Task<TResult> Handle<TResult, TModel>(IRequest<TModel, TResult> request) where TResult : IResult<TModel>
        {
            var desiredType = typeof(IRequestHandler<,,>).MakeGenericType(request.GetType(), typeof(TResult), typeof(TModel));
            
            //does not work
            //using (AsyncScopedLifestyle.BeginScope(_container))
            //{
            //    IEnumerable<dynamic> handlers = _container.GetAllInstances(desiredType);
            //}

            //composite does not work
            dynamic handler = _container.GetInstance(desiredType);
            return handler.Handle((dynamic)request);
        }
    }

    public interface IRequestHandler<TRequest, TResult, TModel>
where TResult : IResult<TModel>
where TRequest : IRequest<TModel, TResult>
    {
        Task<TResult> Handle(TRequest request);
    }



    public class TestRequestHandler : IRequestHandler<Request<TestModel, Result<TestModel>>, Result<TestModel>, TestModel>
    {
        private readonly INavigationManager _navigationManager;

        public TestRequestHandler(INavigationManager navigationManager)
        {
            _navigationManager = navigationManager;
        }

        public Task<Result<TestModel>> Handle(Request<TestModel, Result<TestModel>> request)
        {
            _navigationManager.NavigateTo(request.Model.SomeProperty,true);
            return Task.FromResult(request.CreateResult());
        }
    }

    public class TestRequestHandler2 : IRequestHandler<Request<TestModel, Result<TestModel>>, Result<TestModel>, TestModel>
    {
        private readonly IUserInfoService _userInfoService;

        public TestRequestHandler2(IUserInfoService userInfoService)
        {
            _userInfoService = userInfoService;
        }

        public async Task<Result<TestModel>> Handle(Request<TestModel, Result<TestModel>> request)
        {
            var data = await _userInfoService.GetUserName();
            return request.CreateResult();
        }
    }

    public interface IRequest<TModel, TResult>
    where TResult : IResult<TModel>
    {
        TModel Model { get; set; }
        TResult CreateResult();
    }

    public interface IResult<TModel>
    {
        TModel Model { get; set; }
    }

    public class Request<T, TResult> : IRequest<T, TResult>
    where TResult : IResult<T>, new()
    {
        public T Model { get; set; }
        public TResult CreateResult()
        {
            return new TResult()
            {
                Model = Model
            };
        }
    }
    public class Result<T> : IResult<T>
    {
        public T Model { get; set; }
    }
}
