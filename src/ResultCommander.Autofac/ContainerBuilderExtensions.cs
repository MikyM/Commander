using System.Reflection;
using AttributeBasedRegistration;
using AttributeBasedRegistration.Attributes.Abstractions;
using AttributeBasedRegistration.Autofac;
using AttributeBasedRegistration.Extensions;
using Autofac;
using Autofac.Builder;
using Autofac.Extras.DynamicProxy;
using Autofac.Features.Scanning;
using Castle.DynamicProxy;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using ServiceLifetime = AttributeBasedRegistration.ServiceLifetime;

namespace ResultCommander.Autofac;

/// <summary>
/// DI extensions for <see cref="ContainerBuilder"/>.
/// </summary>
[PublicAPI]
public static class ContainerBuilderExtensions
{
    internal static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> HandleInterception(this IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> builder, Type type)
    {
        var intrAttr = type.GetRegistrationAttributesOfType<IEnableInterceptionAttribute>().ToArray();
        if (!intrAttr.Any())
            return builder;
        if (intrAttr.Length > 1)
            throw new InvalidOperationException($"Only a single enable interception attribute is allowed on type, type: {type.Name}");
        
        if (intrAttr.First().InterceptionStrategy is not InterceptionStrategy.Interface)
            throw new NotSupportedException("Only interface interception is supported for command handlers");
                
        var intrAttrs = type.GetRegistrationAttributesOfType<IInterceptedByAttribute>().ToArray();
        if (!intrAttrs.Any())
            return builder;
        
        if (intrAttrs.GroupBy(x => x.RegistrationOrder).FirstOrDefault(x => x.Count() > 1) is not null)
            throw new InvalidOperationException($"Duplicated interceptor registration order on type {type.Name}");

        if (intrAttrs.GroupBy(x => x.Interceptor)
                .FirstOrDefault(x => x.Count() > 1) is not null)
            throw new InvalidOperationException($"Duplicated interceptor type on type {type.Name}");

        builder = builder.EnableInterfaceInterceptors();
        
        foreach (var interceptor in intrAttrs.OrderByDescending(x => x.RegistrationOrder).Select(x => x.Interceptor).Distinct())
        {
            builder = interceptor.IsAsyncInterceptor()
                ? builder.InterceptedBy(
                    typeof(AsyncInterceptorAdapter<>).MakeGenericType(interceptor))
                : builder.InterceptedBy(interceptor);
        }

        return builder;
    }
    
    private static ContainerBuilder HandleDecoration(this ContainerBuilder builder, Type type)
    {
        var decoratorAttributes = type.GetRegistrationAttributesOfType<IDecoratedByAttribute>().ToArray();
        if (!decoratorAttributes.Any())
            return builder;

        var serviceType = type.GetInterfaces().FirstOrDefault(x => x.IsHandlerInterface());
        if (serviceType is null)
            throw new InvalidOperationException("Couldn't fine the proper service type for a handler");
        
        if (decoratorAttributes.GroupBy(x => x.RegistrationOrder).FirstOrDefault(x => x.Count() > 1) is not null)
            throw new InvalidOperationException($"Duplicated decorator registration order on type {type.Name}");

        if (decoratorAttributes.GroupBy(x => x.Decorator)
                .FirstOrDefault(x => x.Count() > 1) is not null)
            throw new InvalidOperationException($"Duplicated decorator type on type {type.Name}");
            
        foreach (var attribute in decoratorAttributes.OrderBy(x => x.RegistrationOrder))
        {
            if (attribute.Decorator.ShouldSkipRegistration<ISkipDecoratorRegistrationAttribute>())
                continue;
            
            if (attribute.Decorator.IsGenericType && attribute.Decorator.IsGenericTypeDefinition)
                builder.RegisterGenericDecorator(attribute.Decorator, serviceType);
            else
                builder.RegisterDecorator(attribute.Decorator, serviceType);
        }

        return builder;
    }
    
    private static IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> HandleLifetime(this IRegistrationBuilder<object, ScanningActivatorData, DynamicRegistrationStyle> builder, ServiceLifetime lifetime, ILifetimeAttribute? attribute)
    {
        switch (lifetime)
        {
            case ServiceLifetime.SingleInstance:
                builder = builder.SingleInstance();
                break;
            case ServiceLifetime.InstancePerRequest:
                builder = builder.InstancePerRequest();
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                builder = builder.InstancePerLifetimeScope();
                break;
            case ServiceLifetime.InstancePerDependency:
                builder = builder.InstancePerDependency();
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                builder =
                    builder.InstancePerMatchingLifetimeScope(attribute?.Tags?.ToArray() ?? throw new InvalidOperationException());
                break;
            case ServiceLifetime.InstancePerOwned:
                if (attribute?.Owned is null) throw new InvalidOperationException("Owned type was null");

                builder = builder.InstancePerOwned(attribute.Owned);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }   
        
        return builder;
    }

    private static ContainerBuilder HandleNonCustomizedRegistration(this ContainerBuilder builder,
        List<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        if (!types.Any())
            return builder;
        
        switch (config.DefaultHandlerLifetime)
        {
            case ServiceLifetime.SingleInstance:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).SingleInstance();
                break;
            case ServiceLifetime.InstancePerRequest:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerRequest();
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerLifetimeScope();
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException();
            case ServiceLifetime.InstancePerDependency:
                builder.RegisterTypes(types.ToArray())
                    .AsClosedInterfacesOf(handlerServiceType).InstancePerDependency();
                break;
            case ServiceLifetime.InstancePerOwned:
                throw new NotSupportedException();
            default:
                throw new ArgumentOutOfRangeException();
        }
        
        return builder;
    }
    
    private static ContainerBuilder HandleCustomizedRegistration(this ContainerBuilder builder, IEnumerable<Type> types, Type handlerServiceType, ResultCommanderConfiguration config)
    {
        foreach (var type in types)
        {
            var lifeAttrs = type.GetRegistrationAttributesOfType<ILifetimeAttribute>().ToArray();
            if (lifeAttrs.Length > 1)
                throw new InvalidOperationException(
                    $"Only a single lifetime attribute is allowed on a type, {type.Name}");

            var lifeAttr = lifeAttrs.FirstOrDefault();

            var registrationBuilder = builder.RegisterTypes(type).AsClosedInterfacesOf(handlerServiceType);

            var lifetime = lifeAttr?.ServiceLifetime ?? config.DefaultHandlerLifetime;

            registrationBuilder.HandleLifetime(lifetime, lifeAttr);

            registrationBuilder.HandleInterception(type);
            
            builder.HandleDecoration(type);
        }

        return builder;
    }
    private static ContainerBuilder HandleAsynchronousHandlers(this ContainerBuilder builder,
        List<Type> types, ResultCommanderConfiguration config)
        => builder.HandleNonCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsAsyncNonCustomizedHandler).ToList(), ServiceCollectionExtensions.AsyncHandlerType,
                config)
            .HandleNonCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsAsyncResultNonCustomizedHandler).ToList(),
                ServiceCollectionExtensions.AsyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsAsyncCustomizedHandler), ServiceCollectionExtensions.AsyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsAsyncResultCustomizedHandler), ServiceCollectionExtensions.AsyncResultHandlerType, config);

    private static ContainerBuilder HandleSynchronousHandlers(this ContainerBuilder builder,
        List<Type> types, ResultCommanderConfiguration config)
        => builder.HandleNonCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsSyncNonCustomizedHandler).ToList(), ServiceCollectionExtensions.SyncHandlerType,
                config)
            .HandleNonCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsSyncResultNonCustomizedHandler).ToList(),
                ServiceCollectionExtensions.SyncResultHandlerType, config)
            .HandleCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsSyncCustomizedHandler), ServiceCollectionExtensions.SyncHandlerType, config)
            .HandleCustomizedRegistration(types.Where(ServiceCollectionExtensions.IsSyncResultCustomizedHandler), ServiceCollectionExtensions.SyncResultHandlerType, config);

    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Type> assembliesContainingTypesToScan, Action<ResultCommanderConfiguration>? options = null)
        => AddResultCommander(builder, assembliesContainingTypesToScan.Select(x => x.Assembly).Distinct(), options);

    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(builder, assembliesContainingTypesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, params Assembly[] assembliesToScan)
        => AddResultCommander(builder, assembliesToScan, null);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesContainingTypesToScan">Assemblies containing types to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, Action<ResultCommanderConfiguration> options, params Type[] assembliesContainingTypesToScan)
        => AddResultCommander(builder, assembliesContainingTypesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="options">Configuration.</param>
    /// <param name="assembliesToScan">Assemblies to scan for services.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, Action<ResultCommanderConfiguration> options, params Assembly[] assembliesToScan)
        => AddResultCommander(builder, assembliesToScan, options);
    
    /// <summary>
    /// Registers command handlers with the <see cref="ContainerBuilder"/>.
    /// </summary>
    /// <param name="builder">Current instance of <see cref="ContainerBuilder"/>.</param>
    /// <param name="assembliesToScan">Assemblies to scan for handlers.</param>
    /// <param name="options">Optional <see cref="ResultCommanderConfiguration"/> configuration.</param>
    /// <returns>Current <see cref="ContainerBuilder"/> instance.</returns>
    public static ContainerBuilder AddResultCommander(this ContainerBuilder builder, IEnumerable<Assembly> assembliesToScan, Action<ResultCommanderConfiguration>? options = null)
    {
        var config = new ResultCommanderConfiguration();
        options?.Invoke(config);

        var iopt = Options.Create(config);

        builder.RegisterInstance(iopt).As<IOptions<ResultCommanderConfiguration>>().SingleInstance();
        builder.Register(x => x.Resolve<IOptions<ResultCommanderConfiguration>>().Value)
            .As<ResultCommanderConfiguration>().SingleInstance();

        foreach (var assembly in assembliesToScan)
        {
            var types = assembly.GetTypes();
            builder.HandleAsynchronousHandlers(types.Where(ServiceCollectionExtensions.IsAnyAsyncHandler).ToList(), config);
            builder.HandleSynchronousHandlers(types.Where(ServiceCollectionExtensions.IsAnySyncHandler).ToList(), config);
        }
        
        builder.RegisterHelperByLifetime(typeof(ICommandHandlerResolver), typeof(CommandHandlerResolver),
            config.DefaultHandlerResolverLifetime);

        return builder;
    }

    private static ContainerBuilder RegisterHelperByLifetime(this ContainerBuilder containerBuilder, Type serviceType,
        Type implementationType, ServiceLifetime serviceLifetime)
    {
        switch (serviceLifetime)
        {
            case ServiceLifetime.SingleInstance:
                containerBuilder.RegisterType(implementationType).As(serviceType).SingleInstance();
                break;
            case ServiceLifetime.InstancePerRequest:
                containerBuilder.RegisterType(implementationType).As(serviceType).InstancePerRequest();
                break;
            case ServiceLifetime.InstancePerLifetimeScope:
                containerBuilder.RegisterType(implementationType).As(serviceType).InstancePerLifetimeScope();
                break;
            case ServiceLifetime.InstancePerMatchingLifetimeScope:
                throw new NotSupportedException(
                    "Can't register command handler factory or resolver as InstancePerMatchingLifetimeScope");
            case ServiceLifetime.InstancePerDependency:
                containerBuilder.RegisterType(implementationType).As(serviceType).InstancePerDependency();
                break;
            case ServiceLifetime.InstancePerOwned:
                throw new NotSupportedException(
                    "Can't register command handler factory or resolver as InstancePerOwned");
            default:
                throw new ArgumentOutOfRangeException();
        }

        return containerBuilder;
    }

    /// <summary>
    /// Whether given interceptor is an async interceptor.
    /// </summary>
    private static bool IsAsyncInterceptor(this Type interceptorCandidate) => interceptorCandidate.GetInterfaces().Any(x => x == typeof(IAsyncInterceptor));
}
