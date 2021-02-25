using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SimpleInjector;
using SimpleInjector.Advanced;
using SimpleInjector.Diagnostics;
using SimpleInjector.Integration.ServiceCollection;
using SimpleInjector.Lifestyles;


namespace BlazorSimpleInjector
{
    public class Startup
    {
        private readonly Container container = new Container();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddSimpleInjector(container, options =>
            {
                // Custom extension method; see code below.
                options.AddServerSideBlazor(this.GetType().Assembly);

                // Adds the IServiceScopeFactory, required for the IServiceScope registration.
                container.Register(
                    () => options.ApplicationServices.GetRequiredService<IServiceScopeFactory>(),
                    Lifestyle.Singleton);
            });

            // Replace the IServiceScope registration made by .AddSimpleInjector
            // (must be called after AddSimpleInjector)
            container.Options.AllowOverridingRegistrations = true;
            container.Register<ServiceScopeAccessor>(Lifestyle.Scoped);
            this.container.Register<IServiceScope>(
                () => container.GetInstance<ServiceScopeAccessor>().Scope
                    ?? container.GetInstance<IServiceScopeFactory>().CreateScope(),
                Lifestyle.Scoped);
            container.Options.AllowOverridingRegistrations = false;

            InitializeContainer();
        }

        private void InitializeContainer()
        {
            container.Register<IRequestProcessor, RequestProcessor>(); 
            container.Collection.Register(typeof(IRequestHandler<,,>),GetType().Assembly);
            container.Register(typeof(IRequestHandler<,,>), typeof(TestRequestHandlerComposite<,,>));
            container.Register<INavigationManager, BlazorNavigationManager>();
            container.Register<IUserInfoService, UserInfoService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.ApplicationServices.UseSimpleInjector(container);

            // Default VS template stuff
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            });

            container.Verify();
        }
    }

    public static class BlazorExtensions
    {
        private static readonly AsyncScopedLifestyle lifestyle = new AsyncScopedLifestyle();

        public static void AddServerSideBlazor(
            this SimpleInjectorAddOptions options, params Assembly[] assemblies)
        {
            options.Container.Options.DefaultScopedLifestyle = new AsyncScopedLifestyle();

            options.Services.AddScoped<ScopeAccessor>();
            options.Services.AddScoped<IComponentActivator, SimpleInjectorComponentActivator>();

            // HACK: This internal ComponentHub type needs to be added for the
            // SimpleInjectorBlazorHubActivator to work.
            options.Services.AddTransient(
                typeof(Microsoft.AspNetCore.Components.Server.CircuitOptions).Assembly.GetTypes().First(
                    t => t.FullName == "Microsoft.AspNetCore.Components.Server.ComponentHub"));

            options.Services.AddScoped(typeof(IHubActivator<>), typeof(SimpleInjectorBlazorHubActivator<>));

            RegisterBlazorComponents(options, assemblies);
        }

        public static void ApplyServiceScope(this Container container, IServiceProvider requestServices)
        {
            var accessor = requestServices.GetRequiredService<ScopeAccessor>();

            if (accessor.Scope is null)
            {
                accessor.Scope = AsyncScopedLifestyle.BeginScope(container);
                accessor.Scope.GetInstance<ServiceScopeAccessor>().Scope = (IServiceScope)requestServices;
            }
            else
            {
                lifestyle.SetCurrentScope(accessor.Scope);
            }
        }

        private static void RegisterBlazorComponents(SimpleInjectorAddOptions options, Assembly[] assemblies)
        {
            var types = options.Container.GetTypesToRegister(typeof(IComponent), assemblies,
                new TypesToRegisterOptions { IncludeGenericTypeDefinitions = true });

            foreach (Type type in types.Where(t => !t.IsGenericTypeDefinition))
            {
                var registration = Lifestyle.Transient.CreateRegistration(type, options.Container);

                registration.SuppressDiagnosticWarning(
                    DiagnosticType.DisposableTransientComponent,
                    "Blazor will dispose components.");

                options.Container.AddRegistration(type, registration);
            }

            foreach (Type type in types.Where(t => t.IsGenericTypeDefinition))
            {
                options.Container.Register(type, type, Lifestyle.Transient);
            }
        }
    }

    public sealed class ScopeAccessor : IAsyncDisposable
    {
        public Scope Scope { get; set; }
        public ValueTask DisposeAsync() => this.Scope.DisposeAsync();
    }

    public sealed class ServiceScopeAccessor
    {
        public IServiceScope Scope { get; set; }
    }

    public sealed class SimpleInjectorComponentActivator : IComponentActivator
    {
        private readonly Container container;
        private readonly IServiceProvider serviceScope;

        public SimpleInjectorComponentActivator(Container container, IServiceProvider serviceScope)
        {
            this.container = container;
            this.serviceScope = serviceScope;
        }

        public IComponent CreateInstance(Type type) =>
            (IComponent)this.GetInstance(type) ?? (IComponent)Activator.CreateInstance(type);

        private object GetInstance(Type type)
        {
            this.container.ApplyServiceScope(this.serviceScope);
            return this.container.GetRegistration(type)?.GetInstance();
        }
    }

    public sealed class SimpleInjectorBlazorHubActivator<T> : IHubActivator<T> where T : Hub
    {
        private readonly Container container;
        private readonly IServiceProvider serviceScope;

        public SimpleInjectorBlazorHubActivator(Container container, IServiceProvider serviceScope)
        {
            this.container = container;
            this.serviceScope = serviceScope;
        }

        public T Create()
        {
            this.container.ApplyServiceScope(this.serviceScope);
            return this.container.GetInstance<T>();
        }

        public void Release(T hub) { }
    }
}
