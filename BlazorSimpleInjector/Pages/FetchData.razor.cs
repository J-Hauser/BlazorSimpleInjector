using BlazorSimpleInjector.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlazorSimpleInjector.Pages
{
    public partial class FetchData
    {
        public FetchData(IRequestProcessor requestHandler)
        {
            _requestHandler = requestHandler;
        }

        private readonly IRequestProcessor _requestHandler;

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
