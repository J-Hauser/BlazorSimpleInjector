using BlazorSimpleInjector.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSimpleInjector.Pages
{
    public partial class FetchData
    {
        public FetchData(IRequestProcessor requestHandler,TestRequestHandler testRequestHandler, TestRequestHandler2 testRequestHandler2)
        {
            _requestHandler = requestHandler;
            _testRequestHandler = testRequestHandler;
            _testRequestHandler2 = testRequestHandler2;
        }

        private readonly IRequestProcessor _requestHandler;
        private readonly TestRequestHandler _testRequestHandler;
        private readonly TestRequestHandler2 _testRequestHandler2;

        async Task Navigate()
        {
            await _requestHandler.Handle(new Request<TestModel, Result<TestModel>>
            {
                Model = new TestModel()
                {
                    SomeProperty = "test"
                }
            });
        }
    }
}
